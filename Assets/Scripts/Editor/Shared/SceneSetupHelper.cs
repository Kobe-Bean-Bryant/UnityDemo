using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace UnityDemo.Editor
{
    public enum RenderingMode
    {
        Mode3D = 0,
        Mode2D = 1,
    }

    public static class SceneSetupHelper
    {
        private const string Renderer2DAssetPath = "Assets/Settings/Renderer_2D.asset";

        private static readonly string[] PipelineAssetPaths =
        {
            "Assets/Settings/PC_RPAsset.asset",
            "Assets/Settings/Mobile_RPAsset.asset",
        };

        public static void CreateScene(string path, RenderingMode mode)
        {
            int renderer2DIndex = -1;
            if (mode == RenderingMode.Mode2D)
                renderer2DIndex = EnsureRenderer2DSetup();

            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);

            if (mode == RenderingMode.Mode2D)
                Setup2DScene(renderer2DIndex);
            else
                Setup3DScene();

            EditorSceneManager.SaveScene(scene, path);
            EditorSceneManager.CloseScene(scene, true);
        }

        private static void Setup3DScene()
        {
            var cameraGo = new GameObject("Main Camera");
            cameraGo.tag = "MainCamera";
            var camera = cameraGo.AddComponent<Camera>();
            camera.clearFlags = CameraClearFlags.Skybox;
            camera.orthographic = false;
            cameraGo.transform.position = new Vector3(0, 1, -10);
            cameraGo.AddComponent<AudioListener>();

            var lightGo = new GameObject("Directional Light");
            var light = lightGo.AddComponent<Light>();
            light.type = LightType.Directional;
            light.color = new Color(1f, 0.956f, 0.839f);
            light.intensity = 1f;
            lightGo.transform.rotation = Quaternion.Euler(50f, -30f, 0f);
        }

        private static void Setup2DScene(int rendererIndex)
        {
            var cameraGo = new GameObject("Main Camera");
            cameraGo.tag = "MainCamera";
            var camera = cameraGo.AddComponent<Camera>();
            camera.orthographic = true;
            camera.orthographicSize = 5f;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.114f, 0.114f, 0.114f, 1f);
            cameraGo.transform.position = new Vector3(0, 0, -10);
            cameraGo.AddComponent<AudioListener>();

            var cameraData = cameraGo.GetComponent<UniversalAdditionalCameraData>();
            if (cameraData == null)
                cameraData = cameraGo.AddComponent<UniversalAdditionalCameraData>();
            cameraData.SetRenderer(rendererIndex);

            var lightGo = new GameObject("Global Light 2D");
            var light2D = lightGo.AddComponent<Light2D>();
            light2D.lightType = Light2D.LightType.Global;
        }

        private static int EnsureRenderer2DSetup()
        {
            var renderer2D = AssetDatabase.LoadAssetAtPath<Renderer2DData>(Renderer2DAssetPath);
            if (renderer2D == null)
            {
                renderer2D = ScriptableObject.CreateInstance<Renderer2DData>();
                AssetDatabase.CreateAsset(renderer2D, Renderer2DAssetPath);
                AssetDatabase.SaveAssets();
            }

            int resolvedIndex = -1;
            foreach (string pipelinePath in PipelineAssetPaths)
            {
                int idx = RegisterRenderer2DInPipelineAsset(pipelinePath, renderer2D);
                if (resolvedIndex < 0 && idx >= 0)
                    resolvedIndex = idx;
            }

            AssetDatabase.SaveAssets();
            return resolvedIndex;
        }

        private static int RegisterRenderer2DInPipelineAsset(string pipelineAssetPath, Renderer2DData renderer2D)
        {
            var pipelineAsset = AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>(pipelineAssetPath);
            if (pipelineAsset == null)
            {
                Debug.LogWarning($"[SceneSetupHelper] URP Pipeline Asset not found: {pipelineAssetPath}");
                return -1;
            }

            var so = new SerializedObject(pipelineAsset);
            var rendererListProp = so.FindProperty("m_RendererDataList");

            for (int i = 0; i < rendererListProp.arraySize; i++)
            {
                if (rendererListProp.GetArrayElementAtIndex(i).objectReferenceValue == renderer2D)
                    return i;
            }

            int newIndex = rendererListProp.arraySize;
            rendererListProp.arraySize = newIndex + 1;
            rendererListProp.GetArrayElementAtIndex(newIndex).objectReferenceValue = renderer2D;
            so.ApplyModifiedProperties();
            EditorUtility.SetDirty(pipelineAsset);
            return newIndex;
        }
    }
}
