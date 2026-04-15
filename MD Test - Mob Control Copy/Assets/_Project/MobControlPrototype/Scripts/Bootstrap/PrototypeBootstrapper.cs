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
        }

        private void OnDestroy()
        {
            ServiceLocator.Clear();
        }
    }
}
