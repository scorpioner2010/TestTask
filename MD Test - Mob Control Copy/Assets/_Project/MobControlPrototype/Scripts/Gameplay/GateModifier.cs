using UnityEngine;

namespace MobControlPrototype.Gameplay
{
    [RequireComponent(typeof(Collider))]
    [DisallowMultipleComponent]
    public sealed class GateModifier : MonoBehaviour
    {
        [SerializeField] private GateOperation operation = GateOperation.Add;
        [SerializeField, Min(1)] private int value = 10;

        public GateOperation Operation => operation;
        public int Value => value;

        private void Awake()
        {
            Collider trigger = GetComponent<Collider>();
            trigger.isTrigger = true;
        }

        private void OnValidate()
        {
            value = Mathf.Max(1, value);
            Collider trigger = GetComponent<Collider>();
            if (trigger != null)
            {
                trigger.isTrigger = true;
            }
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

            return runner.Manager.ApplyGate(runner, operation, value, spawnZ) != 0;
        }
    }
}
