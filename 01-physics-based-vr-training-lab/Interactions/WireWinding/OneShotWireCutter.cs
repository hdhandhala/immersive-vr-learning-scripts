using System.Collections;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

[RequireComponent(typeof(XRGrabInteractable))]
[DefaultExecutionOrder(+80)]
public class OneShotWireCutter : MonoBehaviour
{
    [Header("Blade Trigger")]
    public BoxCollider bladeTrigger;

    [Header("Cut Settings")]
    public bool cutOnActivate = true;
    public bool enablePhysicalSplitCut = true;
    public bool preferBladeBoxCut = true;
    public float findRadius = 0.04f;

    [Header("Detached Piece Physics")]
    public bool tailShouldFall = true;
    public bool addMeshCollider = true;
    public bool makeTailGrabbable = true;
    public bool disableTailScriptAfterCut = true;
    public bool detachTailFromParent = true;

    public float tailMass = 0.05f;
    public float tailDrag = 1.75f;
    public float tailAngularDrag = 2.25f;
    public float inheritVelocityFactor = 0.02f;

    [Header("Detached Piece Anti-Bounce")]
    public bool suppressInitialBounce = true;
    public bool removeUpwardVelocity = true;
    public bool addSmallDownwardBias = true;
    public float downwardBias = 0.05f;
    public float maxInheritedLinearSpeed = 0.04f;
    public float maxInheritedAngularSpeed = 0.75f;
    public bool settleTailAfterSpawn = true;
    public float settleAfterSeconds = 0.12f;
    public float settleLinearDamping = 4.5f;
    public float settleAngularDamping = 6.5f;
    public PhysicsMaterial tailPhysicsMaterial;

    [Header("Runtime Grab Fix")]
    public bool autoCreateAttachPoint = true;
    public string attachPointName = "Attach";
    public bool centerRigidbodyMass = true;

    [Header("Runtime Debug")]
    public bool addRuntimeGrabDebug = true;

    [Header("Sound")]
    public AudioSource cutSound;

    [Header("Debug Logs")]
    public bool verboseLogs = false;

    private XRGrabInteractable grab;

    void Awake()
    {
        grab = GetComponent<XRGrabInteractable>();
    }

    void OnEnable()
    {
        if (grab != null)
            grab.activated.AddListener(OnActivate);
    }

    void OnDisable()
    {
        if (grab != null)
            grab.activated.RemoveListener(OnActivate);
    }

    void OnActivate(ActivateEventArgs args)
    {
        if (cutOnActivate)
            CutNow();
    }

    public void CutNow()
    {
        if (!enablePhysicalSplitCut)
            return;

        if (bladeTrigger == null)
        {
            if (verboseLogs) Debug.LogWarning("[Cutter] Blade trigger missing.", this);
            return;
        }

        bool success = TryCutBestRope();

        if (success && cutSound != null)
        {
            cutSound.Stop();
            cutSound.Play();
        }

        if (verboseLogs)
            Debug.Log(success ? "[Cutter] Cut succeeded." : "[Cutter] No valid rope found.", this);
    }

    bool TryCutBestRope()
    {
        Vector3 bladeCenter = bladeTrigger.bounds.center;

        VRCoilWithRopeMesh[] ropes = FindObjectsOfType<VRCoilWithRopeMesh>();
        if (ropes == null || ropes.Length == 0)
            return false;

        VRCoilWithRopeMesh bestRope = null;
        VRCoilWithRopeMesh.CutQuery bestCut = default;
        float bestScore = float.PositiveInfinity;

        foreach (VRCoilWithRopeMesh rope in ropes)
        {
            if (rope == null || !rope.isActiveAndEnabled)
                continue;

            if (!rope.enableCutSplit)
                continue;

            VRCoilWithRopeMesh.CutQuery cut = default;
            bool valid = false;

            if (preferBladeBoxCut)
                valid = rope.TryGetSingleCutFromBladeBox(bladeTrigger, out cut);

            if (!valid)
            {
                cut = rope.GetClosestCutQuery(bladeCenter, findRadius);
                valid = cut.valid;
            }

            if (!valid)
                continue;

            if (cut.sqrDistance < bestScore)
            {
                bestScore = cut.sqrDistance;
                bestRope = rope;
                bestCut = cut;
            }
        }

        if (bestRope == null)
            return false;

        bestRope.RequestSplitAtSegment(bestCut.segmentStartIndex, bestCut.t, bestCut.pointWS);

        VRCoilWithRopeMesh tail = VRCoilWithRopeMesh.LastSpawnedTail;
        SetupTail(tail);

        return true;
    }

    void SetupTail(VRCoilWithRopeMesh tail)
    {
        if (tail == null)
            return;

        if (detachTailFromParent)
            tail.transform.SetParent(null, true);

        tail.ForceMeshRefresh();

        Rigidbody rb = tail.GetComponent<Rigidbody>();
        if (rb == null)
            rb = tail.gameObject.AddComponent<Rigidbody>();

        Vector3 sourceLinearVelocity = rb.linearVelocity;
        Vector3 sourceAngularVelocity = rb.angularVelocity;

        Vector3 keptVelocity = sourceLinearVelocity * inheritVelocityFactor;
        Vector3 keptAngularVelocity = sourceAngularVelocity * inheritVelocityFactor;

        if (suppressInitialBounce)
        {
            keptVelocity = Vector3.ClampMagnitude(keptVelocity, maxInheritedLinearSpeed);
            keptAngularVelocity = Vector3.ClampMagnitude(keptAngularVelocity, maxInheritedAngularSpeed);

            if (removeUpwardVelocity && keptVelocity.y > 0f)
                keptVelocity.y = 0f;

            if (addSmallDownwardBias)
                keptVelocity += Vector3.down * downwardBias;
        }

        rb.mass = tailMass;
        rb.useGravity = tailShouldFall;
        rb.linearDamping = tailDrag;
        rb.angularDamping = tailAngularDrag;
        rb.isKinematic = false;
        rb.linearVelocity = keptVelocity;
        rb.angularVelocity = keptAngularVelocity;
        rb.interpolation = RigidbodyInterpolation.Interpolate;
        rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
        rb.ResetInertiaTensor();

        MeshFilter mf = tail.GetComponent<MeshFilter>();

        if (addMeshCollider)
        {
            MeshCollider mc = tail.GetComponent<MeshCollider>();
            if (mc == null)
                mc = tail.gameObject.AddComponent<MeshCollider>();

            if (mf != null)
                mc.sharedMesh = mf.sharedMesh;

            mc.convex = true;
            mc.enabled = true;

            if (tailPhysicsMaterial != null)
                mc.sharedMaterial = tailPhysicsMaterial;
        }

        if (makeTailGrabbable)
            SetupGrabForTail(tail, rb, mf);

        if (addRuntimeGrabDebug)
            SetupRuntimeDebug(tail, rb);

        if (disableTailScriptAfterCut)
            tail.enabled = false;

        if (settleTailAfterSpawn)
            StartCoroutine(SettleTailRoutine(rb));

        if (verboseLogs)
        {
            Debug.Log(
                "[Cutter] Tail physics set." +
                "\nLinear velocity = " + rb.linearVelocity +
                "\nAngular velocity = " + rb.angularVelocity +
                "\nLinear damping = " + rb.linearDamping +
                "\nAngular damping = " + rb.angularDamping,
                tail
            );
        }
    }

    IEnumerator SettleTailRoutine(Rigidbody rb)
    {
        if (rb == null)
            yield break;

        yield return new WaitForSeconds(settleAfterSeconds);

        if (rb == null)
            yield break;

        rb.linearDamping = Mathf.Max(rb.linearDamping, settleLinearDamping);
        rb.angularDamping = Mathf.Max(rb.angularDamping, settleAngularDamping);

        Vector3 v = rb.linearVelocity;
        if (v.y > 0f)
            v.y = 0f;

        rb.linearVelocity = Vector3.ClampMagnitude(v, maxInheritedLinearSpeed);
        rb.angularVelocity = Vector3.ClampMagnitude(rb.angularVelocity, maxInheritedAngularSpeed);
    }

    void SetupGrabForTail(VRCoilWithRopeMesh tail, Rigidbody rb, MeshFilter mf)
    {
        XRGrabInteractable tailGrab = tail.GetComponent<XRGrabInteractable>();
        if (tailGrab == null)
            tailGrab = tail.gameObject.AddComponent<XRGrabInteractable>();

        tailGrab.movementType = XRBaseInteractable.MovementType.VelocityTracking;
        tailGrab.trackPosition = true;
        tailGrab.trackRotation = true;
        tailGrab.throwOnDetach = true;
        tailGrab.smoothPosition = false;
        tailGrab.smoothRotation = false;

        tailGrab.useDynamicAttach = false;
        tailGrab.attachEaseInTime = 0f;
        tailGrab.retainTransformParent = true;

        if (mf == null || mf.sharedMesh == null)
            return;

        Bounds meshBounds = mf.sharedMesh.bounds;
        Vector3 localCenter = meshBounds.center;

        if (autoCreateAttachPoint)
        {
            Transform attach = tail.transform.Find(attachPointName);

            if (attach == null)
            {
                GameObject go = new GameObject(attachPointName);
                attach = go.transform;
                attach.SetParent(tail.transform, false);
            }

            attach.localPosition = localCenter;
            attach.localRotation = Quaternion.identity;
            attach.localScale = Vector3.one;

            tailGrab.attachTransform = attach;
        }

        if (centerRigidbodyMass && rb != null)
        {
            rb.centerOfMass = localCenter;
            rb.ResetInertiaTensor();
        }

        Collider col = tail.GetComponent<Collider>();
        if (col != null)
            col.enabled = true;

        if (verboseLogs)
        {
            Debug.Log(
                "[Cutter] Tail setup complete: " + tail.name +
                "\nAttach Local Pos = " + localCenter +
                "\nDynamic Attach = " + tailGrab.useDynamicAttach +
                "\nMovement Type = " + tailGrab.movementType,
                tail
            );
        }
    }

    void SetupRuntimeDebug(VRCoilWithRopeMesh tail, Rigidbody rb)
    {
        RuntimeGrabPointDebug dbg = tail.GetComponent<RuntimeGrabPointDebug>();
        if (dbg == null)
            dbg = tail.gameObject.AddComponent<RuntimeGrabPointDebug>();

        dbg.grab = tail.GetComponent<XRGrabInteractable>();
        dbg.meshFilter = tail.GetComponent<MeshFilter>();
        dbg.meshRenderer = tail.GetComponent<Renderer>();
        dbg.rb = rb;
    }
}