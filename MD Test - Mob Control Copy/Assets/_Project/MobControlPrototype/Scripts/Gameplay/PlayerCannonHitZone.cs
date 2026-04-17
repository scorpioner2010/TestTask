using MobControlPrototype.Infrastructure;
using UnityEngine;

namespace MobControlPrototype.Gameplay
{
    [RequireComponent(typeof(BoxCollider))]
    [DisallowMultipleComponent]
    public sealed class PlayerCannonHitZone : MonoBehaviour
    {
        [SerializeField] private UnitRunnerManager runnerManager;

        private BoxCollider _trigger;

        public void Configure(UnitRunnerManager manager)
        {
            runnerManager = manager;
            EnsureTrigger();
        }

        private void Awake()
        {
            if (runnerManager == null)
            {
                ServiceLocator.TryGet(out runnerManager);
            }

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
            if (_trigger == null)
            {
                _trigger = GetComponent<BoxCollider>();
                if (_trigger == null)
                {
                    _trigger = gameObject.AddComponent<BoxCollider>();
                }
            }

            _trigger.isTrigger = true;
        }
    }
}
