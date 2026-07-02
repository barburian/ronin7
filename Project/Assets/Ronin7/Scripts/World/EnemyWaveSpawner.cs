using System;
using System.Collections;
using UnityEngine;
using Ronin7.Combat;
using Ronin7.World.Story;

namespace Ronin7.World
{
    /// <summary>
    /// Orchestrates a sequence of enemy waves. Each wave activates pre-placed enemy GameObjects,
    /// optionally plays audio and dialogue, and advances when all enemies in that wave are defeated.
    /// </summary>
    public class EnemyWaveSpawner : MonoBehaviour
    {
        [System.Serializable]
        public class Wave
        {
            public Health[] enemies;
            public DialoguePlayer bark;
        }

        [SerializeField] private Wave[] waves;
        [SerializeField] private Transform triggerPoint;
        [SerializeField] private float triggerRadius = 3f;
        [SerializeField] private AudioClip waveSting;
        [SerializeField] private AudioSource audioSource;

        public event Action Completed;
        public bool IsComplete { get; private set; }

        private int currentWaveIndex = -1;
        private int aliveEnemyCount = 0;
        private Coroutine beginRoutine;

        /// <summary>Arm the spawner to begin polling for the player entering the trigger radius.</summary>
        public void Begin()
        {
            if (beginRoutine != null)
            {
                StopCoroutine(beginRoutine);
            }
            beginRoutine = StartCoroutine(BeginRoutine());
        }

        private IEnumerator BeginRoutine()
        {
            // Poll until the player camera exists.
            while (Camera.main == null)
            {
                yield return null;
            }

            // Poll until the player enters the trigger radius.
            while (true)
            {
                if (triggerPoint != null && Camera.main != null)
                {
                    Vector3 toCamera = Camera.main.transform.position - triggerPoint.position;
                    toCamera.y = 0f; // horizontal distance
                    if (toCamera.magnitude <= triggerRadius)
                    {
                        break;
                    }
                }
                yield return new WaitForSeconds(0.2f); // ~5 Hz
            }

            // Trigger entered: start wave 0.
            StartWave(0);
        }

        private void StartWave(int waveIndex)
        {
            if (waveIndex < 0 || waveIndex >= waves.Length)
            {
                CompleteSpawner();
                return;
            }

            currentWaveIndex = waveIndex;
            Wave wave = waves[waveIndex];

            aliveEnemyCount = 0;

            // Activate enemies and subscribe to their Died events.
            if (wave.enemies != null)
            {
                foreach (Health health in wave.enemies)
                {
                    if (health != null)
                    {
                        health.gameObject.SetActive(true);
                        if (health.IsAlive)
                        {
                            aliveEnemyCount++;
                            health.Died += OnEnemyDefeated;
                        }
                    }
                }
            }

            // Play wave sting if configured.
            if (waveSting != null && audioSource != null)
            {
                audioSource.PlayOneShot(waveSting);
            }

            // Play bark dialogue if configured (non-blocking).
            if (wave.bark != null)
            {
                wave.bark.Play();
            }

            // If no enemies in this wave, advance immediately.
            if (aliveEnemyCount == 0)
            {
                StartWave(currentWaveIndex + 1);
            }
        }

        private void OnEnemyDefeated()
        {
            aliveEnemyCount--;
            if (aliveEnemyCount <= 0)
            {
                UnsubscribeFromCurrentWave();
                StartWave(currentWaveIndex + 1);
            }
        }

        private void UnsubscribeFromCurrentWave()
        {
            if (currentWaveIndex < 0 || currentWaveIndex >= waves.Length)
            {
                return;
            }

            Wave wave = waves[currentWaveIndex];
            if (wave.enemies != null)
            {
                foreach (Health health in wave.enemies)
                {
                    if (health != null)
                    {
                        health.Died -= OnEnemyDefeated;
                    }
                }
            }
        }

        private void CompleteSpawner()
        {
            IsComplete = true;
            Completed?.Invoke();
        }
    }
}
