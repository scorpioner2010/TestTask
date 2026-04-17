using System;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace MobControlPrototype.Gameplay
{
    [Serializable]
    public sealed class PrototypeGameplayVfxSettings
    {
        private const int CurrentVersion = 2;
        private const string ShotPrefabGuid = "bf76576479b56ed419d4e2631595d5cd";
        private const string UnitCollisionPrefabGuid = "b0ce530911e46774bbca841e6306755c";
        private const string GateAddPrefabGuid = "bf76576479b56ed419d4e2631595d5cd";
        private const string GateMultiplyPrefabGuid = "b0ce530911e46774bbca841e6306755c";
        private const string StructureDamagePrefabGuid = "bf76576479b56ed419d4e2631595d5cd";

        [SerializeField] private int serializedVersion;

        [Serializable]
        public struct EffectEntry
        {
            [SerializeField] private GameObject prefab;
            [SerializeField, Min(0.01f)] private float scale;
            [SerializeField, Min(0f)] private float verticalOffset;
            [SerializeField, Min(0f)] private float cameraDepthOffset;
            [SerializeField] private bool faceCamera;
            [SerializeField] private bool keepUpright;
            [SerializeField] private Vector3 localEulerAngles;
            [SerializeField] private bool forceOneShot;

            public GameObject Prefab => prefab;
            public bool IsConfigured => prefab != null;
            public float SafeScale => Mathf.Max(0.01f, scale);
            public float VerticalOffset => Mathf.Max(0f, verticalOffset);
            public float CameraDepthOffset => Mathf.Max(0f, cameraDepthOffset);
            public bool FaceCamera => faceCamera;
            public bool KeepUpright => keepUpright;
            public Vector3 LocalEulerAngles => localEulerAngles;
            public bool ForceOneShot => forceOneShot;

            public static EffectEntry Create(
                float scale,
                float verticalOffset,
                float cameraDepthOffset,
                bool faceCamera,
                bool keepUpright,
                Vector3 localEulerAngles,
                bool forceOneShot)
            {
                return new EffectEntry
                {
                    scale = scale,
                    verticalOffset = verticalOffset,
                    cameraDepthOffset = cameraDepthOffset,
                    faceCamera = faceCamera,
                    keepUpright = keepUpright,
                    localEulerAngles = localEulerAngles,
                    forceOneShot = forceOneShot
                };
            }

            public void OnValidate()
            {
                scale = Mathf.Max(0.01f, scale);
                verticalOffset = Mathf.Max(0f, verticalOffset);
                cameraDepthOffset = Mathf.Max(0f, cameraDepthOffset);
            }

#if UNITY_EDITOR
            public void AutoAssignDefaultPrefab(string guid)
            {
                if (prefab != null || string.IsNullOrEmpty(guid))
                {
                    return;
                }

                string assetPath = AssetDatabase.GUIDToAssetPath(guid);
                if (string.IsNullOrEmpty(assetPath))
                {
                    return;
                }

                prefab = AssetDatabase.LoadAssetAtPath<GameObject>(assetPath);
            }
#endif
        }

        [Header("Shot")]
        [SerializeField] private EffectEntry shot = EffectEntry.Create(0.34f, 0f, 0f, false, true, Vector3.zero, true);

        [Header("Unit Collision")]
        [SerializeField] private EffectEntry unitCollision = EffectEntry.Create(0.72f, 0.52f, 0.12f, true, false, Vector3.zero, true);

        [Header("Gate Hit")]
        [SerializeField] private EffectEntry gateAdd = EffectEntry.Create(1.35f, 0f, 0f, true, true, Vector3.zero, true);
        [SerializeField] private EffectEntry gateMultiply = EffectEntry.Create(1.45f, 0f, 0f, true, true, Vector3.zero, true);

        [Header("Building Damage")]
        [SerializeField] private EffectEntry wallDamage = EffectEntry.Create(1.1f, 0f, 0f, true, true, Vector3.zero, true);
        [SerializeField] private EffectEntry castleDamage = EffectEntry.Create(1.22f, 0f, 0f, true, true, Vector3.zero, true);

        public EffectEntry Shot => shot;
        public EffectEntry UnitCollision => unitCollision;
        public EffectEntry GateAdd => gateAdd;
        public EffectEntry GateMultiply => gateMultiply;
        public EffectEntry WallDamage => wallDamage;
        public EffectEntry CastleDamage => castleDamage;

        public void OnValidate()
        {
            UpgradeIfNeeded();

            shot.OnValidate();
            unitCollision.OnValidate();
            gateAdd.OnValidate();
            gateMultiply.OnValidate();
            wallDamage.OnValidate();
            castleDamage.OnValidate();

#if UNITY_EDITOR
            shot.AutoAssignDefaultPrefab(ShotPrefabGuid);
            unitCollision.AutoAssignDefaultPrefab(UnitCollisionPrefabGuid);
            gateAdd.AutoAssignDefaultPrefab(GateAddPrefabGuid);
            gateMultiply.AutoAssignDefaultPrefab(GateMultiplyPrefabGuid);
            wallDamage.AutoAssignDefaultPrefab(StructureDamagePrefabGuid);
            castleDamage.AutoAssignDefaultPrefab(StructureDamagePrefabGuid);
#endif
        }

        private void UpgradeIfNeeded()
        {
            if (serializedVersion >= CurrentVersion)
            {
                return;
            }

            shot = EffectEntry.Create(0.34f, 0f, 0f, false, true, Vector3.zero, true);
            unitCollision = EffectEntry.Create(0.72f, 0.52f, 0.12f, true, false, Vector3.zero, true);
            gateAdd = EffectEntry.Create(1.35f, 0f, 0f, true, true, Vector3.zero, true);
            gateMultiply = EffectEntry.Create(1.45f, 0f, 0f, true, true, Vector3.zero, true);
            wallDamage = EffectEntry.Create(1.1f, 0f, 0f, true, true, Vector3.zero, true);
            castleDamage = EffectEntry.Create(1.22f, 0f, 0f, true, true, Vector3.zero, true);
            serializedVersion = CurrentVersion;
        }
    }
}
