using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace MobControlPrototype.Gameplay
{
    [RequireComponent(typeof(Collider))]
    [DisallowMultipleComponent]
    public sealed class FinishTarget : MonoBehaviour
    {
        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        private static readonly int ColorId = Shader.PropertyToID("_Color");
        private const string PulseTargetName = "Body";
        private const float PulseScaleBoost = 2.25f;

        [SerializeField, Min(1)] private int health = 20;
        [SerializeField, Min(1)] private int damagePerUnit = 1;
        [SerializeField] private Renderer[] feedbackRenderers;
        [SerializeField] private Transform[] enemySpawnPoints;

        [Header("Feedback")]
        [SerializeField, Min(0.05f)] private float hitPulseDuration = 0.14f;
        [SerializeField, Range(1f, 1.3f)] private float hitPulseScaleMultiplier = 1.08f;
        [SerializeField] private Color hitFlashColor = new Color(1f, 0.78f, 0.42f, 1f);
        [SerializeField] private Color successColor = new Color(0.16f, 0.76f, 0.31f, 1f);

        private int _currentHealth;
        private bool _destroyed;
        private Collider _trigger;
        private Coroutine _feedbackRoutine;
        private Transform[] _pulseTargets = Array.Empty<Transform>();
        private Vector3[] _basePulseTargetScales = Array.Empty<Vector3>();
        private MaterialPropertyBlock _propertyBlock;

        public event Action<int, int> HealthChanged;

        public int CurrentHealth => _currentHealth;
        public int MaxHealth => health;

        private void Awake()
        {
            _trigger = GetComponent<Collider>();
            RefreshPulseTargets();
            _currentHealth = Mathf.Max(1, health);
            _trigger.isTrigger = true;
            NotifyHealthChanged();
        }

        private void OnEnable()
        {
            if (_trigger == null)
            {
                _trigger = GetComponent<Collider>();
            }

            if (_trigger != null)
            {
                _trigger.isTrigger = true;
                _trigger.enabled = true;
            }

            if (_pulseTargets == null || _pulseTargets.Length == 0)
            {
                RefreshPulseTargets();
            }

            if (!_destroyed)
            {
                ResetPulseTargetScale();
                ApplyRendererFlash(Color.white, clearFlash: true);
            }
        }

        private void OnDisable()
        {
            if (_feedbackRoutine != null)
            {
                StopCoroutine(_feedbackRoutine);
                _feedbackRoutine = null;
            }
        }

        private void OnValidate()
        {
            health = Mathf.Max(1, health);
            damagePerUnit = Mathf.Max(1, damagePerUnit);
            hitPulseDuration = Mathf.Max(0.05f, hitPulseDuration);
            hitPulseScaleMultiplier = Mathf.Max(1f, hitPulseScaleMultiplier);
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

            if (_currentHealth > 0)
            {
                PlayHitPulse();
            }
            else
            {
                _destroyed = true;
                StopFeedbackRoutine();
                ResetPulseTargetScale();
                ApplyRendererFlash(Color.white, clearFlash: true);
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

        private void PlayHitPulse()
        {
            if (!gameObject.activeInHierarchy)
            {
                return;
            }

            StopFeedbackRoutine();
            _feedbackRoutine = StartCoroutine(HitPulseRoutine());
        }

        private IEnumerator HitPulseRoutine()
        {
            float pulseScaleMultiplier = GetBoostedScaleMultiplier(hitPulseScaleMultiplier);
            float halfDuration = hitPulseDuration * 0.5f;
            float elapsed = 0f;

            ApplyRendererFlash(hitFlashColor, clearFlash: false);

            while (elapsed < halfDuration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / halfDuration);
                ApplyPulseScaleMultiplier(Mathf.LerpUnclamped(1f, pulseScaleMultiplier, t));
                yield return null;
            }

            elapsed = 0f;
            while (elapsed < halfDuration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / halfDuration);
                ApplyPulseScaleMultiplier(Mathf.LerpUnclamped(pulseScaleMultiplier, 1f, t));
                yield return null;
            }

            ResetPulseTargetScale();
            ApplyRendererFlash(Color.white, clearFlash: true);
            _feedbackRoutine = null;
        }

        private void ApplyRendererFlash(Color color, bool clearFlash)
        {
            if (feedbackRenderers == null || feedbackRenderers.Length == 0)
            {
                return;
            }

            _propertyBlock ??= new MaterialPropertyBlock();

            for (int i = 0; i < feedbackRenderers.Length; i++)
            {
                Renderer renderer = feedbackRenderers[i];
                if (renderer == null)
                {
                    continue;
                }

                renderer.GetPropertyBlock(_propertyBlock);
                if (clearFlash)
                {
                    _propertyBlock.Clear();
                }
                else
                {
                    _propertyBlock.SetColor(BaseColorId, color);
                    _propertyBlock.SetColor(ColorId, color);
                }

                renderer.SetPropertyBlock(_propertyBlock);
            }
        }

        private void StopFeedbackRoutine()
        {
            if (_feedbackRoutine == null)
            {
                return;
            }

            StopCoroutine(_feedbackRoutine);
            _feedbackRoutine = null;
        }

        private void ApplyPulseScaleMultiplier(float scaleMultiplier)
        {
            if (_pulseTargets == null || _pulseTargets.Length == 0)
            {
                RefreshPulseTargets();
            }

            for (int i = 0; i < _pulseTargets.Length; i++)
            {
                Transform pulseTarget = _pulseTargets[i];
                if (pulseTarget == null)
                {
                    continue;
                }

                pulseTarget.localScale = _basePulseTargetScales[i] * scaleMultiplier;
            }
        }

        private void ResetPulseTargetScale()
        {
            if (_pulseTargets == null || _pulseTargets.Length == 0)
            {
                RefreshPulseTargets();
            }

            for (int i = 0; i < _pulseTargets.Length; i++)
            {
                if (_pulseTargets[i] != null)
                {
                    _pulseTargets[i].localScale = _basePulseTargetScales[i];
                }
            }
        }

        private Transform ResolvePulseTarget()
        {
            Transform pulseTarget = FindChildRecursive(transform, PulseTargetName);
            return pulseTarget != null ? pulseTarget : transform;
        }

        private void RefreshPulseTargets()
        {
            List<Transform> pulseTargets = new List<Transform>(4);
            if (feedbackRenderers != null && feedbackRenderers.Length > 0)
            {
                for (int i = 0; i < feedbackRenderers.Length; i++)
                {
                    Renderer renderer = feedbackRenderers[i];
                    if (renderer != null)
                    {
                        AddPulseTarget(pulseTargets, renderer.transform);
                    }
                }
            }
            else
            {
                AddPulseTarget(pulseTargets, ResolvePulseTarget());
            }

            if (pulseTargets.Count == 0)
            {
                AddPulseTarget(pulseTargets, ResolvePulseTarget());
            }

            _pulseTargets = pulseTargets.ToArray();
            _basePulseTargetScales = new Vector3[_pulseTargets.Length];
            for (int i = 0; i < _pulseTargets.Length; i++)
            {
                _basePulseTargetScales[i] = _pulseTargets[i] != null ? _pulseTargets[i].localScale : Vector3.one;
            }
        }

        private static void AddPulseTarget(List<Transform> pulseTargets, Transform candidate)
        {
            if (candidate == null || pulseTargets.Contains(candidate))
            {
                return;
            }

            pulseTargets.Add(candidate);
        }

        private static Transform FindChildRecursive(Transform root, string targetName)
        {
            if (root == null)
            {
                return null;
            }

            for (int i = 0; i < root.childCount; i++)
            {
                Transform child = root.GetChild(i);
                if (child.name == targetName)
                {
                    return child;
                }

                Transform nested = FindChildRecursive(child, targetName);
                if (nested != null)
                {
                    return nested;
                }
            }

            return null;
        }

        private static float GetBoostedScaleMultiplier(float scaleMultiplier)
        {
            return 1f + (Mathf.Max(1f, scaleMultiplier) - 1f) * PulseScaleBoost;
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
