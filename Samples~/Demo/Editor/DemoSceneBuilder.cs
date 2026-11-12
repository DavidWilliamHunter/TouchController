using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Threads.TouchController.Demo
{
    /// <summary>
    /// Creates (or recreates) the demo scenes: a camera and one GameObject with <see cref="TouchDemo"/>,
    /// set to a platform's conventions. Everything else is created at runtime by that component.
    /// </summary>
    public static class DemoSceneBuilder
    {
        /// <summary>
        /// The sample's Scenes folder, found from this script's location, so the scenes are rebuilt wherever the
        /// sample was imported (e.g. Assets/Samples/TouchController/0.2.0/Demo/Scenes).
        /// </summary>
        public static string ScenesFolder
        {
            get
            {
                foreach (var guid in AssetDatabase.FindAssets($"{nameof(DemoSceneBuilder)} t:MonoScript"))
                {
                    var path = AssetDatabase.GUIDToAssetPath(guid);
                    if (System.IO.Path.GetFileNameWithoutExtension(path) != nameof(DemoSceneBuilder)) continue;
                    var editorFolder = System.IO.Path.GetDirectoryName(path);
                    var sampleFolder = System.IO.Path.GetDirectoryName(editorFolder);
                    return (sampleFolder + "/Scenes").Replace('\\', '/');
                }
                return "Assets/Demo/Scenes";
            }
        }

        public static string SurfaceScenePath => ScenesFolder + "/SurfaceDemo.unity";
        public static string AndroidScenePath => ScenesFolder + "/AndroidDemo.unity";
        public static string FrameScenePath => ScenesFolder + "/IntentionsFrameDemo.unity";

        [MenuItem("Threads/TouchController/Create Surface Demo Scene")]
        public static void BuildSurface() => Build(SurfaceScenePath, "Surface Demo", DemoPlatform.Windows);

        [MenuItem("Threads/TouchController/Create Android Demo Scene")]
        public static void BuildAndroid() => Build(AndroidScenePath, "Android Demo", DemoPlatform.Android);

        [MenuItem("Threads/TouchController/Create Intentions Frame Demo Scene")]
        public static void BuildFrame()
        {
            var scene = NewSceneWithCamera();
            new GameObject("Intentions Frame Demo").AddComponent<FrameDemo>();
            Save(scene, FrameScenePath, "Intentions Frame Demo");
        }

        public static string JoystickScenePath => ScenesFolder + "/JoystickDemo.unity";

        [MenuItem("Threads/TouchController/Create Joystick Demo Scene")]
        public static void BuildJoystick()
        {
            var scene = NewSceneWithCamera();
            new GameObject("Joystick Demo").AddComponent<JoystickDemo>();
            Save(scene, JoystickScenePath, "Joystick Demo");
        }

        /// <summary>Every demo scene (used from the command line: -executeMethod Threads.TouchController.Demo.DemoSceneBuilder.BuildAll).</summary>
        public static void BuildAll()
        {
            BuildSurface();
            BuildAndroid();
            BuildFrame();
            BuildJoystick();
        }

        public static void Build(string path, string name, DemoPlatform platform)
        {
            var scene = NewSceneWithCamera();

            var demo = new GameObject(name).AddComponent<TouchDemo>();
            var serialized = new SerializedObject(demo);
            serialized.FindProperty("platform").enumValueIndex = (int)platform;
            serialized.ApplyModifiedPropertiesWithoutUndo();

            Save(scene, path, $"{name} ({platform} conventions)");
        }

        private static UnityEngine.SceneManagement.Scene NewSceneWithCamera()
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            var cameraObject = new GameObject("Main Camera") { tag = "MainCamera" };
            cameraObject.transform.position = new Vector3(0f, 0f, -10f);
            var camera = cameraObject.AddComponent<Camera>();
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.11f, 0.12f, 0.15f);
            camera.orthographic = true;
            return scene;
        }

        private static void Save(UnityEngine.SceneManagement.Scene scene, string path, string description)
        {
            System.IO.Directory.CreateDirectory(System.IO.Path.GetDirectoryName(path));
            EditorSceneManager.SaveScene(scene, path);
            Debug.Log($"{description} scene saved to {path}");
        }
    }
}
