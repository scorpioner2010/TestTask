using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace MobControlPrototype.Gameplay
{
    [DisallowMultipleComponent]
    public sealed class PrototypeGameplayVfxService : MonoBehaviour
    {
        private const float DefaultCameraDepthBias = 0.12f;

        [SerializeField] private PrototypeGameplayVfxSettings settings = new PrototypeGameplayVfxSettings();
        [SerializeField] private Camera mainCamera;

        private readonly Dictionary<GameObject, Queue<PrototypeGameplayVfxInstance>> _poolByPrefab =
            new Dictionary<GameObject, Queue<PrototypeGameplayVfxInstance>>(8);

        public void Configure(PrototypeGameplayVfxSettings newSettings, Camera camera = null)
        {
            if (newSettings != null)
            {
                settings = newSettings;
            }

            if (camera != null)
            {
                mainCamera = camera;
            }
        }

        private void OnValidate()
        {
            settings ??= new PrototypeGameplayVfxSettings();
            settings.OnValidate();
        }

        public void PlayShot(Vector3 worldPosition, Vector3 shotDirection)
        {
            if (settings == null)
            {
                return;
            }

            PlayEffect(settings.Shot, worldPosition, CreateDirectionalRotation(shotDirection, Vector3.forward), shotDirection);
        }

        public void PlayUnitCollision(Vector3 playerPosition, Vector3 enemyPosition, Vector3 fallbackForward)
        {
            if (settings == null)
            {
                return;
            }

            Vector3 collisionPosition = Vector3.LerpUnclamped(playerPosition, enemyPosition, 0.5f);
            PlayEffect(
                settings.UnitCollision,
                collisionPosition,
                CreateEffectRotation(settings.UnitCollision, fallbackForward, collisionPosition),
                fallbackForward);
        }

        public void PlayGateActivation(GateOperation operation, Transform gateTransform, Collider gateCollider)
        {
            if (settings == null)
            {
                return;
            }

            PrototypeGameplayVfxSettings.EffectEntry effectEntry;
            switch (operation)
            {
                case GateOperation.Add:
                    effectEntry = settings.GateAdd;
                    break;
                case GateOperation.Multiply:
                    effectEntry = settings.GateMultiply;
                    break;
                default:
                    return;
            }

            Vector3 anchorPosition = ResolveObjectCenter(gateTransform, gateCollider);
            Vector3 fallbackForward = gateTransform != null ? gateTransform.forward : Vector3.forward;
            PlayEffect(
                effectEntry,
                anchorPosition,
                CreateEffectRotation(effectEntry, fallbackForward, anchorPosition),
                fallbackForward);
        }

        public void PlayWallDamage(Transform targetTransform, Collider targetCollider, Vector3 attackerPosition)
        {
            PlayStructureDamage(settings != null ? settings.WallDamage : default, targetTransform, targetCollider, attackerPosition);
        }

        public void PlayCastleDamage(Transform targetTransform, Collider targetCollider, Vector3 attackerPosition)
        {
            PlayStructureDamage(settings != null ? settings.CastleDamage : default, targetTransform, targetCollider, attackerPosition);
        }

        internal void ReturnToPool(PrototypeGameplayVfxInstance instance)
        {
            if (instance == null)
            {
                return;
            }

            GameObject sourcePrefab = instance.SourcePrefab;
            if (sourcePrefab == null)
            {
                Destroy(instance.gameObject);
                return;
            }

            if (!_poolByPrefab.TryGetValue(sourcePrefab, out Queue<PrototypeGameplayVfxInstance> pool))
            {
                pool = new Queue<PrototypeGameplayVfxInstance>(4);
                _poolByPrefab.Add(sourcePrefab, pool);
            }

            instance.gameObject.SetActive(false);
            instance.transform.SetParent(transform, false);
            pool.Enqueue(instance);
        }

        private void PlayStructureDamage(
            PrototypeGameplayVfxSettings.EffectEntry effectEntry,
            Transform targetTransform,
            Collider targetCollider,
            Vector3 attackerPosition)
        {
            if (settings == null)
            {
                return;
            }

            Vector3 impactPosition = ResolveObjectCenter(targetTransform, targetCollider);
            Vector3 fallbackForward = targetTransform != null ? targetTransform.forward : Vector3.forward;
            PlayEffect(
                effectEntry,
                impactPosition,
                CreateEffectRotation(effectEntry, fallbackForward, impactPosition),
                fallbackForward);
        }

        private void PlayEffect(
            PrototypeGameplayVfxSettings.EffectEntry effectEntry,
            Vector3 worldPosition,
            Quaternion worldRotation,
            Vector3 fallbackForward)
        {
            if (!effectEntry.IsConfigured)
            {
                return;
            }

            Vector3 viewDirection = GetDirectionToCamera(worldPosition, effectEntry.KeepUpright, fallbackForward);
            float depthBias = effectEntry.CameraDepthOffset > 0f
                ? effectEntry.CameraDepthOffset
                : (effectEntry.FaceCamera ? DefaultCameraDepthBias : 0f);
            Vector3 spawnPosition = worldPosition
                + Vector3.up * effectEntry.VerticalOffset
                + viewDirection * depthBias;
            Quaternion spawnRotation = worldRotation * Quaternion.Euler(effectEntry.LocalEulerAngles);

            PrototypeGameplayVfxInstance instance = Rent(effectEntry.Prefab);
            if (instance == null)
            {
                return;
            }

            instance.Play(spawnPosition, spawnRotation, effectEntry.SafeScale, effectEntry.ForceOneShot);
        }

        private PrototypeGameplayVfxInstance Rent(GameObject prefab)
        {
            if (prefab == null)
            {
                return null;
            }

            if (!_poolByPrefab.TryGetValue(prefab, out Queue<PrototypeGameplayVfxInstance> pool))
            {
                pool = new Queue<PrototypeGameplayVfxInstance>(4);
                _poolByPrefab.Add(prefab, pool);
            }

            while (pool.Count > 0)
            {
                PrototypeGameplayVfxInstance pooledInstance = pool.Dequeue();
                if (pooledInstance != null)
                {
                    return pooledInstance;
                }
            }

            GameObject instanceObject = Instantiate(prefab, transform);
            instanceObject.name = $"{prefab.name}_Pooled";
            PrototypeGameplayVfxInstance instance = instanceObject.GetComponent<PrototypeGameplayVfxInstance>();
            if (instance == null)
            {
                instance = instanceObject.AddComponent<PrototypeGameplayVfxInstance>();
            }

            instance.Initialize(this, prefab);
            instance.gameObject.SetActive(false);
            return instance;
        }

        private static Vector3 ResolveObjectCenter(Transform targetTransform, Collider targetCollider)
        {
            if (targetCollider == null)
            {
                return targetTransform != null ? targetTransform.position : Vector3.zero;
            }

            return targetCollider.bounds.center;
        }

        private Quaternion CreateEffectRotation(
            PrototypeGameplayVfxSettings.EffectEntry effectEntry,
            Vector3 fallbackForward,
            Vector3 worldPosition)
        {
            if (effectEntry.FaceCamera)
            {
                return CreateCameraFacingRotation(worldPosition, effectEntry.KeepUpright, fallbackForward);
            }

            return CreateDirectionalRotation(fallbackForward, Vector3.forward);
        }

        private Quaternion CreateCameraFacingRotation(
            Vector3 worldPosition,
            bool keepUpright,
            Vector3 fallbackForward)
        {
            Vector3 toCamera = GetDirectionToCamera(worldPosition, keepUpright, fallbackForward);
            if (keepUpright)
            {
                return CreateDirectionalRotation(toCamera, fallbackForward);
            }

            Camera camera = ResolveCamera();
            Vector3 up = camera != null ? camera.transform.up : Vector3.up;
            if (toCamera.sqrMagnitude < 0.0001f)
            {
                toCamera = fallbackForward.sqrMagnitude > 0.0001f ? fallbackForward.normalized : Vector3.back;
            }

            return Quaternion.LookRotation(toCamera, up.sqrMagnitude > 0.0001f ? up : Vector3.up);
        }

        private static Quaternion CreateDirectionalRotation(Vector3 forward, Vector3 fallbackForward)
        {
            Vector3 direction = forward.sqrMagnitude > 0.0001f
                ? forward.normalized
                : (fallbackForward.sqrMagnitude > 0.0001f ? fallbackForward.normalized : Vector3.forward);
            if (Mathf.Abs(Vector3.Dot(direction, Vector3.up)) > 0.995f)
            {
                direction = fallbackForward.sqrMagnitude > 0.0001f ? fallbackForward.normalized : Vector3.forward;
            }

            return Quaternion.LookRotation(direction, Vector3.up);
        }

        private Vector3 GetDirectionToCamera(Vector3 worldPosition, bool keepUpright, Vector3 fallbackForward)
        {
            Camera camera = ResolveCamera();
            Vector3 direction = camera != null
                ? camera.transform.position - worldPosition
                : (fallbackForward.sqrMagnitude > 0.0001f ? -fallbackForward : Vector3.back);
            if (keepUpright)
            {
                direction.y = 0f;
            }

            if (direction.sqrMagnitude < 0.0001f)
            {
                direction = fallbackForward.sqrMagnitude > 0.0001f ? -fallbackForward.normalized : Vector3.back;
                if (keepUpright)
                {
                    direction.y = 0f;
                }
            }

            return direction.sqrMagnitude > 0.0001f ? direction.normalized : Vector3.back;
        }

        private Camera ResolveCamera()
        {
            if (mainCamera == null || !mainCamera.isActiveAndEnabled)
            {
                mainCamera = Camera.main;
            }

            return mainCamera;
        }
    }

    internal sealed class PrototypeGameplayVfxInstance : MonoBehaviour
    {
        private const float EmptyEffectLifetime = 0.6f;
        private const float MaxLifetime = 8f;

        private PrototypeGameplayVfxService _owner;
        private GameObject _sourcePrefab;
        private ParticleSystem[] _particleSystems = System.Array.Empty<ParticleSystem>();
        private bool[] _defaultLoopValues = System.Array.Empty<bool>();
        private Coroutine _releaseRoutine;

        public GameObject SourcePrefab => _sourcePrefab;

        public void Initialize(PrototypeGameplayVfxService owner, GameObject sourcePrefab)
        {
            _owner = owner;
            _sourcePrefab = sourcePrefab;
            CacheParticleSystems();
        }

        public void Play(Vector3 worldPosition, Quaternion worldRotation, float uniformScale, bool forceOneShot)
        {
            if (_releaseRoutine != null)
            {
                StopCoroutine(_releaseRoutine);
                _releaseRoutine = null;
            }

            if (_particleSystems == null || _particleSystems.Length == 0)
            {
                CacheParticleSystems();
            }

            transform.SetParent(_owner != null ? _owner.transform : null, false);
            gameObject.SetActive(true);
            transform.SetPositionAndRotation(worldPosition, worldRotation);
            transform.localScale = Vector3.one * Mathf.Max(0.01f, uniformScale);

            for (int i = 0; i < _particleSystems.Length; i++)
            {
                ParticleSystem particleSystem = _particleSystems[i];
                if (particleSystem == null)
                {
                    continue;
                }

                ParticleSystem.MainModule main = particleSystem.main;
                bool defaultLoop = _defaultLoopValues != null && i < _defaultLoopValues.Length && _defaultLoopValues[i];
                main.loop = forceOneShot ? false : defaultLoop;
                particleSystem.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
                particleSystem.Clear(true);
                particleSystem.Play(true);
            }

            _releaseRoutine = StartCoroutine(ReleaseWhenFinished());
        }

        private void OnDisable()
        {
            if (_releaseRoutine != null)
            {
                StopCoroutine(_releaseRoutine);
                _releaseRoutine = null;
            }
        }

        private IEnumerator ReleaseWhenFinished()
        {
            float elapsed = 0f;
            while (elapsed < MaxLifetime)
            {
                bool anyAlive = false;
                for (int i = 0; i < _particleSystems.Length; i++)
                {
                    ParticleSystem particleSystem = _particleSystems[i];
                    if (particleSystem != null && particleSystem.IsAlive(true))
                    {
                        anyAlive = true;
                        break;
                    }
                }

                if (!anyAlive)
                {
                    break;
                }

                elapsed += Time.deltaTime;
                yield return null;
            }

            if (_particleSystems.Length == 0)
            {
                yield return new WaitForSeconds(EmptyEffectLifetime);
            }

            _releaseRoutine = null;
            _owner?.ReturnToPool(this);
        }

        private void CacheParticleSystems()
        {
            _particleSystems = GetComponentsInChildren<ParticleSystem>(true);
            _defaultLoopValues = new bool[_particleSystems.Length];
            for (int i = 0; i < _particleSystems.Length; i++)
            {
                ParticleSystem particleSystem = _particleSystems[i];
                if (particleSystem == null)
                {
                    continue;
                }

                ParticleSystem.MainModule main = particleSystem.main;
                _defaultLoopValues[i] = main.loop;
                main.scalingMode = ParticleSystemScalingMode.Hierarchy;
            }
        }
    }
}
