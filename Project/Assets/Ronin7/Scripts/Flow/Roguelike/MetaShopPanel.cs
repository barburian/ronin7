using Ronin7.Core;
using Ronin7.Player;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.XR;

namespace Ronin7.Flow
{
    /// <summary>P2-01: the Shrine of Echoes. Four upgrade rows (name, level pips, cost, BUY) over
    /// <see cref="MetaProgression"/>. Built by <see cref="MetaShopPanelFactory"/>.</summary>
    public class MetaShopPanel : MonoBehaviour
    {
        internal struct Row
        {
            public string id;
            public Text nameText, pipsText, costText;
            public Button buy;
        }

        internal Row[] rows;
        internal Text echoesLabel;
        internal Button closeButton;
        internal System.Action onClosed;
        private readonly LazyFollow follow = new LazyFollow(1.3f);

        internal void Wire()
        {
            for (int i = 0; i < rows.Length; i++)
            {
                string id = rows[i].id;
                rows[i].buy.onClick.AddListener(() => Buy(id));
            }
            closeButton.onClick.AddListener(() => Destroy(gameObject));
            Refresh();
        }

        internal void Buy(string id)
        {
            if (MetaProgression.TryPurchase(id))
            {
                Haptics.Pulse(XRNode.RightHand, 0.6f, 0.1f);
                // AudioDirector (which references Flow, so we can't call it) plays its boonChosen cue on this event.
                EventBus.Publish(new BoonChosen(id));
            }
            Refresh();
        }

        internal void Refresh()
        {
            echoesLabel.text = EchoesText(MetaProgression.Echoes);
            foreach (var r in rows)
            {
                int level = MetaProgression.UpgradeLevel(r.id), max = MetaProgression.MaxLevel(r.id);
                int cost = MetaProgression.UpgradeCost(r.id, level + 1);
                r.pipsText.text = Pips(level, max);
                r.costText.text = level >= max ? "MAX" : "◈ " + cost;
                r.buy.interactable = level < max && MetaProgression.Echoes >= cost;
            }
        }

        internal static string EchoesText(int n) => "◈ ECHOES " + n;

        internal static string Pips(int level, int max) =>
            new string('●', Mathf.Clamp(level, 0, max)) + new string('○', Mathf.Max(0, max - level));

        private void OnDestroy() => onClosed?.Invoke();

        private void Update()
        {
            var cam = Camera.main;
            if (cam != null) follow.Tick(transform, cam.transform, Time.unscaledDeltaTime);
        }
    }
}
