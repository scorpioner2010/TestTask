using UnityEngine;

namespace MobControlPrototype.Bootstrap
{
    [DisallowMultipleComponent]
    public sealed class PrototypeLevelBuilder : MonoBehaviour
    {
        [SerializeField] private Material groundMaterial;
        [SerializeField] private Material roadMaterial;
        [SerializeField] private Material railMaterial;
        [SerializeField] private Material laneMarkerMaterial;
        [SerializeField] private Material cannonMaterial;
        [SerializeField] private GameObject cannonPrefab;

        private bool _built;

        private void Awake()
        {
            Build();
        }

        public void Build()
        {
            if (_built)
            {
                return;
            }

            Transform levelRoot = new GameObject("GeneratedLevel").transform;
            levelRoot.SetParent(transform, false);

            CreateBox(levelRoot, "Ground", new Vector3(0f, -0.12f, 29f), new Vector3(24f, 0.16f, 84f), groundMaterial);
            CreateBox(levelRoot, "Road", new Vector3(0f, 0f, 29f), new Vector3(7.2f, 0.18f, 78f), roadMaterial);
            CreateBox(levelRoot, "LeftRail", new Vector3(-3.9f, 0.18f, 29f), new Vector3(0.28f, 0.28f, 78f), railMaterial);
            CreateBox(levelRoot, "RightRail", new Vector3(3.9f, 0.18f, 29f), new Vector3(0.28f, 0.28f, 78f), railMaterial);

            for (int i = 0; i < 16; i++)
            {
                CreateBox(levelRoot, $"LaneMarker_{i + 1:00}", new Vector3(0f, 0.11f, -5f + i * 4.6f), new Vector3(0.16f, 0.04f, 1.7f), laneMarkerMaterial);
            }

            CreateCannon(levelRoot);
            _built = true;
        }

        private GameObject CreateBox(Transform parent, string objectName, Vector3 position, Vector3 scale, Material material)
        {
            GameObject box = GameObject.CreatePrimitive(PrimitiveType.Cube);
            box.name = objectName;
            box.transform.SetParent(parent, false);
            box.transform.localPosition = position;
            box.transform.localScale = scale;
            ApplyMaterial(box, material);
            RemoveCollider(box);
            return box;
        }

        private void CreateCannon(Transform parent)
        {
            if (cannonPrefab == null)
            {
                Debug.LogError("Cannon model is not assigned. Expected Assets/Models/Cannon.fbx.");
                return;
            }

            Object instantiated = Instantiate((Object)cannonPrefab);
            GameObject cannon = instantiated as GameObject;
            if (cannon == null)
            {
                Debug.LogError($"Cannon model reference is not a GameObject: {cannonPrefab.name}");
                Destroy(instantiated);
                return;
            }

            cannon.name = "StartCannon";
            cannon.transform.SetParent(parent, false);
            cannon.transform.localPosition = new Vector3(0f, 0.08f, -6.1f);
            cannon.transform.localRotation = Quaternion.identity;
            NormalizeHeight(cannon, 1.28f);
            ApplyMaterial(cannon, cannonMaterial);
            RemoveColliders(cannon);
        }

        private static void ApplyMaterial(GameObject target, Material material)
        {
            if (material == null)
            {
                return;
            }

            Renderer[] renderers = target.GetComponentsInChildren<Renderer>(true);
            for (int i = 0; i < renderers.Length; i++)
            {
                renderers[i].sharedMaterial = material;
            }
        }

        private static void RemoveCollider(GameObject target)
        {
            Collider collider = target.GetComponent<Collider>();
            if (collider != null)
            {
                Destroy(collider);
            }
        }

        private static void RemoveColliders(GameObject target)
        {
            Collider[] colliders = target.GetComponentsInChildren<Collider>(true);
            for (int i = 0; i < colliders.Length; i++)
            {
                Destroy(colliders[i]);
            }
        }

        private static void NormalizeHeight(GameObject target, float targetHeight)
        {
            if (!TryGetBounds(target, out Bounds bounds) || bounds.size.y <= 0.001f)
            {
                return;
            }

            float scale = targetHeight / bounds.size.y;
            target.transform.localScale *= scale;

            if (!TryGetBounds(target, out bounds))
            {
                return;
            }

            target.transform.position -= new Vector3(bounds.center.x, bounds.min.y, bounds.center.z);
        }

        private static bool TryGetBounds(GameObject target, out Bounds bounds)
        {
            Renderer[] renderers = target.GetComponentsInChildren<Renderer>(true);
            bounds = new Bounds(target.transform.position, Vector3.zero);

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
    }
}
