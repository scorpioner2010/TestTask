using MobControlPrototype.Crowd;
using MobControlPrototype.Gameplay;
using MobControlPrototype.Infrastructure;
using UnityEngine;

namespace MobControlPrototype.Bootstrap
{
    [DefaultExecutionOrder(-100)]
    [DisallowMultipleComponent]
    public sealed class PrototypeBootstrapper : MonoBehaviour
    {
        [SerializeField] private UnitRunnerManager runnerManager;
        [SerializeField] private CannonShooter cannonShooter;
        [SerializeField] private GameObject unitPrefab;
        [SerializeField] private GameObject unitModelPrefab;
        [SerializeField] private AnimationClip runningClip;
        [SerializeField] private Material fallbackUnitMaterial;
        [SerializeField, Min(0f)] private float forwardSpeed = 5.4f;
        [SerializeField, Min(0f)] private float runningAnimationSpeed = 1.12f;

        private void Awake()
        {
            ServiceLocator.Clear();
            ServiceLocator.Register(this);

            IUnitFactory unitFactory = new UnitFactory(
                unitPrefab,
                unitModelPrefab,
                runningClip,
                fallbackUnitMaterial,
                runningAnimationSpeed);
            IMovementStrategy movementStrategy = new ForwardMovementStrategy(Vector3.forward, forwardSpeed);

            ServiceLocator.Register(unitFactory);
            ServiceLocator.Register(movementStrategy);

            if (runnerManager != null)
            {
                runnerManager.Initialize(unitFactory, movementStrategy);
                ServiceLocator.Register(runnerManager);
            }

            if (cannonShooter != null)
            {
                ServiceLocator.Register(cannonShooter);
            }

            SetupEnemyLoop();
        }

        private void OnDestroy()
        {
            ServiceLocator.Clear();
        }

        private void SetupEnemyLoop()
        {
            if (runnerManager == null || cannonShooter == null || unitPrefab == null)
            {
                return;
            }

            FinishTarget finishTarget = FindObjectOfType<FinishTarget>();
            if (finishTarget == null)
            {
                return;
            }

            EnemyRunnerManager enemyManager = FindObjectOfType<EnemyRunnerManager>();
            if (enemyManager == null)
            {
                GameObject managerObject = new GameObject("EnemyRunnerManager");
                enemyManager = managerObject.AddComponent<EnemyRunnerManager>();
            }

            enemyManager.Configure(runnerManager, cannonShooter, finishTarget, unitPrefab);
            ServiceLocator.Register(enemyManager);

            PlayerCannonHitZone cannonHitZone = cannonShooter.GetComponent<PlayerCannonHitZone>();
            if (cannonHitZone == null)
            {
                cannonHitZone = cannonShooter.gameObject.AddComponent<PlayerCannonHitZone>();
            }

            cannonHitZone.Configure(runnerManager);
        }
    }
}
