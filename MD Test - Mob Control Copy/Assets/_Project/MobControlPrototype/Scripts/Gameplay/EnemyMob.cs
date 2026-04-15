using UnityEngine;

namespace MobControlPrototype.Gameplay
{
    [DisallowMultipleComponent]
    public sealed class EnemyMob : MonoBehaviour
    {
        private EnemyRunnerManager _manager;
        private bool _isAlive = true;
        private CapsuleCollider _trigger;
        private Rigidbody _body;
        private SinkFeedbackAnimator _sinkFeedback;

        public int ActiveIndex { get; set; }
        public bool IsActive => _isAlive;
        public EnemyRunnerManager Manager => _manager;
        public Rigidbody Body => _body;
        public Vector3 WorldPosition => _body != null ? _body.position : transform.position;

        private void Awake()
        {
            EnsureRuntimeComponents();
        }

        private void OnEnable()
        {
            ResetRuntimeState();
        }

        public void ConfigurePhysics(float colliderRadius, float colliderHeight)
        {
            EnsureRuntimeComponents();

            _trigger.isTrigger = true;
            _trigger.radius = colliderRadius;
            _trigger.height = colliderHeight;
            _trigger.center = new Vector3(0f, colliderHeight * 0.5f, 0f);

            _body.isKinematic = true;
            _body.useGravity = false;
            _body.interpolation = RigidbodyInterpolation.Interpolate;
            _body.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;
            _body.constraints = RigidbodyConstraints.FreezeRotation;
        }

        public void Initialize(EnemyRunnerManager manager)
        {
            _manager = manager;
            ResetRuntimeState();
        }

        public void PrepareForRemoval()
        {
            _isAlive = false;
            SetCollisionState(false);
        }

        public void Deactivate()
        {
            _isAlive = false;
            SetCollisionState(true);
            _sinkFeedback?.ResetImmediate();
        }

        public void PlaySinkOut(System.Action onComplete)
        {
            if (_sinkFeedback == null)
            {
                onComplete?.Invoke();
                return;
            }

            _sinkFeedback.Play(onComplete);
        }

        public bool TryConsume(UnitRunner runner)
        {
            if (!_isAlive || _manager == null || runner == null || !runner.IsActive)
            {
                return false;
            }

            runner.Manager.RemoveRunnerWithSink(runner);
            _manager.RemoveEnemyWithSink(this);
            return true;
        }

        private void EnsureRuntimeComponents()
        {
            if (_trigger == null)
            {
                _trigger = GetComponent<CapsuleCollider>();
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
            _isAlive = true;
            SetCollisionState(true);
            _sinkFeedback?.ResetImmediate();
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

        private void OnTriggerEnter(Collider other)
        {
            if (!_isAlive)
            {
                return;
            }

            PlayerCannonHitZone cannonHitZone = other.GetComponentInParent<PlayerCannonHitZone>();
            if (cannonHitZone != null)
            {
                cannonHitZone.TryLose(this);
            }
        }
    }
}
