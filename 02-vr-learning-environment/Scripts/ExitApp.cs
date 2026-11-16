using UnityEngine;

public class ExitApp : MonoBehaviour
{
    public void QuitApp()
    {
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false; // For testing in Editor
#else
            Application.Quit(); // Exits the app in build
#endif
    }
}
