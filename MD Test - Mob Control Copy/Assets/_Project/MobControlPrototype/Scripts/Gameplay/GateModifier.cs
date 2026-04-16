using UnityEngine;

namespace MobControlPrototype.Gameplay
{
    [RequireComponent(typeof(Collider))]
    [DisallowMultipleComponent]
    public sealed class GateModifier : MonoBehaviour
    {
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

        public GateOperation Operation => operation;
        public int Value => value;

        private void Awake()
        {
            _baseLocalPosition = transform.localPosition;
            _baseScale = transform.localScale;
            Collider trigger = GetComponent<Collider>();
            trigger.isTrigger = true;
        }

        private void OnEnable()
        {
            if (_baseScale == Vector3.zero)
            {
                _baseScale = transform.localScale;
            }

            if (_baseLocalPosition == Vector3.zero)
            {
                _baseLocalPosition = transform.localPosition;
            }

            ApplyHorizontalMotion(true);
            transform.localScale = _baseScale;
        }

        private void OnValidate()
        {
            value = Mathf.Max(1, value);
            pulseDuration = Mathf.Max(0.05f, pulseDuration);
            pulseScaleMultiplier = Mathf.Max(1f, pulseScaleMultiplier);
            horizontalTravelDistance = Mathf.Max(0f, horizontalTravelDistance);
            horizontalCycleDuration = Mathf.Max(0.2f, horizontalCycleDuration);
            Collider trigger = GetComponent<Collider>();
            if (trigger != null)
            {
                trigger.isTrigger = true;
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

            Collider trigger = GetComponent<Collider>();
            float spawnZ = transform.position.z + 1.2f;
            if (trigger != null)
            {
                spawnZ = trigger.bounds.max.z + 0.8f;
            }

            bool applied = runner.Manager.ApplyGate(runner, operation, value, spawnZ) != 0;
            if (applied)
            {
                PlayPulse();
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
            Vector3 pulseScale = _baseScale * pulseScaleMultiplier;
            float halfDuration = pulseDuration * 0.5f;
            float elapsed = 0f;

            while (elapsed < halfDuration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / halfDuration);
                transform.localScale = Vector3.LerpUnclamped(_baseScale, pulseScale, t);
                yield return null;
            }

            elapsed = 0f;
            while (elapsed < halfDuration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / halfDuration);
                transform.localScale = Vector3.LerpUnclamped(pulseScale, _baseScale, t);
                yield return null;
            }

            transform.localScale = _baseScale;
            _pulseRoutine = null;
        }

        private void ApplyHorizontalMotion(bool instant)
        {
            if (!moveHorizontally)
            {
                transform.localPosition = _baseLocalPosition;
                return;
            }

            float cycleDuration = Mathf.Max(0.2f, horizontalCycleDuration);
            float angularFrequency = Mathf.PI * 2f / cycleDuration;
            float time = instant ? 0f : Time.time;
            float xOffset = Mathf.Sin((time + horizontalPhaseOffset) * angularFrequency) * horizontalTravelDistance;
            transform.localPosition = _baseLocalPosition + Vector3.right * xOffset;
        }
    }
}
