using System;
using UnityEngine;

namespace MobControlPrototype.Gameplay
{
    [RequireComponent(typeof(Collider))]
    [DisallowMultipleComponent]
    public sealed class FinishTarget : MonoBehaviour
    {
        [SerializeField, Min(1)] private int health = 20;
        [SerializeField, Min(1)] private int damagePerUnit = 1;
        [SerializeField] private Renderer[] feedbackRenderers;
        [SerializeField] private Transform[] enemySpawnPoints;
        [SerializeField] private Color successColor = new Color(0.16f, 0.76f, 0.31f, 1f);

        private int _currentHealth;
        private bool _destroyed;

        public event Action<int, int> HealthChanged;

        public int CurrentHealth => _currentHealth;
        public int MaxHealth => health;

        private void Awake()
        {
            _currentHealth = Mathf.Max(1, health);
            Collider trigger = GetComponent<Collider>();
            trigger.isTrigger = true;
            NotifyHealthChanged();
        }

        private void OnValidate()
        {
            health = Mathf.Max(1, health);
            damagePerUnit = Mathf.Max(1, damagePerUnit);
            if (!Application.isPlaying)
            {
                _currentHealth = health;
            }
        }

        public bool TryDamage(UnitRunner runner)
        {
            if (_destroyed || runner == null || !runner.IsActive)
            {
                return false;
            }

            _currentHealth = Mathf.Max(0, _currentHealth - damagePerUnit);
            runner.Manager.RemoveRunner(runner);
            NotifyHealthChanged();

            if (_currentHealth <= 0)
            {
                _destroyed = true;
                runner.Manager.CompleteLevel(true);
                ApplyResultFeedback(true);
            }

            return true;
        }

        public Vector3 GetEnemySpawnPosition()
        {
            int validSpawnPointCount = GetValidSpawnPointCount();
            if (validSpawnPointCount <= 0)
            {
                return transform.position;
            }

            int targetIndex = UnityEngine.Random.Range(0, validSpawnPointCount);
            for (int i = 0; i < enemySpawnPoints.Length; i++)
            {
                Transform spawnPoint = enemySpawnPoints[i];
                if (spawnPoint == null)
                {
                    continue;
                }

                if (targetIndex == 0)
                {
                    return spawnPoint.position;
                }

                targetIndex--;
            }

            return transform.position;
        }

        private void ApplyResultFeedback(bool success)
        {
            Color color = successColor;
            if (feedbackRenderers == null)
            {
                return;
            }

            for (int i = 0; i < feedbackRenderers.Length; i++)
            {
                if (feedbackRenderers[i] != null)
                {
                    feedbackRenderers[i].material.color = color;
                }
            }
        }

        private void NotifyHealthChanged()
        {
            HealthChanged?.Invoke(_currentHealth, health);
        }

        private int GetValidSpawnPointCount()
        {
            if (enemySpawnPoints == null || enemySpawnPoints.Length == 0)
            {
                return 0;
            }

            int validCount = 0;
            for (int i = 0; i < enemySpawnPoints.Length; i++)
            {
                if (enemySpawnPoints[i] != null)
                {
                    validCount++;
                }
            }

            return validCount;
        }
    }
}
