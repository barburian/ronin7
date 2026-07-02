using Ronin7.Combat;
using Ronin7.Core;
using UnityEngine;

namespace Ronin7.World
{
    /// <summary>
    /// Cycles through temporal phases on a timer, toggling which GameObjects are visible/active per phase.
    /// Optionally damages a target when they are out of the current phase.
    /// </summary>
    public class TimeLoopController : MonoBehaviour
    {
        [System.Serializable]
        public struct PhaseGroup
        {
            public GameObject[] members;
        }

        [SerializeField] private float phaseInterval = 2f;
        [SerializeField] private PhaseGroup[] phaseGroups;
        [SerializeField] private bool damageWhenOutOfPhase = false;
        [SerializeField] private Health target;
        [SerializeField] private float outOfPhaseDamage = 5f;

        public int CurrentPhase { get; private set; }

        private float accumulator;

        private void Start()
        {
            ApplyPhase();
        }

        private void Update()
        {
            if (phaseGroups == null || phaseGroups.Length == 0 || phaseInterval <= 0f)
            {
                return;
            }

            accumulator += Time.deltaTime;
            if (accumulator >= phaseInterval)
            {
                accumulator = 0f;
                AdvancePhase();
            }

            if (damageWhenOutOfPhase && target != null && target.IsAlive)
            {
                var damageInfo = new DamageInfo(
                    outOfPhaseDamage * Time.deltaTime,
                    transform.position,
                    Vector3.zero,
                    gameObject
                );
                target.ApplyDamage(damageInfo);
            }
        }

        public void AdvancePhase()
        {
            if (phaseGroups == null || phaseGroups.Length == 0)
            {
                return;
            }

            CurrentPhase = (CurrentPhase + 1) % phaseGroups.Length;
            ApplyPhase();
        }

        private void ApplyPhase()
        {
            if (phaseGroups == null)
            {
                return;
            }

            for (int i = 0; i < phaseGroups.Length; i++)
            {
                var group = phaseGroups[i];
                if (group.members == null) continue;

                foreach (var member in group.members)
                {
                    if (member != null)
                    {
                        member.SetActive(i == CurrentPhase);
                    }
                }
            }
        }
    }
}
