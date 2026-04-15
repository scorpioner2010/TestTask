using System;
using System.Collections.Generic;
using UnityEngine;

namespace MobControlPrototype.Gameplay
{
    [RequireComponent(typeof(Collider))]
    [RequireComponent(typeof(Rigidbody))]
    [DisallowMultipleComponent]
    public sealed class UnitRunner : MonoBehaviour
    {
        private readonly List<int> _passedGateIds = new List<int>(4);
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

        public int ActiveIndex { get; set; }
        public bool IsActive { get; private set; }
        public UnitRunnerManager Manager => _manager;
        public Rigidbody Body => _body;
        public Vector3 WorldPosition => _body != null ? _body.position : transform.position;

        private void Awake()
        {
            _body = GetComponent<Rigidbody>();
            _trigger = GetComponent<Collider>();
            _sinkFeedback = GetComponent<SinkFeedbackAnimator>();
            if (_sinkFeedback == null)
            {
                _sinkFeedback = gameObject.AddComponent<SinkFeedbackAnimator>();
            }

            ResolveVisualRoot();
        }

        public void Initialize(UnitRunnerManager manager)
        {
            _manager = manager;
            IsActive = true;
            _passedGateIds.Clear();
            if (_body == null)
            {
                _body = GetComponent<Rigidbody>();
            }

            if (_trigger == null)
            {
                _trigger = GetComponent<Collider>();
            }

            if (_trigger != null)
            {
                _trigger.enabled = true;
            }

            if (_body != null)
            {
                _body.detectCollisions = true;
            }

            _isSpawnAnimating = false;
            _spawnElapsed = 0f;
            _sinkFeedback?.ResetImmediate();
            ResetVisualScale();
        }

        public void Deactivate()
        {
            IsActive = false;
            _passedGateIds.Clear();

            if (_trigger != null)
            {
                _trigger.enabled = true;
            }

            if (_body != null)
            {
                _body.detectCollisions = true;
            }

            _isSpawnAnimating = false;
            _spawnElapsed = 0f;
            _sinkFeedback?.ResetImmediate();
            ResetVisualScale();
        }

        public bool HasPassedGate(int gateId)
        {
            return _passedGateIds.Contains(gateId);
        }

        public void MarkGatePassed(int gateId)
        {
            if (!_passedGateIds.Contains(gateId))
            {
                _passedGateIds.Add(gateId);
            }
        }

        public void PrepareForRemoval()
        {
            IsActive = false;
            _passedGateIds.Clear();
            _isSpawnAnimating = false;
            _spawnElapsed = 0f;

            if (_trigger != null)
            {
                _trigger.enabled = false;
            }

            if (_body != null)
            {
                _body.detectCollisions = false;
            }

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

            if (_trigger != null)
            {
                _trigger.enabled = false;
            }

            if (_body != null)
            {
                _body.detectCollisions = false;
            }

            if (_visualRoot != null)
            {
                _visualRoot.localScale = _baseVisualScale * _spawnStartScaleMultiplier;
            }
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

            if (_trigger != null)
            {
                _trigger.enabled = true;
            }

            if (_body != null)
            {
                _body.detectCollisions = true;
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
    }
}
