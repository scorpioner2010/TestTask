using System;
using System.Collections.Generic;
using Dreamteck.Splines;
using MobControlPrototype.Crowd;
using MobControlPrototype.Infrastructure;
using UnityEngine;

namespace MobControlPrototype.Gameplay
{
    [DisallowMultipleComponent]
    public sealed class UnitRunnerManager : MonoBehaviour
    {
        [Header("Units")]
        [SerializeField, Min(1)] private int maxActiveUnits = 160;
        [SerializeField, Min(0.1f)] private float runnerColliderRadius = 0.28f;
        [SerializeField, Min(0.1f)] private float runnerColliderHeight = 1.25f;

        [Header("Movement")]
        [SerializeField, Min(0f)] private float fallbackMoveSpeed = 5.4f;
        [SerializeField, Min(1f)] private float despawnZ = 72f;

        [Header("Gate Spawn")]
        [SerializeField, Min(0.05f)] private float cloneSpacing = 0.42f;
        [SerializeField, Min(0f)] private float cloneForwardOffset = 0.85f;

        [Header("Gate Spawn Animation")]
        [SerializeField, Min(0.05f)] private float cloneSpreadDuration = 0.24f;
        [SerializeField, Min(0f)] private float cloneSpreadArcHeight = 0.34f;
        [SerializeField, Range(0.1f, 1f)] private float cloneStartScaleMultiplier = 0.78f;

        private readonly List<UnitRunner> _activeRunners = new List<UnitRunner>(160);
        private readonly Stack<GameObject> _pool = new Stack<GameObject>(160);
        private readonly TubeTraversalRegistry _tubeTraversalRegistry = new TubeTraversalRegistry();
        private IUnitFactory _unitFactory;
        private IMovementStrategy _movementStrategy;
        private Transform _poolRoot;
        private int _nextRunnerId;
        private bool _levelEnded;

        public event Action<int> CountChanged;
        public event Action<bool> LevelEnded;

        public int ActiveCount => _activeRunners.Count;
        public int MaxActiveUnits => maxActiveUnits;
        public bool IsLevelEnded => _levelEnded;

        public void Initialize(IUnitFactory unitFactory, IMovementStrategy movementStrategy)
        {
            _unitFactory = unitFactory;
            _movementStrategy = movementStrategy;
        }

        private void Awake()
        {
            EnsurePoolRoot();
        }

        private void Start()
        {
            if (_unitFactory == null && ServiceLocator.TryGet(out IUnitFactory unitFactory))
            {
                _unitFactory = unitFactory;
            }

            if (_movementStrategy == null && ServiceLocator.TryGet(out IMovementStrategy movementStrategy))
            {
                _movementStrategy = movementStrategy;
            }

            _tubeTraversalRegistry.Refresh();
            CountChanged?.Invoke(ActiveCount);
        }

        private void FixedUpdate()
        {
            if (_levelEnded)
            {
                return;
            }

            _tubeTraversalRegistry.Tick(Time.fixedDeltaTime);

            if (ActiveCount == 0)
            {
                return;
            }

            Vector3 delta = _movementStrategy != null
                ? _movementStrategy.GetDelta(Time.fixedDeltaTime)
                : Vector3.forward * (fallbackMoveSpeed * Time.fixedDeltaTime);
            float movementDistance = delta.magnitude;
            Vector3 defaultMoveDirection = movementDistance > 0.0001f
                ? delta / movementDistance
                : Vector3.forward;
            float moveSpeed = Time.fixedDeltaTime > 0f
                ? movementDistance / Time.fixedDeltaTime
                : 0f;

            for (int i = ActiveCount - 1; i >= 0; i--)
            {
                UnitRunner runner = _activeRunners[i];
                if (runner.TickSpawnAnimation(Time.fixedDeltaTime))
                {
                    continue;
                }

                if (runner.TickTubeTraversal(Time.fixedDeltaTime))
                {
                    continue;
                }

                Vector3 runnerDirection = runner.HasMovementDirectionOverride
                    ? runner.MovementDirection
                    : defaultMoveDirection;
                Vector3 runnerDelta = runnerDirection * movementDistance;

                if (_tubeTraversalRegistry.TryEnterRunner(runner, runnerDelta, moveSpeed))
                {
                    continue;
                }

                Rigidbody body = runner.Body;
                Vector3 nextPosition = body != null ? body.position + runnerDelta : runner.transform.position + runnerDelta;
                if (body != null)
                {
                    body.MovePosition(nextPosition);
                }
                else
                {
                    runner.transform.position = nextPosition;
                }

                if (nextPosition.z >= despawnZ)
                {
                    BeginRunnerRemoval(runner, false);
                }
            }
        }

        public UnitRunner FireUnit(Vector3 worldPosition, Quaternion worldRotation)
        {
            if (_levelEnded || _unitFactory == null || ActiveCount >= maxActiveUnits)
            {
                return null;
            }

            return SpawnRunner(worldPosition, worldRotation);
        }

        public int ApplyGate(UnitRunner source, GateOperation operation, int value, float spawnZ)
        {
            if (source == null || !source.IsActive || _levelEnded)
            {
                return 0;
            }

            switch (operation)
            {
                case GateOperation.Add:
                    return SpawnCopies(source, value, spawnZ);
                case GateOperation.Multiply:
                    return SpawnCopies(source, Mathf.Max(0, value - 1), spawnZ);
                case GateOperation.Subtract:
                    return -RemoveUnits(value, source);
                default:
                    return 0;
            }
        }

        public void RemoveRunner(UnitRunner runner)
        {
            BeginRunnerRemoval(runner, false);
        }

        public void RemoveRunnerWithSink(UnitRunner runner)
        {
            BeginRunnerRemoval(runner, true);
        }

        public void CompleteLevel(bool success)
        {
            if (_levelEnded)
            {
                return;
            }

            _levelEnded = true;
            Debug.Log(success ? "Castle destroyed." : "Level ended.");
            LevelEnded?.Invoke(success);
        }

        private UnitRunner SpawnRunner(Vector3 worldPosition, Quaternion worldRotation)
        {
            GameObject instance = GetFromPool();
            if (instance == null)
            {
                return null;
            }

            instance.transform.SetParent(transform, true);
            instance.transform.SetPositionAndRotation(worldPosition, worldRotation);
            instance.name = $"Runner_{++_nextRunnerId:000}";
            instance.SetActive(true);

            UnitRunner runner = EnsureRunnerComponents(instance);
            if (runner == null)
            {
                instance.SetActive(false);
                instance.transform.SetParent(_poolRoot, false);
                return null;
            }

            runner.Initialize(this);
            runner.ActiveIndex = _activeRunners.Count;
            _activeRunners.Add(runner);
            CountChanged?.Invoke(ActiveCount);
            return runner;
        }

        private GameObject GetFromPool()
        {
            if (_pool.Count > 0)
            {
                return _pool.Pop();
            }

            return _unitFactory.CreateUnit(transform, Vector3.zero, Quaternion.identity);
        }

        private void BeginRunnerRemoval(UnitRunner runner, bool playSinkAnimation)
        {
            if (!ExtractRunner(runner))
            {
                return;
            }

            CountChanged?.Invoke(ActiveCount);

            if (playSinkAnimation)
            {
                runner.PlaySinkOut(() => FinalizeRunnerDespawn(runner));
                return;
            }

            FinalizeRunnerDespawn(runner);
        }

        private bool ExtractRunner(UnitRunner runner)
        {
            if (runner == null || !runner.IsActive)
            {
                return false;
            }

            int index = runner.ActiveIndex;
            int lastIndex = _activeRunners.Count - 1;
            UnitRunner last = _activeRunners[lastIndex];
            _activeRunners[index] = last;
            last.ActiveIndex = index;
            _activeRunners.RemoveAt(lastIndex);
            runner.PrepareForRemoval();
            return true;
        }

        private void FinalizeRunnerDespawn(UnitRunner runner)
        {
            runner.Deactivate();
            GameObject runnerObject = runner.gameObject;
            runnerObject.SetActive(false);
            runnerObject.transform.SetParent(_poolRoot, false);
            runnerObject.transform.localPosition = Vector3.zero;
            _pool.Push(runnerObject);
        }

        private UnitRunner EnsureRunnerComponents(GameObject runnerObject)
        {
            if (runnerObject == null)
            {
                Debug.LogError("Cannot spawn runner: factory returned null.");
                return null;
            }

            EnsurePhysicsComponents(runnerObject);

            UnitRunner runner = runnerObject.GetComponent<UnitRunner>();
            if (runner == null)
            {
                runner = runnerObject.AddComponent<UnitRunner>();
            }

            if (runner == null)
            {
                Debug.LogError($"Cannot spawn runner: UnitRunner component was not added to {runnerObject.name}.");
                return null;
            }

            runner.ConfigurePhysics(runnerColliderRadius, runnerColliderHeight);
            return runner;
        }

        private static void EnsurePhysicsComponents(GameObject runnerObject)
        {
            if (runnerObject.GetComponent<CapsuleCollider>() == null)
            {
                runnerObject.AddComponent<CapsuleCollider>();
            }

            if (runnerObject.GetComponent<Rigidbody>() == null)
            {
                runnerObject.AddComponent<Rigidbody>();
            }
        }

        private void EnsurePoolRoot()
        {
            if (_poolRoot != null)
            {
                return;
            }

            GameObject poolObject = new GameObject("InactiveRunnerPool");
            poolObject.transform.SetParent(transform, false);
            poolObject.SetActive(false);
            _poolRoot = poolObject.transform;
        }

        private static float GetCenteredOffset(int index, int total)
        {
            return index - (total - 1) * 0.5f;
        }

        private int SpawnCopies(UnitRunner source, int count, float spawnZ)
        {
            int extraCount = Mathf.Min(Mathf.Max(0, count), maxActiveUnits - ActiveCount);
            if (extraCount <= 0)
            {
                return 0;
            }

            Vector3 sourcePosition = source.WorldPosition;
            Vector3 basePosition = sourcePosition;
            basePosition.z = Mathf.Max(spawnZ, source.transform.position.z + cloneForwardOffset);
            Quaternion rotation = source.transform.rotation;
            int spawned = 0;

            for (int i = 0; i < extraCount; i++)
            {
                float xOffset = GetCenteredOffset(i + 1, extraCount + 1) * cloneSpacing;
                Vector3 targetPosition = basePosition + Vector3.right * xOffset;
                UnitRunner spawnedRunner = SpawnRunner(sourcePosition, rotation);
                if (spawnedRunner != null)
                {
                    if (source.HasMovementDirectionOverride)
                    {
                        spawnedRunner.SetMovementDirectionOverride(source.MovementDirection);
                    }

                    spawnedRunner.BeginSpawnAnimation(
                        sourcePosition,
                        targetPosition,
                        cloneSpreadDuration,
                        cloneSpreadArcHeight,
                        cloneStartScaleMultiplier);
                    spawned++;
                }
            }

            return spawned;
        }

        private int RemoveUnits(int count, UnitRunner source)
        {
            int remaining = Mathf.Max(0, count);
            int removed = 0;

            if (remaining > 0 && source != null && source.IsActive)
            {
                RemoveRunner(source);
                removed++;
                remaining--;
            }

            while (remaining > 0 && ActiveCount > 0)
            {
                RemoveRunner(_activeRunners[ActiveCount - 1]);
                removed++;
                remaining--;
            }

            return removed;
        }
    }

    internal sealed class TubeTraversalRegistry
    {
        private const string EntranceTag = "SplineEntrance";
        private const string ExitTag = "SplineExit";
        private const string ExitDirectionName = "ExitDirection";
        private const float DefaultEntryRadius = 1f;
        private const float EntryProjectionWindow = 0.16f;
        private const float EntrancePulseDuration = 0.16f;
        private const float EntrancePulseScaleMultiplier = 1.08f;
        private const float EntrancePulseScaleBoost = 2.25f;

        private readonly List<TubePath> _paths = new List<TubePath>(4);

        public void Refresh()
        {
            _paths.Clear();

            Dictionary<SplineComputer, TubePath> pathBySpline = new Dictionary<SplineComputer, TubePath>();
            RegisterTaggedEndpoints(EntranceTag, true, pathBySpline);
            RegisterTaggedEndpoints(ExitTag, false, pathBySpline);

            for (int i = _paths.Count - 1; i >= 0; i--)
            {
                if (!_paths[i].IsValid)
                {
                    _paths.RemoveAt(i);
                }
            }
        }

        public void Tick(float deltaTime)
        {
            for (int i = 0; i < _paths.Count; i++)
            {
                _paths[i].TickFeedback(deltaTime);
            }
        }

        public bool TryEnterRunner(UnitRunner runner, Vector3 movementDelta, float moveSpeed)
        {
            if (runner == null || !runner.IsActive || runner.IsInTube || _paths.Count == 0)
            {
                return false;
            }

            Vector3 currentPosition = runner.WorldPosition;
            Vector3 nextPosition = currentPosition + movementDelta;
            TubePath bestPath = null;
            Vector3 bestCapturePoint = Vector3.zero;
            float bestDistanceSqr = float.MaxValue;

            for (int i = 0; i < _paths.Count; i++)
            {
                TubePath path = _paths[i];
                if (!path.TryCapture(currentPosition, nextPosition, out Vector3 capturePoint, out float distanceSqr))
                {
                    continue;
                }

                if (distanceSqr < bestDistanceSqr)
                {
                    bestPath = path;
                    bestCapturePoint = capturePoint;
                    bestDistanceSqr = distanceSqr;
                }
            }

            if (bestPath == null)
            {
                return false;
            }

            SplineSample projectedSample = new SplineSample();
            bestPath.Spline.Project(
                bestCapturePoint,
                ref projectedSample,
                0.0,
                EntryProjectionWindow,
                SplineComputer.EvaluateMode.Cached,
                4);

            double startPercent = Mathf.Clamp01((float)projectedSample.percent);
            bool started = runner.BeginTubeTraversal(
                bestPath.Spline,
                startPercent,
                moveSpeed,
                bestPath.ExitPosition,
                bestPath.ExitRotation,
                bestPath.ExitDirection);
            if (started)
            {
                bestPath.PlayEntrancePulse();
            }

            return started;
        }

        private void RegisterTaggedEndpoints(
            string tag,
            bool isEntrance,
            Dictionary<SplineComputer, TubePath> pathBySpline)
        {
            Transform[] taggedTransforms = FindTaggedTransforms(tag);
            for (int i = 0; i < taggedTransforms.Length; i++)
            {
                Transform taggedTransform = taggedTransforms[i];
                if (taggedTransform == null)
                {
                    continue;
                }

                SplineComputer spline = taggedTransform.GetComponentInParent<SplineComputer>();
                if (spline == null)
                {
                    continue;
                }

                if (!pathBySpline.TryGetValue(spline, out TubePath path))
                {
                    path = new TubePath(spline);
                    pathBySpline.Add(spline, path);
                    _paths.Add(path);
                }

                if (isEntrance)
                {
                    path.SetEntrance(taggedTransform);
                }
                else
                {
                    path.SetExit(taggedTransform);
                }
            }
        }

        private static Transform[] FindTaggedTransforms(string tag)
        {
            GameObject[] taggedObjects;
            try
            {
                taggedObjects = GameObject.FindGameObjectsWithTag(tag);
            }
            catch (UnityException)
            {
                return Array.Empty<Transform>();
            }

            Transform[] result = new Transform[taggedObjects.Length];
            for (int i = 0; i < taggedObjects.Length; i++)
            {
                result[i] = taggedObjects[i] != null ? taggedObjects[i].transform : null;
            }

            return result;
        }

        private sealed class TubePath
        {
            private Transform _entranceMarker;
            private Transform _exitMarker;
            private Transform _exitDirectionMarker;
            private float _entryRadius = DefaultEntryRadius;
            private Transform _entranceAnchor;
            private Vector3 _entranceBaseScale = Vector3.one;
            private float _entrancePulseElapsed = -1f;

            public TubePath(SplineComputer spline)
            {
                Spline = spline;
            }

            public SplineComputer Spline { get; }

            public bool IsValid => Spline != null && _entranceMarker != null && _exitMarker != null;

            public Vector3 ExitPosition => _exitDirectionMarker != null ? _exitDirectionMarker.position : _exitMarker.position;

            public Vector3 ExitDirection
            {
                get
                {
                    Transform directionMarker = _exitDirectionMarker != null ? _exitDirectionMarker : _exitMarker;
                    return CreateDirection(directionMarker != null ? directionMarker.forward : Vector3.forward, Vector3.forward);
                }
            }

            public Quaternion ExitRotation
            {
                get
                {
                    SplineSample sample = Spline.Evaluate(1.0);
                    Vector3 preferredForward = _exitDirectionMarker != null ? _exitDirectionMarker.forward : sample.forward;
                    Vector3 fallbackForward = _exitMarker != null ? _exitMarker.forward : sample.forward;
                    return CreateRotation(preferredForward, fallbackForward);
                }
            }

            public void SetEntrance(Transform entranceMarker)
            {
                _entranceMarker = entranceMarker;
                _entranceAnchor = GetEndpointAnchor(entranceMarker);
                _entryRadius = ResolveEntryRadius(_entranceAnchor);
                if (_entranceAnchor != null)
                {
                    _entranceBaseScale = _entranceAnchor.localScale;
                }
            }

            public void SetExit(Transform exitMarker)
            {
                _exitMarker = exitMarker;
                _exitDirectionMarker = ResolveExitDirectionMarker(exitMarker);
            }

            public bool TryCapture(
                Vector3 from,
                Vector3 to,
                out Vector3 capturePoint,
                out float distanceSqr)
            {
                Transform entranceAnchor = _entranceAnchor != null ? _entranceAnchor : GetEndpointAnchor(_entranceMarker);
                if (entranceAnchor == null)
                {
                    capturePoint = Vector3.zero;
                    distanceSqr = float.MaxValue;
                    return false;
                }

                float t = GetClosestPointFactorXZ(from, to, entranceAnchor.position);
                capturePoint = Vector3.LerpUnclamped(from, to, t);

                Vector2 captureXZ = new Vector2(capturePoint.x, capturePoint.z);
                Vector2 entranceXZ = new Vector2(entranceAnchor.position.x, entranceAnchor.position.z);
                distanceSqr = (captureXZ - entranceXZ).sqrMagnitude;
                return distanceSqr <= _entryRadius * _entryRadius;
            }

            public void PlayEntrancePulse()
            {
                if (_entranceAnchor == null)
                {
                    return;
                }

                _entrancePulseElapsed = 0f;
            }

            public void TickFeedback(float deltaTime)
            {
                if (_entranceAnchor == null)
                {
                    return;
                }

                if (_entrancePulseElapsed < 0f)
                {
                    if (_entranceAnchor.localScale != _entranceBaseScale)
                    {
                        _entranceAnchor.localScale = _entranceBaseScale;
                    }

                    return;
                }

                _entrancePulseElapsed += deltaTime;
                float t = Mathf.Clamp01(_entrancePulseElapsed / EntrancePulseDuration);
                float pulse = Mathf.Sin(t * Mathf.PI);
                Vector3 pulseScale = _entranceBaseScale * GetBoostedScaleMultiplier(EntrancePulseScaleMultiplier);
                _entranceAnchor.localScale = Vector3.LerpUnclamped(_entranceBaseScale, pulseScale, pulse);

                if (t >= 1f)
                {
                    _entranceAnchor.localScale = _entranceBaseScale;
                    _entrancePulseElapsed = -1f;
                }
            }

            private static float GetBoostedScaleMultiplier(float scaleMultiplier)
            {
                return 1f + (Mathf.Max(1f, scaleMultiplier) - 1f) * EntrancePulseScaleBoost;
            }

            private static Transform GetEndpointAnchor(Transform marker)
            {
                if (marker == null)
                {
                    return null;
                }

                return marker.parent != null ? marker.parent : marker;
            }

            private static Transform ResolveExitDirectionMarker(Transform exitMarker)
            {
                Transform exitAnchor = GetEndpointAnchor(exitMarker);
                if (exitAnchor == null)
                {
                    return null;
                }

                Transform anchorChild = exitAnchor.Find(ExitDirectionName);
                if (anchorChild != null)
                {
                    return anchorChild;
                }

                return exitMarker != null ? exitMarker.Find(ExitDirectionName) : null;
            }

            private static float ResolveEntryRadius(Transform entranceAnchor)
            {
                if (entranceAnchor == null)
                {
                    return DefaultEntryRadius;
                }

                CapsuleCollider capsule = entranceAnchor.GetComponent<CapsuleCollider>();
                if (capsule == null)
                {
                    return DefaultEntryRadius;
                }

                Vector3 lossyScale = entranceAnchor.lossyScale;
                float horizontalScale = Mathf.Max(Mathf.Abs(lossyScale.x), Mathf.Abs(lossyScale.z));
                return Mathf.Max(DefaultEntryRadius, capsule.radius * horizontalScale);
            }

            private static float GetClosestPointFactorXZ(Vector3 segmentStart, Vector3 segmentEnd, Vector3 point)
            {
                Vector2 start = new Vector2(segmentStart.x, segmentStart.z);
                Vector2 end = new Vector2(segmentEnd.x, segmentEnd.z);
                Vector2 target = new Vector2(point.x, point.z);
                Vector2 segment = end - start;
                float lengthSqr = segment.sqrMagnitude;
                if (lengthSqr <= Mathf.Epsilon)
                {
                    return 0f;
                }

                float factor = Vector2.Dot(target - start, segment) / lengthSqr;
                return Mathf.Clamp01(factor);
            }

            private static Quaternion CreateRotation(Vector3 preferredForward, Vector3 fallbackForward)
            {
                Vector3 horizontalForward = CreateDirection(preferredForward, fallbackForward);
                return Quaternion.LookRotation(horizontalForward.normalized, Vector3.up);
            }

            private static Vector3 CreateDirection(Vector3 preferredForward, Vector3 fallbackForward)
            {
                Vector3 horizontalForward = new Vector3(preferredForward.x, 0f, preferredForward.z);
                if (horizontalForward.sqrMagnitude > 0.0001f)
                {
                    return horizontalForward.normalized;
                }

                Vector3 horizontalFallback = new Vector3(fallbackForward.x, 0f, fallbackForward.z);
                if (horizontalFallback.sqrMagnitude > 0.0001f)
                {
                    return horizontalFallback.normalized;
                }

                return Vector3.forward;
            }
        }
    }
}
