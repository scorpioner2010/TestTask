using UnityEngine;

namespace MobControlPrototype.Crowd
{
    public sealed class UnitFactory : IUnitFactory
    {
        private const float TargetUnitHeight = 1.2f;

        private readonly GameObject _unitPrefab;
        private readonly GameObject _unitModelPrefab;
        private readonly AnimationClip _runningClip;
        private readonly Material _fallbackMaterial;
        private readonly float _animationSpeed;
        private int _spawnedCount;

        public UnitFactory(
            GameObject unitPrefab,
            GameObject unitModelPrefab,
            AnimationClip runningClip,
            Material fallbackMaterial,
            float animationSpeed)
        {
            _unitPrefab = unitPrefab;
            _unitModelPrefab = unitModelPrefab;
            _runningClip = runningClip;
            _fallbackMaterial = fallbackMaterial;
            _animationSpeed = Mathf.Max(0f, animationSpeed);
        }

        public GameObject CreateUnit(Transform parent, Vector3 localPosition, Quaternion localRotation)
        {
            GameObject unit = CreateUnitInstance(parent);

            unit.transform.localPosition = localPosition;
            unit.transform.localRotation = localRotation;
            ConfigureAnimator(unit);

            return unit;
        }

        private GameObject CreateUnitInstance(Transform parent)
        {
            if (_unitPrefab != null)
            {
                return Object.Instantiate(_unitPrefab, parent, false);
            }

            if (_unitModelPrefab != null)
            {
                return CreateModelUnit(parent);
            }

            Debug.LogError("Runner model is not assigned. Expected Assets/Models/LowPoly_Stickaman_Running.fbx.");
            GameObject missingUnit = new GameObject("MissingRunnerModel");
            missingUnit.transform.SetParent(parent, false);
            return missingUnit;
        }

        private GameObject CreateModelUnit(Transform parent)
        {
            GameObject unit = new GameObject("RunnerUnit");
            unit.transform.SetParent(parent, false);

            GameObject visual = Object.Instantiate(_unitModelPrefab, unit.transform, false);

            visual.name = "LowPoly_Stickaman_Running";
            visual.transform.localPosition = Vector3.zero;
            visual.transform.localRotation = Quaternion.identity;

            NormalizeVisual(visual, TargetUnitHeight);
            AssignMaterial(visual, _fallbackMaterial);
            RemoveColliders(visual);

            return unit;
        }

        private void ConfigureAnimator(GameObject unit)
        {
            Animator animator = unit.GetComponentInChildren<Animator>();
            if (animator == null)
            {
                return;
            }

            animator.applyRootMotion = false;
            animator.updateMode = AnimatorUpdateMode.Normal;
            animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;

            if (_runningClip == null)
            {
                return;
            }

            UnitAnimationPlayer animationPlayer = animator.GetComponent<UnitAnimationPlayer>();
            if (animationPlayer == null)
            {
                animationPlayer = animator.gameObject.AddComponent<UnitAnimationPlayer>();
            }

            animationPlayer.Initialize(_runningClip, _animationSpeed, GetStartOffset());
        }

        private float GetStartOffset()
        {
            int index = _spawnedCount++;
            return Mathf.Repeat(index * 0.173f, 1f);
        }

        private static void NormalizeVisual(GameObject visual, float targetHeight)
        {
            if (!TryGetBounds(visual, out Bounds bounds) || bounds.size.y <= 0.001f)
            {
                return;
            }

            float scale = targetHeight / bounds.size.y;
            visual.transform.localScale *= scale;

            if (!TryGetBounds(visual, out bounds))
            {
                return;
            }

            visual.transform.position -= new Vector3(bounds.center.x, bounds.min.y, bounds.center.z);
        }

        private static bool TryGetBounds(GameObject root, out Bounds bounds)
        {
            Renderer[] renderers = root.GetComponentsInChildren<Renderer>(true);
            bounds = new Bounds(root.transform.position, Vector3.zero);

            if (renderers.Length == 0)
            {
                return false;
            }

            bounds = renderers[0].bounds;
            for (int i = 1; i < renderers.Length; i++)
            {
                bounds.Encapsulate(renderers[i].bounds);
            }

            return true;
        }

        private static void AssignMaterial(GameObject root, Material material)
        {
            if (material == null)
            {
                return;
            }

            Renderer[] renderers = root.GetComponentsInChildren<Renderer>(true);
            for (int i = 0; i < renderers.Length; i++)
            {
                renderers[i].sharedMaterial = material;
            }
        }

        private static void RemoveColliders(GameObject root)
        {
            Collider[] colliders = root.GetComponentsInChildren<Collider>(true);
            for (int i = 0; i < colliders.Length; i++)
            {
                Object.Destroy(colliders[i]);
            }
        }
    }
}
