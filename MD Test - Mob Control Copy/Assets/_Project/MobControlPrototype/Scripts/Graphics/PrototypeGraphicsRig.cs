using UnityEngine;

namespace MobControlPrototype.Graphics
{
    [DisallowMultipleComponent]
    public sealed class PrototypeGraphicsRig : MonoBehaviour
    {
        [Header("Scene References")]
        [SerializeField] private Camera targetCamera;
        [SerializeField] private Light mainDirectionalLight;
        [SerializeField] private GameObject pointLightsRoot;
        [SerializeField] private Material skyboxMaterial;
        [SerializeField] private Cubemap customReflection;

        public Camera TargetCamera => targetCamera;
        public Light MainDirectionalLight => mainDirectionalLight;
        public GameObject PointLightsRoot => pointLightsRoot;
        public Material SkyboxMaterial => skyboxMaterial;
        public Cubemap CustomReflection => customReflection;

        public void ConfigureScene(
            Camera camera,
            Light directionalLight,
            GameObject pointLights,
            Material skybox,
            Cubemap reflection)
        {
            targetCamera = camera;
            mainDirectionalLight = directionalLight;
            pointLightsRoot = pointLights;
            skyboxMaterial = skybox;
            customReflection = reflection;
        }
    }
}
