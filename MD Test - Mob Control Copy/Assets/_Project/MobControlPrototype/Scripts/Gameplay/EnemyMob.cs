using UnityEngine;

namespace MobControlPrototype.Gameplay
{
    [DisallowMultipleComponent]
    public sealed class EnemyMob : MonoBehaviour
    {
        [SerializeField, Min(0.1f)] private float colliderRadius = 0.28f;
        [SerializeField, Min(0.1f)] private float colliderHeight = 1.25f;

        private bool _isAlive = true;
        private CapsuleCollider _trigger;
        private Rigidbody _body;
        private SinkFeedbackAnimator _sinkFeedback;

        private void Awake()
        {
            EnsurePhysics();
            _sinkFeedback = GetComponent<SinkFeedbackAnimator>();
            if (_sinkFeedback == null)
            {
                _sinkFeedback = gameObject.AddComponent<SinkFeedbackAnimator>();
            }
        }

        private void OnEnable()
        {
            _isAlive = true;

            if (_trigger != null)
            {
                _trigger.enabled = true;
            }

            if (_body != null)
            {
                _body.detectCollisions = true;
            }

            _sinkFeedback?.ResetImmediate();
        }

        public bool TryConsume(UnitRunner runner)
        {
            if (!_isAlive || runner == null || !runner.IsActive)
            {
                return false;
            }

            _isAlive = false;

            if (_trigger != null)
            {
                _trigger.enabled = false;
            }

            if (_body != null)
            {
                _body.detectCollisions = false;
            }

            runner.Manager.RemoveRunnerWithSink(runner);

            if (_sinkFeedback != null)
            {
                _sinkFeedback.Play(DeactivateSelf);
            }
            else
            {
                DeactivateSelf();
            }

            return true;
        }

        private void EnsurePhysics()
        {
            _trigger = GetComponent<CapsuleCollider>();
            if (_trigger == null)
            {
                _trigger = gameObject.AddComponent<CapsuleCollider>();
            }

            _trigger.isTrigger = true;
            _trigger.radius = colliderRadius;
            _trigger.height = colliderHeight;
            _trigger.center = new Vector3(0f, colliderHeight * 0.5f, 0f);

            _body = GetComponent<Rigidbody>();
            if (_body == null)
            {
                _body = gameObject.AddComponent<Rigidbody>();
            }

            _body.isKinematic = true;
            _body.useGravity = false;
            _body.detectCollisions = true;
        }

        private void DeactivateSelf()
        {
            gameObject.SetActive(false);
        }
    }
}
