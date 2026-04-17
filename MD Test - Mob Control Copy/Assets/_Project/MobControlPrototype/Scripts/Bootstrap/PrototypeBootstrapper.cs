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

        [Header("VFX")]
        [SerializeField] private PrototypeGameplayVfxSettings gameplayVfxSettings = new PrototypeGameplayVfxSettings();

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

            if (gameplayVfxSettings == null)
            {
                gameplayVfxSettings = new PrototypeGameplayVfxSettings();
            }

            PrototypeGameplayVfxService gameplayVfxService =
                PrototypeGameplayVfxService.Create(gameplayVfxSettings, transform);
            if (gameplayVfxService != null)
            {
                ServiceLocator.Register(gameplayVfxService);
            }

            SetupEnemyLoop();
        }

        private void OnValidate()
        {
            gameplayVfxSettings ??= new PrototypeGameplayVfxSettings();
            gameplayVfxSettings.OnValidate();
        }

        private void OnDestroy()
        {
            ServiceLocator.Clear();
        }

        private void SetupEnemyLoop()
        {
            if (runnerManager == null || unitPrefab == null)
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

            enemyManager.Configure(runnerManager, finishTarget, unitPrefab);
            ServiceLocator.Register(enemyManager);

            PlayerCannonHitZone cannonHitZone = FindObjectOfType<PlayerCannonHitZone>();
            if (cannonHitZone == null)
            {
                if (cannonShooter == null)
                {
                    return;
                }

                GameObject loseZoneObject = new GameObject("PlayerLoseZone");
                loseZoneObject.transform.SetPositionAndRotation(
                    cannonShooter.transform.position + new Vector3(0f, 0.9f, 2.8f),
                    Quaternion.identity);
                BoxCollider loseZoneCollider = loseZoneObject.AddComponent<BoxCollider>();
                loseZoneCollider.size = new Vector3(13.6f, 2.4f, 0.8f);
                cannonHitZone = loseZoneObject.AddComponent<PlayerCannonHitZone>();
            }

            cannonHitZone.Configure(runnerManager);
        }
    }
}
