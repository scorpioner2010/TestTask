using UnityEngine;
using UnityEngine.Rendering;

namespace MobControlPrototype.Graphics
{
    [ExecuteAlways]
    public sealed class PrototypeGraphicsRig : MonoBehaviour
    {
        [Header("Scene References")]
        [SerializeField] private Camera targetCamera;
        [SerializeField] private Light mainDirectionalLight;
        [SerializeField] private GameObject pointLightsRoot;
        [SerializeField] private Material skyboxMaterial;
        [SerializeField] private Cubemap customReflection;

        [Header("Render Settings")]
        [SerializeField] private bool fogEnabled;
        [SerializeField] private AmbientMode ambientMode = AmbientMode.Skybox;
        [SerializeField] private Color ambientSkyColor = new Color(0.19607843f, 0.19607843f, 0.19607843f, 1f);
        [SerializeField] private Color ambientEquatorColor = new Color(0.19607843f, 0.19607843f, 0.19607843f, 1f);
        [SerializeField] private Color ambientGroundColor = new Color(0.19607843f, 0.19607843f, 0.19607843f, 1f);
        [SerializeField] private float ambientIntensity = 1f;
        [SerializeField] private DefaultReflectionMode reflectionMode = DefaultReflectionMode.Custom;
        [SerializeField] private int reflectionResolution = 256;
        [SerializeField] private int reflectionBounces = 1;
        [SerializeField] private float reflectionIntensity = 1f;

        [Header("Camera")]
        [SerializeField] private CameraClearFlags cameraClearFlags = CameraClearFlags.Skybox;
        [SerializeField] private Color cameraBackground = Color.black;
        [SerializeField] private float cameraFieldOfView = 58f;
        [SerializeField] private float cameraNearClipPlane = 0.1f;
        [SerializeField] private float cameraFarClipPlane = 120f;
        [SerializeField] private RenderingPath cameraRenderingPath = RenderingPath.UsePlayerSettings;
        [SerializeField] private bool cameraAllowHdr = true;
        [SerializeField] private bool cameraAllowMsaa = true;
        [SerializeField] private bool cameraUseOcclusionCulling = true;

        [Header("Directional Light")]
        [SerializeField] private Vector3 directionalLightEuler = new Vector3(37f, 260f, 0f);
        [SerializeField] private Color directionalLightColor = new Color(0.76470596f, 0.76470596f, 0.76470596f, 1f);
        [SerializeField] private float directionalLightIntensity = 1f;
        [SerializeField] private LightShadows directionalLightShadows = LightShadows.Soft;

        [Header("Point Lights")]
        [SerializeField] private bool enablePointLights;
        [SerializeField] private bool rotatePointLights;
        [SerializeField] private float pointLightRotationSpeed = 20f;

        public void ConfigureScene(Camera camera, Light directionalLight, GameObject pointLights, Material skybox, Cubemap reflection)
        {
            targetCamera = camera;
            mainDirectionalLight = directionalLight;
            pointLightsRoot = pointLights;
            skyboxMaterial = skybox;
            customReflection = reflection;
            Apply();
        }

        private void OnEnable()
        {
            Apply();
        }

        private void OnValidate()
        {
            Apply();
        }

        private void Update()
        {
            if (!Application.isPlaying || !rotatePointLights || !enablePointLights || pointLightsRoot == null)
            {
                return;
            }

            pointLightsRoot.transform.Rotate(Vector3.up, pointLightRotationSpeed * Time.deltaTime, Space.Self);
        }

        [ContextMenu("Apply Graphics Preset")]
        public void Apply()
        {
            if (!gameObject.scene.IsValid())
            {
                return;
            }

            ApplyRenderSettings();
            ApplyCameraSettings();
            ApplyDirectionalLightSettings();
            ApplyPointLightSettings();
        }

        private void ApplyRenderSettings()
        {
            RenderSettings.fog = fogEnabled;
            RenderSettings.ambientMode = ambientMode;
            RenderSettings.ambientSkyColor = ambientSkyColor;
            RenderSettings.ambientEquatorColor = ambientEquatorColor;
            RenderSettings.ambientGroundColor = ambientGroundColor;
            RenderSettings.ambientIntensity = ambientIntensity;
            RenderSettings.defaultReflectionMode = reflectionMode;
            RenderSettings.defaultReflectionResolution = reflectionResolution;
            RenderSettings.reflectionBounces = reflectionBounces;
            RenderSettings.reflectionIntensity = reflectionIntensity;
            RenderSettings.skybox = skyboxMaterial;
            RenderSettings.customReflection = ResolveReflectionCubemap();
            RenderSettings.sun = mainDirectionalLight;

            DynamicGI.UpdateEnvironment();
        }

        private void ApplyCameraSettings()
        {
            if (targetCamera == null)
            {
                return;
            }

            targetCamera.clearFlags = cameraClearFlags;
            targetCamera.backgroundColor = cameraBackground;
            targetCamera.fieldOfView = cameraFieldOfView;
            targetCamera.nearClipPlane = cameraNearClipPlane;
            targetCamera.farClipPlane = cameraFarClipPlane;
            targetCamera.renderingPath = cameraRenderingPath;
            targetCamera.allowHDR = cameraAllowHdr;
            targetCamera.allowMSAA = cameraAllowMsaa;
            targetCamera.useOcclusionCulling = cameraUseOcclusionCulling;
        }

        private void ApplyDirectionalLightSettings()
        {
            if (mainDirectionalLight == null)
            {
                return;
            }

            mainDirectionalLight.type = LightType.Directional;
            mainDirectionalLight.color = directionalLightColor;
            mainDirectionalLight.intensity = directionalLightIntensity;
            mainDirectionalLight.shadows = directionalLightShadows;
            mainDirectionalLight.transform.rotation = Quaternion.Euler(directionalLightEuler);
        }

        private void ApplyPointLightSettings()
        {
            if (pointLightsRoot == null)
            {
                return;
            }

            pointLightsRoot.SetActive(enablePointLights);
        }

        private Cubemap ResolveReflectionCubemap()
        {
            if (customReflection != null)
            {
                return customReflection;
            }

            if (skyboxMaterial == null || !skyboxMaterial.HasProperty("_Tex"))
            {
                return null;
            }

            return skyboxMaterial.GetTexture("_Tex") as Cubemap;
        }
    }
}
