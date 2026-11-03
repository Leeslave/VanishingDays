using UnityEditor;

namespace VanishingDays.Editor
{
    public static class NicknameInputChecks
    {
        // Batch validation uses -vdNicknameInput; the actual scene and Play-mode UI run.
        public static void Play()
        {
            UnityEngine.InputSystem.InputSystem.settings.editorInputBehaviorInPlayMode=UnityEngine.InputSystem.InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
            UnityEngine.InputSystem.InputSystem.settings.backgroundBehavior=UnityEngine.InputSystem.InputSettings.BackgroundBehavior.IgnoreFocus;
            UnityEditor.SceneManagement.EditorSceneManager.OpenScene("Assets/Scenes/Prototype.unity");
            EditorApplication.EnterPlaymode();
        }
    }
}
