using System;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;

namespace Unity.Entities.Runtime.Build
{
    internal class LoadedSceneScope : IDisposable
    {
        private bool m_sceneLoaded;

        public Scene ProjectScene { get; }

        public LoadedSceneScope(string ident, bool isName = false)
        {
            var path = isName ? ConversionUtils.GetScenePathForSceneWithName(ident) : ident;
            var projScene = SceneManager.GetSceneByPath(path);
            m_sceneLoaded = projScene.IsValid() && projScene.isLoaded;
            if (!m_sceneLoaded)
            {
                ProjectScene = EditorSceneManager.OpenScene(path, UnityEditor.SceneManagement.OpenSceneMode.Additive);
            }
            else
            {
                ProjectScene = projScene;
            }
        }

        public void Dispose()
        {
            if (!m_sceneLoaded)
            {
                EditorSceneManager.CloseScene(ProjectScene, true);
            }
        }
    }
}
