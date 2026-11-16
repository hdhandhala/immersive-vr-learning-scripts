using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

public class RuntimeGrabPointDebug : MonoBehaviour
{
    public XRGrabInteractable grab;
    public MeshFilter meshFilter;
    public Rigidbody rb;
    public Renderer meshRenderer;

    public bool drawLine = true;
    public bool logDistanceOnce = true;
    public float sphereSize = 0.01f;

    void Awake()
    {
        if (grab == null) grab = GetComponent<XRGrabInteractable>();
        if (meshFilter == null) meshFilter = GetComponent<MeshFilter>();
        if (meshRenderer == null) meshRenderer = GetComponent<Renderer>();
        if (rb == null) rb = GetComponent<Rigidbody>();
    }

    void Update()
    {
        if (!logDistanceOnce) return;
        if (grab == null) return;
        if (grab.attachTransform == null) return;

        Vector3 meshCenterWS = GetMeshCenter();

        float dist = Vector3.Distance(meshCenterWS, grab.attachTransform.position);
        Debug.Log("[GrabDebug] " + name + " distance = " + dist.ToString("F4") + " m", this);

        logDistanceOnce = false;
    }

    Vector3 GetMeshCenter()
    {
        if (meshFilter != null && meshFilter.sharedMesh != null)
        {
            return transform.TransformPoint(meshFilter.sharedMesh.bounds.center);
        }

        if (meshRenderer != null)
        {
            return meshRenderer.bounds.center;
        }

        return transform.position;
    }

    void OnDrawGizmos()
    {
        if (grab == null) grab = GetComponent<XRGrabInteractable>();
        if (meshFilter == null) meshFilter = GetComponent<MeshFilter>();
        if (meshRenderer == null) meshRenderer = GetComponent<Renderer>();
        if (rb == null) rb = GetComponent<Rigidbody>();

        if (grab == null) return;
        if (grab.attachTransform == null) return;

        Vector3 meshCenterWS = GetMeshCenter();
        Vector3 grabPointWS = grab.attachTransform.position;

        Gizmos.color = Color.blue;
        Gizmos.DrawSphere(meshCenterWS, sphereSize);

        Gizmos.color = Color.green;
        Gizmos.DrawSphere(grabPointWS, sphereSize);

        if (drawLine)
        {
            Gizmos.color = Color.red;
            Gizmos.DrawLine(meshCenterWS, grabPointWS);
        }

        if (rb != null)
        {
            Gizmos.color = Color.yellow;
            Gizmos.DrawSphere(rb.worldCenterOfMass, sphereSize * 0.9f);
        }
    }
}