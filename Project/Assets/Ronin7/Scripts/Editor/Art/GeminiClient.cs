using System;
using System.IO;
using System.Runtime.CompilerServices;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using UnityEditor;
using UnityEngine;
using UnityEngine.Networking;

[assembly: InternalsVisibleTo("Ronin7.Tests.EditMode")]

namespace Ronin7.Editor.Art
{
    /// <summary>
    /// Editor-only client for Google's Gemini image-generation REST API.
    /// Caches results on disk by SHA256(prompt|seed|WxH) so repeated calls don't re-bill.
    /// Endpoint: https://generativelanguage.googleapis.com/v1beta/models/gemini-2.5-flash-image:generateContent
    /// API key is taken from EditorPrefs key "Ronin7.GeminiApiKey" (set via the
    /// Tools/Space Samurai/Art/Set Gemini API Key menu).
    /// </summary>
    public static class GeminiClient
    {
        public const string ApiKeyPref = "Ronin7.GeminiApiKey";
        public const string CacheFolder = "Assets/Ronin7/Art/Generated/Cache";

        // Image-generation model on generativelanguage.googleapis.com. The old
        // gemini-2.0-flash-preview-image-generation was retired by Google (404). This key has
        // gemini-2.5-flash-image ("Nano Banana") plus newer gemini-3.1-flash-image / *-pro-image
        // variants — verify with Tools/Space Samurai/Art/List Available Models. Swap this one
        // constant to bump models (e.g. "gemini-3.1-flash-image" for higher quality).
        private const string Model = "gemini-2.5-flash-image";
        private const string Endpoint =
            "https://generativelanguage.googleapis.com/v1beta/models/" + Model + ":generateContent";

        // Text model for the description -> JSON character-spec path. Separate from the image model:
        // the *-image variants only return inlineData PNGs, whereas this returns a text part we parse
        // as JSON. Swap this one constant to bump models (e.g. "gemini-3.1-flash").
        private const string TextModel = "gemini-2.5-flash";
        private const string TextEndpoint =
            "https://generativelanguage.googleapis.com/v1beta/models/" + TextModel + ":generateContent";

        /// <summary>
        /// Deterministic cache key for a given request. Exposed so tests (and future tooling)
        /// can compute the same path the client will read/write without duplicating hash logic.
        /// </summary>
        public static string ComputeCacheKey(string prompt, int seed, int width, int height)
            => Sha256Hex($"{prompt}|{seed}|{width}x{height}");

        /// <summary>Absolute-from-project-root cache path for a given request.</summary>
        public static string CachePathFor(string prompt, int seed, int width, int height)
            => $"{CacheFolder}/{ComputeCacheKey(prompt, seed, width, height)}.png";

        /// <summary>Cache path for a text (JSON) request. Distinct "|text" salt + .json extension so
        /// it never collides with an image request that happens to share a prompt+seed.</summary>
        public static string TextCachePathFor(string prompt, int seed)
            => $"{CacheFolder}/{Sha256Hex($"{prompt}|{seed}|text")}.json";

        public static async Task<byte[]> GenerateImageAsync(string prompt, int seed, int width, int height)
        {
            if (string.IsNullOrEmpty(prompt))
                throw new ArgumentException("prompt must not be empty", nameof(prompt));

            // Cache lookup first — keyed by the full request so width/seed changes miss intentionally.
            string cachePath = CachePathFor(prompt, seed, width, height);
            EnsureCacheFolder();

            if (File.Exists(cachePath))
            {
                Debug.Log("[GeminiClient] cache hit");
                return await Task.FromResult(File.ReadAllBytes(cachePath));
            }

            string apiKey = EditorPrefs.GetString(ApiKeyPref, "");
            if (string.IsNullOrEmpty(apiKey))
                throw new InvalidOperationException(
                    "Gemini API key not set. Use Tools/Space Samurai/Art/Set Gemini API Key to store it.");

            // Minimal JSON body for an image generation request. Seed is passed via generationConfig
            // so the same prompt+seed reproduces a deterministic result on retries.
            // gemini-2.0-flash-preview-image-generation REQUIRES responseModalities to include
            // both "TEXT" and "IMAGE" (the model produces both); it does NOT accept imageConfig
            // (that field was added in 2.5). The image dimensions are model-determined; width/height
            // params here remain meaningful only for cache differentiation.
            string body =
                "{\"contents\":[{\"parts\":[{\"text\":" + JsonEscape(prompt) + "}]}]," +
                "\"generationConfig\":{\"seed\":" + seed + ",\"responseModalities\":[\"TEXT\",\"IMAGE\"]}}";

            string url = $"{Endpoint}?key={UnityWebRequest.EscapeURL(apiKey)}";
            using var req = new UnityWebRequest(url, UnityWebRequest.kHttpVerbPOST)
            {
                uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes(body)),
                downloadHandler = new DownloadHandlerBuffer(),
            };
            req.SetRequestHeader("Content-Type", "application/json");

            var op = req.SendWebRequest();
            while (!op.isDone) await Task.Yield();

            if (req.result != UnityWebRequest.Result.Success)
                throw new InvalidOperationException(
                    $"[GeminiClient] HTTP {req.responseCode} {req.error}: {req.downloadHandler?.text}");

            byte[] png = ExtractInlinePng(req.downloadHandler.text);
            WriteAtomic(cachePath, png);
            Debug.Log($"[GeminiClient] cache miss → wrote {cachePath}");
            return png;
        }

        /// <summary>
        /// Generates a text response (used for description → JSON character specs). Mirrors
        /// <see cref="GenerateImageAsync"/>: disk-cached by prompt+seed so re-rolls with the same
        /// inputs don't re-bill, atomic writes, same API-key source. Asks the model for
        /// application/json so the returned string is the spec with no prose around it; the caller
        /// parses it. Returns the extracted text part (JSON), not the raw HTTP envelope.
        /// </summary>
        public static async Task<string> GenerateTextAsync(string prompt, int seed)
        {
            if (string.IsNullOrEmpty(prompt))
                throw new ArgumentException("prompt must not be empty", nameof(prompt));

            string cachePath = TextCachePathFor(prompt, seed);
            EnsureCacheFolder();

            if (File.Exists(cachePath))
            {
                Debug.Log("[GeminiClient] text cache hit");
                return await Task.FromResult(File.ReadAllText(cachePath));
            }

            string apiKey = EditorPrefs.GetString(ApiKeyPref, "");
            if (string.IsNullOrEmpty(apiKey))
                throw new InvalidOperationException(
                    "Gemini API key not set. Use Tools/Space Samurai/Art/Set Gemini API Key to store it.");

            // responseMimeType application/json makes the model emit bare JSON (no markdown fence /
            // prose), so ExtractInlineText returns something JsonUtility can parse directly.
            string body =
                "{\"contents\":[{\"parts\":[{\"text\":" + JsonEscape(prompt) + "}]}]," +
                "\"generationConfig\":{\"seed\":" + seed + ",\"responseMimeType\":\"application/json\"}}";

            string url = $"{TextEndpoint}?key={UnityWebRequest.EscapeURL(apiKey)}";
            using var req = new UnityWebRequest(url, UnityWebRequest.kHttpVerbPOST)
            {
                uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes(body)),
                downloadHandler = new DownloadHandlerBuffer(),
            };
            req.SetRequestHeader("Content-Type", "application/json");

            var op = req.SendWebRequest();
            while (!op.isDone) await Task.Yield();

            if (req.result != UnityWebRequest.Result.Success)
                throw new InvalidOperationException(
                    $"[GeminiClient] HTTP {req.responseCode} {req.error}: {req.downloadHandler?.text}");

            string text = ExtractInlineText(req.downloadHandler.text);
            WriteAtomicText(cachePath, text);
            Debug.Log($"[GeminiClient] text cache miss → wrote {cachePath}");
            return text;
        }

        /// <summary>
        /// Pulls the first inlineData.data base64 PNG out of a Gemini response.
        /// Scans for "inlineData" (or "inline_data") first to anchor the search inside the
        /// correct sub-object — a bare "data" search is fragile because the token can appear
        /// in error envelopes and unrelated fields. Internal so tests can exercise it directly.
        /// </summary>
        internal static byte[] ExtractInlinePng(string json)
        {
            if (string.IsNullOrEmpty(json))
                throw new InvalidOperationException("[GeminiClient] empty response body");

            int anchor = json.IndexOf("\"inlineData\"", StringComparison.Ordinal);
            if (anchor < 0) anchor = json.IndexOf("\"inline_data\"", StringComparison.Ordinal);
            if (anchor < 0)
                throw new InvalidOperationException(
                    "[GeminiClient] response had no inlineData block: " + Truncate(json));

            int dataKey = json.IndexOf("\"data\"", anchor, StringComparison.Ordinal);
            if (dataKey < 0)
                throw new InvalidOperationException(
                    "[GeminiClient] inlineData block had no data field: " + Truncate(json));

            int colon = json.IndexOf(':', dataKey + 6);
            int firstQuote = json.IndexOf('"', colon + 1);
            if (colon < 0 || firstQuote < 0)
                throw new InvalidOperationException("[GeminiClient] malformed inlineData.data");

            // Find the matching closing quote, respecting backslash escapes. Base64 never contains
            // a backslash or quote, but we still walk safely in case Google ever wraps the value.
            int i = firstQuote + 1;
            while (i < json.Length)
            {
                char c = json[i];
                if (c == '\\' && i + 1 < json.Length) { i += 2; continue; }
                if (c == '"') break;
                i++;
            }
            if (i >= json.Length)
                throw new InvalidOperationException("[GeminiClient] unterminated inlineData.data string");

            string b64 = json.Substring(firstQuote + 1, i - firstQuote - 1);
            try { return Convert.FromBase64String(b64); }
            catch (FormatException ex)
            {
                throw new InvalidOperationException(
                    "[GeminiClient] inlineData.data was not valid base64: " + ex.Message);
            }
        }

        /// <summary>
        /// Pulls the first candidate text part out of a generateContent response and JSON-unescapes
        /// it. Anchors on "parts" so it ignores a "text" key that might appear elsewhere in the
        /// envelope, then reads the first "text":"..." value. Strips a wrapping ```json fence if the
        /// model added one despite responseMimeType. Internal so tests can exercise it directly.
        /// </summary>
        internal static string ExtractInlineText(string json)
        {
            if (string.IsNullOrEmpty(json))
                throw new InvalidOperationException("[GeminiClient] empty response body");

            int parts = json.IndexOf("\"parts\"", StringComparison.Ordinal);
            int searchFrom = parts >= 0 ? parts : 0;
            int textKey = json.IndexOf("\"text\"", searchFrom, StringComparison.Ordinal);
            if (textKey < 0)
                throw new InvalidOperationException(
                    "[GeminiClient] response had no text part: " + Truncate(json));

            int colon = json.IndexOf(':', textKey + 6);
            int firstQuote = colon < 0 ? -1 : json.IndexOf('"', colon + 1);
            if (colon < 0 || firstQuote < 0)
                throw new InvalidOperationException("[GeminiClient] malformed text part");

            // Walk to the matching closing quote, honoring backslash escapes (the value is itself
            // JSON, so it is full of escaped quotes).
            var sb = new StringBuilder();
            int i = firstQuote + 1;
            while (i < json.Length)
            {
                char c = json[i];
                if (c == '\\' && i + 1 < json.Length)
                {
                    char n = json[i + 1];
                    switch (n)
                    {
                        case 'n': sb.Append('\n'); break;
                        case 'r': sb.Append('\r'); break;
                        case 't': sb.Append('\t'); break;
                        case '"': sb.Append('"'); break;
                        case '\\': sb.Append('\\'); break;
                        case '/': sb.Append('/'); break;
                        case 'u':
                            if (i + 5 < json.Length &&
                                int.TryParse(json.Substring(i + 2, 4),
                                    System.Globalization.NumberStyles.HexNumber,
                                    System.Globalization.CultureInfo.InvariantCulture, out int cp))
                            {
                                sb.Append((char)cp);
                                i += 6; continue;
                            }
                            sb.Append(n); break;
                        default: sb.Append(n); break;
                    }
                    i += 2; continue;
                }
                if (c == '"') break;
                sb.Append(c);
                i++;
            }
            if (i >= json.Length)
                throw new InvalidOperationException("[GeminiClient] unterminated text part string");

            return StripJsonFence(sb.ToString());
        }

        /// <summary>Removes a wrapping ```json ... ``` (or bare ```) fence the model may add despite
        /// responseMimeType, returning the inner payload trimmed. Leaves unfenced text untouched.</summary>
        internal static string StripJsonFence(string s)
        {
            if (string.IsNullOrEmpty(s)) return s;
            string t = s.Trim();
            if (!t.StartsWith("```")) return t;
            int firstNewline = t.IndexOf('\n');
            if (firstNewline < 0) return t; // a lone fence line, nothing to unwrap
            int end = t.LastIndexOf("```", StringComparison.Ordinal);
            if (end <= firstNewline) return t.Substring(firstNewline + 1).Trim();
            return t.Substring(firstNewline + 1, end - firstNewline - 1).Trim();
        }

        private static string Sha256Hex(string s)
        {
            using var sha = SHA256.Create();
            byte[] hash = sha.ComputeHash(Encoding.UTF8.GetBytes(s));
            var sb = new StringBuilder(hash.Length * 2);
            foreach (byte b in hash) sb.Append(b.ToString("x2"));
            return sb.ToString();
        }

        private static void EnsureCacheFolder()
        {
            string abs = Path.Combine(Directory.GetCurrentDirectory(), CacheFolder);
            if (!Directory.Exists(abs)) Directory.CreateDirectory(abs);
        }

        /// <summary>
        /// Write bytes via a temp file + atomic rename so an Editor crash mid-write can't leave
        /// a half-written PNG that subsequent runs would happily return as a "cache hit".
        /// </summary>
        private static void WriteAtomic(string destPath, byte[] bytes)
        {
            string tmp = destPath + ".tmp";
            File.WriteAllBytes(tmp, bytes);
            if (File.Exists(destPath)) File.Delete(destPath);
            File.Move(tmp, destPath);
        }

        /// <summary>UTF-8 text counterpart to <see cref="WriteAtomic"/> for the JSON spec cache.</summary>
        private static void WriteAtomicText(string destPath, string text)
            => WriteAtomic(destPath, Encoding.UTF8.GetBytes(text));

        internal static string JsonEscape(string s)
        {
            var sb = new StringBuilder(s.Length + 2);
            sb.Append('"');
            foreach (char c in s)
            {
                switch (c)
                {
                    case '\\': sb.Append("\\\\"); break;
                    case '"': sb.Append("\\\""); break;
                    case '\n': sb.Append("\\n"); break;
                    case '\r': sb.Append("\\r"); break;
                    case '\t': sb.Append("\\t"); break;
                    default:
                        if (c < 0x20) sb.AppendFormat("\\u{0:x4}", (int)c);
                        else sb.Append(c);
                        break;
                }
            }
            sb.Append('"');
            return sb.ToString();
        }

        private static string Truncate(string s) => s.Length <= 200 ? s : s.Substring(0, 200) + "...";

        /// <summary>
        /// Maps a (width, height) request to the closest aspect-ratio string accepted by
        /// gemini-2.5-flash-image's imageConfig.aspectRatio field. The model returns its own
        /// pixel dimensions; this just communicates intended shape. Unsupported ratios fall back
        /// to "1:1" with a warning so the call still succeeds.
        /// </summary>
        internal static string ToAspectRatio(int width, int height)
        {
            if (width <= 0 || height <= 0) return "1:1";
            if (width == height) return "1:1";
            double r = (double)width / height;
            // Supported by gemini-2.5-flash-image as of 2026-05; tolerance ±3% absorbs callers
            // who pass slightly off-spec dimensions (e.g., 1920x1080 vs exact 16:9).
            (string label, double value)[] ratios = {
                ("16:9", 16.0/9.0),
                ("9:16", 9.0/16.0),
                ("4:3",  4.0/3.0),
                ("3:4",  3.0/4.0),
                ("3:2",  3.0/2.0),
                ("2:3",  2.0/3.0),
            };
            foreach (var (label, value) in ratios)
                if (Math.Abs(r - value) / value < 0.03) return label;
            Debug.LogWarning($"[GeminiClient] Aspect {width}x{height} (r={r:F3}) is not a supported ratio; using 1:1.");
            return "1:1";
        }
    }
}
