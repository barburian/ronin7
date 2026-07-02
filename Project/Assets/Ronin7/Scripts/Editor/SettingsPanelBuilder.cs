using System.Collections.Generic;
using Ronin7.Audio;
using Ronin7.Player;
using Unity.XR.CoreUtils;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactors;
using UnityEngine.XR.Interaction.Toolkit.UI;
// Disambiguate from UnityEditor.SettingsService (the Editor namespace defines a type of the same name).
using SettingsService = Ronin7.Audio.SettingsService;

namespace Ronin7.EditorTools
{
    /// <summary>
    /// One-click assembly of the worldspace VR settings panel into the CURRENTLY OPEN scene, so
    /// the human never hand-builds the Canvas hierarchy. Mirrors <see cref="XRRigBuilder"/>'s
    /// approach: build a Canvas with uGUI controls (toggles/sliders/text), bind them to a
    /// <see cref="SettingsPanel"/>, add the VR UI plumbing (EventSystem + XRUIInputModule, a
    /// TrackedDeviceGraphicRaycaster on the canvas, and an XRInteractionManager), and attach an
    /// <see cref="XRRayInteractor"/> to the rig's right hand for pointing.
    ///
    /// Run with a built scene open (e.g. Phase4_Zone or Phase7_SpaceCombat). The control wiring
    /// uses stable object references (Toggle/Slider/Text), which survive rebuilds; the ONE
    /// InputActionReference involved is the ray interactor's UI-press (Right Hand/Select) —
    /// verify it after running, as documented in the log.
    /// </summary>
    public static class SettingsPanelBuilder
    {
        private const string InputAssetPath = "Assets/Ronin7/Settings/Ronin7Input.inputactions";

        [MenuItem("Tools/Space Samurai/Build Settings Panel (in open scene)", priority = 20)]
        public static void BuildSettingsPanel()
        {
            // 1. Persistent SettingsService so the panel has something to drive at runtime. If a
            //    Game object exists, add it there; otherwise create a standalone holder.
            EnsureSettingsService();

            // 2. EventSystem + XR UI input module (one per scene).
            EnsureXRUIEventSystem();

            // 3. Interaction manager (interactors require one).
            if (Object.FindAnyObjectByType<XRInteractionManager>() == null)
                new GameObject("XR Interaction Manager").AddComponent<XRInteractionManager>();

            // 4. Worldspace canvas with controls, placed in front of the player.
            var panel = BuildCanvas();

            // 5. Ray interactor on the right hand for pointing (best-effort; logs if no rig).
            WireRightHandRayInteractor();

            // 6. Menu toggle on a separate always-active object; panel starts hidden until the
            //    controller Menu button is pressed (and recentres in front of the player on open).
            EnsureMenuToggle(panel.gameObject);
            panel.gameObject.SetActive(false);

            EditorSceneManager.MarkSceneDirty(panel.gameObject.scene);
            Selection.activeObject = panel.gameObject;
            EditorGUIUtility.PingObject(panel.gameObject);
            Debug.Log("[Space Samurai] Settings panel built in the open scene (hidden until the left " +
                      "controller Menu button is pressed). Save the scene to keep it. " +
                      "VERIFY in the Inspector: the Right Hand's XR Ray Interactor → UI Press Input → " +
                      "Input Action Reference Performed is set to 'Right Hand/Select' (rebuilds can re-null it).");
        }

        private static void EnsureSettingsService()
        {
            if (Object.FindAnyObjectByType<SettingsService>() != null) return;
            var game = GameObject.Find("Game");
            if (game == null) game = new GameObject("Game");
            game.AddComponent<SettingsService>();
        }

        private static void EnsureMenuToggle(GameObject panel)
        {
            var toggle = Object.FindAnyObjectByType<SettingsMenuToggle>();
            if (toggle == null)
                toggle = new GameObject("Settings Menu").AddComponent<SettingsMenuToggle>();
            var so = new SerializedObject(toggle);
            var p = so.FindProperty("panel");
            if (p != null) p.objectReferenceValue = panel;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void EnsureXRUIEventSystem()
        {
            var es = Object.FindAnyObjectByType<EventSystem>();
            if (es == null) es = new GameObject("EventSystem").AddComponent<EventSystem>();
            // Replace the default StandaloneInputModule with the XR one if needed.
            if (es.GetComponent<XRUIInputModule>() == null)
            {
                var legacy = es.GetComponent<StandaloneInputModule>();
                if (legacy != null) Object.DestroyImmediate(legacy);
                es.gameObject.AddComponent<XRUIInputModule>();
            }
        }

        private static SettingsPanel BuildCanvas()
        {
            var canvasGo = new GameObject("Settings Canvas");
            var canvas = canvasGo.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;
            canvasGo.AddComponent<CanvasScaler>();
            canvasGo.AddComponent<GraphicRaycaster>();
            canvasGo.AddComponent<TrackedDeviceGraphicRaycaster>(); // makes the canvas hittable by XR rays

            // Worldspace size: a ~0.6m wide panel a metre in front at chest height.
            // Height grew from 520 → 610 → 840 → 980 to make room for "Return to Main Menu" + save/load
            // sections and the two graphics rows (High Quality + Combat Blood).
            var rt = canvas.GetComponent<RectTransform>();
            rt.sizeDelta = new Vector2(600f, 980f);
            rt.localScale = Vector3.one * 0.001f; // 1px = 1mm → 0.6m wide
            rt.position = new Vector3(0f, 1.2f, 1f);

            // Background.
            var bg = canvasGo.AddComponent<Image>();
            bg.color = new Color(0.05f, 0.06f, 0.09f, 0.85f);

            float y = 415f;  // Shifted up by 70 (half of the 140px added for the two graphics rows) to keep content centered

            MakeTitle(rt, "SETTINGS", ref y);
            var snapToggle = MakeToggle(rt, "Snap Turn", ref y);
            var snapSlider = MakeSlider(rt, "Snap Angle: 45°", 15f, 90f, 45f, ref y, out var snapLabel);
            var vignetteToggle = MakeToggle(rt, "Comfort Vignette", ref y);
            var highQualityToggle = MakeToggle(rt, "High Quality (PCVR)", ref y);
            var bloodToggle = MakeToggle(rt, "Combat Blood", ref y);
            var sfxSlider = MakeSlider(rt, "SFX Volume", 0f, 1f, 1f, ref y, out _);
            var musicSlider = MakeSlider(rt, "Music Volume", 0f, 1f, 0.45f, ref y, out _);
            var ambienceSlider = MakeSlider(rt, "Ambience Volume", 0f, 1f, 0.5f, ref y, out _);

            // SAVE GAME section: label, three slot buttons side-by-side, and status text.
            MakeTitle(rt, "SAVE GAME", ref y);
            var saveSlotButtons = new Button[3];
            float[] slotX = { -165f, 0f, 165f };
            string[] slotLabels = { "SLOT 1", "SLOT 2", "SLOT 3" };
            float slotButtonY = y;
            for (int i = 0; i < 3; i++)
            {
                var go = new GameObject("SaveSlot" + (i + 1), typeof(RectTransform));
                var brt = go.GetComponent<RectTransform>();
                brt.SetParent(rt, false);
                Anchor(brt, new Vector2(slotX[i], slotButtonY), new Vector2(150f, 60f));
                var img = go.AddComponent<Image>();
                img.color = new Color(0.15f, 0.18f, 0.25f, 1f);
                var btn = go.AddComponent<Button>();
                btn.targetGraphic = img;
                saveSlotButtons[i] = btn;

                var labelGo = new GameObject("Text", typeof(RectTransform));
                var lrt = labelGo.GetComponent<RectTransform>();
                lrt.SetParent(brt, false);
                lrt.anchorMin = Vector2.zero;
                lrt.anchorMax = Vector2.one;
                lrt.pivot = new Vector2(0.5f, 0.5f);
                lrt.anchoredPosition = Vector2.zero;
                lrt.sizeDelta = Vector2.zero;
                var t = labelGo.AddComponent<Text>();
                t.text = slotLabels[i];
                t.font = LegacyFont();
                t.fontSize = 24;
                t.fontStyle = FontStyle.Bold;
                t.color = Color.white;
                t.alignment = TextAnchor.MiddleCenter;
            }
            y -= 80f;

            var saveStatusLabel = NewText(rt, "", new Vector2(0f, y), new Vector2(560f, 50f), 18, TextAnchor.MiddleCenter);
            y -= 60f;

            // Return to Main Menu button: appended at the bottom so existing rows are not perturbed.
            // Red-ish tint flags it as a destructive/navigation action distinct from the settings controls above.
            var returnBtn = MakeButton(rt, "RETURN TO MAIN MENU", new Color(0.5f, 0.15f, 0.18f, 1f), ref y);

            var panel = canvasGo.AddComponent<SettingsPanel>();
            var so = new SerializedObject(panel);
            SetRef(so, "snapTurnToggle", snapToggle);
            SetRef(so, "snapDegreesSlider", snapSlider);
            SetRef(so, "snapDegreesLabel", snapLabel);
            SetRef(so, "vignetteToggle", vignetteToggle);
            SetRef(so, "highQualityToggle", highQualityToggle);
            SetRef(so, "bloodToggle", bloodToggle);
            SetRef(so, "sfxSlider", sfxSlider);
            SetRef(so, "musicSlider", musicSlider);
            SetRef(so, "ambienceSlider", ambienceSlider);
            SetRef(so, "returnToMenuButton", returnBtn);

            // Wire save slot buttons and status label.
            var saveSlotsProperty = so.FindProperty("saveSlotButtons");
            if (saveSlotsProperty != null)
            {
                saveSlotsProperty.arraySize = 3;
                for (int i = 0; i < 3; i++)
                {
                    saveSlotsProperty.GetArrayElementAtIndex(i).objectReferenceValue = saveSlotButtons[i];
                }
            }
            SetRef(so, "saveStatusLabel", saveStatusLabel);

            so.ApplyModifiedPropertiesWithoutUndo();

            // Wire onclick listeners for save slot buttons (1-based slot numbering).
            for (int i = 0; i < 3; i++)
            {
                UnityEditor.Events.UnityEventTools.AddIntPersistentListener(saveSlotButtons[i].onClick,
                    new UnityEngine.Events.UnityAction<int>(panel.OnSaveSlotClicked), i + 1);
            }

            return panel;
        }

        private static Button MakeButton(RectTransform parent, string label, Color tint, ref float y)
        {
            var go = new GameObject("Button", typeof(RectTransform));
            var brt = go.GetComponent<RectTransform>();
            brt.SetParent(parent, false);
            Anchor(brt, new Vector2(0f, y), new Vector2(480f, 60f));

            var img = go.AddComponent<Image>();
            img.color = tint;

            var btn = go.AddComponent<Button>();
            btn.targetGraphic = img;

            // Label fills the button; bold white for legibility against the dark tint.
            var labelGo = new GameObject("Text", typeof(RectTransform));
            var lrt = labelGo.GetComponent<RectTransform>();
            lrt.SetParent(brt, false);
            lrt.anchorMin = Vector2.zero;
            lrt.anchorMax = Vector2.one;
            lrt.pivot = new Vector2(0.5f, 0.5f);
            lrt.anchoredPosition = Vector2.zero;
            lrt.sizeDelta = Vector2.zero;
            var t = labelGo.AddComponent<Text>();
            t.text = label;
            t.font = LegacyFont();
            t.fontSize = 24;
            t.fontStyle = FontStyle.Bold;
            t.color = Color.white;
            t.alignment = TextAnchor.MiddleCenter;

            y -= 80f;
            return btn;
        }

        // ---- uGUI control factories (worldspace pixels; parented to the canvas). ----

        private static void MakeTitle(RectTransform parent, string text, ref float y)
        {
            var label = NewText(parent, text, new Vector2(0f, y), new Vector2(560f, 50f), 34, TextAnchor.MiddleCenter);
            label.fontStyle = FontStyle.Bold;
            y -= 80f;
        }

        private static Toggle MakeToggle(RectTransform parent, string label, ref float y)
        {
            var row = NewRow(parent, label, ref y, out var rowRt);

            var toggleGo = new GameObject("Toggle", typeof(RectTransform));
            var trt = toggleGo.GetComponent<RectTransform>();
            trt.SetParent(rowRt, false);
            Anchor(trt, new Vector2(200f, 0f), new Vector2(40f, 40f));
            var toggle = toggleGo.AddComponent<Toggle>();

            var bg = NewImage(trt, "Background", Vector2.zero, new Vector2(40f, 40f), new Color(0.2f, 0.22f, 0.28f, 1f));
            var check = NewImage(bg.rectTransform, "Checkmark", Vector2.zero, new Vector2(28f, 28f), new Color(0.4f, 0.8f, 1f, 1f));
            toggle.targetGraphic = bg;
            toggle.graphic = check;
            _ = row;
            return toggle;
        }

        private static Slider MakeSlider(RectTransform parent, string label, float min, float max, float value,
            ref float y, out Text valueLabel)
        {
            valueLabel = NewRow(parent, label, ref y, out var rowRt);

            var sliderGo = new GameObject("Slider", typeof(RectTransform));
            var srt = sliderGo.GetComponent<RectTransform>();
            srt.SetParent(rowRt, false);
            Anchor(srt, new Vector2(150f, 0f), new Vector2(240f, 24f));
            var slider = sliderGo.AddComponent<Slider>();

            var bg = NewImage(srt, "Background", Vector2.zero, new Vector2(240f, 12f), new Color(0.2f, 0.22f, 0.28f, 1f));
            var fillArea = new GameObject("Fill Area", typeof(RectTransform)).GetComponent<RectTransform>();
            fillArea.SetParent(srt, false);
            Anchor(fillArea, Vector2.zero, new Vector2(240f, 12f));
            var fill = NewImage(fillArea, "Fill", Vector2.zero, new Vector2(120f, 12f), new Color(0.4f, 0.8f, 1f, 1f));

            var handleArea = new GameObject("Handle Slide Area", typeof(RectTransform)).GetComponent<RectTransform>();
            handleArea.SetParent(srt, false);
            Anchor(handleArea, Vector2.zero, new Vector2(240f, 24f));
            var handle = NewImage(handleArea, "Handle", Vector2.zero, new Vector2(24f, 24f), Color.white);

            slider.fillRect = fill.rectTransform;
            slider.handleRect = handle.rectTransform;
            slider.targetGraphic = handle;
            slider.minValue = min;
            slider.maxValue = max;
            slider.value = value;
            _ = bg;
            return slider;
        }

        // A labelled row; returns the row's label Text (used as the value label for sliders).
        private static Text NewRow(RectTransform parent, string label, ref float y, out RectTransform rowRt)
        {
            var rowGo = new GameObject("Row", typeof(RectTransform));
            rowRt = rowGo.GetComponent<RectTransform>();
            rowRt.SetParent(parent, false);
            Anchor(rowRt, new Vector2(0f, y), new Vector2(560f, 50f));
            var text = NewText(rowRt, label, new Vector2(-150f, 0f), new Vector2(260f, 50f), 24, TextAnchor.MiddleLeft);
            y -= 70f;
            return text;
        }

        private static Text NewText(RectTransform parent, string text, Vector2 pos, Vector2 size, int fontSize, TextAnchor anchor)
        {
            var go = new GameObject("Text", typeof(RectTransform));
            var rt = go.GetComponent<RectTransform>();
            rt.SetParent(parent, false);
            Anchor(rt, pos, size);
            var t = go.AddComponent<Text>();
            t.text = text;
            t.font = LegacyFont();
            t.fontSize = fontSize;
            t.color = Color.white;
            t.alignment = anchor;
            return t;
        }

        private static Image NewImage(RectTransform parent, string name, Vector2 pos, Vector2 size, Color color)
        {
            var go = new GameObject(name, typeof(RectTransform));
            var rt = go.GetComponent<RectTransform>();
            rt.SetParent(parent, false);
            Anchor(rt, pos, size);
            var img = go.AddComponent<Image>();
            img.color = color;
            return img;
        }

        private static void Anchor(RectTransform rt, Vector2 anchoredPos, Vector2 size)
        {
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = anchoredPos;
            rt.sizeDelta = size;
        }

        private static Font LegacyFont()
        {
            // LegacyRuntime.ttf is the built-in replacement for the old Arial.ttf default. It's a
            // builtin *resource* (Resources.GetBuiltinResource), not a builtin *extra* asset, so
            // AssetDatabase.GetBuiltinExtraResource can't find it. Fall back to an OS font if needed.
            var font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            if (font == null) font = Font.CreateDynamicFontFromOSFont("Arial", 16);
            return font;
        }

        /// <summary>Find the rig's right hand and add an XRRayInteractor wired to fire UI on Right Hand/Select.</summary>
        private static void WireRightHandRayInteractor()
        {
            var origin = Object.FindAnyObjectByType<XROrigin>();
            if (origin == null)
            {
                Debug.LogWarning("[Space Samurai] No XR Origin in the open scene; skipped adding a UI ray interactor. " +
                                 "Open a scene that has the rig (e.g. Phase4_Zone) before building the panel.");
                return;
            }

            // The rig builder names the right hand "Right Hand Controller".
            var hand = FindChildByName(origin.transform, "Right Hand Controller");
            if (hand == null)
            {
                Debug.LogWarning("[Space Samurai] Could not find 'Right Hand Controller' under the XR Origin; " +
                                 "skipped the UI ray interactor. Add an XRRayInteractor to a hand manually.");
                return;
            }
            if (hand.GetComponent<XRRayInteractor>() != null) return; // already has one

            // enableUIInteraction defaults to true on the component, so the panel is hittable
            // once the UI press action below is wired.
            var interactor = hand.gameObject.AddComponent<XRRayInteractor>();

            // Wire the UI press to Right Hand/Select via the nested XRInputButtonReader.
            var pressRef = LoadActionRef("Right Hand", "Select");
            if (pressRef != null)
            {
                var so = new SerializedObject(interactor);
                var prop = so.FindProperty("m_UIPressInput.m_InputActionReferencePerformed");
                if (prop != null)
                {
                    prop.objectReferenceValue = pressRef;
                    so.ApplyModifiedPropertiesWithoutUndo();
                }
            }
        }

        private static Transform FindChildByName(Transform root, string name)
        {
            if (root.name == name) return root;
            foreach (Transform child in root)
            {
                var found = FindChildByName(child, name);
                if (found != null) return found;
            }
            return null;
        }

        private static InputActionReference LoadActionRef(string map, string action)
        {
            var refs = AssetDatabase.LoadAllAssetRepresentationsAtPath(InputAssetPath);
            if (refs == null) return null;
            foreach (var o in refs)
            {
                if (o is InputActionReference r && r.action != null && r.action.actionMap != null
                    && r.action.actionMap.name == map && r.action.name == action)
                    return r;
            }
            Debug.LogWarning($"[Space Samurai] Input action {map}/{action} not found for the UI ray interactor.");
            return null;
        }

        private static void SetRef(SerializedObject so, string property, Object value)
        {
            var prop = so.FindProperty(property);
            if (prop != null) prop.objectReferenceValue = value;
        }
    }
}
