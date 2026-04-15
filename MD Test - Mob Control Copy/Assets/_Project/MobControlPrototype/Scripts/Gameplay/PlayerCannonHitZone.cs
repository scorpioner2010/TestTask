using MobControlPrototype.Infrastructure;
using UnityEngine;

namespace MobControlPrototype.Gameplay
{
    [DisallowMultipleComponent]
    public sealed class PlayerCannonHitZone : MonoBehaviour
    {
        [SerializeField] private UnitRunnerManager runnerManager;
        [SerializeField] private Vector3 sizePadding = new Vector3(0.8f, 0.3f, 0.8f);
        [SerializeField] private Vector3 fallbackCenter = new Vector3(0f, 0.9f, 0f);
        [SerializeField] private Vector3 fallbackSize = new Vector3(1.8f, 1.8f, 2.8f);

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
            UpdateTriggerBounds();
        }

        private void UpdateTriggerBounds()
        {
            Renderer[] renderers = GetComponentsInChildren<Renderer>(true);
            if (renderers.Length == 0)
            {
                _trigger.center = fallbackCenter;
                _trigger.size = fallbackSize;
                return;
            }

            Bounds bounds = renderers[0].bounds;
            for (int i = 1; i < renderers.Length; i++)
            {
                bounds.Encapsulate(renderers[i].bounds);
            }

            _trigger.center = transform.InverseTransformPoint(bounds.center);
            _trigger.size = bounds.size + sizePadding;
        }
    }
}
