using UnityEngine;

namespace MobControlPrototype.Crowd
{
    public interface IUnitFactory
    {
        GameObject CreateUnit(Transform parent, Vector3 localPosition, Quaternion localRotation);
    }
}
