using System.Collections.Generic;
using UnityEngine;
using Ronin7.Combat;
using Ronin7.Core;

namespace Ronin7.World.Story
{
    /// <summary>
    /// Orchestrates a sequence of mission steps (dialogue, combat, navigation, extraction).
    /// </summary>
    public class MissionDirector : MonoBehaviour
    {
        [SerializeField] private List<MissionStep> steps = new();

        private int stepIndex = 0;
        private int aliveEnemyCount = 0;
        private bool isReachTriggerActive = false;
        private List<Health> subscribedEnemies = new();
        private int pendingNpcCount = 0;
        private List<DialoguePlayer> subscribedNpcDialogues = new();

        private void Start()
        {
            if (steps.Count > 0)
            {
                BeginStep(0);
            }
        }

        private void Update()
        {
            if (!isReachTriggerActive || stepIndex < 0 || stepIndex >= steps.Count)
            {
                return;
            }

            MissionStep step = steps[stepIndex];
            if (step.kind != MissionStepKind.ReachTrigger)
            {
                return;
            }

            if (step.reachPoint == null || Camera.main == null)
            {
                return;
            }

            float distance = Vector3.Distance(Camera.main.transform.position, step.reachPoint.position);
            if (distance <= step.reachRadius)
            {
                isReachTriggerActive = false;
                Advance();
            }
        }

        private void BeginStep(int index)
        {
            if (index < 0 || index >= steps.Count)
            {
                return;
            }

            stepIndex = index;
            MissionStep step = steps[index];

            switch (step.kind)
            {
                case MissionStepKind.Dialogue:
                    BeginDialogue(step);
                    break;
                case MissionStepKind.DefeatEnemies:
                    BeginDefeatEnemies(step);
                    break;
                case MissionStepKind.SpawnAndDefeat:
                    BeginSpawnAndDefeat(step);
                    break;
                case MissionStepKind.ReachTrigger:
                    BeginReachTrigger(step);
                    break;
                case MissionStepKind.Prompt:
                    BeginPrompt(step);
                    break;
                case MissionStepKind.ReachExtraction:
                    BeginReachExtraction(step);
                    break;
                case MissionStepKind.Trigger:
                    BeginTrigger(step);
                    break;
                case MissionStepKind.TalkToNpcs:
                    BeginTalkToNpcs(step);
                    break;
                case MissionStepKind.Hack:
                    BeginHack(step);
                    break;
                case MissionStepKind.DefeatWaves:
                    BeginDefeatWaves(step);
                    break;
            }
        }

        private void BeginTrigger(MissionStep step)
        {
            if (step.triggerObjects != null)
            {
                foreach (GameObject go in step.triggerObjects)
                {
                    if (go != null)
                    {
                        go.SetActive(true);
                    }
                }
            }

            Advance();
        }

        private void BeginTalkToNpcs(MissionStep step)
        {
            pendingNpcCount = 0;
            subscribedNpcDialogues.Clear();

            if (step.npcs == null || step.npcs.Length == 0)
            {
                Advance();
                return;
            }

            foreach (StoryNpc npc in step.npcs)
            {
                if (npc != null && npc.Dialogue != null && !npc.Talked)
                {
                    pendingNpcCount++;
                    npc.Dialogue.Finished += OnNpcDialogueFinished;
                    subscribedNpcDialogues.Add(npc.Dialogue);
                }
            }

            if (pendingNpcCount == 0)
            {
                Advance();
            }
        }

        private void OnNpcDialogueFinished()
        {
            // DialoguePlayer now guarantees Finished fires even from OnDisable during scene
            // teardown — don't advance/begin steps on a director that is itself going away.
            if (!isActiveAndEnabled) return;

            pendingNpcCount--;
            if (pendingNpcCount <= 0)
            {
                UnsubscribeFromNpcs();
                Advance();
            }
        }

        private void UnsubscribeFromNpcs()
        {
            foreach (DialoguePlayer dialogue in subscribedNpcDialogues)
            {
                if (dialogue != null)
                {
                    dialogue.Finished -= OnNpcDialogueFinished;
                }
            }
            subscribedNpcDialogues.Clear();
        }

        private void BeginHack(MissionStep step)
        {
            if (step.hackTerminal == null)
            {
                Advance();
                return;
            }

            step.hackTerminal.Hacked += OnHacked;
            step.hackTerminal.Activate();
        }

        private void BeginDefeatWaves(MissionStep step)
        {
            if (step.waveSpawner == null)
            {
                Advance();
                return;
            }

            step.waveSpawner.Completed += OnWavesCompleted;
            step.waveSpawner.Begin();
        }

        private void OnHacked()
        {
            if (stepIndex >= 0 && stepIndex < steps.Count)
            {
                MissionStep step = steps[stepIndex];
                if (step.hackTerminal != null)
                {
                    step.hackTerminal.Hacked -= OnHacked;
                }
            }
            Advance();
        }

        private void OnWavesCompleted()
        {
            if (stepIndex >= 0 && stepIndex < steps.Count)
            {
                MissionStep step = steps[stepIndex];
                if (step.waveSpawner != null)
                {
                    step.waveSpawner.Completed -= OnWavesCompleted;
                }
            }
            Advance();
        }

        private void BeginDialogue(MissionStep step)
        {
            if (step.dialogue == null)
            {
                Advance();
                return;
            }

            step.dialogue.Finished += OnDialogueFinished;
            step.dialogue.Play();
        }

        private void OnDialogueFinished()
        {
            // See OnNpcDialogueFinished: Finished can now arrive from a teardown-path OnDisable.
            if (!isActiveAndEnabled) return;

            if (stepIndex >= 0 && stepIndex < steps.Count)
            {
                MissionStep step = steps[stepIndex];
                if (step.dialogue != null)
                {
                    step.dialogue.Finished -= OnDialogueFinished;
                }
            }
            Advance();
        }

        private void BeginDefeatEnemies(MissionStep step)
        {
            aliveEnemyCount = 0;
            subscribedEnemies.Clear();

            if (step.enemies == null || step.enemies.Length == 0)
            {
                Advance();
                return;
            }

            foreach (Health health in step.enemies)
            {
                if (health != null)
                {
                    health.gameObject.SetActive(true);
                    if (health.IsAlive)
                    {
                        aliveEnemyCount++;
                        health.Died += OnEnemyDefeated;
                        subscribedEnemies.Add(health);
                    }
                }
            }

            if (aliveEnemyCount == 0)
            {
                Advance();
            }
        }

        private void BeginSpawnAndDefeat(MissionStep step)
        {
            aliveEnemyCount = 0;
            subscribedEnemies.Clear();

            if (step.enemyPrefab == null || step.spawnPoints == null || step.spawnPoints.Length == 0)
            {
                Advance();
                return;
            }

            foreach (Transform spawnPoint in step.spawnPoints)
            {
                if (spawnPoint != null)
                {
                    GameObject enemyInstance = Instantiate(step.enemyPrefab, spawnPoint.position, spawnPoint.rotation);
                    Health health = enemyInstance.GetComponentInChildren<Health>();
                    if (health != null && health.IsAlive)
                    {
                        aliveEnemyCount++;
                        health.Died += OnEnemyDefeated;
                        subscribedEnemies.Add(health);
                    }
                }
            }

            if (aliveEnemyCount == 0)
            {
                Advance();
            }
        }

        private void OnEnemyDefeated()
        {
            aliveEnemyCount--;
            if (aliveEnemyCount <= 0)
            {
                UnsubscribeFromEnemies();
                Advance();
            }
        }

        private void UnsubscribeFromEnemies()
        {
            foreach (Health health in subscribedEnemies)
            {
                if (health != null)
                {
                    health.Died -= OnEnemyDefeated;
                }
            }
            subscribedEnemies.Clear();
        }

        private void BeginReachTrigger(MissionStep step)
        {
            isReachTriggerActive = true;
        }

        private void BeginPrompt(MissionStep step)
        {
            if (step.promptObject != null)
            {
                step.promptObject.SetActive(true);
            }
        }

        private void BeginReachExtraction(MissionStep step)
        {
            if (step.extraction == null)
            {
                Advance();
                return;
            }

            step.extraction.Extracted += OnExtractionComplete;
            step.extraction.Activate();
        }

        private void OnExtractionComplete()
        {
            if (stepIndex >= 0 && stepIndex < steps.Count)
            {
                MissionStep step = steps[stepIndex];
                if (step.extraction != null)
                {
                    step.extraction.Extracted -= OnExtractionComplete;
                }
            }

            EventBus.Publish(new ZoneCompleted());
            Advance();
        }

        public void AdvanceFromPrompt()
        {
            if (stepIndex < 0 || stepIndex >= steps.Count)
            {
                return;
            }

            MissionStep step = steps[stepIndex];
            if (step.kind != MissionStepKind.Prompt)
            {
                Debug.LogWarning("[Mission] AdvanceFromPrompt called but current step is not a Prompt.");
                return;
            }

            if (step.promptObject != null)
            {
                step.promptObject.SetActive(false);
            }

            Advance();
        }

        private void Advance()
        {
            // Backstop for every event path (dialogue, hack, waves, ...): DialoguePlayer's
            // Finished guarantee can cascade events out of teardown-path OnDisables — never
            // begin new steps (Instantiate/SetActive/StartCoroutine) on a director going away.
            if (!isActiveAndEnabled) return;

            UnsubscribeFromCurrentStep();
            stepIndex++;

            if (stepIndex >= steps.Count)
            {
                Debug.Log("[Mission] Complete");
                return;
            }

            BeginStep(stepIndex);
        }

        private void UnsubscribeFromCurrentStep()
        {
            if (stepIndex < 0 || stepIndex >= steps.Count)
            {
                return;
            }

            MissionStep step = steps[stepIndex];

            switch (step.kind)
            {
                case MissionStepKind.Dialogue:
                    if (step.dialogue != null)
                    {
                        step.dialogue.Finished -= OnDialogueFinished;
                    }
                    break;

                case MissionStepKind.DefeatEnemies:
                case MissionStepKind.SpawnAndDefeat:
                    UnsubscribeFromEnemies();
                    break;

                case MissionStepKind.ReachExtraction:
                    if (step.extraction != null)
                    {
                        step.extraction.Extracted -= OnExtractionComplete;
                    }
                    break;

                case MissionStepKind.TalkToNpcs:
                    UnsubscribeFromNpcs();
                    break;

                case MissionStepKind.Hack:
                    if (step.hackTerminal != null)
                    {
                        step.hackTerminal.Hacked -= OnHacked;
                    }
                    break;

                case MissionStepKind.DefeatWaves:
                    if (step.waveSpawner != null)
                    {
                        step.waveSpawner.Completed -= OnWavesCompleted;
                    }
                    break;
            }

            isReachTriggerActive = false;
            subscribedEnemies.Clear();
            subscribedNpcDialogues.Clear();
        }
    }
}
