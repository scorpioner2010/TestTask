using UnityEngine;

namespace MobControlPrototype.Gameplay
{
    [DisallowMultipleComponent]
    public sealed class PlayerCannonHitZone : MonoBehaviour
    {
        [SerializeField] private UnitRunnerManager runnerManager;
        [SerializeField] private BoxCollider triggerCollider;

        public void Configure(UnitRunnerManager manager)
        {
            runnerManager = manager;
            EnsureTrigger();
        }

        private void Awake()
        {
            EnsureTrigger();
        }

        private void OnValidate()
        {
            EnsureTrigger();
        }

        public bool TryLose(EnemyMob enemy)
        {
            if (runnerManager == null || runnerManager.IsLevelEnded)
            {
                return false;
            }

            if (enemy != null && enemy.Manager != null)
            {
                enemy.Manager.RemoveEnemy(enemy);
            }

            runnerManager.CompleteLevel(false);
            return true;
        }

        private void EnsureTrigger()
        {
            if (triggerCollider == null)
            {
                triggerCollider = GetComponent<BoxCollider>();
                triggerCollider ??= GetComponentInChildren<BoxCollider>(true);
            }

            if (triggerCollider != null)
            {
                triggerCollider.isTrigger = true;
            }
        }
    }
}
