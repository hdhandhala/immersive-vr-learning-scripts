using UnityEngine;

public class WheelLift : MonoBehaviour
{
    [Header("References")]
    public SteeringWheelZDial wheelDial;   // Drag the wheel object that has SteeringWheelZDial
    public Transform targetToMove;         // Lift transform

    [Header("Lift Range (Y)")]
    public float minY = 0.3f;
    public float maxY = 2.0f;
    public bool useLocalPosition = true;

    [Header("Wheel Angle Range (degrees)")]
    public float wheelMinAngle = -540f;
    public float wheelMaxAngle = 540f;
    public bool invert = false;

    void Update()
    {
        if (!wheelDial || !targetToMove) return;

        float a = wheelDial.GetAngle();
        if (invert) a = -a;

        // Convert wheel angle -> 0..1
        float t = Mathf.InverseLerp(wheelMinAngle, wheelMaxAngle, a);

        float y = Mathf.Lerp(minY, maxY, t);

        if (useLocalPosition)
        {
            var p = targetToMove.localPosition;
            p.y = y;
            targetToMove.localPosition = p;
        }
        else
        {
            var p = targetToMove.position;
            p.y = y;
            targetToMove.position = p;
        }
    }
}