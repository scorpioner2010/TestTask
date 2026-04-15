using System;
using System.Collections.Generic;
using MobControlPrototype.Gameplay;
using MobControlPrototype.Infrastructure;
using UnityEngine;

namespace MobControlPrototype.Crowd
{
    [DisallowMultipleComponent]
    public sealed class CrowdController : MonoBehaviour
    {
        [Header("Count")]
        [SerializeField, Min(0)] private int initialUnitCount = 8;
        [SerializeField, Min(0)] private int totalSpawnCount = 42;
        [SerializeField, Min(1)] private int maxActiveUnits = 120;

        [Header("Spawn")]
        [SerializeField, Min(0f)] private float spawnRate = 8f;
        [SerializeField] private Vector3 spawnLocalOffset = new Vector3(0f, 0f, -1.4f);

        [Header("Formation")]
        [SerializeField, Min(1)] private int unitsPerRow = 5;
        [SerializeField, Min(0.1f)] private float horizontalSpacing = 0.62f;
        [SerializeField, Min(0.1f)] private float depthSpacing = 0.68f;
        [SerializeField, Min(0.1f)] private float reflowLerp = 18f;

        [Header("Collision")]
        [SerializeField] private BoxCollider crowdTrigger;
        [SerializeField, Min(0.1f)] private float triggerHeight = 1.8f;

        private readonly List<GameObject> _activeUnits = new List<GameObject>(128);
        private readonly Stack<GameObject> _unitPool = new Stack<GameObject>(64);
        private IUnitFactory _unitFactory;
        private IMovementStrategy _movementStrategy;
        private Transform _poolRoot;
        private float _spawnAccumulator;
        private int _releasedCount;
        private int _nextUnitId;
        private bool _spawned;
        private bool _levelEnded;

        public event Action<int> CountChanged;

        public int CurrentCount => _activeUnits.Count;
        public int MaxActiveUnits => maxActiveUnits;
        public Transform CrowdRoot => transform;
        public bool IsLevelEnded => _levelEnded;

        public void Initialize(IUnitFactory unitFactory, IMovementStrategy movementStrategy)
        {
            _unitFactory = unitFactory;
            _movementStrategy = movementStrategy;
        }

        private void Awake()
        {
            EnsurePoolRoot();
            EnsureCrowdTrigger();
        }

        private void Start()
        {
            if (!EnsureServices())
            {
                enabled = false;
                Debug.LogError($"{nameof(CrowdController)} could not resolve required services.");
                return;
            }

            SpawnInitialCrowd();
            UpdateCrowdTrigger();
        }

        private void Update()
        {
            if (_levelEnded)
            {
                return;
            }

            HandleTimedSpawn(Time.deltaTime);
            ReflowFormation(Time.deltaTime);

            if (_movementStrategy == null || CurrentCount <= 0)
            {
                return;
            }

            transform.position += _movementStrategy.GetDelta(Time.deltaTime);
        }

        private bool EnsureServices()
        {
            if (_unitFactory == null && ServiceLocator.TryGet(out IUnitFactory unitFactory))
            {
                _unitFactory = unitFactory;
            }

            if (_movementStrategy == null && ServiceLocator.TryGet(out IMovementStrategy movementStrategy))
            {
                _movementStrategy = movementStrategy;
            }

            return _unitFactory != null && _movementStrategy != null;
        }

        public int AddUnits(int count)
        {
            if (count <= 0 || _unitFactory == null || _levelEnded)
            {
                return 0;
            }

            int unitsToAdd = Mathf.Min(count, maxActiveUnits - CurrentCount);
            for (int i = 0; i < unitsToAdd; i++)
            {
                GameObject unit = GetUnitFromPool();
                unit.transform.SetParent(transform, false);
                unit.transform.localPosition = spawnLocalOffset;
                unit.transform.localRotation = Quaternion.identity;
                unit.SetActive(true);
                unit.name = $"Runner_{++_nextUnitId:000}";
                _activeUnits.Add(unit);
            }

            if (unitsToAdd > 0)
            {
                ReflowFormation(0f);
                UpdateCrowdTrigger();
                CountChanged?.Invoke(CurrentCount);
            }

            return unitsToAdd;
        }

        public int RemoveUnits(int count)
        {
            if (count <= 0 || CurrentCount <= 0)
            {
                return 0;
            }

            int unitsToRemove = Mathf.Min(count, CurrentCount);
            for (int i = 0; i < unitsToRemove; i++)
            {
                int index = _activeUnits.Count - 1;
                GameObject unit = _activeUnits[index];
                _activeUnits.RemoveAt(index);
                ReturnUnitToPool(unit);
            }

            ReflowFormation(0f);
            UpdateCrowdTrigger();
            CountChanged?.Invoke(CurrentCount);

            if (CurrentCount == 0)
            {
                _levelEnded = true;
            }

            return unitsToRemove;
        }

        public int ApplyGate(GateOperation operation, int value)
        {
            if (_levelEnded || CurrentCount <= 0)
            {
                return 0;
            }

            int before = CurrentCount;
            switch (operation)
            {
                case GateOperation.Add:
                    AddUnits(value);
                    break;
                case GateOperation.Multiply:
                    AddUnits(CurrentCount * Mathf.Max(0, value - 1));
                    break;
            }

            return CurrentCount - before;
        }

        public void CompleteLevel(bool success)
        {
            _levelEnded = true;
            Debug.Log(success ? $"Level complete with {CurrentCount} units." : "Level failed.");
        }

        private void SpawnInitialCrowd()
        {
            if (_spawned)
            {
                return;
            }

            int initialSpawned = AddUnits(initialUnitCount);
            _releasedCount = initialSpawned;
            _spawned = true;
        }

        private void HandleTimedSpawn(float deltaTime)
        {
            if (!_spawned || spawnRate <= 0f || _releasedCount >= totalSpawnCount || CurrentCount >= maxActiveUnits)
            {
                return;
            }

            _spawnAccumulator += spawnRate * deltaTime;
            int unitsToSpawn = Mathf.FloorToInt(_spawnAccumulator);
            if (unitsToSpawn <= 0)
            {
                return;
            }

            _spawnAccumulator -= unitsToSpawn;
            unitsToSpawn = Mathf.Min(unitsToSpawn, totalSpawnCount - _releasedCount, maxActiveUnits - CurrentCount);
            int spawned = AddUnits(unitsToSpawn);
            _releasedCount += spawned;
        }

        private GameObject GetUnitFromPool()
        {
            if (_unitPool.Count > 0)
            {
                return _unitPool.Pop();
            }

            return _unitFactory.CreateUnit(transform, spawnLocalOffset, Quaternion.identity);
        }

        private void ReturnUnitToPool(GameObject unit)
        {
            if (unit == null)
            {
                return;
            }

            EnsurePoolRoot();
            unit.SetActive(false);
            unit.transform.SetParent(_poolRoot, false);
            unit.transform.localPosition = Vector3.zero;
            _unitPool.Push(unit);
        }

        private void ReflowFormation(float deltaTime)
        {
            int total = CurrentCount;
            for (int i = 0; i < total; i++)
            {
                Transform unitTransform = _activeUnits[i].transform;
                Vector3 target = GetFormationPosition(i, total);
                if (deltaTime <= 0f)
                {
                    unitTransform.localPosition = target;
                }
                else
                {
                    unitTransform.localPosition = Vector3.Lerp(unitTransform.localPosition, target, 1f - Mathf.Exp(-reflowLerp * deltaTime));
                }
            }
        }

        private Vector3 GetFormationPosition(int index, int totalCount)
        {
            int row = index / unitsPerRow;
            int column = index % unitsPerRow;
            int rowCount = Mathf.Min(unitsPerRow, totalCount - row * unitsPerRow);

            float xOffset = (column - (rowCount - 1) * 0.5f) * horizontalSpacing;
            float zOffset = -row * depthSpacing;

            return new Vector3(xOffset, 0f, zOffset);
        }

        private void EnsurePoolRoot()
        {
            if (_poolRoot != null)
            {
                return;
            }

            GameObject poolObject = new GameObject("InactiveUnitPool");
            poolObject.transform.SetParent(transform, false);
            poolObject.SetActive(false);
            _poolRoot = poolObject.transform;
        }

        private void EnsureCrowdTrigger()
        {
            if (crowdTrigger == null)
            {
                crowdTrigger = GetComponent<BoxCollider>();
            }

            if (crowdTrigger == null)
            {
                crowdTrigger = gameObject.AddComponent<BoxCollider>();
            }

            crowdTrigger.isTrigger = true;

            Rigidbody body = GetComponent<Rigidbody>();
            if (body == null)
            {
                body = gameObject.AddComponent<Rigidbody>();
            }

            body.isKinematic = true;
            body.useGravity = false;
        }

        private void UpdateCrowdTrigger()
        {
            if (crowdTrigger == null)
            {
                return;
            }

            int count = Mathf.Max(CurrentCount, 1);
            int rows = Mathf.CeilToInt(count / (float)unitsPerRow);
            int widestRow = Mathf.Min(unitsPerRow, count);

            float width = Mathf.Max(1.4f, (widestRow - 1) * horizontalSpacing + 1.3f);
            float depth = Mathf.Max(1.4f, (rows - 1) * depthSpacing + 1.3f);
            float centerZ = -Mathf.Max(0, rows - 1) * depthSpacing * 0.5f;

            crowdTrigger.center = new Vector3(0f, triggerHeight * 0.5f, centerZ);
            crowdTrigger.size = new Vector3(width, triggerHeight, depth);
        }
    }
}
