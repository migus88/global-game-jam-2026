using UnityEngine;
using UnityEngine.SceneManagement;

namespace Game.Scenes
{
    public static class BootstrapLoader
    {
        private const string BootstrapSceneName = "Bootstrap";

        public static string RequestedSceneName { get; private set; }
        public static bool HasRequestedScene => !string.IsNullOrEmpty(RequestedSceneName);

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void OnBeforeSceneLoad()
        {
            RequestedSceneName = null;

            var activeScene = SceneManager.GetActiveScene();

            if (activeScene.name == BootstrapSceneName)
            {
                return;
            }

            var bootstrapLoaded = false;

            for (var i = 0; i < SceneManager.sceneCount; i++)
            {
                var scene = SceneManager.GetSceneAt(i);
                if (scene.name == BootstrapSceneName)
                {
                    bootstrapLoaded = true;
                    break;
                }
            }

            if (bootstrapLoaded)
            {
                return;
            }

            RequestedSceneName = activeScene.name;
            Debug.Log($"BootstrapLoader: Redirecting to Bootstrap. Will load '{RequestedSceneName}' after initialization.");

            SceneManager.LoadScene(BootstrapSceneName, LoadSceneMode.Single);
        }

        public static void ClearRequestedScene()
        {
            RequestedSceneName = null;
        }
    }
}
