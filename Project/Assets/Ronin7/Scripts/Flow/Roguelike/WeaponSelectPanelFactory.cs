using Ronin7.Core;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using UnityEngine.XR.Interaction.Toolkit.UI;

namespace Ronin7.Flow
{
    /// <summary>Builds the worldspace WEAPON SELECT panel from primitives, same idiom as
    /// <see cref="MetaShopPanelFactory"/>. Returns null without a camera or EventSystem.</summary>
    public static class WeaponSelectPanelFactory
    {
        private const float CardW = 285f, CardH = 560f, ModelFit = 190f;
        private static readonly Color LockedTint = new Color(0.12f, 0.12f, 0.15f);

        /// <param name="requireEventSystem">Tests pass false: an EventSystem cannot register itself in edit mode.</param>
        public static WeaponSelectPanel Build(Camera targetCamera, bool requireEventSystem = true)
        {
            if (targetCamera == null || (requireEventSystem && EventSystem.current == null)) return null;

            var canvasGo = new GameObject("Weapon Select Canvas");
            var canvas = canvasGo.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;
            canvasGo.AddComponent<CanvasScaler>();
            canvasGo.AddComponent<GraphicRaycaster>();
            canvasGo.AddComponent<TrackedDeviceGraphicRaycaster>();

            var rt = canvas.GetComponent<RectTransform>();
            rt.sizeDelta = new Vector2(1240f, 960f);
            rt.localScale = Vector3.one * 0.001f;
            rt.gameObject.AddComponent<UiScaleFollower>();
            BoonOfferPanel.Place(rt, targetCamera.transform);
            UiKit.Frame(canvasGo.AddComponent<Image>(), UiKit.Cyan);

            var panel = canvasGo.AddComponent<WeaponSelectPanel>();
            MakeText(rt, "CHOOSE YOUR WEAPON", 48, TextAlignmentOptions.Center, new Vector2(0f, 425f), new Vector2(900f, 80f), Color.white);

            var models = Resources.Load<WeaponCardModels>(WeaponCardModels.ResourcePath);
            panel.cards = new WeaponSelectPanel.Card[WeaponSelectPanel.Cards.Length];
            for (int i = 0; i < panel.cards.Length; i++)
            {
                var (id, name, blurb) = WeaponSelectPanel.Cards[i];
                var b = MakeButton(rt, name, new Vector2(-450f + i * 300f, 100f), new Vector2(CardW, CardH));
                var cardRt = (RectTransform)b.transform;
                var label = cardRt.GetChild(0).GetComponent<TMP_Text>(); // MakeButton's centred label becomes the name line
                label.rectTransform.anchorMin = label.rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
                label.rectTransform.sizeDelta = new Vector2(CardW - 20f, 70f);
                label.rectTransform.anchoredPosition = new Vector2(0f, -30f);
                MakeText(cardRt, blurb, UiKit.MinBody, TextAlignmentOptions.Top, new Vector2(0f, -120f), new Vector2(CardW - 20f, 130f), new Color(1f, 1f, 1f, 0.8f));
                var state = MakeText(cardRt, "", UiKit.MinBody, TextAlignmentOptions.Center, new Vector2(0f, -215f), new Vector2(CardW - 20f, 110f), Color.white);
                var model = models != null ? models.Get(i) : null;
                bool unlocked = MetaProgression.IsWeaponUnlocked(id);
                if (model != null) AttachModel(model, cardRt, new Vector3(0f, 130f, -60f), unlocked);
                panel.cards[i] = new WeaponSelectPanel.Card { id = id, button = b, stateText = state, group = b.gameObject.AddComponent<CanvasGroup>() };
            }

            panel.modeButtons = new Button[Difficulty.Count];
            for (int i = 0; i < Difficulty.Count; i++)
                panel.modeButtons[i] = MakeButton(rt, Difficulty.Label((DifficultyMode)i), new Vector2(-300f + i * 300f, -230f), new Vector2(280f, 70f));
            panel.modeText = MakeText(rt, "", UiKit.MinBody, TextAlignmentOptions.Center, new Vector2(0f, -300f), new Vector2(1180f, 50f), new Color(1f, 1f, 1f, 0.8f));

            panel.backButton = MakeButton(rt, "BACK", new Vector2(-170f, -400f), new Vector2(280f, 80f));
            panel.startButton = MakeButton(rt, "START", new Vector2(170f, -400f), new Vector2(280f, 80f));
            panel.Wire();
            return panel;
        }

        /// <summary>A15.13: an inert display copy. Scripts go first (they RequireComponent colliders, trails, lights),
        /// then everything but transforms and mesh renderers, so a card model can never grab, hit or parry.</summary>
        internal static void StripToVisual(GameObject root)
        {
            var doomed = new System.Collections.Generic.List<Component>();
            foreach (var c in root.GetComponentsInChildren<Component>(true))
                if (c != null && !(c is Transform) && !(c is MeshFilter) && !(c is MeshRenderer) && !(c is SkinnedMeshRenderer))
                    doomed.Add(c);
            // Unity refuses to remove a component another one RequireComponent's, so peel off what nothing needs first.
            while (doomed.Count > 0)
            {
                int before = doomed.Count;
                for (int i = doomed.Count - 1; i >= 0; i--)
                {
                    if (IsRequiredByAny(doomed[i], doomed)) continue;
                    Object.DestroyImmediate(doomed[i]);
                    doomed.RemoveAt(i);
                }
                if (doomed.Count == before) break; // cyclic or unknown: leave it rather than log errors
            }
        }

        private static bool IsRequiredByAny(Component c, System.Collections.Generic.List<Component> others)
        {
            var type = c.GetType();
            foreach (var o in others)
            {
                if (o == c) continue;
                foreach (var req in (RequireComponent[])System.Attribute.GetCustomAttributes(o.GetType(), typeof(RequireComponent), true))
                    if (Needs(req.m_Type0, type) || Needs(req.m_Type1, type) || Needs(req.m_Type2, type)) return true;
            }
            return false;
        }

        private static bool Needs(System.Type required, System.Type have) => required != null && required.IsAssignableFrom(have);

        /// <summary>Instantiates a stripped copy, scales it so its longest side is <see cref="ModelFit"/> panel units and
        /// centres it on <paramref name="localPos"/>. Locked cards tint dark (MPB, no material clones).</summary>
        internal static GameObject AttachModel(GameObject prefab, Transform card, Vector3 localPos, bool unlocked)
        {
            var go = Object.Instantiate(prefab);
            go.name = prefab.name;
            StripToVisual(go);
            var rs = go.GetComponentsInChildren<Renderer>();
            if (rs.Length > 0)
            {
                var b = rs[0].bounds;
                foreach (var r in rs) b.Encapsulate(r.bounds);
                float tall = Mathf.Max(b.size.x, b.size.y, b.size.z); // fit the longest side: a model authored on its side still lands inside the card
                float scale = tall > 1e-4f ? ModelFit / tall : 1f;
                go.transform.SetParent(card, false);
                go.transform.localScale = Vector3.one * scale;
                go.transform.localPosition = localPos - b.center * scale;
                if (!unlocked) foreach (var r in rs) RendererTint.ApplyLocked(r, LockedTint);
            }
            else go.transform.SetParent(card, false);
            return go;
        }

        private static TMP_Text MakeText(RectTransform parent, string s, float size, TextAlignmentOptions anchor, Vector2 pos, Vector2 dim, Color color)
        {
            var go = new GameObject("Text");
            go.transform.SetParent(parent, false);
            var rt = go.AddComponent<RectTransform>();
            rt.sizeDelta = dim;
            rt.anchoredPosition = pos;
            return UiKit.Text(go, s, size, color, anchor, bold: true, title: size >= UiKit.MinTitle);
        }

        private static Button MakeButton(RectTransform parent, string label, Vector2 pos, Vector2 dim)
        {
            var go = new GameObject(label);
            go.transform.SetParent(parent, false);
            var rt = go.AddComponent<RectTransform>();
            rt.sizeDelta = dim;
            rt.anchoredPosition = pos;
            var img = go.AddComponent<Image>();
            UiKit.Frame(img, UiKit.Cyan, 0.95f);
            var b = go.AddComponent<Button>();
            b.targetGraphic = img;
            var colors = b.colors;
            colors.disabledColor = Color.white; // locked cards dim through their CanvasGroup instead
            b.colors = colors;
            var lt = MakeText(rt, label, 30, TextAlignmentOptions.Center, Vector2.zero, dim, Color.white);
            lt.rectTransform.anchorMin = Vector2.zero;
            lt.rectTransform.anchorMax = Vector2.one;
            lt.rectTransform.offsetMin = lt.rectTransform.offsetMax = Vector2.zero;
            return b;
        }
    }
}
