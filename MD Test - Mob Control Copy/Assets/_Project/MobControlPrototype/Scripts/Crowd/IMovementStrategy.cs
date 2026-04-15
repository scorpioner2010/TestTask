using UnityEngine;

namespace MobControlPrototype.Crowd
{
    public interface IMovementStrategy
    {
        Vector3 GetDelta(float deltaTime);
    }
}
