using MobControlPrototype.Infrastructure;
using UnityEngine;

namespace MobControlPrototype.Gameplay
{
    [DisallowMultipleComponent]
    public sealed class GateModifier : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private Transform animatedRoot;
        [SerializeField] private Collider triggerCollider;
        [SerializeField] private PrototypeGameplayVfxService gameplayVfxService;

        [Header("Gate")]
        [SerializeField] private GateOperation operation = GateOperation.Add;
        [SerializeField, Min(1)] private int value = 10;
        [SerializeField, Min(0.05f)] private float pulseDuration = 0.14f;
        [SerializeField, Range(1f, 1.5f)] private float pulseScaleMultiplier = 1.08f;
        [SerializeField] private bool moveHorizontally = true;
        [SerializeField, Min(0f)] private float horizontalTravelDistance = 2.8f;
        [SerializeField, Min(0.2f)] private float horizontalCycleDuration = 2.4f;
        [SerializeField] private float horizontalPhaseOffset;

        private Vector3 _baseLocalPosition;
        private Vector3 _baseScale;
        private Coroutine _pulseRoutine;
        private bool _hasCachedBaseTransform;

        public GateOperation Operation => operation;
        public int Value => value;

        private void Awake()
        {
            CacheBaseTransform(force: true);
            EnsureTrigger();
        }

        private void OnEnable()
        {
            CacheBaseTransform(force: false);

            ApplyHorizontalMotion(true);
            Transform root = ResolveAnimatedRoot();
            if (root != null)
            {
                root.localScale = _baseScale;
            }
        }

        private void OnValidate()
        {
            value = Mathf.Max(1, value);
            pulseDuration = Mathf.Max(0.05f, pulseDuration);
            pulseScaleMultiplier = Mathf.Max(1f, pulseScaleMultiplier);
            horizontalTravelDistance = Mathf.Max(0f, horizontalTravelDistance);
            horizontalCycleDuration = Mathf.Max(0.2f, horizontalCycleDuration);
            EnsureTrigger();

            if (!Application.isPlaying)
            {
                CacheBaseTransform(force: true);
            }
        }

        private void Update()
        {
            ApplyHorizontalMotion(false);
        }

        public bool TryApply(UnitRunner runner)
        {
            if (runner == null || !runner.IsActive)
            {
                return false;
            }

            int gateId = GetInstanceID();
            if (runner.HasPassedGate(gateId))
            {
                return false;
            }

            runner.MarkGatePassed(gateId);

            Collider trigger = ResolveTriggerCollider();
            Transform root = ResolveAnimatedRoot();
            float spawnZ = (root != null ? root.position.z : transform.position.z) + 1.2f;
            if (trigger != null)
            {
                spawnZ = trigger.bounds.max.z + 0.8f;
            }

            int gateDelta = runner.Manager.ApplyGate(runner, operation, value, spawnZ);
            bool applied = gateDelta != 0;
            if (applied)
            {
                PlayPulse();
                if (gameplayVfxService == null)
                {
                    ServiceLocator.TryGet(out gameplayVfxService);
                }

                gameplayVfxService?.PlayGateActivation(operation, root != null ? root : transform, trigger);
            }

            return applied;
        }

        private void PlayPulse()
        {
            if (!gameObject.activeInHierarchy)
            {
                return;
            }

            if (_pulseRoutine != null)
            {
                StopCoroutine(_pulseRoutine);
            }

            _pulseRoutine = StartCoroutine(PulseRoutine());
        }

        private System.Collections.IEnumerator PulseRoutine()
        {
            Transform root = ResolveAnimatedRoot();
            if (root == null)
            {
                _pulseRoutine = null;
                yield break;
            }

            Vector3 pulseScale = _baseScale * pulseScaleMultiplier;
            float halfDuration = pulseDuration * 0.5f;
            float elapsed = 0f;

            while (elapsed < halfDuration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / halfDuration);
                root.localScale = Vector3.LerpUnclamped(_baseScale, pulseScale, t);
                yield return null;
            }

            elapsed = 0f;
            while (elapsed < halfDuration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / halfDuration);
                root.localScale = Vector3.LerpUnclamped(pulseScale, _baseScale, t);
                yield return null;
            }

            root.localScale = _baseScale;
            _pulseRoutine = null;
        }

        private void ApplyHorizontalMotion(bool instant)
        {
            Transform root = ResolveAnimatedRoot();
            if (root == null)
            {
                return;
            }

            if (!moveHorizontally)
            {
                root.localPosition = _baseLocalPosition;
                return;
            }

            float cycleDuration = Mathf.Max(0.2f, horizontalCycleDuration);
            float angularFrequency = Mathf.PI * 2f / cycleDuration;
            float time = instant ? 0f : Time.time;
            float xOffset = Mathf.Sin((time + horizontalPhaseOffset) * angularFrequency) * horizontalTravelDistance;
            root.localPosition = _baseLocalPosition + Vector3.right * xOffset;
        }

        private Transform ResolveAnimatedRoot()
        {
            if (animatedRoot == null)
            {
                animatedRoot = transform.childCount > 0 ? transform.GetChild(0) : transform;
            }

            return animatedRoot;
        }

        private Collider ResolveTriggerCollider()
        {
            if (triggerCollider == null)
            {
                triggerCollider = GetComponent<Collider>();
                triggerCollider ??= GetComponentInChildren<Collider>(true);
            }

            return triggerCollider;
        }

        private void EnsureTrigger()
        {
            Collider trigger = ResolveTriggerCollider();
            if (trigger != null)
            {
                trigger.isTrigger = true;
            }
        }

        private void CacheBaseTransform(bool force)
        {
            if (_hasCachedBaseTransform && !force)
            {
                return;
            }

            Transform root = ResolveAnimatedRoot();
            if (root == null)
            {
                return;
            }

            _baseLocalPosition = root.localPosition;
            _baseScale = root.localScale;
            _hasCachedBaseTransform = true;
        }
    }
}
