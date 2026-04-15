using UnityEngine;

namespace MobControlPrototype.Gameplay
{
    [DisallowMultipleComponent]
    public sealed class EnemyMob : MonoBehaviour
    {
        [SerializeField, Min(0.1f)] private float colliderRadius = 0.28f;
        [SerializeField, Min(0.1f)] private float colliderHeight = 1.25f;

        private bool _isAlive = true;

        private void Awake()
        {
            EnsurePhysics();
        }

        public bool TryConsume(UnitRunner runner)
        {
            if (!_isAlive || runner == null || !runner.IsActive)
            {
                return false;
            }

            _isAlive = false;
            runner.Manager.RemoveRunner(runner);
            gameObject.SetActive(false);
            return true;
        }

        private void EnsurePhysics()
        {
            CapsuleCollider collider = GetComponent<CapsuleCollider>();
            if (collider == null)
            {
                collider = gameObject.AddComponent<CapsuleCollider>();
            }

            collider.isTrigger = true;
            collider.radius = colliderRadius;
            collider.height = colliderHeight;
            collider.center = new Vector3(0f, colliderHeight * 0.5f, 0f);

            Rigidbody body = GetComponent<Rigidbody>();
            if (body == null)
            {
                body = gameObject.AddComponent<Rigidbody>();
            }

            body.isKinematic = true;
            body.useGravity = false;
        }
    }
}
