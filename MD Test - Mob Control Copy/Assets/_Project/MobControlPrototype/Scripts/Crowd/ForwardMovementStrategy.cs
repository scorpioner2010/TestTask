using UnityEngine;

namespace MobControlPrototype.Crowd
{
    public sealed class ForwardMovementStrategy : IMovementStrategy
    {
        private readonly Vector3 _direction;
        private readonly float _speed;

        public ForwardMovementStrategy(Vector3 direction, float speed)
        {
            _direction = direction.sqrMagnitude > 0f ? direction.normalized : Vector3.forward;
            _speed = Mathf.Max(0f, speed);
        }

        public Vector3 GetDelta(float deltaTime)
        {
            return _direction * (_speed * deltaTime);
        }
    }
}
