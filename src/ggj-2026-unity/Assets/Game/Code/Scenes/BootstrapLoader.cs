using UnityEngine;
using UnityEngine.SceneManagement;

namespace Game.Scenes
{
    public static class BootstrapLoader
    {
        private const string BootstrapSceneName = "Bootstrap";

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void OnBeforeSceneLoad()
        {
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

            var activeScene = SceneManager.GetActiveScene();

            if (activeScene.name == BootstrapSceneName)
            {
                return;
            }

            Debug.Log($"BootstrapLoader: Bootstrap scene not loaded. Loading it before {activeScene.name}");

            SceneManager.LoadScene(BootstrapSceneName, LoadSceneMode.Single);
        }
    }
}
