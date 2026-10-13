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
    /// <see cref="ScenePath"/> (a scene holding only a <see cref="GameBootstrap"/>), adds it to the
    /// build settings and opens it, so pressing Play starts the game. Also adds an
    /// "FRC 6874" menu with the same actions.
    /// </summary>
    [InitializeOnLoad]
    public static class FRC6874DemoSetup
    {
        public const string ScenePath = "Assets/FRC6874/Scenes/FRC6874_Demo.unity";

        static FRC6874DemoSetup()
        {
            EditorApplication.delayCall += AutoSetup;
        }

        static void AutoSetup()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) return;

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
            var root = new GameObject("FRC 6874 Game");
            root.AddComponent<GameBootstrap>();

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
