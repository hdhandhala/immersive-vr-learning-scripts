using UnityEngine;

[DisallowMultipleComponent]
public class WireAnchorFollower : MonoBehaviour
{
    [Header("References")]
    public VRCoilWithRopeMesh wire;    // your existing rope script
    public Transform anchorPoint;      // empty GameObject placed exactly where wire should start (on coil surface)
    public bool matchRotation = true;  // optionally align wire’s rotation

    void LateUpdate()
    {
        if (!wire || !anchorPoint) return;

        // Update wire root position dynamically
        wire.transform.position = anchorPoint.position;

        if (matchRotation)
            wire.transform.rotation = anchorPoint.rotation;
    }
}
