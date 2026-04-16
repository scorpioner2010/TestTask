using System.Collections.Generic;
using MobControlPrototype.Infrastructure;
using UnityEngine;

namespace MobControlPrototype.Gameplay
{
    [DisallowMultipleComponent]
    public sealed class EnemyRunnerManager : MonoBehaviour
    {
        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        private static readonly int ColorId = Shader.PropertyToID("_Color");

        [Header("References")]
        [SerializeField] private UnitRunnerManager runnerManager;
        [SerializeField] private CannonShooter cannonShooter;
        [SerializeField] private FinishTarget finishTarget;
        [SerializeField] private GameObject enemyPrefab;

        [Header("Spawning")]
        [SerializeField, Min(0.1f)] private float spawnRate = 2f;
        [SerializeField, Min(1)] private int maxActiveEnemies = 48;
        [SerializeField] private Vector3 spawnOffset = new Vector3(0f, 0f, -1.6f);

        [Header("Movement")]
        [SerializeField, Min(0.1f)] private float moveSpeed = 5.1f;
        [SerializeField, Min(90f)] private float turnSpeed = 540f;

        [Header("Physics")]
        [SerializeField, Min(0.1f)] private float colliderRadius = 0.28f;
        [SerializeField, Min(0.1f)] private float colliderHeight = 1.25f;

        [Header("Visuals")]
        [SerializeField] private Color enemyTint = new Color(0.84f, 0.18f, 0.22f, 1f);

        private readonly List<EnemyMob> _activeEnemies = new List<EnemyMob>(48);
        private readonly Stack<GameObject> _pool = new Stack<GameObject>(48);
        private Transform _poolRoot;
        private MaterialPropertyBlock _propertyBlock;
        private float _spawnTimer;
        private int _spawnedCount;
        private bool _cleanedLegacyEnemies;

        public void Configure(
            UnitRunnerManager unitRunnerManager,
            CannonShooter shooter,
            FinishTarget target,
            GameObject prefab)
        {
            runnerManager = unitRunnerManager;
            cannonShooter = shooter;
            finishTarget = target;
            enemyPrefab = prefab;
        }

        private void Awake()
        {
            EnsurePoolRoot();
        }

        private void Start()
        {
            if (runnerManager == null)
            {
                ServiceLocator.TryGet(out runnerManager);
            }

            if (cannonShooter == null)
            {
                cannonShooter = FindObjectOfType<CannonShooter>();
            }

            if (finishTarget == null)
            {
                finishTarget = FindObjectOfType<FinishTarget>();
            }

            CleanupLegacySceneEnemies();
        }

        private void FixedUpdate()
        {
            if (runnerManager == null || runnerManager.IsLevelEnded || enemyPrefab == null)
            {
                return;
            }

            if (finishTarget != null && finishTarget.CurrentHealth > 0)
            {
                TrySpawnEnemies(Time.fixedDeltaTime);
            }

            MoveEnemies(Time.fixedDeltaTime);
        }

        public void RemoveEnemy(EnemyMob enemy)
        {
            BeginEnemyRemoval(enemy, false);
        }

        public void RemoveEnemyWithSink(EnemyMob enemy)
        {
            BeginEnemyRemoval(enemy, true);
        }

        private void TrySpawnEnemies(float deltaTime)
        {
            if (spawnRate <= 0f || _activeEnemies.Count >= maxActiveEnemies)
            {
                return;
            }

            float interval = 1f / spawnRate;
            _spawnTimer += deltaTime;

            while (_spawnTimer >= interval && _activeEnemies.Count < maxActiveEnemies)
            {
                _spawnTimer -= interval;
                SpawnEnemy();
            }
        }

        private void MoveEnemies(float deltaTime)
        {
            for (int i = _activeEnemies.Count - 1; i >= 0; i--)
            {
                EnemyMob enemy = _activeEnemies[i];
                if (enemy == null || !enemy.IsActive)
                {
                    continue;
                }

                Vector3 currentPosition = enemy.WorldPosition;
                Vector3 targetDirection = GetDirectionToCannon(currentPosition);
                Quaternion currentRotation = enemy.transform.rotation;
                Quaternion targetRotation = Quaternion.LookRotation(targetDirection, Vector3.up);
                Quaternion nextRotation = Quaternion.RotateTowards(currentRotation, targetRotation, turnSpeed * deltaTime);
                Vector3 moveDirection = nextRotation * Vector3.forward;
                moveDirection.y = 0f;
                if (moveDirection.sqrMagnitude <= 0.000001f)
                {
                    continue;
                }

                moveDirection.Normalize();
                Vector3 nextPosition = currentPosition + moveDirection * (moveSpeed * deltaTime);
                Rigidbody body = enemy.Body;
                if (body != null)
                {
                    body.MovePosition(nextPosition);
                    body.MoveRotation(nextRotation);
                }
                else
                {
                    enemy.transform.position = nextPosition;
                    enemy.transform.rotation = nextRotation;
                }
            }
        }

        private void SpawnEnemy()
        {
            GameObject instance = GetFromPool();
            Vector3 spawnPosition = GetSpawnPosition();
            Vector3 direction = GetDirectionToCannon(spawnPosition);

            instance.transform.SetParent(transform, true);
            instance.transform.SetPositionAndRotation(spawnPosition, Quaternion.LookRotation(direction, Vector3.up));
            instance.name = $"EnemyRunner_{++_spawnedCount:000}";
            ApplyEnemyTint(instance);
            instance.SetActive(true);

            EnemyMob enemy = EnsureEnemyComponents(instance);
            enemy.Initialize(this);
            enemy.ActiveIndex = _activeEnemies.Count;
            _activeEnemies.Add(enemy);
        }

        private GameObject GetFromPool()
        {
            if (_pool.Count > 0)
            {
                return _pool.Pop();
            }

            GameObject instance = Instantiate(enemyPrefab, _poolRoot, false);
            instance.SetActive(false);
            return instance;
        }

        private void BeginEnemyRemoval(EnemyMob enemy, bool playSinkAnimation)
        {
            if (!ExtractEnemy(enemy))
            {
                return;
            }

            if (playSinkAnimation)
            {
                enemy.PlaySinkOut(() => FinalizeEnemyRemoval(enemy));
                return;
            }

            FinalizeEnemyRemoval(enemy);
        }

        private bool ExtractEnemy(EnemyMob enemy)
        {
            if (enemy == null || !enemy.IsActive)
            {
                return false;
            }

            int index = enemy.ActiveIndex;
            int lastIndex = _activeEnemies.Count - 1;
            EnemyMob last = _activeEnemies[lastIndex];
            _activeEnemies[index] = last;
            last.ActiveIndex = index;
            _activeEnemies.RemoveAt(lastIndex);
            enemy.PrepareForRemoval();
            return true;
        }

        private void FinalizeEnemyRemoval(EnemyMob enemy)
        {
            enemy.Deactivate();
            GameObject enemyObject = enemy.gameObject;
            enemyObject.SetActive(false);
            enemyObject.transform.SetParent(_poolRoot, false);
            enemyObject.transform.localPosition = Vector3.zero;
            _pool.Push(enemyObject);
        }

        private EnemyMob EnsureEnemyComponents(GameObject enemyObject)
        {
            EnemyMob enemy = enemyObject.GetComponent<EnemyMob>();
            if (enemy == null)
            {
                enemy = enemyObject.AddComponent<EnemyMob>();
            }

            enemy.ConfigurePhysics(colliderRadius, colliderHeight);
            return enemy;
        }

        private void EnsurePoolRoot()
        {
            if (_poolRoot != null)
            {
                return;
            }

            GameObject poolObject = new GameObject("InactiveEnemyPool");
            poolObject.transform.SetParent(transform, false);
            poolObject.SetActive(false);
            _poolRoot = poolObject.transform;
        }

        private Vector3 GetSpawnPosition()
        {
            Vector3 position = finishTarget != null ? finishTarget.transform.position : transform.position;
            position += spawnOffset;

            if (cannonShooter != null)
            {
                position.y = cannonShooter.SpawnWorldPosition.y;
            }

            return position;
        }

        private Vector3 GetDirectionToCannon(Vector3 fromPosition)
        {
            Vector3 targetPosition = GetCannonTargetPosition(fromPosition.y);
            targetPosition.y = fromPosition.y;

            Vector3 direction = targetPosition - fromPosition;
            direction.y = 0f;
            if (direction.sqrMagnitude <= 0.0001f)
            {
                return Vector3.back;
            }

            return direction.normalized;
        }

        private Vector3 GetCannonTargetPosition(float yPosition)
        {
            Vector3 targetPosition;
            if (cannonShooter != null)
            {
                targetPosition = cannonShooter.SpawnWorldPosition;
            }
            else
            {
                targetPosition = transform.position + Vector3.back;
            }

            targetPosition.y = yPosition;
            return targetPosition;
        }

        private void ApplyEnemyTint(GameObject enemyObject)
        {
            if (_propertyBlock == null)
            {
                _propertyBlock = new MaterialPropertyBlock();
            }

            Renderer[] renderers = enemyObject.GetComponentsInChildren<Renderer>(true);
            for (int rendererIndex = 0; rendererIndex < renderers.Length; rendererIndex++)
            {
                Renderer renderer = renderers[rendererIndex];
                if (renderer == null)
                {
                    continue;
                }

                Material[] materials = renderer.sharedMaterials;
                for (int materialIndex = 0; materialIndex < materials.Length; materialIndex++)
                {
                    int colorPropertyId = GetColorPropertyId(materials[materialIndex]);
                    if (colorPropertyId == -1)
                    {
                        continue;
                    }

                    renderer.GetPropertyBlock(_propertyBlock, materialIndex);
                    _propertyBlock.SetColor(colorPropertyId, enemyTint);
                    renderer.SetPropertyBlock(_propertyBlock, materialIndex);
                }
            }
        }

        private static int GetColorPropertyId(Material material)
        {
            if (material == null)
            {
                return -1;
            }

            if (material.HasProperty(BaseColorId))
            {
                return BaseColorId;
            }

            return material.HasProperty(ColorId) ? ColorId : -1;
        }

        private void CleanupLegacySceneEnemies()
        {
            if (_cleanedLegacyEnemies)
            {
                return;
            }

            _cleanedLegacyEnemies = true;

            EnemyMob[] legacyEnemies = FindObjectsOfType<EnemyMob>(true);
            for (int i = 0; i < legacyEnemies.Length; i++)
            {
                EnemyMob enemy = legacyEnemies[i];
                if (enemy == null || enemy.Manager == this)
                {
                    continue;
                }

                Destroy(enemy.gameObject);
            }

            Transform[] transforms = FindObjectsOfType<Transform>(true);
            for (int i = 0; i < transforms.Length; i++)
            {
                Transform candidate = transforms[i];
                if (candidate == null || !candidate.gameObject.scene.IsValid())
                {
                    continue;
                }

                if (candidate.name.StartsWith("EnemyGroup_"))
                {
                    Destroy(candidate.gameObject);
                }
            }
        }
    }
}
