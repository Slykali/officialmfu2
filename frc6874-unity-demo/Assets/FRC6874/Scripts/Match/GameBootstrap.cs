using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

namespace FRC6874
{
    /// <summary>
    /// The single entry point of the game. Put it on one GameObject in an otherwise empty scene
    /// (or press Play in an empty scene: <see cref="AutoBootstrap"/> creates it). On Awake it sets
    /// up the frame rate, physics step, camera, lights and arena look, then builds the field, the
    /// match manager (which spawns the robots), the camera rig, the HUD and the synthesized audio,
    /// and finally opens the main menu with the attract-mode match running behind it.
    /// </summary>
    [DefaultExecutionOrder(-100)]
    public class GameBootstrap : MonoBehaviour
    {
        /// <summary>The running bootstrap, or null before startup.</summary>
        public static GameBootstrap Instance { get; private set; }

        /// <summary>Background color the camera clears to (dark arena navy).</summary>
        public static readonly Color BackgroundColor = new Color(0.035f, 0.045f, 0.07f);

        public GameObject Field { get; private set; }
        public MatchManager Match { get; private set; }
        public CameraRig CameraRig { get; private set; }
        public HUD Hud { get; private set; }
        public GameAudioSynth AudioSynth { get; private set; }
        public Camera MainCamera { get; private set; }
        public Light MainLight { get; private set; }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            // Supports "Enter Play Mode Options" with domain reload disabled.
            Instance = null;
        }

        /// <summary>
        /// Pressing Play in a brand-new scene just works: if the loaded scene has no bootstrap and
        /// contains nothing but cameras, lights (and URP's global volume), create one.
        /// </summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void AutoBootstrap()
        {
            if (Instance != null) return;
            Scene scene = SceneManager.GetActiveScene();
            if (!scene.IsValid() || !IsEmptyScene(scene)) return;
            new GameObject("FRC6874 Game").AddComponent<GameBootstrap>();
        }

        void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Debug.LogWarning("[FRC6874] A second GameBootstrap was found and disabled.", this);
                Destroy(this);
                return;
            }
            Instance = this;

            // The field is built under this object in Arena's world coordinates, so keep it at the origin.
            if (transform.parent == null)
            {
                transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
                transform.localScale = Vector3.one;
            }

            if (Application.platform != RuntimePlatform.WebGLPlayer)
                Application.targetFrameRate = 60;   // WebGL should keep the browser's own frame pacing
            Time.fixedDeltaTime = 0.01f;            // 100 Hz physics: smoother, more reliable ball contacts
            Time.timeScale = 1f;
            AudioListener.pause = false;

            List<GameObject> roots = SceneRoots();
            SetupEnvironment();
            MainCamera = SetupCamera(roots);
            SetupAudioListener(roots, MainCamera);
            MainLight = SetupLights(roots, transform);

            BuildGame();
        }

        void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        void BuildGame()
        {
            Transform root = transform;

            try
            {
                Field = FieldBuilder.Build(root);
            }
            catch (System.Exception e)
            {
                Debug.LogException(e);
            }

            var matchObject = new GameObject("Match");
            matchObject.transform.SetParent(root, false);
            Match = matchObject.AddComponent<MatchManager>();
            Match.Initialize(root);

            CameraRig = MainCamera.GetComponent<CameraRig>();
            if (CameraRig == null) CameraRig = MainCamera.gameObject.AddComponent<CameraRig>();

            Hud = gameObject.AddComponent<HUD>();

            var audioObject = new GameObject("Audio");
            audioObject.transform.SetParent(root, false);
            AudioSynth = audioObject.AddComponent<GameAudioSynth>();

            Hud.Initialize(Match, CameraRig, AudioSynth);
            Match.ReturnToMenu();
            CameraRig.SnapToTarget();
        }

        // ---- Environment -----------------------------------------------------------------

        static void SetupEnvironment()
        {
            // Indoor arena: neutral, moderately bright ambient so the field never looks muddy.
            Color sky = new Color(0.50f, 0.53f, 0.60f);
            Color equator = new Color(0.36f, 0.37f, 0.42f);
            Color ground = new Color(0.20f, 0.20f, 0.23f);
            RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = sky;
            RenderSettings.ambientEquatorColor = equator;
            RenderSettings.ambientGroundColor = ground;
            RenderSettings.ambientIntensity = 1f;

            // Set the ambient probe explicitly too: URP reads ambient light from it, and it is not
            // always refreshed when the colors change at runtime.
            var probe = new SphericalHarmonicsL2();
            probe.AddAmbientLight(new Color(0.40f, 0.42f, 0.47f));
            RenderSettings.ambientProbe = probe;
            RenderSettings.reflectionIntensity = 0.5f;

            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.Linear;
            RenderSettings.fogColor = BackgroundColor;
            RenderSettings.fogStartDistance = 28f;
            RenderSettings.fogEndDistance = 95f;

            QualitySettings.shadowDistance = 45f;   // Built-in pipeline; URP takes this from its asset
        }

        static Camera SetupCamera(List<GameObject> roots)
        {
            Camera camera = Camera.main;
            if (camera == null)
            {
                // Camera.main can miss a scene camera whose OnEnable has not run yet, so scan the scene.
                Camera untagged = null;
                foreach (GameObject root in roots)
                {
                    foreach (Camera candidate in root.GetComponentsInChildren<Camera>(true))
                    {
                        if (candidate.CompareTag("MainCamera")) { camera = candidate; break; }
                        if (untagged == null) untagged = candidate;
                    }
                    if (camera != null) break;
                }
                if (camera == null) camera = untagged;
            }

            if (camera == null)
            {
                var go = new GameObject("Main Camera");
                camera = go.AddComponent<Camera>();
            }

            camera.gameObject.SetActive(true);
            camera.enabled = true;
            camera.gameObject.tag = "MainCamera";
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = BackgroundColor;
            camera.nearClipPlane = 0.1f;
            camera.farClipPlane = 250f;
            camera.fieldOfView = 55f;
            return camera;
        }

        static void SetupAudioListener(List<GameObject> roots, Camera camera)
        {
            AudioListener keep = camera.GetComponent<AudioListener>();
            if (keep == null) keep = camera.gameObject.AddComponent<AudioListener>();
            keep.enabled = true;

            foreach (GameObject root in roots)
            {
                foreach (AudioListener listener in root.GetComponentsInChildren<AudioListener>(true))
                {
                    if (listener == keep) continue;
                    listener.enabled = false;   // takes effect immediately; Destroy is deferred
                    Destroy(listener);
                }
            }
        }

        static Light SetupLights(List<GameObject> roots, Transform parent)
        {
            Light main = null;
            foreach (GameObject root in roots)
            {
                foreach (Light light in root.GetComponentsInChildren<Light>(true))
                {
                    if (light.type == LightType.Directional) { main = light; break; }
                }
                if (main != null) break;
            }

            if (main == null)
            {
                var go = new GameObject("Arena Key Light");
                go.transform.SetParent(parent, false);
                main = go.AddComponent<Light>();
                main.type = LightType.Directional;
            }

            // High, slightly angled key light like arena rigging: soft shadows that read well
            // from the driver station without hiding robots in darkness.
            main.gameObject.SetActive(true);
            main.enabled = true;
            main.color = new Color(1.0f, 0.97f, 0.92f);
            main.intensity = 1.1f;
            main.shadows = LightShadows.Soft;
            main.shadowStrength = 0.65f;
            main.transform.rotation = Quaternion.Euler(58f, 32f, 0f);
            RenderSettings.sun = main;

            // Dim, cool fill from the opposite side. Vertex-lit in the Built-in pipeline (no extra
            // pass per object); URP treats it as a regular additional light.
            var fillObject = new GameObject("Arena Fill Light");
            fillObject.transform.SetParent(parent, false);
            var fill = fillObject.AddComponent<Light>();
            fill.type = LightType.Directional;
            fill.color = new Color(0.78f, 0.84f, 1.0f);
            fill.intensity = 0.3f;
            fill.shadows = LightShadows.None;
            fill.renderMode = LightRenderMode.ForceVertex;
            fill.transform.rotation = Quaternion.Euler(38f, 212f, 0f);
            return main;
        }

        // ---- Scene inspection ---------------------------------------------------------------

        static List<GameObject> SceneRoots()
        {
            var roots = new List<GameObject>();
            Scene scene = SceneManager.GetActiveScene();
            if (scene.IsValid()) scene.GetRootGameObjects(roots);
            return roots;
        }

        static bool IsEmptyScene(Scene scene)
        {
            var roots = new List<GameObject>();
            scene.GetRootGameObjects(roots);
            foreach (GameObject root in roots)
            {
                if (root.GetComponentInChildren<GameBootstrap>(true) != null) return false;
                if (!IsCameraOrLightOnly(root)) return false;
            }
            return true;
        }

        /// <summary>True for a childless object holding only a camera, a light or a post-processing volume.</summary>
        static bool IsCameraOrLightOnly(GameObject go)
        {
            if (go.transform.childCount > 0) return false;
            bool hasContent = false;
            foreach (Component component in go.GetComponents<Component>())
            {
                if (component == null) return false;   // missing script: something is there
                if (component is Transform) continue;
                if (component is Camera || component is Light) { hasContent = true; continue; }
                if (component is AudioListener) continue;

                string typeName = component.GetType().Name;
                if (typeName == "Volume") { hasContent = true; continue; }
                if (typeName == "FlareLayer" || typeName == "GUILayer" ||
                    typeName.EndsWith("AdditionalCameraData") || typeName.EndsWith("AdditionalLightData"))
                    continue;
                return false;
            }
            return hasContent;
        }
    }
}
