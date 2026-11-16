using UnityEngine;

public class SkyboxSwitcher : MonoBehaviour
{
    public Material galaxySkybox;
    public Material defaultSkybox;

    // Call this to switch to normal skybox
    public void SwitchToDefault()
    {
        RenderSettings.skybox = defaultSkybox;
    }

    // Optional: Call this at start if you want to enforce galaxy manually
    void Start()
    {
        RenderSettings.skybox = galaxySkybox;
    }
}

