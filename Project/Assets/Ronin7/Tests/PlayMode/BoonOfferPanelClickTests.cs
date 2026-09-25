using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using Ronin7.Combat;
using Ronin7.Flow;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.TestTools;

namespace Ronin7.Tests.PlayMode
{
    /// <summary>
    /// End-to-end coverage for the boon offer panel's click path, built by the real
    /// <see cref="BoonOfferPanelFactory"/> exactly as <c>RunDirector</c> builds it.
    ///
    /// This lives in PlayMode, not EditMode, for two reasons: <see cref="EventSystem.current"/> is
    /// only populated by <c>OnEnable</c>, which does not run in edit mode (so the factory would
    /// early-return null there), and the defect under test is an <c>Awake</c>-timing one that only
    /// reproduces on a live, active GameObject.
    ///
    /// The defect: <c>AddComponent</c> on an active GameObject runs <c>Awake</c> synchronously, so a
    /// panel that wired its listeners in <c>Awake</c> saw null button arrays and attached nothing.
    /// Every click was swallowed and the run soft-locked at the first reward (A7.2). The EditMode
    /// suite could not catch it because it builds the panel inactive on purpose.
    /// </summary>
    public class BoonOfferPanelClickTests
    {
        private readonly List<Object> _created = new();

        [TearDown]
        public void TearDown()
        {
            foreach (var obj in _created)
                if (obj != null) Object.DestroyImmediate(obj);
            _created.Clear();
        }

        private BoonDefinition MakeBoon(string id)
        {
            var def = ScriptableObject.CreateInstance<BoonDefinition>();
            def.id = id;
            def.displayName = id;
            def.description = id + " description";
            def.rarity = BoonRarity.Common;
            _created.Add(def);
            return def;
        }

        /// <summary>Stands up the EventSystem + camera the factory requires, mirroring what
        /// RoguelikeArenaBuilder bakes into RunArena.unity.</summary>
        private BoonOfferPanel BuildPanel(bool rerollAvailable = true)
        {
            var esGo = new GameObject("EventSystem");
            _created.Add(esGo);
            esGo.AddComponent<EventSystem>();

            var camGo = new GameObject("Camera");
            _created.Add(camGo);
            var cam = camGo.AddComponent<Camera>();

            var choices = new List<BoonDefinition> { MakeBoon("boon_a"), MakeBoon("boon_b"), MakeBoon("boon_c") };
            var panel = BoonOfferPanelFactory.Build(cam, choices, rerollAvailable);
            if (panel != null) _created.Add(panel.gameObject);
            return panel;
        }

        [UnityTest]
        public IEnumerator FactoryBuiltPanel_ClickingAChoice_RaisesChoiceSelectedWithThatIndex()
        {
            var panel = BuildPanel();
            Assert.IsNotNull(panel, "Factory returned null despite an EventSystem and a camera being present.");
            yield return null; // let Awake/Start/OnEnable settle exactly as they would in a real run

            int fired = -1;
            int fireCount = 0;
            panel.ChoiceSelected += i => { fired = i; fireCount++; };

            panel.choiceButtons[1].onClick.Invoke();

            Assert.AreEqual(1, fireCount, "Clicking a boon choice must raise ChoiceSelected exactly once.");
            Assert.AreEqual(1, fired, "ChoiceSelected must carry the index of the clicked choice.");
        }

        [UnityTest]
        public IEnumerator FactoryBuiltPanel_EveryChoiceButtonIsWired()
        {
            var panel = BuildPanel();
            Assert.IsNotNull(panel);
            yield return null;

            for (int i = 0; i < panel.choiceButtons.Length; i++)
            {
                int fired = -1;
                void Handler(int index) => fired = index;
                panel.ChoiceSelected += Handler;
                panel.choiceButtons[i].onClick.Invoke();
                panel.ChoiceSelected -= Handler;

                Assert.AreEqual(i, fired, $"Choice button {i} raised the wrong index (or none at all).");
            }
        }

        [UnityTest]
        public IEnumerator FactoryBuiltPanel_ClickingReroll_RaisesRerollRequested()
        {
            var panel = BuildPanel();
            Assert.IsNotNull(panel);
            yield return null;

            int rerolls = 0;
            panel.RerollRequested += () => rerolls++;

            panel.rerollButton.onClick.Invoke();

            Assert.AreEqual(1, rerolls, "Clicking REROLL must raise RerollRequested exactly once.");
        }

        [UnityTest]
        public IEnumerator WireButtons_IsIdempotent_ASecondCallCannotDoubleFire()
        {
            var panel = BuildPanel();
            Assert.IsNotNull(panel);
            yield return null;

            panel.WireButtons(); // the factory already called it once

            int fireCount = 0;
            panel.ChoiceSelected += _ => fireCount++;
            panel.choiceButtons[0].onClick.Invoke();

            Assert.AreEqual(1, fireCount, "WireButtons must not stack duplicate listeners.");
        }
    }
}
