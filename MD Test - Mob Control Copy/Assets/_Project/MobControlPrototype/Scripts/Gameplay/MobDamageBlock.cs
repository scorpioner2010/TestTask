using System;
using System.Collections;
using UnityEngine;

namespace MobControlPrototype.Gameplay
{
    [RequireComponent(typeof(Collider))]
    [DisallowMultipleComponent]
    public sealed class MobDamageBlock : MonoBehaviour
    {
        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        private static readonly int ColorId = Shader.PropertyToID("_Color");
        private const float PulseScaleBoost = 2.25f;

        [SerializeField, Min(1)] private int health = 30;
        [SerializeField, Min(1)] private int damagePerUnit = 1;
        [SerializeField] private Renderer[] feedbackRenderers;
        [SerializeField] private TextMesh healthLabel;

        [Header("Feedback")]
        [SerializeField, Min(0.05f)] private float hitPulseDuration = 0.12f;
        [SerializeField, Range(1f, 1.3f)] private float hitPulseScaleMultiplier = 1.08f;
        [SerializeField] private Color hitFlashColor = new Color(1f, 0.78f, 0.42f, 1f);
        [SerializeField, Min(0.05f)] private float destroyDuration = 0.18f;
        [SerializeField, Min(0f)] private float sinkDistance = 0.5f;
        [SerializeField] private Color destroyedTint = new Color(0.28f, 0.28f, 0.28f, 1f);

        private int _currentHealth;
        private bool _destroyed;
        private Collider _trigger;
        private Coroutine _feedbackRoutine;
        private Vector3 _baseLocalScale = Vector3.one;
        private Vector3 _baseLocalPosition;
        private Color _baseLabelColor = Color.white;
        private MaterialPropertyBlock _propertyBlock;

        public event Action<int, int> HealthChanged;

        public int CurrentHealth => _currentHealth;
        public int MaxHealth => health;

        private void Awake()
        {
            _trigger = GetComponent<Collider>();
            _trigger.isTrigger = true;
            _baseLocalScale = transform.localScale;
            _baseLocalPosition = transform.localPosition;
            if (healthLabel != null)
            {
                _baseLabelColor = healthLabel.color;
            }

            _currentHealth = Mathf.Max(1, health);
            ApplyRuntimeState();
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

            transform.localScale = _baseLocalScale;
            transform.localPosition = _baseLocalPosition;
            ApplyRendererTint(Color.white, clearTint: true);
            if (healthLabel != null)
            {
                healthLabel.color = _baseLabelColor;
            }

            UpdateHealthLabel();
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
            destroyDuration = Mathf.Max(0.05f, destroyDuration);
            sinkDistance = Mathf.Max(0f, sinkDistance);

            Collider trigger = GetComponent<Collider>();
            if (trigger != null)
            {
                trigger.isTrigger = true;
            }

            if (!Application.isPlaying)
            {
                _currentHealth = health;
                UpdateHealthLabel();
            }
        }

        public bool TryDamage(UnitRunner runner)
        {
            if (_destroyed || runner == null || !runner.IsActive || runner.Manager == null)
            {
                return false;
            }

            _currentHealth = Mathf.Max(0, _currentHealth - damagePerUnit);
            runner.Manager.RemoveRunnerWithSink(runner);
            NotifyHealthChanged();

            if (_currentHealth > 0)
            {
                PlayHitPulse();
            }
            else
            {
                DestroyBlock();
            }

            return true;
        }

        private void ApplyRuntimeState()
        {
            _destroyed = false;
            transform.localScale = _baseLocalScale;
            transform.localPosition = _baseLocalPosition;
            ApplyRendererTint(Color.white, clearTint: true);

            if (healthLabel != null)
            {
                healthLabel.color = _baseLabelColor;
            }

            UpdateHealthLabel();
        }

        private void PlayHitPulse()
        {
            if (!gameObject.activeInHierarchy)
            {
                return;
            }

            if (_feedbackRoutine != null)
            {
                StopCoroutine(_feedbackRoutine);
            }

            _feedbackRoutine = StartCoroutine(HitPulseRoutine());
        }

        private IEnumerator HitPulseRoutine()
        {
            Vector3 pulseScale = _baseLocalScale * GetBoostedScaleMultiplier(hitPulseScaleMultiplier);
            float halfDuration = hitPulseDuration * 0.5f;
            float elapsed = 0f;

            ApplyRendererTint(hitFlashColor, clearTint: false);
            if (healthLabel != null)
            {
                healthLabel.color = hitFlashColor;
            }

            while (elapsed < halfDuration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / halfDuration);
                transform.localScale = Vector3.LerpUnclamped(_baseLocalScale, pulseScale, t);
                yield return null;
            }

            elapsed = 0f;
            while (elapsed < halfDuration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / halfDuration);
                transform.localScale = Vector3.LerpUnclamped(pulseScale, _baseLocalScale, t);
                yield return null;
            }

            transform.localScale = _baseLocalScale;
            ApplyRendererTint(Color.white, clearTint: true);
            if (healthLabel != null)
            {
                healthLabel.color = _baseLabelColor;
            }

            _feedbackRoutine = null;
        }

        private static float GetBoostedScaleMultiplier(float scaleMultiplier)
        {
            return 1f + (Mathf.Max(1f, scaleMultiplier) - 1f) * PulseScaleBoost;
        }

        private void DestroyBlock()
        {
            _destroyed = true;

            if (_trigger != null)
            {
                _trigger.enabled = false;
            }

            if (healthLabel != null)
            {
                healthLabel.color = destroyedTint;
            }

            ApplyRendererTint(destroyedTint, clearTint: false);

            if (!gameObject.activeInHierarchy)
            {
                gameObject.SetActive(false);
                return;
            }

            if (_feedbackRoutine != null)
            {
                StopCoroutine(_feedbackRoutine);
            }

            _feedbackRoutine = StartCoroutine(DestroyRoutine());
        }

        private IEnumerator DestroyRoutine()
        {
            Vector3 startScale = transform.localScale;
            Vector3 endScale = new Vector3(
                _baseLocalScale.x,
                Mathf.Max(0.01f, _baseLocalScale.y * 0.08f),
                _baseLocalScale.z);
            Vector3 startPosition = transform.localPosition;
            Vector3 endPosition = _baseLocalPosition + Vector3.down * sinkDistance;
            float elapsed = 0f;

            while (elapsed < destroyDuration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / destroyDuration);
                float eased = t * t * (3f - 2f * t);
                transform.localScale = Vector3.LerpUnclamped(startScale, endScale, eased);
                transform.localPosition = Vector3.LerpUnclamped(startPosition, endPosition, eased);
                yield return null;
            }

            _feedbackRoutine = null;
            gameObject.SetActive(false);
        }

        private void ApplyRendererTint(Color color, bool clearTint)
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
                if (clearTint)
                {
                    _propertyBlock.Clear();
                }
                else
                {
                    _propertyBlock.SetColor(ColorId, color);
                    _propertyBlock.SetColor(BaseColorId, color);
                }

                renderer.SetPropertyBlock(_propertyBlock);
            }
        }

        private void UpdateHealthLabel()
        {
            if (healthLabel != null)
            {
                healthLabel.text = _currentHealth.ToString();
            }
        }

        private void NotifyHealthChanged()
        {
            UpdateHealthLabel();
            HealthChanged?.Invoke(_currentHealth, health);
        }
    }
}
