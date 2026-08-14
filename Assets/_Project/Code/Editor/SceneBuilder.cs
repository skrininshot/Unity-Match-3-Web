using System.IO;
using System.Linq;
using Match3.App;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Match3.EditorTools
{
    /// <summary>
    /// Creates the one scene the game needs and registers it for builds.
    /// <para>
    /// The scene is generated rather than hand-authored so that its contents stay a single reviewable
    /// object; everything else is built from code at runtime.
    /// </para>
    /// </summary>
    public static class SceneBuilder
    {
        public const string ScenePath = "Assets/_Project/Scenes/Game.unity";

        public static void BuildGameScene()
        {
            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            var cameraGo = new GameObject("Main Camera") { tag = "MainCamera" };
            Camera camera = cameraGo.AddComponent<Camera>();
            camera.orthographic = true;
            camera.orthographicSize = 6f;
            camera.transform.position = new Vector3(0f, 0f, -10f);
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = Presentation.SpriteLibrary.BoardBackground;

            var gameGo = new GameObject("Game");
            gameGo.AddComponent<GameBootstrap>();

            Directory.CreateDirectory(Path.GetDirectoryName(ScenePath));
            EditorSceneManager.SaveScene(scene, ScenePath);

            RegisterInBuildSettings();
            AssetDatabase.SaveAssets();

            Debug.Log($"[TOOL] scene written to {ScenePath}");
        }

        public static void RegisterInBuildSettings()
        {
            var scenes = EditorBuildSettings.scenes
                .Where(s => s.path != ScenePath && File.Exists(s.path))
                .ToList();

            scenes.Insert(0, new EditorBuildSettingsScene(ScenePath, true));
            EditorBuildSettings.scenes = scenes.ToArray();

            Debug.Log($"[TOOL] build scenes: {string.Join(", ", EditorBuildSettings.scenes.Select(s => s.path))}");
        }
    }
}
