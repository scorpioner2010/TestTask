using System;
using System.Collections.Generic;
using Dreamteck.Splines;
using UnityEngine;

namespace MobControlPrototype.Gameplay
{
    [RequireComponent(typeof(CapsuleCollider))]
    [RequireComponent(typeof(Rigidbody))]
    [DisallowMultipleComponent]
    public sealed class UnitRunner : MonoBehaviour
    {
        private const float TubeEntrySpeedFactor = 0.35f;
        private const float MinTubeEntryDuration = 0.12f;
        private const float MaxTubeEntryDuration = 0.30f;

        private readonly HashSet<int> _passedGateIds = new HashSet<int>();
        private UnitRunnerManager _manager;
        private Rigidbody _body;
        private Collider _trigger;
        private SinkFeedbackAnimator _sinkFeedback;
        private Transform _visualRoot;
        private Vector3 _baseVisualScale = Vector3.one;
        private bool _hasVisualScale;
        private bool _isSpawnAnimating;
        private float _spawnElapsed;
        private float _spawnDuration;
        private float _spawnArcHeight;
        private float _spawnStartScaleMultiplier = 1f;
        private Vector3 _spawnStartPosition;
        private Vector3 _spawnTargetPosition;
        private bool _isInTube;
        private SplineComputer _tubeSpline;
        private double _tubePercent;
        private float _tubeSpeed;
        private bool _isTubeEntering;
        private float _tubeEntryElapsed;
        private float _tubeEntryDuration;
        private Vector3 _tubeEntryStartPosition;
        private Vector3 _tubeEntryTargetPosition;
        private Quaternion _tubeEntryStartRotation = Quaternion.identity;
        private Quaternion _tubeEntryTargetRotation = Quaternion.identity;
        private Vector3 _tubeExitPosition;
        private Quaternion _tubeExitRotation = Quaternion.identity;
        private Vector3 _tubeExitDirection = Vector3.forward;
        private bool _hasMovementDirectionOverride;
        private Vector3 _movementDirectionOverride = Vector3.forward;

        public int ActiveIndex { get; set; }
        public bool IsActive { get; private set; }
        public bool IsInTube => _isInTube;
        public bool HasMovementDirectionOverride => _hasMovementDirectionOverride;
        public UnitRunnerManager Manager => _manager;
        public Rigidbody Body => _body;
        public Vector3 WorldPosition => _body != null ? _body.position : transform.position;
        public Vector3 MovementDirection => _movementDirectionOverride;

        private void Awake()
        {
            EnsureRuntimeComponents();
            ResolveVisualRoot();
        }

        public void ConfigurePhysics(float colliderRadius, float colliderHeight)
        {
            EnsureRuntimeComponents();

            _trigger.isTrigger = true;
            if (_trigger is CapsuleCollider capsule)
            {
                capsule.radius = colliderRadius;
                capsule.height = colliderHeight;
                capsule.center = new Vector3(0f, colliderHeight * 0.5f, 0f);
            }

            _body.isKinematic = true;
            _body.useGravity = false;
            _body.interpolation = RigidbodyInterpolation.Interpolate;
            _body.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;
            _body.constraints = RigidbodyConstraints.FreezeRotation;
        }

        public void Initialize(UnitRunnerManager manager)
        {
            _manager = manager;
            IsActive = true;
            _passedGateIds.Clear();
            ResetRuntimeState();
        }

        public void Deactivate()
        {
            IsActive = false;
            _passedGateIds.Clear();
            ResetRuntimeState();
        }

        public bool HasPassedGate(int gateId)
        {
            return _passedGateIds.Contains(gateId);
        }

        public void MarkGatePassed(int gateId)
        {
            _passedGateIds.Add(gateId);
        }

        public void PrepareForRemoval()
        {
            IsActive = false;
            _passedGateIds.Clear();
            _isSpawnAnimating = false;
            _spawnElapsed = 0f;
            ResetTubeTraversal();
            ClearMovementDirectionOverride();
            SetCollisionState(false);
            ResetVisualScale();
        }

        public void PlaySinkOut(Action onComplete)
        {
            if (_sinkFeedback == null)
            {
                onComplete?.Invoke();
                return;
            }

            _sinkFeedback.Play(onComplete);
        }

        public void BeginSpawnAnimation(
            Vector3 startPosition,
            Vector3 targetPosition,
            float duration,
            float arcHeight,
            float startScaleMultiplier)
        {
            ResolveVisualRoot();

            _spawnStartPosition = startPosition;
            _spawnTargetPosition = targetPosition;
            _spawnDuration = Mathf.Max(0.05f, duration);
            _spawnArcHeight = Mathf.Max(0f, arcHeight);
            _spawnStartScaleMultiplier = Mathf.Max(0.1f, startScaleMultiplier);
            _spawnElapsed = 0f;
            _isSpawnAnimating = true;

            SetWorldPosition(startPosition);
            SetCollisionState(false);

            if (_visualRoot != null)
            {
                _visualRoot.localScale = _baseVisualScale * _spawnStartScaleMultiplier;
            }
        }

        public bool BeginTubeTraversal(
            SplineComputer spline,
            double startPercent,
            float speed,
            Vector3 exitPosition,
            Quaternion exitRotation,
            Vector3 exitDirection)
        {
            if (!IsActive || spline == null || _isInTube)
            {
                return false;
            }

            _isInTube = true;
            _tubeSpline = spline;
            _tubePercent = Mathf.Clamp01((float)startPercent);
            _tubeSpeed = Mathf.Max(0.1f, speed);
            _tubeExitPosition = exitPosition;
            _tubeExitRotation = exitRotation;
            _tubeExitDirection = NormalizeMovementDirection(exitDirection, exitRotation * Vector3.forward);
            _isSpawnAnimating = false;
            _spawnElapsed = 0f;
            _isTubeEntering = true;
            _tubeEntryElapsed = 0f;

            SetCollisionState(false);
            SplineSample entrySample = _tubeSpline.Evaluate(_tubePercent);
            _tubeEntryStartPosition = WorldPosition;
            _tubeEntryTargetPosition = entrySample.position;
            _tubeEntryStartRotation = _body != null ? _body.rotation : transform.rotation;
            _tubeEntryTargetRotation = CreateTubeRotation(entrySample.forward);

            float approachSpeed = Mathf.Max(0.25f, _tubeSpeed * TubeEntrySpeedFactor);
            float entryDistance = Vector3.Distance(_tubeEntryStartPosition, _tubeEntryTargetPosition);
            _tubeEntryDuration = Mathf.Clamp(
                entryDistance / approachSpeed,
                MinTubeEntryDuration,
                MaxTubeEntryDuration);
            return true;
        }

        public void SetMovementDirectionOverride(Vector3 worldDirection)
        {
            Vector3 normalizedDirection = NormalizeMovementDirection(worldDirection, Vector3.forward);
            _movementDirectionOverride = normalizedDirection;
            _hasMovementDirectionOverride = true;

            if (!IsActive || _isInTube)
            {
                return;
            }

            SetWorldRotation(Quaternion.LookRotation(normalizedDirection, Vector3.up));
        }

        public void ClearMovementDirectionOverride()
        {
            _hasMovementDirectionOverride = false;
            _movementDirectionOverride = Vector3.forward;
        }

        public bool TickSpawnAnimation(float deltaTime)
        {
            if (!_isSpawnAnimating)
            {
                return false;
            }

            _spawnElapsed += deltaTime;
            float t = Mathf.Clamp01(_spawnElapsed / _spawnDuration);
            float eased = t * t * (3f - 2f * t);

            Vector3 nextPosition = Vector3.LerpUnclamped(_spawnStartPosition, _spawnTargetPosition, eased);
            nextPosition.y += Mathf.Sin(t * Mathf.PI) * _spawnArcHeight;
            SetWorldPosition(nextPosition);

            if (_visualRoot != null)
            {
                float scaleT = 1f - Mathf.Pow(1f - eased, 2f);
                _visualRoot.localScale = Vector3.LerpUnclamped(
                    _baseVisualScale * _spawnStartScaleMultiplier,
                    _baseVisualScale,
                    scaleT);
            }

            if (t < 1f)
            {
                return true;
            }

            _isSpawnAnimating = false;
            SetWorldPosition(_spawnTargetPosition);
            ResetVisualScale();
            SetCollisionState(true);

            return true;
        }

        public bool TickTubeTraversal(float deltaTime)
        {
            if (!_isInTube || _tubeSpline == null)
            {
                return false;
            }

            if (_isTubeEntering)
            {
                _tubeEntryElapsed += deltaTime;
                float t = _tubeEntryDuration > 0f
                    ? Mathf.Clamp01(_tubeEntryElapsed / _tubeEntryDuration)
                    : 1f;
                float eased = t * t * (3f - 2f * t);

                Vector3 position = Vector3.LerpUnclamped(_tubeEntryStartPosition, _tubeEntryTargetPosition, eased);
                Quaternion rotation = Quaternion.SlerpUnclamped(
                    _tubeEntryStartRotation,
                    _tubeEntryTargetRotation,
                    eased);
                SetWorldPose(position, rotation);

                if (t < 1f)
                {
                    return true;
                }

                _isTubeEntering = false;
                ApplyTubePose(_tubePercent);
                return true;
            }

            float travelDistance = Mathf.Max(0f, _tubeSpeed) * deltaTime;
            float moved;
            _tubePercent = _tubeSpline.Travel(_tubePercent, travelDistance, out moved, Spline.Direction.Forward);
            ApplyTubePose(_tubePercent);

            bool reachedEnd = _tubePercent >= 0.9999 || moved + 0.0001f < travelDistance;
            if (reachedEnd)
            {
                EndTubeTraversal();
            }

            return true;
        }

        private void OnTriggerEnter(Collider other)
        {
            if (!IsActive || _manager == null)
            {
                return;
            }

            GateModifier gate = other.GetComponentInParent<GateModifier>();
            if (gate != null)
            {
                gate.TryApply(this);
                return;
            }

            EnemyMob enemyMob = other.GetComponentInParent<EnemyMob>();
            if (enemyMob != null)
            {
                enemyMob.TryConsume(this);
                return;
            }

            MobDamageBlock damageBlock = other.GetComponentInParent<MobDamageBlock>();
            if (damageBlock != null)
            {
                damageBlock.TryDamage(this);
                return;
            }

            FinishTarget finishTarget = other.GetComponentInParent<FinishTarget>();
            if (finishTarget != null)
            {
                finishTarget.TryDamage(this);
            }
        }

        private void SetWorldPosition(Vector3 position)
        {
            if (_body != null)
            {
                _body.position = position;
                return;
            }

            transform.position = position;
        }

        private void SetWorldRotation(Quaternion rotation)
        {
            if (_body != null)
            {
                _body.rotation = rotation;
                return;
            }

            transform.rotation = rotation;
        }

        private void SetWorldPose(Vector3 position, Quaternion rotation)
        {
            SetWorldPosition(position);
            SetWorldRotation(rotation);
        }

        private void EnsureRuntimeComponents()
        {
            if (_trigger == null)
            {
                _trigger = GetComponent<CapsuleCollider>();
                if (_trigger == null)
                {
                    _trigger = GetComponent<Collider>();
                }

                if (_trigger == null)
                {
                    _trigger = gameObject.AddComponent<CapsuleCollider>();
                }
            }

            if (_body == null)
            {
                _body = GetComponent<Rigidbody>();
                if (_body == null)
                {
                    _body = gameObject.AddComponent<Rigidbody>();
                }
            }

            if (_sinkFeedback == null)
            {
                _sinkFeedback = GetComponent<SinkFeedbackAnimator>();
                if (_sinkFeedback == null)
                {
                    _sinkFeedback = gameObject.AddComponent<SinkFeedbackAnimator>();
                }
            }
        }

        private void ResetRuntimeState()
        {
            _isSpawnAnimating = false;
            _spawnElapsed = 0f;
            ResetTubeTraversal();
            ClearMovementDirectionOverride();
            SetCollisionState(true);
            _sinkFeedback?.ResetImmediate();
            ResetVisualScale();
        }

        private void SetCollisionState(bool enabled)
        {
            if (_trigger != null)
            {
                _trigger.enabled = enabled;
            }

            if (_body != null)
            {
                _body.detectCollisions = enabled;
            }
        }

        private void ResolveVisualRoot()
        {
            if (_visualRoot != null && _hasVisualScale)
            {
                return;
            }

            Renderer renderer = GetComponentInChildren<Renderer>(true);
            if (renderer == null)
            {
                _visualRoot = transform;
            }
            else
            {
                Transform candidate = renderer.transform;
                while (candidate.parent != null && candidate.parent != transform)
                {
                    candidate = candidate.parent;
                }

                _visualRoot = candidate;
            }

            if (!_hasVisualScale && _visualRoot != null)
            {
                _baseVisualScale = _visualRoot.localScale;
                _hasVisualScale = true;
            }
        }

        private void ResetVisualScale()
        {
            ResolveVisualRoot();
            if (_visualRoot != null)
            {
                _visualRoot.localScale = _baseVisualScale;
            }
        }

        private void ApplyTubePose(double percent)
        {
            if (_tubeSpline == null)
            {
                return;
            }

            SplineSample sample = _tubeSpline.Evaluate(percent);
            SetWorldPose(sample.position, CreateTubeRotation(sample.forward));
        }

        private void EndTubeTraversal()
        {
            SetWorldPose(_tubeExitPosition, _tubeExitRotation);
            SetMovementDirectionOverride(_tubeExitDirection);
            ResetTubeTraversal();
            SetCollisionState(true);
        }

        private void ResetTubeTraversal()
        {
            _isInTube = false;
            _tubeSpline = null;
            _tubePercent = 0.0;
            _tubeSpeed = 0f;
            _isTubeEntering = false;
            _tubeEntryElapsed = 0f;
            _tubeEntryDuration = 0f;
            _tubeEntryStartPosition = Vector3.zero;
            _tubeEntryTargetPosition = Vector3.zero;
            _tubeEntryStartRotation = Quaternion.identity;
            _tubeEntryTargetRotation = Quaternion.identity;
            _tubeExitPosition = Vector3.zero;
            _tubeExitRotation = Quaternion.identity;
            _tubeExitDirection = Vector3.forward;
        }

        private static Quaternion CreateTubeRotation(Vector3 preferredForward)
        {
            Vector3 horizontalForward = NormalizeMovementDirection(preferredForward, Vector3.forward);
            return Quaternion.LookRotation(horizontalForward.normalized, Vector3.up);
        }

        private static Vector3 NormalizeMovementDirection(Vector3 candidate, Vector3 fallback)
        {
            Vector3 horizontal = new Vector3(candidate.x, 0f, candidate.z);
            if (horizontal.sqrMagnitude > 0.0001f)
            {
                return horizontal.normalized;
            }

            Vector3 horizontalFallback = new Vector3(fallback.x, 0f, fallback.z);
            if (horizontalFallback.sqrMagnitude > 0.0001f)
            {
                return horizontalFallback.normalized;
            }

            return Vector3.forward;
        }
    }
}
