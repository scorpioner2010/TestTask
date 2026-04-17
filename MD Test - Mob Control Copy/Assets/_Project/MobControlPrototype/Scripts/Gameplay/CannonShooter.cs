using MobControlPrototype.Infrastructure;
using UnityEngine;

namespace MobControlPrototype.Gameplay
{
    [DisallowMultipleComponent]
    public sealed class CannonShooter : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private Transform spawnPoint;
        [SerializeField] private Transform muzzle;
        [SerializeField] private UnitRunnerManager runnerManager;
        [SerializeField] private Transform recoilRoot;

        [Header("Movement")]
        [SerializeField, Min(0f)] private float horizontalSpeed = 5f;
        [SerializeField] private float minX = -2.7f;
        [SerializeField] private float maxX = 2.7f;
        [SerializeField] private bool followMouseWithoutClick = true;
        [SerializeField, Min(0.1f)] private float mouseFollowSharpness = 16f;

        [Header("Shooting")]
        [SerializeField, Min(0.05f)] private float shotsPerSecond = 4f;
        [SerializeField] private KeyCode keyboardFireKey = KeyCode.Space;
        [SerializeField] private Vector3 runnerSpawnOffset = new Vector3(0f, 0.12f, 0.95f);

        [Header("Shot Feedback")]
        [SerializeField, Min(0.05f)] private float recoilDuration = 0.16f;
        [SerializeField] private Vector3 compressedScaleMultiplier = new Vector3(1.05f, 0.9f, 0.84f);
        [SerializeField] private Vector3 stretchedScaleMultiplier = new Vector3(0.96f, 1.04f, 1.12f);

        private UnityEngine.Camera _camera;
        private PrototypeGameplayVfxService _vfxService;
        private float _shotTimer;
        private float _recoilTimer;
        private bool _isRecoiling;
        private Vector3 _baseRecoilScale = Vector3.one;
        private Transform _cachedBaseScaleRoot;

        public Vector3 SpawnWorldPosition
        {
            get
            {
                EnsureResolvedReferences();
                if (spawnPoint != null)
                {
                    return spawnPoint.position;
                }

                if (muzzle != null)
                {
                    return muzzle.TransformPoint(runnerSpawnOffset);
                }

                return transform.position;
            }
        }

        private void Awake()
        {
            if (ShouldDisableAsDuplicate())
            {
                enabled = false;
            }
        }

        private void Start()
        {
            if (!enabled)
            {
                return;
            }

            EnsureResolvedReferences();

            if (runnerManager == null)
            {
                ServiceLocator.TryGet(out runnerManager);
            }

            _camera = UnityEngine.Camera.main;
        }

        private void EnsureResolvedReferences()
        {
            if (runnerManager == null)
            {
                ServiceLocator.TryGet(out runnerManager);
            }

            if (_vfxService == null)
            {
                ServiceLocator.TryGet(out _vfxService);
            }

            if (spawnPoint == null)
            {
                spawnPoint = FindChildRecursive(transform, "Hole");
            }

            if (muzzle == null)
            {
                muzzle = FindChildRecursive(transform, "Muzzle");
            }

            if (muzzle == null)
            {
                muzzle = transform;
            }

            if (spawnPoint == null)
            {
                spawnPoint = muzzle;
            }

            if (recoilRoot == null)
            {
                recoilRoot = transform;
            }

            if (recoilRoot != null && recoilRoot != _cachedBaseScaleRoot)
            {
                _baseRecoilScale = recoilRoot.localScale;
                _cachedBaseScaleRoot = recoilRoot;
            }
        }

        private void Update()
        {
            HandleHorizontalInput(Time.deltaTime);
            HandleShooting(Time.deltaTime);
            UpdateShotFeedback(Time.deltaTime);
        }

        private void HandleHorizontalInput(float deltaTime)
        {
            float keyboardInput = Input.GetAxisRaw("Horizontal");
            if (Mathf.Abs(keyboardInput) > 0.01f)
            {
                MoveByKeyboard(keyboardInput, deltaTime);
                return;
            }

            if (followMouseWithoutClick)
            {
                MoveTowardMouse(deltaTime);
            }
        }

        private void MoveByKeyboard(float input, float deltaTime)
        {
            Vector3 position = transform.position;
            position.x = Mathf.Clamp(position.x + input * horizontalSpeed * deltaTime, minX, maxX);
            transform.position = position;
        }

        private void MoveTowardMouse(float deltaTime)
        {
            if (_camera == null)
            {
                return;
            }

            Ray ray = _camera.ScreenPointToRay(Input.mousePosition);
            Plane groundPlane = new Plane(Vector3.up, Vector3.zero);
            if (!groundPlane.Raycast(ray, out float distance))
            {
                return;
            }

            float targetX = Mathf.Clamp(ray.GetPoint(distance).x, minX, maxX);
            Vector3 position = transform.position;
            position.x = Mathf.Lerp(position.x, targetX, 1f - Mathf.Exp(-mouseFollowSharpness * deltaTime));
            transform.position = position;
        }

        private void HandleShooting(float deltaTime)
        {
            if (runnerManager == null || runnerManager.IsLevelEnded || shotsPerSecond <= 0f)
            {
                return;
            }

            if (!IsFireHeld())
            {
                _shotTimer = 1f / shotsPerSecond;
                return;
            }

            _shotTimer += deltaTime;
            float interval = 1f / shotsPerSecond;
            while (_shotTimer >= interval)
            {
                _shotTimer -= interval;
                FireSingleRunner();
            }
        }

        private bool IsFireHeld()
        {
            return Input.GetKey(keyboardFireKey) || Input.GetMouseButton(0);
        }

        private void FireSingleRunner()
        {
            EnsureResolvedReferences();
            Vector3 spawnPosition = SpawnWorldPosition;
            Quaternion spawnRotation = Quaternion.LookRotation(Vector3.forward, Vector3.up);
            if (runnerManager.FireUnit(spawnPosition, spawnRotation) != null)
            {
                Vector3 effectPosition = spawnPoint != null ? spawnPoint.position : spawnPosition;
                Vector3 effectForward = spawnPoint != null
                    ? spawnPoint.forward
                    : (muzzle != null ? muzzle.forward : Vector3.forward);
                _vfxService?.PlayShot(effectPosition, effectForward);
                TriggerShotFeedback();
            }
        }

        private static Transform FindChildRecursive(Transform root, string childName)
        {
            if (root == null)
            {
                return null;
            }

            for (int i = 0; i < root.childCount; i++)
            {
                Transform child = root.GetChild(i);
                if (child.name == childName)
                {
                    return child;
                }

                Transform nested = FindChildRecursive(child, childName);
                if (nested != null)
                {
                    return nested;
                }
            }

            return null;
        }

        private void TriggerShotFeedback()
        {
            if (recoilRoot == null || recoilDuration <= 0f)
            {
                return;
            }

            recoilRoot.localScale = _baseRecoilScale;
            _recoilTimer = 0f;
            _isRecoiling = true;
        }

        private void UpdateShotFeedback(float deltaTime)
        {
            if (recoilRoot == null)
            {
                return;
            }

            if (!_isRecoiling)
            {
                recoilRoot.localScale = _baseRecoilScale;
                return;
            }

            _recoilTimer += deltaTime;
            float normalizedTime = Mathf.Clamp01(_recoilTimer / recoilDuration);
            Vector3 compressedScale = Vector3.Scale(_baseRecoilScale, compressedScaleMultiplier);
            Vector3 stretchedScale = Vector3.Scale(_baseRecoilScale, stretchedScaleMultiplier);

            if (normalizedTime < 0.32f)
            {
                recoilRoot.localScale = Vector3.LerpUnclamped(_baseRecoilScale, compressedScale, normalizedTime / 0.32f);
            }
            else if (normalizedTime < 0.68f)
            {
                recoilRoot.localScale = Vector3.LerpUnclamped(compressedScale, stretchedScale, (normalizedTime - 0.32f) / 0.36f);
            }
            else
            {
                recoilRoot.localScale = Vector3.LerpUnclamped(stretchedScale, _baseRecoilScale, (normalizedTime - 0.68f) / 0.32f);
            }

            if (normalizedTime >= 1f)
            {
                _isRecoiling = false;
                recoilRoot.localScale = _baseRecoilScale;
            }
        }

        private bool ShouldDisableAsDuplicate()
        {
            Transform root = transform.root;
            CannonShooter[] shooters = root.GetComponentsInChildren<CannonShooter>(true);
            CannonShooter primary = this;

            for (int i = 0; i < shooters.Length; i++)
            {
                CannonShooter candidate = shooters[i];
                if (candidate == null)
                {
                    continue;
                }

                if (IsHigherPriority(candidate, primary))
                {
                    primary = candidate;
                }
            }

            return primary != this;
        }

        private static bool IsHigherPriority(CannonShooter candidate, CannonShooter current)
        {
            int candidateScore = GetPriorityScore(candidate);
            int currentScore = GetPriorityScore(current);
            if (candidateScore != currentScore)
            {
                return candidateScore > currentScore;
            }

            int candidateDepth = GetHierarchyDepth(candidate.transform);
            int currentDepth = GetHierarchyDepth(current.transform);
            if (candidateDepth != currentDepth)
            {
                return candidateDepth < currentDepth;
            }

            return candidate.GetInstanceID() < current.GetInstanceID();
        }

        private static int GetPriorityScore(CannonShooter shooter)
        {
            int score = 0;
            if (shooter.spawnPoint != null)
            {
                score += 4;
            }

            if (shooter.muzzle != null)
            {
                score += 2;
            }

            if (shooter.runnerManager != null)
            {
                score += 1;
            }

            return score;
        }

        private static int GetHierarchyDepth(Transform node)
        {
            int depth = 0;
            while (node.parent != null)
            {
                depth++;
                node = node.parent;
            }

            return depth;
        }
    }
}
