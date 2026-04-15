using System.Collections.Generic;
using MobControlPrototype.Bootstrap;
using MobControlPrototype.Crowd;
using MobControlPrototype.Gameplay;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;

namespace MobControlPrototype.Editor
{
    public static class PrototypeSceneBuilder
    {
        private const string ProjectRoot = "Assets/_Project/MobControlPrototype";
        private const string ScenePath = ProjectRoot + "/Scenes/VoodooStage01Skeleton.unity";
        private const string UnitPrefabPath = ProjectRoot + "/Prefabs/RunnerUnit.prefab";
        private const string RunnerModelPath = "Assets/Models/LowPoly_Stickaman_Running.fbx";
        private const string CannonModelPath = "Assets/Models/Cannon.fbx";
        private const string AutoBuildVersion = "stage04-simple-permanent-gates-ui-v2";

        [InitializeOnLoadMethod]
        private static void AutoRebuildOnceAfterScriptsReload()
        {
            EditorApplication.delayCall += () =>
            {
                string key = $"MobControlPrototype.{AutoBuildVersion}.{Application.dataPath}";
                if (EditorPrefs.GetString(key) == AutoBuildVersion && GeneratedAssetsAreReady())
                {
                    return;
                }

                TryAutoBuild(key);
            };
        }

        private static void TryAutoBuild(string editorPrefsKey)
        {
            if (EditorApplication.isCompiling || EditorApplication.isUpdating || EditorApplication.isPlayingOrWillChangePlaymode)
            {
                EditorApplication.delayCall += () => TryAutoBuild(editorPrefsKey);
                return;
            }

            Debug.Log("Auto rebuilding Mob Control Stage 01 with project FBX assets.");
            if (BuildStage01SkeletonInternal())
            {
                EditorPrefs.SetString(editorPrefsKey, AutoBuildVersion);
            }
        }

        [MenuItem("Tools/Mob Control Prototype/Rebuild Stage 01 Skeleton")]
        public static void BuildStage01Skeleton()
        {
            BuildStage01SkeletonInternal();
        }

        private static bool BuildStage01SkeletonInternal()
        {
            EnsureFolders();
            ConfigureRunnerModelImport();

            Material roadMaterial = CreateMaterial(ProjectRoot + "/Materials/Prototype_Road.mat", new Color(0.18f, 0.2f, 0.23f), 0.38f);
            Material groundMaterial = CreateMaterial(ProjectRoot + "/Materials/Prototype_Ground.mat", new Color(0.32f, 0.58f, 0.31f), 0.25f);
            Material railMaterial = CreateMaterial(ProjectRoot + "/Materials/Prototype_Rail.mat", new Color(0.92f, 0.86f, 0.28f), 0.34f);
            Material laneMaterial = CreateMaterial(ProjectRoot + "/Materials/Prototype_LaneMarker.mat", new Color(0.88f, 0.92f, 0.96f), 0.2f);
            Material unitMaterial = CreateMaterial(ProjectRoot + "/Materials/Prototype_RunnerBlue.mat", new Color(0.12f, 0.48f, 0.96f), 0.3f);
            Material cannonMaterial = CreateMaterial(ProjectRoot + "/Materials/Prototype_CannonDark.mat", new Color(0.04f, 0.05f, 0.06f), 0.42f);
            Material gateAddMaterial = CreateMaterial(ProjectRoot + "/Materials/Prototype_GateAdd.mat", new Color(0.11f, 0.72f, 0.34f), 0.24f);
            Material gateMultiplyMaterial = CreateMaterial(ProjectRoot + "/Materials/Prototype_GateMultiply.mat", new Color(0.08f, 0.42f, 0.92f), 0.24f);
            Material finishMaterial = CreateMaterial(ProjectRoot + "/Materials/Prototype_FinishTarget.mat", new Color(0.98f, 0.8f, 0.18f), 0.36f);

            GameObject runnerModel = AssetDatabase.LoadAssetAtPath<GameObject>(RunnerModelPath);
            AnimationClip runningClip = FindRunningClip();
            Avatar runnerAvatar = FindRunnerAvatar();
            GameObject cannonModel = AssetDatabase.LoadAssetAtPath<GameObject>(CannonModelPath);

            if (runnerModel == null || runningClip == null || cannonModel == null)
            {
                Debug.LogError($"Cannot build stage. Runner: {runnerModel != null}, Running clip: {runningClip != null}, Cannon: {cannonModel != null}");
                return false;
            }

            GameObject runnerPrefab = CreateRunnerPrefab(runnerModel, runningClip, runnerAvatar, unitMaterial);
            if (runnerPrefab == null)
            {
                Debug.LogError("Cannot build stage. RunnerUnit prefab was not created.");
                return false;
            }

            Scene originalActiveScene = SceneManager.GetActiveScene();
            Scene scene = SceneManager.GetSceneByPath(ScenePath);
            bool targetSceneWasOpen = scene.IsValid() && scene.isLoaded;
            bool closeSceneWhenDone = false;

            if (targetSceneWasOpen)
            {
                SceneManager.SetActiveScene(scene);
                ClearScene(scene);
            }
            else if (AssetDatabase.LoadAssetAtPath<SceneAsset>(ScenePath) != null)
            {
                scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Additive);
                closeSceneWhenDone = true;
                SceneManager.SetActiveScene(scene);
                ClearScene(scene);
            }
            else
            {
                scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
                scene.name = "VoodooStage01Skeleton";
                closeSceneWhenDone = true;
                SceneManager.SetActiveScene(scene);
            }

            SceneManager.SetActiveScene(scene);

            ConfigureRenderSettings();
            FinishTarget finishTarget;
            GameObject cannon = BuildEnvironment(
                roadMaterial,
                groundMaterial,
                railMaterial,
                laneMaterial,
                cannonMaterial,
                cannonModel,
                gateAddMaterial,
                gateMultiplyMaterial,
                finishMaterial,
                out finishTarget);

            UnitRunnerManager runnerManager = CreateRunnerManager();
            CannonShooter cannonShooter = ConfigureCannonShooter(cannon, runnerManager);
            EnsureMainCameraExists();
            CreateHud(runnerManager, finishTarget);
            CreateLighting();
            CreateBootstrapper(runnerManager, cannonShooter, runnerPrefab, runnerModel, runningClip, unitMaterial);

            bool sceneSaved = EditorSceneManager.SaveScene(scene, ScenePath);
            if (!sceneSaved)
            {
                Debug.LogError($"Cannot save Mob Control skeleton scene: {ScenePath}");
                return false;
            }

            AddSceneToBuildSettings(ScenePath);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            if (originalActiveScene.IsValid() && originalActiveScene != scene)
            {
                SceneManager.SetActiveScene(originalActiveScene);
            }

            if (closeSceneWhenDone)
            {
                EditorSceneManager.CloseScene(scene, true);
            }

            Debug.Log($"Mob Control skeleton scene generated: {ScenePath}");
            return true;
        }

        private static void ClearScene(Scene scene)
        {
            GameObject[] roots = scene.GetRootGameObjects();
            for (int i = 0; i < roots.Length; i++)
            {
                if (ShouldPreserveRoot(roots[i]))
                {
                    continue;
                }

                Object.DestroyImmediate(roots[i]);
            }
        }

        private static bool ShouldPreserveRoot(GameObject root)
        {
            return root.GetComponentInChildren<UnityEngine.Camera>(true) != null;
        }

        private static bool GeneratedAssetsAreReady()
        {
            return AssetDatabase.LoadAssetAtPath<GameObject>(UnitPrefabPath) != null
                && AssetDatabase.LoadAssetAtPath<SceneAsset>(ScenePath) != null;
        }

        private static void EnsureFolders()
        {
            EnsureFolder("Assets", "_Project");
            EnsureFolder("Assets/_Project", "MobControlPrototype");
            EnsureFolder(ProjectRoot, "Animation");
            EnsureFolder(ProjectRoot, "Materials");
            EnsureFolder(ProjectRoot, "Prefabs");
            EnsureFolder(ProjectRoot, "Scenes");
            EnsureFolder(ProjectRoot + "/Scripts", "Gameplay");
        }

        private static void EnsureFolder(string parent, string folderName)
        {
            string path = parent + "/" + folderName;
            if (!AssetDatabase.IsValidFolder(path))
            {
                AssetDatabase.CreateFolder(parent, folderName);
            }
        }

        private static Material CreateMaterial(string path, Color color, float smoothness)
        {
            Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
            Shader shader = Shader.Find("Standard") != null ? Shader.Find("Standard") : Shader.Find("Diffuse");

            if (material == null)
            {
                material = new Material(shader);
                AssetDatabase.CreateAsset(material, path);
            }

            material.shader = shader;
            material.color = color;

            if (material.HasProperty("_Glossiness"))
            {
                material.SetFloat("_Glossiness", smoothness);
            }

            if (material.HasProperty("_Metallic"))
            {
                material.SetFloat("_Metallic", 0f);
            }

            EditorUtility.SetDirty(material);
            return material;
        }

        private static void ConfigureRunnerModelImport()
        {
            ModelImporter importer = AssetImporter.GetAtPath(RunnerModelPath) as ModelImporter;
            if (importer == null)
            {
                return;
            }

            ModelImporterClipAnimation[] clips = importer.clipAnimations;
            if (clips == null || clips.Length == 0)
            {
                clips = importer.defaultClipAnimations;
            }

            bool changed = false;
            for (int i = 0; i < clips.Length; i++)
            {
                if (clips.Length > 1 && clips[i].name != "Running")
                {
                    continue;
                }

                if (!clips[i].loopTime || !clips[i].loopPose || !clips[i].keepOriginalPositionY || !clips[i].keepOriginalPositionXZ)
                {
                    clips[i].loopTime = true;
                    clips[i].loopPose = true;
                    clips[i].keepOriginalPositionY = true;
                    clips[i].keepOriginalPositionXZ = true;
                    changed = true;
                }
            }

            if (changed)
            {
                importer.clipAnimations = clips;
                importer.SaveAndReimport();
            }
        }

        private static AnimationClip FindRunningClip()
        {
            Object[] assets = AssetDatabase.LoadAllAssetsAtPath(RunnerModelPath);
            for (int i = 0; i < assets.Length; i++)
            {
                AnimationClip clip = assets[i] as AnimationClip;
                if (clip != null && clip.name == "Running")
                {
                    return clip;
                }
            }

            for (int i = 0; i < assets.Length; i++)
            {
                AnimationClip clip = assets[i] as AnimationClip;
                if (clip != null && !clip.name.StartsWith("__preview"))
                {
                    return clip;
                }
            }

            return null;
        }

        private static Avatar FindRunnerAvatar()
        {
            Object[] assets = AssetDatabase.LoadAllAssetsAtPath(RunnerModelPath);
            for (int i = 0; i < assets.Length; i++)
            {
                Avatar avatar = assets[i] as Avatar;
                if (avatar != null)
                {
                    return avatar;
                }
            }

            return null;
        }

        private static GameObject CreateRunnerPrefab(GameObject runnerModel, AnimationClip runningClip, Avatar runnerAvatar, Material unitMaterial)
        {
            GameObject root = new GameObject("RunnerUnit");
            GameObject visual = PrefabUtility.InstantiatePrefab(runnerModel) as GameObject;
            if (visual == null)
            {
                Debug.LogError($"Runner model cannot be instantiated as GameObject: {RunnerModelPath}");
                Object.DestroyImmediate(root);
                return null;
            }

            visual.name = "LowPoly_Stickaman_Running";
            visual.transform.SetParent(root.transform, false);
            visual.transform.localPosition = Vector3.zero;
            visual.transform.localRotation = Quaternion.identity;

            NormalizeHeight(root, visual.transform, 1.2f);
            AssignMaterial(root, unitMaterial);
            ConfigureRunnerAnimator(visual, runningClip, runnerAvatar);

            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, UnitPrefabPath);
            Object.DestroyImmediate(root);
            return prefab;
        }

        private static void ConfigureRunnerAnimator(GameObject visual, AnimationClip runningClip, Avatar runnerAvatar)
        {
            Animator animator = visual.GetComponent<Animator>();
            if (animator == null)
            {
                animator = visual.GetComponentInChildren<Animator>();
            }

            if (animator == null)
            {
                animator = visual.AddComponent<Animator>();
            }

            animator.runtimeAnimatorController = null;
            animator.applyRootMotion = false;
            animator.avatar = runnerAvatar;
            animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
            animator.updateMode = AnimatorUpdateMode.Normal;

            UnitAnimationPlayer animationPlayer = animator.GetComponent<UnitAnimationPlayer>();
            if (animationPlayer == null)
            {
                animationPlayer = animator.gameObject.AddComponent<UnitAnimationPlayer>();
            }

            SerializedObject serializedPlayer = new SerializedObject(animationPlayer);
            serializedPlayer.FindProperty("clip").objectReferenceValue = runningClip;
            serializedPlayer.FindProperty("playbackSpeed").floatValue = 1.12f;
            serializedPlayer.FindProperty("normalizedStartTime").floatValue = 0f;
            serializedPlayer.ApplyModifiedPropertiesWithoutUndo();
        }

        private static GameObject BuildEnvironment(
            Material roadMaterial,
            Material groundMaterial,
            Material railMaterial,
            Material laneMaterial,
            Material cannonMaterial,
            GameObject cannonModel,
            Material gateAddMaterial,
            Material gateMultiplyMaterial,
            Material finishMaterial,
            out FinishTarget finishTarget)
        {
            GameObject environment = new GameObject("Environment");

            CreateBox("Ground", new Vector3(0f, -0.12f, 29f), new Vector3(24f, 0.16f, 84f), groundMaterial, environment.transform);
            CreateBox("Road", new Vector3(0f, 0f, 29f), new Vector3(7.2f, 0.18f, 78f), roadMaterial, environment.transform);
            CreateBox("LeftRail", new Vector3(-3.9f, 0.18f, 29f), new Vector3(0.28f, 0.28f, 78f), railMaterial, environment.transform);
            CreateBox("RightRail", new Vector3(3.9f, 0.18f, 29f), new Vector3(0.28f, 0.28f, 78f), railMaterial, environment.transform);

            for (int i = 0; i < 16; i++)
            {
                CreateBox($"LaneMarker_{i + 1:00}", new Vector3(0f, 0.11f, -5f + i * 4.6f), new Vector3(0.16f, 0.04f, 1.7f), laneMaterial, environment.transform);
            }

            GameObject cannon = CreateStartCannon(environment.transform, cannonMaterial, cannonModel);
            CreateGate(environment.transform, "Gate_Add_4", 8.2f, GateOperation.Add, 4, gateAddMaterial);
            CreateGate(environment.transform, "Gate_Multiply_2", 17.6f, GateOperation.Multiply, 2, gateMultiplyMaterial);
            CreateGate(environment.transform, "Gate_Multiply_3", 36.4f, GateOperation.Multiply, 3, gateMultiplyMaterial);
            finishTarget = CreateFinishTarget(environment.transform, finishMaterial);
            return cannon;
        }

        private static GameObject CreateBox(string name, Vector3 position, Vector3 scale, Material material, Transform parent)
        {
            GameObject box = GameObject.CreatePrimitive(PrimitiveType.Cube);
            box.name = name;
            box.transform.SetParent(parent, false);
            box.transform.localPosition = position;
            box.transform.localScale = scale;

            Renderer renderer = box.GetComponent<Renderer>();
            if (renderer != null)
            {
                renderer.sharedMaterial = material;
            }

            Collider collider = box.GetComponent<Collider>();
            if (collider != null)
            {
                Object.DestroyImmediate(collider);
            }

            GameObjectUtility.SetStaticEditorFlags(box, StaticEditorFlags.BatchingStatic);
            return box;
        }

        private static GameObject CreateCylinder(string name, Vector3 position, Vector3 scale, Material material, Transform parent)
        {
            GameObject cylinder = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            cylinder.name = name;
            cylinder.transform.SetParent(parent, false);
            cylinder.transform.localPosition = position;
            cylinder.transform.localScale = scale;

            Renderer renderer = cylinder.GetComponent<Renderer>();
            if (renderer != null)
            {
                renderer.sharedMaterial = material;
            }

            Collider collider = cylinder.GetComponent<Collider>();
            if (collider != null)
            {
                Object.DestroyImmediate(collider);
            }

            GameObjectUtility.SetStaticEditorFlags(cylinder, StaticEditorFlags.BatchingStatic);
            return cylinder;
        }

        private static void CreateGate(Transform parent, string name, float zPosition, GateOperation operation, int value, Material material)
        {
            GameObject gate = GameObject.CreatePrimitive(PrimitiveType.Cube);
            gate.name = name;
            gate.transform.SetParent(parent, false);
            gate.transform.localPosition = new Vector3(0f, 0.1f, zPosition);
            gate.transform.localScale = new Vector3(6.4f, 2.2f, 0.35f);

            Renderer renderer = gate.GetComponent<Renderer>();
            if (renderer != null)
            {
                renderer.sharedMaterial = material;
            }

            BoxCollider trigger = gate.GetComponent<BoxCollider>();
            if (trigger == null)
            {
                trigger = gate.AddComponent<BoxCollider>();
            }

            trigger.isTrigger = true;
            trigger.center = new Vector3(0f, 0.38f, 0f);
            trigger.size = new Vector3(1.04f, 0.82f, 2.6f);

            GateModifier modifier = gate.AddComponent<GateModifier>();
            SerializedObject serializedGate = new SerializedObject(modifier);
            serializedGate.FindProperty("operation").enumValueIndex = (int)operation;
            serializedGate.FindProperty("value").intValue = value;
            serializedGate.ApplyModifiedPropertiesWithoutUndo();
        }

        private static FinishTarget CreateFinishTarget(Transform parent, Material material)
        {
            GameObject finish = new GameObject("FinalTarget");
            finish.transform.SetParent(parent, false);
            finish.transform.localPosition = new Vector3(0f, 0.1f, 58.5f);

            BoxCollider trigger = finish.AddComponent<BoxCollider>();
            trigger.isTrigger = true;
            trigger.center = new Vector3(0f, 1.3f, 0f);
            trigger.size = new Vector3(6.8f, 2.6f, 1.3f);

            GameObject baseObject = CreateCylinder("TowerBase", new Vector3(0f, 0.35f, 0f), new Vector3(1.9f, 0.35f, 1.9f), material, finish.transform);
            GameObject tower = CreateCylinder("Tower", new Vector3(0f, 1.35f, 0f), new Vector3(1.15f, 1.2f, 1.15f), material, finish.transform);
            GameObject cap = CreateBox("TowerCap", new Vector3(0f, 2.65f, 0f), new Vector3(2.4f, 0.35f, 2.4f), material, finish.transform);

            FinishTarget target = finish.AddComponent<FinishTarget>();
            SerializedObject serializedTarget = new SerializedObject(target);
            serializedTarget.FindProperty("health").intValue = 20;
            serializedTarget.FindProperty("damagePerUnit").intValue = 1;

            SerializedProperty renderers = serializedTarget.FindProperty("feedbackRenderers");
            renderers.arraySize = 3;
            renderers.GetArrayElementAtIndex(0).objectReferenceValue = baseObject.GetComponent<Renderer>();
            renderers.GetArrayElementAtIndex(1).objectReferenceValue = tower.GetComponent<Renderer>();
            renderers.GetArrayElementAtIndex(2).objectReferenceValue = cap.GetComponent<Renderer>();
            serializedTarget.ApplyModifiedPropertiesWithoutUndo();
            return target;
        }

        private static GameObject CreateStartCannon(Transform parent, Material cannonMaterial, GameObject cannonModel)
        {
            GameObject cannon = PrefabUtility.InstantiatePrefab(cannonModel) as GameObject;
            if (cannon == null)
            {
                Debug.LogError($"Cannon model cannot be instantiated as GameObject: {CannonModelPath}");
                return null;
            }

            cannon.name = "StartCannon";
            cannon.transform.SetParent(parent, false);
            cannon.transform.position = new Vector3(0f, 0.14f, -6.2f);
            cannon.transform.rotation = Quaternion.Euler(0f, 0f, 0f);

            NormalizeHeight(cannon, cannon.transform, 1.28f);
            GroundObject(cannon);
            AssignMaterial(cannon, cannonMaterial);
            return cannon;
        }

        private static UnitRunnerManager CreateRunnerManager()
        {
            GameObject managerObject = new GameObject("UnitRunnerManager");
            UnitRunnerManager runnerManager = managerObject.AddComponent<UnitRunnerManager>();

            SerializedObject serializedManager = new SerializedObject(runnerManager);
            serializedManager.FindProperty("maxActiveUnits").intValue = 160;
            serializedManager.FindProperty("runnerColliderRadius").floatValue = 0.28f;
            serializedManager.FindProperty("runnerColliderHeight").floatValue = 1.25f;
            serializedManager.FindProperty("fallbackMoveSpeed").floatValue = 5.4f;
            serializedManager.FindProperty("despawnZ").floatValue = 72f;
            serializedManager.FindProperty("cloneSpacing").floatValue = 0.42f;
            serializedManager.FindProperty("cloneForwardOffset").floatValue = 0.85f;
            serializedManager.ApplyModifiedPropertiesWithoutUndo();

            return runnerManager;
        }

        private static CannonShooter ConfigureCannonShooter(GameObject cannon, UnitRunnerManager runnerManager)
        {
            if (cannon == null)
            {
                cannon = new GameObject("StartCannon");
                cannon.transform.position = new Vector3(0f, 0.14f, -6.2f);
            }

            GameObject muzzle = new GameObject("Muzzle");
            muzzle.transform.SetParent(cannon.transform, false);
            muzzle.transform.localPosition = new Vector3(0f, 0f, 1.05f);

            CannonShooter shooter = cannon.AddComponent<CannonShooter>();
            SerializedObject serializedShooter = new SerializedObject(shooter);
            serializedShooter.FindProperty("muzzle").objectReferenceValue = muzzle.transform;
            serializedShooter.FindProperty("runnerManager").objectReferenceValue = runnerManager;
            serializedShooter.FindProperty("recoilRoot").objectReferenceValue = cannon.transform;
            serializedShooter.FindProperty("horizontalSpeed").floatValue = 5f;
            serializedShooter.FindProperty("minX").floatValue = -2.7f;
            serializedShooter.FindProperty("maxX").floatValue = 2.7f;
            serializedShooter.FindProperty("followMouseWithoutClick").boolValue = true;
            serializedShooter.FindProperty("mouseFollowSharpness").floatValue = 16f;
            serializedShooter.FindProperty("shotsPerSecond").floatValue = 4f;
            serializedShooter.FindProperty("keyboardFireKey").enumValueIndex = (int)KeyCode.Space;
            serializedShooter.FindProperty("runnerSpawnOffset").vector3Value = new Vector3(0f, 0.12f, 0.75f);
            serializedShooter.ApplyModifiedPropertiesWithoutUndo();
            return shooter;
        }

        private static void CreateHud(UnitRunnerManager runnerManager, FinishTarget finishTarget)
        {
            GameObject canvasObject = new GameObject("HUDCanvas");
            Canvas canvas = canvasObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.pixelPerfect = true;
            canvasObject.AddComponent<GraphicRaycaster>();

            CanvasScaler scaler = canvasObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;
            scaler.referencePixelsPerUnit = 100f;

            Font font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

            GameObject barObject = new GameObject("TopBar");
            barObject.transform.SetParent(canvasObject.transform, false);
            RectTransform barRect = barObject.AddComponent<RectTransform>();
            barRect.anchorMin = new Vector2(0f, 1f);
            barRect.anchorMax = new Vector2(1f, 1f);
            barRect.pivot = new Vector2(0.5f, 1f);
            barRect.sizeDelta = new Vector2(0f, 112f);

            Image barImage = barObject.AddComponent<Image>();
            barImage.color = new Color(0.04f, 0.05f, 0.07f, 0.78f);

            RectTransform canvasRect = canvasObject.GetComponent<RectTransform>();
            Text unitsLabel = CreateHudText("UnitsLabel", barRect, font, "Units 0", 46, TextAnchor.MiddleLeft, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(40f, -56f), new Vector2(420f, 80f));
            Text castleLabel = CreateHudText("CastleLabel", barRect, font, "Castle 20/20", 46, TextAnchor.MiddleRight, new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-40f, -56f), new Vector2(420f, 80f));
            Text stateLabel = CreateHudText("StateLabel", barRect, font, string.Empty, 42, TextAnchor.MiddleCenter, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -56f), new Vector2(620f, 80f));
            CreateHudText("HintLabel", canvasRect, font, "Move: A/D or Mouse   Fire: Hold Space or LMB", 32, TextAnchor.LowerCenter, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 36f), new Vector2(960f, 60f));

            PrototypeHud hud = canvasObject.AddComponent<PrototypeHud>();
            SerializedObject serializedHud = new SerializedObject(hud);
            serializedHud.FindProperty("runnerManager").objectReferenceValue = runnerManager;
            serializedHud.FindProperty("finishTarget").objectReferenceValue = finishTarget;
            serializedHud.FindProperty("unitsLabel").objectReferenceValue = unitsLabel;
            serializedHud.FindProperty("castleLabel").objectReferenceValue = castleLabel;
            serializedHud.FindProperty("stateLabel").objectReferenceValue = stateLabel;
            serializedHud.ApplyModifiedPropertiesWithoutUndo();
        }

        private static Text CreateHudText(
            string name,
            RectTransform parent,
            Font font,
            string text,
            int fontSize,
            TextAnchor alignment,
            Vector2 anchorMin,
            Vector2 anchorMax,
            Vector2 anchoredPosition,
            Vector2 sizeDelta)
        {
            GameObject textObject = new GameObject(name);
            textObject.transform.SetParent(parent, false);

            RectTransform rect = textObject.AddComponent<RectTransform>();
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.pivot = new Vector2((anchorMin.x + anchorMax.x) * 0.5f, (anchorMin.y + anchorMax.y) * 0.5f);
            rect.anchoredPosition = anchoredPosition;
            rect.sizeDelta = sizeDelta;

            Text uiText = textObject.AddComponent<Text>();
            uiText.font = font;
            uiText.text = text;
            uiText.fontSize = fontSize;
            uiText.alignment = alignment;
            uiText.color = Color.white;
            uiText.horizontalOverflow = HorizontalWrapMode.Overflow;
            uiText.verticalOverflow = VerticalWrapMode.Overflow;

            Outline outline = textObject.AddComponent<Outline>();
            outline.effectColor = new Color(0f, 0f, 0f, 0.92f);
            outline.effectDistance = new Vector2(2f, -2f);

            return uiText;
        }

        private static void EnsureMainCameraExists()
        {
            if (Object.FindObjectOfType<UnityEngine.Camera>() != null)
            {
                return;
            }

            GameObject cameraObject = new GameObject("Main Camera");
            cameraObject.tag = "MainCamera";
            cameraObject.transform.SetPositionAndRotation(new Vector3(0f, 7.8f, -12.2f), Quaternion.Euler(57f, 0f, 0f));

            UnityEngine.Camera camera = cameraObject.AddComponent<UnityEngine.Camera>();
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.62f, 0.78f, 0.92f);
            camera.fieldOfView = 54f;
            camera.nearClipPlane = 0.1f;
            camera.farClipPlane = 120f;

            cameraObject.AddComponent<AudioListener>();
        }

        private static void CreateLighting()
        {
            GameObject lightObject = new GameObject("Sun Light");
            lightObject.transform.rotation = Quaternion.Euler(48f, -32f, 0f);

            Light light = lightObject.AddComponent<Light>();
            light.type = LightType.Directional;
            light.intensity = 1.18f;
            light.color = new Color(1f, 0.94f, 0.84f);
            light.shadows = LightShadows.Soft;
        }

        private static void CreateBootstrapper(
            UnitRunnerManager runnerManager,
            CannonShooter cannonShooter,
            GameObject runnerPrefab,
            GameObject runnerModel,
            AnimationClip runningClip,
            Material fallbackUnitMaterial)
        {
            GameObject bootstrapObject = new GameObject("PrototypeBootstrapper");
            PrototypeBootstrapper bootstrapper = bootstrapObject.AddComponent<PrototypeBootstrapper>();

            SerializedObject serializedBootstrapper = new SerializedObject(bootstrapper);
            serializedBootstrapper.FindProperty("runnerManager").objectReferenceValue = runnerManager;
            serializedBootstrapper.FindProperty("cannonShooter").objectReferenceValue = cannonShooter;
            serializedBootstrapper.FindProperty("unitPrefab").objectReferenceValue = runnerPrefab;
            serializedBootstrapper.FindProperty("unitModelPrefab").objectReferenceValue = runnerModel;
            serializedBootstrapper.FindProperty("runningClip").objectReferenceValue = runningClip;
            serializedBootstrapper.FindProperty("fallbackUnitMaterial").objectReferenceValue = fallbackUnitMaterial;
            serializedBootstrapper.FindProperty("forwardSpeed").floatValue = 5.4f;
            serializedBootstrapper.FindProperty("runningAnimationSpeed").floatValue = 1.12f;
            serializedBootstrapper.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void ConfigureRenderSettings()
        {
            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.58f, 0.62f, 0.68f);
            RenderSettings.fog = false;
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

        private static void NormalizeHeight(GameObject boundsRoot, Transform scaleRoot, float targetHeight)
        {
            if (!TryGetBounds(boundsRoot, out Bounds bounds) || bounds.size.y <= 0.001f)
            {
                return;
            }

            float scale = targetHeight / bounds.size.y;
            scaleRoot.localScale *= scale;
            GroundObject(boundsRoot);
            CenterObject(boundsRoot, scaleRoot);
        }

        private static void GroundObject(GameObject root)
        {
            if (!TryGetBounds(root, out Bounds bounds))
            {
                return;
            }

            root.transform.position -= new Vector3(0f, bounds.min.y, 0f);
        }

        private static void CenterObject(GameObject boundsRoot, Transform moveRoot)
        {
            if (!TryGetBounds(boundsRoot, out Bounds bounds))
            {
                return;
            }

            moveRoot.position -= new Vector3(bounds.center.x, 0f, bounds.center.z);
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

        private static void AddSceneToBuildSettings(string scenePath)
        {
            List<EditorBuildSettingsScene> scenes = new List<EditorBuildSettingsScene>(EditorBuildSettings.scenes);
            for (int i = 0; i < scenes.Count; i++)
            {
                if (scenes[i].path == scenePath)
                {
                    scenes[i] = new EditorBuildSettingsScene(scenePath, true);
                    EditorBuildSettings.scenes = scenes.ToArray();
                    return;
                }
            }

            scenes.Add(new EditorBuildSettingsScene(scenePath, true));
            EditorBuildSettings.scenes = scenes.ToArray();
        }
    }
}
