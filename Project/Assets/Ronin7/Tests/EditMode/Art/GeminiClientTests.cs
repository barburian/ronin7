using System;
using System.IO;
using NUnit.Framework;
using Ronin7.Editor.Art;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;

namespace Ronin7.Tests.EditMode.Art
{
    /// <summary>
    /// Boundary tests for <see cref="GeminiClient"/>: cache lookup, missing-key error path,
    /// JSON escaping, and hash determinism. Crucially, no network calls are issued — the
    /// network path is exercised indirectly only via the cache-hit shortcut.
    /// </summary>
    public class GeminiClientTests
    {
        private const string TestPrompt = "unit-test-prompt-do-not-call-network";
        private const int TestSeed = 1234;
        private const int TestW = 64;
        private const int TestH = 64;

        // Snapshot the user's real API key (if any) and restore it in teardown so tests can't
        // permanently clobber it. EditorPrefs is global state — be a good citizen.
        private string _savedApiKey;
        private string _cachePath;

        [SetUp]
        public void SetUp()
        {
            _savedApiKey = EditorPrefs.GetString(GeminiClient.ApiKeyPref, "");
            _cachePath = GeminiClient.CachePathFor(TestPrompt, TestSeed, TestW, TestH);

            // Defensive: a leftover cache file from a previous aborted run would make the
            // "missing key throws" test silently pass via the cache shortcut.
            if (File.Exists(_cachePath)) File.Delete(_cachePath);
        }

        [TearDown]
        public void TearDown()
        {
            EditorPrefs.SetString(GeminiClient.ApiKeyPref, _savedApiKey ?? "");
            if (File.Exists(_cachePath)) File.Delete(_cachePath);
            string meta = _cachePath + ".meta";
            if (File.Exists(meta)) File.Delete(meta);
        }

        [Test]
        public void GenerateImageAsync_CacheHit_ReturnsCachedBytesWithoutNetwork()
        {
            // Arrange: ensure the API key is unset so any network attempt would throw, then
            // pre-populate the cache so the client can never reach the network branch.
            EditorPrefs.SetString(GeminiClient.ApiKeyPref, "");
            byte[] expected = new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 1, 2, 3 };
            Directory.CreateDirectory(Path.GetDirectoryName(_cachePath)!);
            File.WriteAllBytes(_cachePath, expected);

            // Act
            var task = GeminiClient.GenerateImageAsync(TestPrompt, TestSeed, TestW, TestH);
            task.Wait(); // EditMode test — synchronous wait is fine; the cache path is purely sync.

            // Assert
            Assert.AreEqual(expected, task.Result, "Cache hit must return the cached bytes verbatim.");
        }

        [Test]
        public void GenerateImageAsync_CacheMissAndNoApiKey_ThrowsInvalidOperation()
        {
            EditorPrefs.SetString(GeminiClient.ApiKeyPref, "");
            Assert.IsFalse(File.Exists(_cachePath), "Sanity: cache must be empty for this test.");

            var ex = Assert.Throws<AggregateException>(() =>
            {
                var task = GeminiClient.GenerateImageAsync(TestPrompt, TestSeed, TestW, TestH);
                task.Wait();
            });
            Assert.IsInstanceOf<InvalidOperationException>(ex.InnerException,
                "Missing API key on cache miss must surface as InvalidOperationException.");
        }

        [Test]
        public void ComputeCacheKey_SameInputs_ProduceSameKey()
        {
            string a = GeminiClient.ComputeCacheKey("hello", 1, 256, 256);
            string b = GeminiClient.ComputeCacheKey("hello", 1, 256, 256);
            Assert.AreEqual(a, b);
        }

        [Test]
        public void ComputeCacheKey_DifferentWidth_ProducesDifferentKey()
        {
            string a = GeminiClient.ComputeCacheKey("hello", 1, 256, 256);
            string b = GeminiClient.ComputeCacheKey("hello", 1, 512, 256);
            Assert.AreNotEqual(a, b, "Width must participate in the cache key.");
        }

        [Test]
        public void ComputeCacheKey_DifferentSeed_ProducesDifferentKey()
        {
            string a = GeminiClient.ComputeCacheKey("hello", 1, 256, 256);
            string b = GeminiClient.ComputeCacheKey("hello", 2, 256, 256);
            Assert.AreNotEqual(a, b, "Seed must participate in the cache key.");
        }

        [Test]
        public void JsonEscape_HandlesControlCharsAndQuotesAndBackslashes()
        {
            // Backslash, quote, newline, tab, carriage return, sub-0x20 unicode (form feed = 0x0C).
            string input = "a\\b\"c\nd\te\rfg";
            string escaped = GeminiClient.JsonEscape(input);

            Assert.AreEqual("\"a\\\\b\\\"c\\nd\\te\\rf\\u000cg\"", escaped);
        }

        [Test]
        public void ExtractInlinePng_FindsBase64InsideInlineDataBlock()
        {
            // Minimal Gemini-shaped envelope. PNG magic bytes round-tripped through base64
            // so the assert exercises the full parse-and-decode chain.
            byte[] payload = new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A };
            string b64 = Convert.ToBase64String(payload);
            string json =
                "{\"candidates\":[{\"content\":{\"parts\":[" +
                "{\"inlineData\":{\"mimeType\":\"image/png\",\"data\":\"" + b64 + "\"}}" +
                "]}}]}";

            byte[] decoded = GeminiClient.ExtractInlinePng(json);
            Assert.AreEqual(payload, decoded);
        }

        [Test]
        public void ExtractInlinePng_ResponseWithoutInlineData_Throws()
        {
            // An error envelope that happens to contain "data" elsewhere must NOT misparse —
            // this is the exact failure mode the hardened anchor protects against.
            string json = "{\"error\":{\"code\":400,\"message\":\"bad request\",\"data\":\"not-a-png\"}}";
            Assert.Throws<InvalidOperationException>(() => GeminiClient.ExtractInlinePng(json));
        }

        [Test]
        public void ToAspectRatio_SquareReturnsOneToOne()
        {
            Assert.AreEqual("1:1", GeminiClient.ToAspectRatio(1024, 1024));
            Assert.AreEqual("1:1", GeminiClient.ToAspectRatio(512, 512));
        }

        [Test]
        public void ToAspectRatio_KnownRatiosResolveCorrectly()
        {
            Assert.AreEqual("16:9", GeminiClient.ToAspectRatio(1920, 1080));
            Assert.AreEqual("9:16", GeminiClient.ToAspectRatio(1080, 1920));
            Assert.AreEqual("4:3",  GeminiClient.ToAspectRatio(1024, 768));
            Assert.AreEqual("3:4",  GeminiClient.ToAspectRatio(768, 1024));
            Assert.AreEqual("3:2",  GeminiClient.ToAspectRatio(1500, 1000));
            Assert.AreEqual("2:3",  GeminiClient.ToAspectRatio(1000, 1500));
        }

        [Test]
        public void ToAspectRatio_UnsupportedRatioFallsBackToOneToOne()
        {
            // 2:1 (equirectangular) is not in gemini-2.5-flash-image's supported set; the helper
            // logs a warning and degrades to 1:1 so the call still succeeds.
            LogAssert.Expect(LogType.Warning, new System.Text.RegularExpressions.Regex(@"\[GeminiClient\] Aspect 2048x1024.*not a supported ratio"));
            Assert.AreEqual("1:1", GeminiClient.ToAspectRatio(2048, 1024));
        }

        [Test]
        public void ToAspectRatio_ZeroOrNegativeReturnsOneToOne()
        {
            Assert.AreEqual("1:1", GeminiClient.ToAspectRatio(0, 0));
            Assert.AreEqual("1:1", GeminiClient.ToAspectRatio(-100, 200));
        }
    }
}
