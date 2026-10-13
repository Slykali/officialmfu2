using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace FRC6874.EditorTools
{
    /// <summary>
    /// One-click setup for the demo. The first time the project is opened it creates
    /// <see cref="ScenePath"/> (a scene holding only a <see cref="GameBootstrap"/>), generates the
    /// emissive template material builds need, adds the scene to the build settings and opens it,
    /// so pressing Play starts the game. Also adds an "FRC 6874" menu with the same actions.
    /// </summary>
    [InitializeOnLoad]
    public static class FRC6874DemoSetup
    {
        public const string ScenePath = "Assets/FRC6874/Scenes/FRC6874_Demo.unity";
        public const string EmissiveTemplatePath =
            "Assets/FRC6874/Resources/" + MaterialFactory.EmissiveTemplateResource + ".mat";
        const string AutoSetupDoneKey = "FRC6874.AutoSetupDone";

        static FRC6874DemoSetup()
        {
            // Once per editor session, not after every script recompile.
            if (SessionState.GetBool(AutoSetupDoneKey, false)) return;
            EditorApplication.delayCall += AutoSetup;
        }

        static void AutoSetup()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) return;
            SessionState.SetBool(AutoSetupDoneKey, true);

            if (!File.Exists(ScenePath))
            {
                CreateScene();
                return;
            }

            EnsureInBuildSettings();

            // Open the demo on startup when the editor would otherwise show an empty untitled scene.
            Scene active = SceneManager.GetActiveScene();
            if (string.IsNullOrEmpty(active.path) && !active.isDirty && SceneManager.sceneCount == 1)
                EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        }

        [MenuItem("FRC 6874/Open Demo Scene", priority = 0)]
        public static void OpenScene()
        {
            if (!File.Exists(ScenePath))
            {
                CreateScene();
                return;
            }
            if (EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
                EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        }

        [MenuItem("FRC 6874/Play Demo", priority = 1)]
        public static void PlayDemo()
        {
            OpenScene();
            if (SceneManager.GetActiveScene().path == ScenePath) EditorApplication.isPlaying = true;
        }

        [MenuItem("FRC 6874/Rebuild Demo Scene", priority = 20)]
        public static void CreateScene()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;

            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            CreateEmissiveTemplate();   // needs a scene for its probe primitive; this one is about to be saved anyway

            var root = new GameObject("FRC 6874 Game");
            root.AddComponent<GameBootstrap>();

            // Builds strip fog shader variants unless a scene in the build uses fog. The bootstrap
            // configures the real values at runtime.
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.Linear;
            RenderSettings.fogColor = GameBootstrap.BackgroundColor;
            RenderSettings.fogStartDistance = 28f;
            RenderSettings.fogEndDistance = 95f;

            Directory.CreateDirectory(Path.GetDirectoryName(ScenePath));
            if (EditorSceneManager.SaveScene(scene, ScenePath))
            {
                AssetDatabase.Refresh();
                EnsureInBuildSettings();
                Debug.Log($"[FRC 6874] Created {ScenePath}. Press Play to start the demo.");
            }
            else
            {
                Debug.LogError($"[FRC 6874] Could not save {ScenePath}.");
            }
        }

        /// <summary>
        /// Saves a copy of the active pipeline's default lit material with emission enabled into a
        /// Resources folder, so player builds keep the emission shader variant that
        /// <see cref="MaterialFactory"/> uses for LEDs and light bars. Rebuilt with the scene, e.g.
        /// after switching render pipelines.
        /// </summary>
        static void CreateEmissiveTemplate()
        {
            GameObject probe = GameObject.CreatePrimitive(PrimitiveType.Cube);
            Material source = probe.GetComponent<Renderer>().sharedMaterial;
            Material material = source != null ? new Material(source) : null;
            Object.DestroyImmediate(probe);
            if (material == null) return;

            material.name = MaterialFactory.EmissiveTemplateResource;
            material.EnableKeyword("_EMISSION");
            material.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
            if (material.HasProperty("_EmissionColor")) material.SetColor("_EmissionColor", Color.white);

            Directory.CreateDirectory(Path.GetDirectoryName(EmissiveTemplatePath));
            if (File.Exists(EmissiveTemplatePath)) AssetDatabase.DeleteAsset(EmissiveTemplatePath);
            AssetDatabase.CreateAsset(material, EmissiveTemplatePath);
            AssetDatabase.SaveAssets();
        }

        static void EnsureInBuildSettings()
        {
            EditorBuildSettingsScene[] scenes = EditorBuildSettings.scenes;
            if (scenes.Any(s => s.path == ScenePath)) return;
            EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(ScenePath, true) }
                .Concat(scenes)
                .ToArray();
        }
    }
}
