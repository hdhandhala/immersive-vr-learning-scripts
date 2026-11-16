using UnityEngine;
using UnityEngine.XR;

public class VRRenderScaleManager : MonoBehaviour
{
    [Range(0.5f, 2.0f)]
    public float renderScale = 1.0f;

    void Start()
    {
        XRSettings.eyeTextureResolutionScale = renderScale;
    }
}
