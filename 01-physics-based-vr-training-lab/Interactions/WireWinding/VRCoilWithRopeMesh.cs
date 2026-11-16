using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

[DisallowMultipleComponent]
[RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
[DefaultExecutionOrder(+50)]
public class VRCoilWithRopeMesh : MonoBehaviour
{
    // ---------- Placement ----------
    public enum PlacementMode { AtThisTransform, Anchors, ExplicitWorld }
    [Header("Placement")]
    public PlacementMode placement = PlacementMode.AtThisTransform;
    public Transform explicitRoot, explicitTip, rootAnchor, tipAnchor;

    // ---------- Post (capture) ----------
    [Header("Post (capture)")]
    public CapsuleCollider post;
    public bool requireGrabToCapture = true;
    [Min(0f)] public float captureMinHoldTime = 0.08f;
    [Range(0.0005f, 0.06f)] public float captureBand = 0.008f;
    [Range(0.001f, 0.12f)] public float exitBand = 0.02f;

    // ---------- Geometry ----------
    [Header("Helix Geometry")]
    [Min(0.001f)] public float pitch = 0.01f;     // meters per turn along axis
    [Min(0.0005f)] public float tubeRadius = 0.004f;
    [Range(3, 24)] public int tubeSides = 8;
    [Range(16, 1200)] public int segmentsPerMeter = 200;
    public bool limitToCylinderSpan = true;
    [Min(0f)] public float endMargin = 0.0015f;

    // ---------- Direction ----------
    public enum WindingDirection { Clockwise = +1, CounterClockwise = -1 }
    [Header("Direction")]
    public WindingDirection allowedDirection = WindingDirection.Clockwise;

    // ---------- Input ----------
    public enum WindInput { TipOrbital }
    [Header("Input")]
    public WindInput windInput = WindInput.TipOrbital;
    [Range(0f, 15f)] public float rollDeadbandDeg = 2.0f;
    [Range(10f, 180f)] public float rollMaxDegPerStep = 45f;
    [Header("Start / Release Gates")]
    [Range(10f, 360f)] public float minDegreesToStart = 90f;
    [Range(0f, 20f)] public float zeroReleaseAngleDeg = 3f;
    [Range(0f, 90f)] public float releaseAngleDeg = 25f;
    public bool autoReleaseOnLetGoNearZero = true;

    // ---------- Tip lock ----------
    [Header("Tip Surface Lock")]
    public bool lockTipToSurface = true;
    [Min(0f)] public float tipSphereRadius = 0.02f;
    public bool disableTipCollisionsWhileCaptured = true;

    [Header("Tip / Tube Attachment")]
    [Tooltip("Keeps the sphere visually overlapped into the tube so no gap appears at the endpoint.")]
    [Min(0f)] public float tubeTipVisualOverlap = 0.0035f;

    // ---------- Post radius override ----------
    [Header("Post Radius Override")]
    public bool overridePostRadius = false;
    [Min(0f)] public float postRadiusOverride = 0.06f;

    // ---------- Tip (XR) ----------
    [Header("Tip (XR)")]
    public Transform tip;
    public XRGrabInteractable tipGrab;
    public Rigidbody tipRB;

    // ---------- Length & physics ----------
    [Header("Length & Physics")]
    [Min(0.02f)] public float startVisibleLength = 1f;
    [Min(0.2f)] public float maxLength = 50f;
    public bool useGravity = true;
    public Vector3 gravity = new(0, -9.81f, 0);
    [Range(0.90f, 0.9995f)] public float damping = 0.996f;
    [Range(1, 40)] public int constraintIterations = 18;
    public bool pinRoot = true;
    [Range(0, 20)] public int pinHeadCount = 0;
    public bool groundClamp = false;
    public float groundY = 0f;

    // ---------- Length budget ----------
    [Header("Length Budget")]
    public bool autoExtendLength = true;
    [Min(1)] public int lengthTurnsBudget = 30;
    [Range(64, 20000)] public int maxSegmentsHardCap = 6000;

    // ---------- Read-only helpers ----------
    public float VisibleLengthMeters => Mathf.Max(0f, (count - 1) * segLen);
    public bool IsFrozenAfterRelease => frozenAfterRelease;

    // ---------- Internals ----------
    MeshFilter mf; Mesh mesh;
    Vector3[] pos, prev; int count; float segLen; int maxSeg;

    enum Mode { Free, Hover, Captured }
    Mode mode = Mode.Free;

    // capsule/post (world-space)
    Vector3 capCenterWS, capAxisDirWS; float capHalfSpanWS, capRadiusWS;
    float targetR, halfLinearSpan;

    // continuous helix tracking
    Vector3 baseRadial; float baseZ;
    float thetaTotal; float prevPhi; bool hasPrevPhi;
    bool committed; float commitAccumRad; float hoverTime;

    // arc-length mapping (meters/radian along helix)
    float sPerRad;

    // helpers
    int allowedSign => (int)allowedDirection;
    Collider tipCol;
    bool isGrabbed => tipGrab && tipGrab.isSelected;

    // cooldown after release
    float releaseCooldown = 0f;
    [Min(0f)] public float releaseCooldownTime = 0.15f;

    // freeze wound helix after letting go: keep coil, stop orbiting
    bool frozenAfterRelease = false;
    float frozenPhi = 0f;
    float frozenMinVisibleLength = 0f;

    // ==================== CUT / SPLIT SUPPORT ====================
    [Header("Cut / Split")]
    [Tooltip("Master switch for physical split behaviour. Turn OFF to disable cut-tail spawning without removing scripts.")]
    public bool enableCutSplit = true;
    [Tooltip("Prefab of a rope object (same script) used to spawn the cut tail.")]
    public GameObject ropePrefabForTail;
    [Tooltip("When cut, move the tip sphere to the new reel-side endpoint.")]
    public bool snapTipToCutPoint = true;
    [Tooltip("Keep the currently visible helix/rope length visible after the cut so it does not visually retract.")]
    public bool preserveVisibleLengthAfterCut = true;
    [Tooltip("After a cut, keep the remaining reel-side shape frozen until the user grabs again.")]
    public bool freezeReelSideAfterCut = true;
    [Tooltip("When ON, the detached tail is cloned from the current live rope so the cut piece always stays visible with the exact same mesh/material setup.")]
    public bool cloneSelfForTail = true;
    [Tooltip("Reject cuts too close to either rope end so you do not get tiny useless fragments.")]
    [Min(1)] public int minSegmentsFromEndsForCut = 4;
    [Tooltip("Make the detached tail fall calmly instead of springing away.")]
    public bool calmDetachedTail = true;
    [Range(0f, 1f)] public float detachedTailVelocityKeep = 0.05f;

    // published pointer to the most recent tail (used by WireCutter)
    public static VRCoilWithRopeMesh LastSpawnedTail;

    public int CurrentSegmentCount => count;

    public struct CutQuery
    {
        public int segmentStartIndex;
        public float t;
        public Vector3 pointWS;
        public float sqrDistance;
        public bool valid;
    }

    // ---------- Unity ----------
    void Awake()
    {
        mf = GetComponent<MeshFilter>();
        if (!mf.sharedMesh) mf.sharedMesh = new Mesh { name = "VRCoilMesh" };
        mesh = mf.sharedMesh; mesh.MarkDynamic();

        TryAutoAssignPost();
        ResolvePost();
        PrecomputeStepAndArc();

        segLen = 1f / Mathf.Max(1, segmentsPerMeter);

        float budgetMeters = Mathf.Max(startVisibleLength, maxLength);
        if (autoExtendLength) budgetMeters = Mathf.Max(budgetMeters, lengthTurnsBudget * pitch + 0.5f);

        maxSeg = Mathf.Clamp(Mathf.CeilToInt(budgetMeters / segLen) + 8, 32, maxSegmentsHardCap);
        pos = new Vector3[maxSeg]; prev = new Vector3[maxSeg];

        Vector3 root = GetRootWS();
        Vector3 tip0 = GetTipWS();
        count = Mathf.Clamp(Mathf.CeilToInt(startVisibleLength / segLen) + 1, 2, maxSeg);
        for (int i = 0; i < count; i++) pos[i] = prev[i] = Vector3.Lerp(root, tip0, i / (float)(count - 1));

        if (tipGrab)
        {
            tipGrab.selectEntered.AddListener(_ => OnGrab(true));
            tipGrab.selectExited.AddListener(_ => OnGrab(false));
        }
        tipCol = tip ? tip.GetComponent<Collider>() : null;

        PushToMesh();
    }

    void OnDestroy()
    {
        if (tipGrab)
        {
            tipGrab.selectEntered.RemoveAllListeners();
            tipGrab.selectExited.RemoveAllListeners();
        }
    }

    void FixedUpdate()
    {
        if (releaseCooldown > 0f) releaseCooldown = Mathf.Max(0f, releaseCooldown - Time.fixedDeltaTime);

        ResolvePost();
        PrecomputeStepAndArc();

        IntegrateVerlet(Time.fixedDeltaTime);
        if (groundClamp) ClampGround();

        if (mode == Mode.Free) UpdateFree();
        else if (mode == Mode.Hover) UpdateHover();
        else UpdateCaptured();

        ApplyDistanceConstraints();
        EnforceTipAttachment();
        PushToMesh();
    }


    float EffectiveTipOffset()
    {
        if (!lockTipToSurface) return 0f;
        float overlap = Mathf.Max(tubeTipVisualOverlap, tubeRadius * 0.85f);
        return Mathf.Max(0f, tipSphereRadius - overlap);
    }

    void EnforceTipAttachment()
    {
        if (!tip || count < 1) return;

        int last = count - 1;

        if (mode == Mode.Captured)
        {
            Vector3 axisPoint = ClosestOnAxis(pos[last]);
            Vector3 radial = pos[last] - axisPoint;
            if (radial.sqrMagnitude < 1e-10f) radial = AxisRight();
            else radial.Normalize();

            Vector3 pTip = pos[last] + radial * EffectiveTipOffset();
            if (tipRB && tipRB.isKinematic) tipRB.MovePosition(pTip);
            else tip.position = pTip;
            return;
        }

        Vector3 lastPos = tip.position;
        if (count >= 2)
        {
            Vector3 dir = tip.position - pos[count - 2];
            if (dir.sqrMagnitude > 1e-10f)
            {
                float overlap = Mathf.Max(tubeTipVisualOverlap, tubeRadius * 0.85f);
                lastPos = tip.position - dir.normalized * overlap;
            }
        }

        pos[last] = prev[last] = lastPos;
    }

    // ---------- Physics ----------
    void IntegrateVerlet(float dt)
    {
        Vector3 g = useGravity ? gravity * (dt * dt) : Vector3.zero;
        for (int i = 0; i < count; i++)
        {
            Vector3 p = pos[i];
            Vector3 v = (pos[i] - prev[i]) * damping;
            prev[i] = p; pos[i] = p + v + g;
        }
    }

    void ApplyDistanceConstraints()
    {
        for (int it = 0; it < constraintIterations; it++)
        {
            if (pinRoot) { prev[0] = pos[0]; pos[0] = GetRootWS(); }

            for (int i = 0; i < count - 1; i++)
            {
                Vector3 d = pos[i + 1] - pos[i]; float m = d.magnitude; if (m < 1e-8f) continue;
                Vector3 c = d * (1f - segLen / m) * 0.5f; pos[i] += c; pos[i + 1] -= c;
            }

            if (mode != Mode.Captured && tip)
            {
                int last = count - 1; pos[last] = tip.position; prev[last] = tip.position;
            }
        }
    }

    void ClampGround() { for (int i = 0; i < count; i++) if (pos[i].y < groundY) pos[i].y = groundY; }

    // ---------- States ----------

    // NEW: much more forgiving capture — anywhere around the post
    void UpdateFree()
    {
        if (!tip || !post) return;
        if (requireGrabToCapture && !isGrabbed) return;
        if (releaseCooldown > 0f) return;

        Vector3 tipPos = tip.position;
        float r = (tipPos - ClosestOnAxis(tipPos)).magnitude;

        float desired = targetR + (lockTipToSurface ? tipSphereRadius : 0f);

        // generous capture radius: a bit bigger than the post + tip radius
        float captureRadius = desired + captureBand * 2f;

        if (r <= captureRadius)
        {
            EnterCapturedFromTip();
        }
    }

    // NEW: go straight into Captured when near the post (no picky hover region)
    void EnterCapturedFromTip()
    {
        if (!tip || !post) return;

        Vector3 tipPos = tip.position;

        // Start at the z-position where the user is around the post (clamped to span)
        float tipZ = AxisT(tipPos);
        baseZ = Mathf.Clamp(tipZ, -halfLinearSpan + endMargin, halfLinearSpan - endMargin);

        // radial direction from axis through tip
        Vector3 center = AxisPoint(baseZ);
        Vector3 rVec = tipPos - center;
        baseRadial = (rVec.sqrMagnitude > 1e-10f) ? rVec.normalized : AxisRight();

        thetaTotal = 0f;
        committed = false;
        commitAccumRad = 0f;
        hoverTime = 0f;

        float phi = TipAzimuth(tipPos, baseRadial);
        prevPhi = phi;
        hasPrevPhi = true;

        // lock collisions the same way as captured state
        if (disableTipCollisionsWhileCaptured)
        {
            if (tipRB) tipRB.detectCollisions = false;
            if (tipCol) tipCol.isTrigger = true;
        }
        if (tipGrab) tipGrab.movementType = XRGrabInteractable.MovementType.Instantaneous;

        mode = Mode.Captured;

        // snap tip onto the helix surface immediately
        PlaceTipOnSurface(phi, true);
    }

    // (Kept for completeness, but normal flow now skips Hover and goes straight to Captured)
    void UpdateFree_OLD()
    {
        if (!tip || !post) return;
        if (requireGrabToCapture && !isGrabbed) return;
        if (releaseCooldown > 0f) return;

        float desired = targetR + (lockTipToSurface ? tipSphereRadius : 0f);
        float r = (tip.position - ClosestOnAxis(tip.position)).magnitude;
        if (Mathf.Abs(r - desired) <= captureBand) EnterHover();
    }

    void EnterHover()
    {
        Vector3 end = pos[count - 1];

        float z = AxisT(end); baseZ = z;

        Vector3 center = AxisPoint(z);
        Vector3 r = end - center;
        baseRadial = (r.sqrMagnitude > 1e-10f) ? r.normalized : AxisRight();

        thetaTotal = 0f; committed = false; commitAccumRad = 0f;
        hasPrevPhi = false; hoverTime = 0f;

        if (disableTipCollisionsWhileCaptured)
        {
            if (tipRB) tipRB.detectCollisions = true;
            if (tipCol) tipCol.isTrigger = false;
        }
        if (tipGrab) tipGrab.movementType = XRGrabInteractable.MovementType.Instantaneous;

        mode = Mode.Hover;

        float phi = TipAzimuth(tip.position, baseRadial);
        PlaceTipOnSurface(phi, true);
    }

    void UpdateHover()
    {
        if (!tip || !post || (requireGrabToCapture && !isGrabbed)) { ResetCaptureState(); mode = Mode.Free; return; }

        float phi = TipAzimuth(tip.position, baseRadial);
        PlaceTipOnSurface(phi, false);

        float desired = targetR + (lockTipToSurface ? tipSphereRadius : 0f);
        float r = (tip.position - ClosestOnAxis(tip.position)).magnitude;
        if (Mathf.Abs(r - desired) > captureBand + exitBand) { ResetCaptureState(); mode = Mode.Free; return; }

        hoverTime += Time.fixedDeltaTime;
        if (hoverTime >= captureMinHoldTime)
        {
            float leftZ = -halfLinearSpan + endMargin;
            float rightZ = halfLinearSpan - endMargin;
            float tipZ = AxisT(tip.position);
            baseZ = leftZ; // older behaviour we experimented with; no longer used in normal flow

            Vector3 center = AxisPoint(baseZ);
            Vector3 rVec = tip.position - center; rVec.Normalize();
            baseRadial = rVec;

            thetaTotal = 0f; committed = false; commitAccumRad = 0f;
            hasPrevPhi = true; prevPhi = phi;

            if (disableTipCollisionsWhileCaptured)
            {
                if (tipRB) tipRB.detectCollisions = false;
                if (tipCol) tipCol.isTrigger = true;
            }
            if (tipGrab) tipGrab.movementType = XRGrabInteractable.MovementType.Instantaneous;

            mode = Mode.Captured;
        }
    }

    void UpdateCaptured()
    {
        float holdPhi = frozenAfterRelease ? frozenPhi : prevPhi;
        if (!tip)
        {
            PlaceFromTotal(thetaTotal, holdPhi, false);
            if (committed) PackTailByArc(thetaTotal, holdPhi);
            return;
        }

        // Released after winding: keep the helix frozen exactly as formed.
        // Do not recompute phi from the free tip, otherwise it starts orbiting the post.
        if (frozenAfterRelease && (requireGrabToCapture && !isGrabbed))
        {
            PlaceFromTotal(thetaTotal, frozenPhi, false);
            PackTailByArc(thetaTotal, frozenPhi);
            return;
        }

        // Re-grab after frozen state: resume smooth winding from the current tip angle.
        if (frozenAfterRelease && isGrabbed)
        {
            float resumePhi = TipAzimuth(tip.position, baseRadial);
            prevPhi = resumePhi;
            hasPrevPhi = true;
            frozenPhi = resumePhi;
            frozenAfterRelease = false;
            PlaceFromTotal(thetaTotal, resumePhi, true);
        }

        float phi = TipAzimuth(tip.position, baseRadial);
        float d = ShortestAngle(phi - prevPhi);
        prevPhi = phi;

        if (!committed)
        {
            float dead = Mathf.Deg2Rad * rollDeadbandDeg;
            float clamp = Mathf.Deg2Rad * rollMaxDegPerStep;
            if (Mathf.Abs(d) >= dead) commitAccumRad += Mathf.Abs(Mathf.Clamp(d, -clamp, clamp));
            PlaceTipOnSurface(phi, false);
            if (commitAccumRad >= Mathf.Deg2Rad * minDegreesToStart) committed = true;
            if (requireGrabToCapture && !isGrabbed) { ForceRelease(); return; }
            return;
        }

        {
            float dead = Mathf.Deg2Rad * rollDeadbandDeg;
            float clamp = Mathf.Deg2Rad * rollMaxDegPerStep;
            float step = (Mathf.Abs(d) >= dead) ? Mathf.Clamp(d, -clamp, clamp) : 0f;

            int sgn = step > 0f ? +1 : -1;
            if (sgn == allowedSign) thetaTotal += Mathf.Abs(step);
            else thetaTotal = Mathf.Max(0f, thetaTotal - Mathf.Abs(step));
        }

        if (thetaTotal < Mathf.Deg2Rad * zeroReleaseAngleDeg) { ForceRelease(); return; }

        float visMeters = Mathf.Min(maxLength, thetaTotal * sPerRad + 2f * pitch);
        int desiredSegs = Mathf.Clamp(Mathf.CeilToInt(visMeters * segmentsPerMeter) + 2, 2, maxSeg);
        if (desiredSegs > count)
        {
            for (int i = count; i < desiredSegs && i < maxSeg; i++) pos[i] = prev[i] = pos[count - 1];
            count = Mathf.Min(desiredSegs, maxSeg);
        }

        PlaceFromTotal(thetaTotal, phi, false);
        PackTailByArc(thetaTotal, phi);

        float desiredR2 = targetR + (lockTipToSurface ? tipSphereRadius : 0f);
        float tipR = (tip.position - ClosestOnAxis(tip.position)).magnitude;
        bool pulledAway = tipR > (desiredR2 + exitBand * 0.8f);
        float releaseRad = Mathf.Deg2Rad * Mathf.Max(0f, releaseAngleDeg);
        if (thetaTotal < releaseRad && pulledAway) ForceRelease();
    }

    // ---------- Placement along helix ----------
    void PlaceFromTotal(float thetaTot, float phiEnd, bool instant)
    {
        float twoPi = 2f * Mathf.PI;
        float z = baseZ + allowedSign * (thetaTot / twoPi) * pitch;
        z = Mathf.Clamp(z, -halfLinearSpan + endMargin, halfLinearSpan - endMargin);

        Vector3 radial = RotateAroundAxis(baseRadial, AxisDir(), phiEnd);
        Vector3 pWire = AxisPoint(z) + radial * targetR;
        Vector3 pTip = AxisPoint(z) + radial * (targetR + EffectiveTipOffset());

        int last = count - 1;
        pos[last] = prev[last] = pWire;

        if (tip)
        {
            if (tipRB && tipRB.isKinematic) { if (instant) tipRB.position = pTip; else tipRB.MovePosition(pTip); }
            else tip.position = pTip;
        }
    }

    void PackTailByArc(float thetaTot, float phiEnd)
    {
        if (count < 3) return;

        int last = count - 1;
        int start = Mathf.Max(1, Mathf.Clamp(pinHeadCount, 0, Mathf.Max(0, count - 2)));

        for (int i = last - 1; i >= start; i--)
        {
            int segsFromEnd = last - i;
            float backMeters = segsFromEnd * segLen;

            float theta_i = Mathf.Max(0f, thetaTot - backMeters / Mathf.Max(1e-8f, sPerRad));
            float twoPi = 2f * Mathf.PI;

            float z_i = baseZ + allowedSign * (theta_i / twoPi) * pitch;
            z_i = Mathf.Clamp(z_i, -halfLinearSpan + endMargin, halfLinearSpan - endMargin);

            float ang_i = phiEnd - (thetaTot - theta_i);

            Vector3 radial_i = RotateAroundAxis(baseRadial, AxisDir(), ang_i);
            pos[i] = prev[i] = AxisPoint(z_i) + radial_i * targetR;
        }
    }

    void PlaceTipOnSurface(float phi, bool instant)
    {
        float z = Mathf.Clamp(baseZ, -halfLinearSpan + endMargin, halfLinearSpan - endMargin);
        Vector3 radial = RotateAroundAxis(baseRadial, AxisDir(), phi);

        Vector3 pWire = AxisPoint(z) + radial * targetR;
        Vector3 pTip = AxisPoint(z) + radial * (targetR + EffectiveTipOffset());

        int last = count - 1;
        pos[last] = prev[last] = pWire;
        if (tip)
        {
            if (tipRB && tipRB.isKinematic) { if (instant) tipRB.position = pTip; else tipRB.MovePosition(pTip); }
            else tip.position = pTip;
        }
    }

    // ---------- Capture / Release ----------
    void OnGrab(bool down)
    {
        if (down)
        {
            if (mode != Mode.Free && disableTipCollisionsWhileCaptured)
            {
                if (tipRB) tipRB.detectCollisions = false;
                if (tipCol) tipCol.isTrigger = true;
            }
            if (mode == Mode.Captured && frozenAfterRelease)
            {
                float resumePhi = tip ? TipAzimuth(tip.position, baseRadial) : frozenPhi;
                prevPhi = resumePhi;
                frozenPhi = resumePhi;
                hasPrevPhi = true;
                frozenAfterRelease = false;
                frozenMinVisibleLength = 0f;
            }
            if (tipGrab) tipGrab.movementType = XRGrabInteractable.MovementType.Instantaneous;
        }
        else
        {
            if (mode == Mode.Captured)
            {
                if (!committed) { ForceRelease(); return; }
                if (autoReleaseOnLetGoNearZero && thetaTotal < Mathf.Deg2Rad * zeroReleaseAngleDeg) { ForceRelease(); return; }

                // Keep the formed helix and cutter-ready state, but stop live orbiting.
                // IMPORTANT: after release the tip must become grabbable again for unwind / forward wind.
                frozenAfterRelease = true;
                frozenPhi = prevPhi;
                frozenMinVisibleLength = VisibleLengthMeters;

                if (disableTipCollisionsWhileCaptured)
                {
                    // Re-enable normal grab collision state while the helix stays frozen.
                    if (tipRB) tipRB.detectCollisions = true;
                    if (tipCol) tipCol.isTrigger = false;
                }
                if (tipGrab) tipGrab.movementType = XRGrabInteractable.MovementType.VelocityTracking;
            }
            else
            {
                if (disableTipCollisionsWhileCaptured)
                {
                    if (tipRB) tipRB.detectCollisions = true;
                    if (tipCol) tipCol.isTrigger = false;
                }
                ResetCaptureState();
            }
        }
    }

    void ForceRelease()
    {
        ResetCaptureState();
        mode = Mode.Free;
        releaseCooldown = releaseCooldownTime;
    }

    void ResetCaptureState()
    {
        committed = false; commitAccumRad = 0f;
        thetaTotal = 0f; hasPrevPhi = false;
        frozenAfterRelease = false;
        frozenPhi = 0f;
        frozenMinVisibleLength = 0f;

        if (disableTipCollisionsWhileCaptured)
        {
            if (tipRB) tipRB.detectCollisions = true;
            if (tipCol) tipCol.isTrigger = false;
        }
        if (tipGrab) tipGrab.movementType = XRGrabInteractable.MovementType.VelocityTracking;
    }

    // ---------- Axis / geometry ----------
    void ResolvePost()
    {
        var p = post;
        if (!p)
        {
            capCenterWS = transform.position;
            capAxisDirWS = Vector3.up;
            capHalfSpanWS = 0.12f;
            capRadiusWS = 0.06f;
        }
        else
        {
            if (overridePostRadius) p.radius = postRadiusOverride;

            capCenterWS = p.transform.TransformPoint(p.center);

            Vector3 localAxis = (p.direction == 0) ? Vector3.right : (p.direction == 1) ? Vector3.up : Vector3.forward;
            Vector3 axisWorld = p.transform.TransformDirection(localAxis);
            if (axisWorld.sqrMagnitude < 1e-12f) axisWorld = Vector3.up;
            capAxisDirWS = axisWorld.normalized;

            float axisScale = p.transform.TransformVector(localAxis).magnitude;
            Vector3 localPerpA = (p.direction == 0) ? Vector3.up : Vector3.right;
            Vector3 localPerpB = (p.direction == 2) ? Vector3.up : Vector3.forward;
            float radiusScale = Mathf.Max(
                p.transform.TransformVector(localPerpA).magnitude,
                p.transform.TransformVector(localPerpB).magnitude);

            capRadiusWS = Mathf.Max(1e-5f, p.radius * radiusScale);
            float cylHalfLenLocal = Mathf.Max(0f, p.height * 0.5f - p.radius);
            capHalfSpanWS = cylHalfLenLocal * Mathf.Max(1e-6f, axisScale);
        }

        targetR = capRadiusWS;
        halfLinearSpan = capHalfSpanWS;
    }

    void PrecomputeStepAndArc()
    {
        float R = Mathf.Max(0.0001f, targetR);
        float k = pitch / (2f * Mathf.PI);
        sPerRad = Mathf.Sqrt(R * R + k * k); // meters per radian along helix
        segLen = 1f / Mathf.Max(1, segmentsPerMeter);
    }

    Vector3 AxisDir() => capAxisDirWS;
    Vector3 AxisPoint(float z) { z = Mathf.Clamp(z, -halfLinearSpan, halfLinearSpan); return capCenterWS + capAxisDirWS * z; }
    float AxisT(Vector3 worldPos) { float t = Vector3.Dot(worldPos - capCenterWS, capAxisDirWS); return Mathf.Clamp(t, -halfLinearSpan, halfLinearSpan); }
    Vector3 ClosestOnAxis(Vector3 wp)
    {
        Vector3 a = capCenterWS - capAxisDirWS * capHalfSpanWS;
        Vector3 b = capCenterWS + capAxisDirWS * capHalfSpanWS;
        Vector3 ab = b - a; float u = Mathf.Clamp01(Vector3.Dot(wp - a, ab) / Mathf.Max(1e-8f, ab.sqrMagnitude)); return a + ab * u;
    }
    Vector3 AxisRight()
    {
        Vector3 d = AxisDir(); Vector3 up = Mathf.Abs(Vector3.Dot(d, Vector3.up)) > 0.9f ? Vector3.right : Vector3.up;
        return Vector3.Cross(up, d).normalized;
    }
    static Vector3 RotateAroundAxis(Vector3 v, Vector3 axis, float angRad)
    {
        return Quaternion.AngleAxis(angRad * Mathf.Rad2Deg, axis) * v;
    }
    static float ShortestAngle(float d) { while (d > Mathf.PI) d -= 2f * Mathf.PI; while (d < -Mathf.PI) d += 2f * Mathf.PI; return d; }

    float TipAzimuth(Vector3 worldPos, Vector3 baseU)
    {
        Vector3 axis = AxisDir();
        Vector3 center = ClosestOnAxis(worldPos);
        Vector3 r = worldPos - center;

        if (r.sqrMagnitude < 1e-10f)
            r = baseU * (targetR + Mathf.Max(0f, tipSphereRadius));

        r.Normalize();

        Vector3 u = baseU;
        Vector3 v = Vector3.Cross(axis, u);
        if (v.sqrMagnitude < 1e-8f)
        {
            u = AxisRight();
            v = Vector3.Cross(axis, u).normalized;
        }

        float x = Vector3.Dot(r, u);
        float y = Vector3.Dot(r, v);
        return Mathf.Atan2(y, x); // radians
    }

    // ---------- Mesh ----------
    void PushToMesh()
    {
        if (count < 2) return;
        int ringCount = count, vertsPerRing = tubeSides;
        int vertCount = ringCount * vertsPerRing, triCount = (ringCount - 1) * tubeSides * 2;

        var v = new Vector3[vertCount];
        var n = new Vector3[vertCount];
        var uv = new Vector2[vertCount];
        var t = new int[triCount * 3];

        for (int i = 0; i < ringCount; i++)
        {
            Vector3 p = pos[i];
            Vector3 fwd = (i == 0) ? (pos[i + 1] - pos[i]).normalized :
                          (i == ringCount - 1) ? (pos[i] - pos[i - 1]).normalized :
                                            (pos[i + 1] - pos[i - 1]).normalized;
            if (fwd.sqrMagnitude < 1e-6f) fwd = Vector3.forward;

            Vector3 up = Mathf.Abs(Vector3.Dot(fwd.normalized, Vector3.up)) > 0.9f ? Vector3.right : Vector3.up;
            Vector3 right = Vector3.Cross(fwd, up).normalized;
            up = Vector3.Cross(right, fwd).normalized;

            for (int s = 0; s < tubeSides; s++)
            {
                float a = (s / (float)tubeSides) * Mathf.PI * 2f;
                Vector3 off = right * Mathf.Cos(a) * tubeRadius + up * Mathf.Sin(a) * tubeRadius;
                int idx = i * vertsPerRing + s;
                v[idx] = transform.InverseTransformPoint(p + off);
                n[idx] = transform.InverseTransformDirection(off.normalized);
                uv[idx] = new Vector2(s / (float)tubeSides, i / (float)(ringCount - 1));
            }
        }
        int tri = 0;
        for (int i = 0; i < ringCount - 1; i++)
            for (int s = 0; s < tubeSides; s++)
            {
                int a0 = i * vertsPerRing + s, a1 = i * vertsPerRing + ((s + 1) % tubeSides);
                int b0 = (i + 1) * vertsPerRing + s, b1 = (i + 1) * vertsPerRing + ((s + 1) % tubeSides);
                t[tri++] = a0; t[tri++] = b0; t[tri++] = a1;
                t[tri++] = a1; t[tri++] = b0; t[tri++] = b1;
            }

        mesh.Clear();
        mesh.vertices = v; mesh.normals = n; mesh.uv = uv; mesh.triangles = t; mesh.RecalculateBounds();
    }

    // ---------- Public API used by feeder & cutter ----------
    public void SetVisibleLengthMeters(float meters)
    {
        if (frozenAfterRelease) meters = Mathf.Max(meters, frozenMinVisibleLength);
        meters = Mathf.Clamp(meters, 0.02f, maxLength);
        int desired = Mathf.Clamp(Mathf.CeilToInt(meters * Mathf.Max(1, segmentsPerMeter)) + 1, 2, maxSeg);
        if (desired != count)
        {
            if (desired > count)
                for (int i = count; i < desired && i < maxSeg; i++)
                    pos[i] = prev[i] = pos[count - 1];

            count = desired;
        }
    }

    static bool ClipSegmentToLocalBox(Vector3 a, Vector3 b, Vector3 halfExtents, out float tEnter, out float tExit)
    {
        tEnter = 0f;
        tExit = 1f;
        Vector3 d = b - a;

        for (int axis = 0; axis < 3; axis++)
        {
            float p = axis == 0 ? a.x : axis == 1 ? a.y : a.z;
            float q = axis == 0 ? d.x : axis == 1 ? d.y : d.z;
            float min = -(axis == 0 ? halfExtents.x : axis == 1 ? halfExtents.y : halfExtents.z);
            float max = +(axis == 0 ? halfExtents.x : axis == 1 ? halfExtents.y : halfExtents.z);

            if (Mathf.Abs(q) < 1e-8f)
            {
                if (p < min || p > max) return false;
                continue;
            }

            float t0 = (min - p) / q;
            float t1 = (max - p) / q;
            if (t0 > t1) { float tmp = t0; t0 = t1; t1 = tmp; }

            tEnter = Mathf.Max(tEnter, t0);
            tExit = Mathf.Min(tExit, t1);
            if (tEnter > tExit) return false;
        }

        return true;
    }

    public bool TryGetSingleCutFromBladeBox(BoxCollider bladeTrigger, out CutQuery q)
    {
        q = new CutQuery { segmentStartIndex = -1, t = 0f, pointWS = Vector3.zero, sqrDistance = float.MaxValue, valid = false };
        if (!bladeTrigger || count < 2) return false;

        Vector3 half = bladeTrigger.size * 0.5f;
        Vector3 bladeCenterLocal = bladeTrigger.center;

        float bestScore = float.MaxValue;

        for (int i = 0; i < count - 1; i++)
        {
            Vector3 aLocal = bladeTrigger.transform.InverseTransformPoint(pos[i]) - bladeCenterLocal;
            Vector3 bLocal = bladeTrigger.transform.InverseTransformPoint(pos[i + 1]) - bladeCenterLocal;

            if (!ClipSegmentToLocalBox(aLocal, bLocal, half, out float tEnter, out float tExit))
                continue;

            Vector3 abLocal = bLocal - aLocal;
            float denom = abLocal.sqrMagnitude;
            float tClosestToCenter = 0.5f * (tEnter + tExit);
            if (denom > 1e-10f)
                tClosestToCenter = Mathf.Clamp(-Vector3.Dot(aLocal, abLocal) / denom, tEnter, tExit);

            float t = Mathf.Clamp01(tClosestToCenter);

            bool tooCloseToRoot = (i < minSegmentsFromEndsForCut) || (i == minSegmentsFromEndsForCut && t < 0.25f);
            bool tooCloseToTip = (i > (count - 2 - minSegmentsFromEndsForCut)) || (i == (count - 2 - minSegmentsFromEndsForCut) && t > 0.75f);
            if (tooCloseToRoot || tooCloseToTip)
                continue;

            Vector3 pLocal = aLocal + abLocal * t;
            float score = pLocal.sqrMagnitude;

            if (score < bestScore)
            {
                bestScore = score;
                q.segmentStartIndex = i;
                q.t = t;
                q.pointWS = Vector3.Lerp(pos[i], pos[i + 1], t);
                q.sqrDistance = score;
                q.valid = true;
            }
        }

        return q.valid;
    }

    public CutQuery GetClosestCutQuery(Vector3 worldPoint, float maxDist)
    {
        CutQuery q = new CutQuery { segmentStartIndex = -1, t = 0f, pointWS = worldPoint, sqrDistance = maxDist * maxDist, valid = false };
        if (count < 2) return q;

        float bestSqr = maxDist * maxDist;
        for (int i = 0; i < count - 1; i++)
        {
            Vector3 a = pos[i];
            Vector3 b = pos[i + 1];
            Vector3 ab = b - a;
            float len2 = ab.sqrMagnitude;
            float t = 0f;
            Vector3 p = a;
            if (len2 > 1e-10f)
            {
                t = Mathf.Clamp01(Vector3.Dot(worldPoint - a, ab) / len2);
                p = a + ab * t;
            }

            float d2 = (p - worldPoint).sqrMagnitude;
            if (d2 < bestSqr)
            {
                bestSqr = d2;
                q.segmentStartIndex = i;
                q.t = t;
                q.pointWS = p;
                q.sqrDistance = d2;
                q.valid = true;
            }
        }
        return q;
    }

    public int GetClosestSegmentIndex(Vector3 worldPoint, float maxDist)
    {
        var q = GetClosestCutQuery(worldPoint, maxDist);
        return q.valid ? (q.segmentStartIndex + (q.t >= 0.5f ? 1 : 0)) : -1;
    }

    public Vector3 GetLastParticleWS() => (count > 0) ? pos[count - 1] : transform.position;
    public void ForceMeshRefresh() => PushToMesh();

    public void RequestSplitAt(int splitIndex, Vector3 worldCutPoint)
    {
        RequestSplitAtSegment(splitIndex, 0f, worldCutPoint);
    }

    public void RequestSplitAtSegment(int segmentStartIndex, float tOnSegment, Vector3 worldCutPoint)
    {
        if (!enableCutSplit) return;
        if (count < 3) return;

        segmentStartIndex = Mathf.Clamp(segmentStartIndex, 0, count - 2);
        tOnSegment = Mathf.Clamp01(tOnSegment);

        bool tooCloseToRoot = (segmentStartIndex < minSegmentsFromEndsForCut) || (segmentStartIndex == minSegmentsFromEndsForCut && tOnSegment < 0.25f);
        bool tooCloseToTip = (segmentStartIndex > (count - 2 - minSegmentsFromEndsForCut)) || (segmentStartIndex == (count - 2 - minSegmentsFromEndsForCut) && tOnSegment > 0.75f);
        if (tooCloseToRoot || tooCloseToTip) return;

        Vector3 a = pos[segmentStartIndex];
        Vector3 b = pos[segmentStartIndex + 1];
        Vector3 cutPoint = Vector3.Lerp(a, b, tOnSegment);
        if ((worldCutPoint - cutPoint).sqrMagnitude > 1e-8f) cutPoint = worldCutPoint;

        var feeder = GetComponentInParent<WireReelFeederRealistic>();
        bool reenable = feeder && feeder.enabled;
        if (reenable) feeder.enabled = false;

        float visibleBeforeCut = VisibleLengthMeters;
        bool wasCaptured = (mode == Mode.Captured);

        GameObject tailGO = null;
        if (cloneSelfForTail || !ropePrefabForTail)
        {
            tailGO = Instantiate(gameObject, transform.position, transform.rotation, transform.parent);
        }
        else
        {
            tailGO = Instantiate(ropePrefabForTail, transform.position, transform.rotation, transform.parent);
        }

        tailGO.name = gameObject.name + "_CutTail";

        var tail = tailGO.GetComponent<VRCoilWithRopeMesh>();
        if (!tail)
        {
            Debug.LogError("[VRCoilWithRopeMesh] Tail object must also have VRCoilWithRopeMesh.");
            Destroy(tailGO);
            if (reenable) feeder.enabled = true;
            return;
        }

        CopyPublicSettingsTo(tail);
        SplitParticlesAtCutInto(this, tail, segmentStartIndex, tOnSegment, cutPoint);
        ApplyPostCutState(cutPoint, visibleBeforeCut, wasCaptured);
        PrepareTailAfterCut(tail, cutPoint, a, b);

        ForceMeshRefresh();
        tail.ForceMeshRefresh();

        LastSpawnedTail = tail;

        if (reenable) feeder.enabled = true;
    }

    void CopyPublicSettingsTo(VRCoilWithRopeMesh dst)
    {
        // Geometry & sim
        dst.pitch = this.pitch;
        dst.tubeRadius = this.tubeRadius;
        dst.tubeSides = this.tubeSides;
        dst.segmentsPerMeter = this.segmentsPerMeter;
        dst.limitToCylinderSpan = this.limitToCylinderSpan;
        dst.endMargin = this.endMargin;

        // physics
        dst.useGravity = this.useGravity;
        dst.gravity = this.gravity;
        dst.damping = this.damping;
        dst.constraintIterations = this.constraintIterations;

        // post/refs
        dst.post = this.post;
        dst.disableTipCollisionsWhileCaptured = this.disableTipCollisionsWhileCaptured;

        // materials
        var mrSrc = GetComponent<MeshRenderer>();
        var mrDst = dst.GetComponent<MeshRenderer>();
        if (mrSrc && mrDst) mrDst.sharedMaterials = mrSrc.sharedMaterials;
    }

    void EnsureCapacityLike(VRCoilWithRopeMesh other)
    {
        if (pos == null || pos.Length < other.pos.Length) pos = new Vector3[other.pos.Length];
        if (prev == null || prev.Length < other.prev.Length) prev = new Vector3[other.prev.Length];
        if (maxSeg < other.maxSeg) maxSeg = other.maxSeg;
    }

    void SplitParticlesAtCutInto(VRCoilWithRopeMesh reelSide, VRCoilWithRopeMesh tailSide, int segmentStartIndex, float tOnSegment, Vector3 cutPointWS)
    {
        // IMPORTANT: copy the original particle buffers BEFORE changing reelSide.count.
        // The earlier version shortened the reel side first and then used the already-shortened
        // count to build the tail, which is why the detached part became tiny or invisible.
        int originalCount = count;
        var srcPos = new Vector3[originalCount];
        var srcPrev = new Vector3[originalCount];
        System.Array.Copy(pos, srcPos, originalCount);
        System.Array.Copy(prev, srcPrev, originalCount);

        // Reel side keeps [0..segmentStartIndex] plus a new endpoint at the exact cut point.
        int reelCount = Mathf.Clamp(segmentStartIndex + 2, 2, originalCount);
        reelSide.count = reelCount;
        for (int i = 0; i < reelCount - 1; i++)
        {
            reelSide.pos[i] = srcPos[i];
            reelSide.prev[i] = srcPrev[i];
        }
        reelSide.pos[reelCount - 1] = cutPointWS;
        reelSide.prev[reelCount - 1] = cutPointWS;
        for (int i = reelCount; i < reelSide.pos.Length; i++)
            reelSide.pos[i] = reelSide.prev[i] = cutPointWS;

        // Tail side starts at the same exact cut point and keeps the remaining original rope.
        int tailSourceStart = segmentStartIndex + 1;
        int nTail = 1 + Mathf.Max(0, originalCount - tailSourceStart);
        tailSide.EnsureCapacityLike(reelSide);
        tailSide.count = Mathf.Clamp(nTail, 2, tailSide.pos.Length);
        tailSide.pos[0] = tailSide.prev[0] = cutPointWS;

        int dst = 1;
        for (int src = tailSourceStart; src < originalCount && dst < tailSide.count; src++, dst++)
        {
            tailSide.pos[dst] = srcPos[src];
            tailSide.prev[dst] = srcPrev[src];
        }
        while (dst < tailSide.count)
        {
            tailSide.pos[dst] = tailSide.prev[dst] = cutPointWS;
            dst++;
        }
    }

    void PrepareTailAfterCut(VRCoilWithRopeMesh tail, Vector3 cutPointWS, Vector3 segA, Vector3 segB)
    {
        tail.mode = Mode.Free;
        tail.committed = false;
        tail.frozenAfterRelease = false;
        tail.hasPrevPhi = false;
        tail.thetaTotal = 0f;
        tail.hoverTime = 0f;
        tail.releaseCooldown = 0f;

        if (tail.count > 0)
        {
            tail.pos[0] = cutPointWS;
        }

        Vector3 inherited = Vector3.zero;
        for (int i = 0; i < tail.count; i++)
        {
            Vector3 velocity = tail.pos[i] - tail.prev[i];
            inherited = calmDetachedTail ? velocity * Mathf.Clamp01(detachedTailVelocityKeep) : velocity;
            tail.prev[i] = tail.pos[i] - inherited;
        }

        tail.pinRoot = false;
        tail.pinHeadCount = 0;
        tail.lockTipToSurface = false;
        tail.requireGrabToCapture = false;
        tail.autoExtendLength = false;
        tail.enableCutSplit = false;
        tail.frozenMinVisibleLength = 0f;

        if (tail.tip)
        {
            tail.tip.gameObject.SetActive(false);
        }
        tail.tip = null;
        tail.tipGrab = null;
        tail.tipRB = null;
        tail.tipCol = null;
        tail.post = null;
        tail.mode = Mode.Free;

        tail.SetVisibleLengthMeters(Mathf.Max(tail.VisibleLengthMeters, (tail.count - 1) * tail.segLen));
    }

    void ApplyPostCutState(Vector3 cutPointWS, float visibleBeforeCut, bool wasCaptured)
    {
        if (count > 0)
        {
            pos[count - 1] = cutPointWS;
            prev[count - 1] = cutPointWS;
        }

        if (snapTipToCutPoint && tip)
        {
            if (tipRB)
            {
                if (tipRB.isKinematic) tipRB.position = cutPointWS;
                else
                {
                    tipRB.position = cutPointWS;
                    tipRB.linearVelocity = Vector3.zero;
                    tipRB.angularVelocity = Vector3.zero;
                }
            }
            tip.position = cutPointWS;
        }

        if (preserveVisibleLengthAfterCut)
            frozenMinVisibleLength = Mathf.Max(frozenMinVisibleLength, VisibleLengthMeters);

        // Only keep a frozen helix if the rope was actually wound/captured on the post.
        // For a free straight wire cut, forcing Captured mode is wrong and makes the cut behave oddly.
        bool keepFrozenHelix = freezeReelSideAfterCut && wasCaptured && post;
        if (keepFrozenHelix)
        {
            mode = Mode.Captured;
            committed = true;
            frozenAfterRelease = true;
            Vector3 axisPoint = ClosestOnAxis(cutPointWS);
            Vector3 radial = cutPointWS - axisPoint;
            if (radial.sqrMagnitude > 1e-10f)
            {
                radial.Normalize();
                frozenPhi = Mathf.Atan2(Vector3.Dot(radial, Vector3.Cross(AxisDir(), AxisRight()).normalized), Vector3.Dot(radial, AxisRight()));
                prevPhi = frozenPhi;
                hasPrevPhi = true;
            }
            if (disableTipCollisionsWhileCaptured)
            {
                if (tipRB) tipRB.detectCollisions = true;
                if (tipCol) tipCol.isTrigger = false;
            }
            if (tipGrab) tipGrab.movementType = XRGrabInteractable.MovementType.VelocityTracking;
        }
        else
        {
            // Normal non-wound cut: stay free, keep the new endpoint at the cut point,
            // and allow re-grab / further feeding immediately.
            mode = Mode.Free;
            committed = false;
            frozenAfterRelease = false;
            hasPrevPhi = false;
            if (disableTipCollisionsWhileCaptured)
            {
                if (tipRB) tipRB.detectCollisions = true;
                if (tipCol) tipCol.isTrigger = false;
            }
            if (tipGrab) tipGrab.movementType = XRGrabInteractable.MovementType.VelocityTracking;
        }
    }

    // ---------- Utilities ----------
    Vector3 GetRootWS()
    {
        return placement switch
        {
            PlacementMode.AtThisTransform => transform.position,
            PlacementMode.Anchors => rootAnchor ? rootAnchor.position : transform.position,
            PlacementMode.ExplicitWorld => explicitRoot ? explicitRoot.position : transform.position,
            _ => transform.position
        };
    }

    Vector3 GetTipWS()
    {
        if (placement == PlacementMode.AtThisTransform) return transform.position + transform.up * startVisibleLength;
        if (placement == PlacementMode.Anchors && tipAnchor) return tipAnchor.position;
        if (placement == PlacementMode.ExplicitWorld && explicitTip) return explicitTip.position;
        return transform.position + transform.up * startVisibleLength;
    }

    void TryAutoAssignPost()
    {
        if (post) return;
        post = GetComponent<CapsuleCollider>();
        if (!post) post = GetComponentInChildren<CapsuleCollider>();
    }
}

