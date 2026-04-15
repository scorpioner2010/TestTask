using MobControlPrototype.Crowd;
using MobControlPrototype.Infrastructure;
using UnityEngine;

namespace MobControlPrototype.Camera
{
    [DisallowMultipleComponent]
    public sealed class FollowCamera : MonoBehaviour
    {
        [SerializeField] private Transform target;
        [SerializeField] private Vector3 offset = new Vector3(0f, 7.2f, -9.5f);
        [SerializeField] private Vector3 lookAtOffset = new Vector3(0f, 1.15f, 2.5f);
        [SerializeField, Min(0.01f)] private float smoothTime = 0.18f;
        [SerializeField, Min(1f)] private float rotationSharpness = 12f;

        private Vector3 _velocity;

        public void SetTarget(Transform newTarget)
        {
            target = newTarget;
        }

        private void LateUpdate()
        {
            ResolveTargetIfNeeded();

            if (target == null)
            {
                return;
            }

            Vector3 desiredPosition = target.position + offset;
            transform.position = Vector3.SmoothDamp(transform.position, desiredPosition, ref _velocity, smoothTime);

            Vector3 lookPoint = target.position + lookAtOffset;
            Vector3 lookDirection = lookPoint - transform.position;
            if (lookDirection.sqrMagnitude <= 0.0001f)
            {
                return;
            }

            Quaternion targetRotation = Quaternion.LookRotation(lookDirection, Vector3.up);
            float t = 1f - Mathf.Exp(-rotationSharpness * Time.deltaTime);
            transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, t);
        }

        private void ResolveTargetIfNeeded()
        {
            if (target != null)
            {
                return;
            }

            if (ServiceLocator.TryGet(out CrowdController crowdController))
            {
                target = crowdController.CrowdRoot;
            }
        }
    }
}
