using UnityEngine;
using UnityEngine.XR;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactors;
using UnityEngine.InputSystem;

using XRInputDevice = UnityEngine.XR.InputDevice;
using XRCommonUsages = UnityEngine.XR.CommonUsages;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

[DisallowMultipleComponent]
public class GlueGunMeshExtruder_Final : MonoBehaviour
{
    [Header("References")]
    public Transform nozzle;
    public XRBaseInteractor interactorOverride;
    public XRGrabInteractable grabInteractable;

    [Header("Input")]
    public InputActionReference triggerAction;
    public XRNode handNode = XRNode.RightHand;
    [Range(0f, 1f)] public float triggerThreshold = 0.1f;
    public float flowResponse = 18f;

    [Header("Surface Gating")]
    public LayerMask glueableLayers = ~0;
    public LayerMask blockLayers = 0;
    public float rayDistance = 0.08f;
    public float surfaceOffset = 0.0012f;
    public bool requireContact = true;
    public float contactDistance = 0.012f;
    public float breakDistance = 0.035f;

    [Header("Materials")]
    public Material wetMaterial;
    public Material dryMaterial;
    public float hardenSeconds = 10f;

    [Header("Bead Shape")]
    public float minRadius = 0.0015f;
    public float maxRadius = 0.0052f;
    public float pointMinDistance = 0.0035f;
    public float deadZone = 0.0007f;
    [Range(3, 24)] public int tubeSides = 12;

    [Header("Flow / Thickness")]
    public float slowSpeed = 0.02f;
    public float fastSpeed = 0.25f;
    public float flowThicknessBoost = 1.12f;

    [Header("Rounded Start / End")]
    public float startDropRadiusMul = 1.08f;
    public float endDropRadiusMul = 1.08f;
    public float startCapLength = 0.003f;
    public float endCapLength = 0.0035f;
    [Range(2, 16)] public int capSegments = 6;

    [Header("Path Smoothing")]
    [Range(5f, 175f)] public float maxTurnAngle = 90f;

    [Tooltip("Higher = more liquid lag while moving, but still stops instantly on release.")]
    public float viscosity = 14f;

    public float sagStrength = 0.0025f;
    public float sagOverTime = 1.2f;

    [Header("Parenting")]
    public bool parentGlueToHitObject = true;

    [Header("Haptics")]
    [Range(0f, 1f)] public float startPulseStrength = 0.14f;
    public float startPulseDuration = 0.03f;

    [Range(0f, 1f)] public float flowHapticMin = 0.03f;
    [Range(0f, 1f)] public float flowHapticMax = 0.08f;

    [Range(0f, 1f)] public float surfaceHapticMin = 0.06f;
    [Range(0f, 1f)] public float surfaceHapticMax = 0.14f;

    [Range(0f, 1f)] public float movementHapticBoost = 0.04f;

    [Range(0f, 1f)] public float breakPulseStrength = 0.22f;
    public float breakPulseDuration = 0.025f;

    public float hapticDuration = 0.02f;
    public float hapticPulseInterval = 0.04f;
    public float hapticOrganicWave = 0.01f;
    public float hapticOrganicSpeed = 18f;

    [Header("Glue Target Detection")]
    public bool logGlueHits = true;

    [Header("Debug")]
    public bool drawDebugRay = false;

    XRBaseInteractor _interactor;
    XRNode _handNode;

    float _triggerRaw;
    float _flow01;
    bool _wasExtrudingLastFrame;

    GlueStrandMesh_Final _activeStrand;
    Transform _activeParent;

    Vector3 _smoothedPos;
    Vector3 _smoothedNormal;
    bool _haveSmoothed;

    Vector3 _viscPos;
    bool _haveVisc;

    bool _havePrevContact;
    Vector3 _prevContact;

    bool _haveLastDir;
    Vector3 _lastAcceptedDir;

    bool _sessionActive;
    float _hapticTimer;
    bool _hadSurfaceContactThisSession;

    void Reset()
    {
        if (!grabInteractable) grabInteractable = GetComponent<XRGrabInteractable>();
    }

    void Awake()
    {
        if (!grabInteractable) grabInteractable = GetComponent<XRGrabInteractable>();
        _handNode = handNode;
    }

    void OnEnable()
    {
        if (grabInteractable != null)
        {
            grabInteractable.selectEntered.AddListener(OnGrab);
            grabInteractable.selectExited.AddListener(OnRelease);
        }

        if (triggerAction && triggerAction.action != null)
            triggerAction.action.Enable();
    }

    void OnDisable()
    {
        if (grabInteractable != null)
        {
            grabInteractable.selectEntered.RemoveListener(OnGrab);
            grabInteractable.selectExited.RemoveListener(OnRelease);
        }

        if (triggerAction && triggerAction.action != null)
            triggerAction.action.Disable();

        ForceStopSession(false);
    }

    void OnGrab(SelectEnterEventArgs args)
    {
        _interactor = interactorOverride ? interactorOverride : args.interactorObject as XRBaseInteractor;
        _handNode = GuessHandNode(_interactor);
    }

    void OnRelease(SelectExitEventArgs args)
    {
        ForceStopSession(false);
        _interactor = null;
    }

    void Update()
    {
        if (nozzle == null || wetMaterial == null)
        {
            ForceStopSession(false);
            return;
        }

        if (!interactorOverride && grabInteractable && !grabInteractable.isSelected)
        {
            ForceStopSession(false);
            return;
        }

        _triggerRaw = ReadTrigger();
        bool rawPressed = _triggerRaw >= triggerThreshold;

        if (!rawPressed && _wasExtrudingLastFrame)
        {
            _flow01 = 0f;
            ForceStopSession(false);
            _wasExtrudingLastFrame = false;
            return;
        }

        if (!rawPressed)
        {
            _flow01 = 0f;
            return;
        }

        float targetFlow = Mathf.InverseLerp(triggerThreshold, 1f, _triggerRaw);
        float a = 1f - Mathf.Exp(-flowResponse * Time.deltaTime);
        _flow01 = Mathf.Lerp(_flow01, targetFlow, a);

        if (!_wasExtrudingLastFrame)
        {
            BeginSession();
            SendHaptics(startPulseStrength, startPulseDuration);
        }

        _wasExtrudingLastFrame = true;
        ExtrudeTick();
    }

    void BeginSession()
    {
        _sessionActive = true;
        _activeStrand = null;
        _activeParent = null;

        _haveSmoothed = false;
        _haveVisc = false;
        _havePrevContact = false;
        _haveLastDir = false;

        _hapticTimer = 0f;
        _hadSurfaceContactThisSession = false;
    }

    void ExtrudeTick()
    {
        Ray ray = new Ray(nozzle.position, nozzle.forward);

        if (drawDebugRay)
            Debug.DrawRay(ray.origin, ray.direction * rayDistance, Color.yellow, 0f, false);

        if (!Physics.Raycast(ray, out RaycastHit hit, rayDistance, glueableLayers, QueryTriggerInteraction.Ignore))
        {
            if (logGlueHits)
                Debug.Log("[GlueGun] No valid hit.");
            ForceStopSession(true);
            return;
        }

        if (hit.collider != null)
        {
            int layerMask = 1 << hit.collider.gameObject.layer;
            if ((blockLayers.value & layerMask) != 0)
            {
                if (logGlueHits)
                    Debug.Log($"[GlueGun] Hit blocked layer on {hit.collider.name}");
                ForceStopSession(true);
                return;
            }
        }

        if (requireContact)
        {
            float limit = (_activeStrand == null) ? contactDistance : breakDistance;
            if (hit.distance > limit)
            {
                if (logGlueHits)
                    Debug.Log($"[GlueGun] Hit too far. distance={hit.distance:F4} limit={limit:F4}");
                ForceStopSession(true);
                return;
            }
        }

        _hadSurfaceContactThisSession = true;

        Vector3 rawPos = hit.point + hit.normal * surfaceOffset;
        Vector3 rawN = hit.normal.sqrMagnitude > 0.0001f ? hit.normal.normalized : Vector3.up;

        if (!_haveSmoothed)
        {
            _smoothedPos = rawPos;
            _smoothedNormal = rawN;
            _haveSmoothed = true;
        }
        else
        {
            float aPos = 1f - Mathf.Exp(-12f * Time.deltaTime);
            float aNrm = 1f - Mathf.Exp(-12f * Time.deltaTime);
            _smoothedPos = Vector3.Lerp(_smoothedPos, rawPos, aPos);
            _smoothedNormal = Vector3.Slerp(_smoothedNormal, rawN, aNrm).normalized;
        }

        if (!_haveVisc)
        {
            _viscPos = _smoothedPos;
            _haveVisc = true;
        }
        else
        {
            float aVisc = 1f - Mathf.Exp(-viscosity * Time.deltaTime);
            _viscPos = Vector3.Lerp(_viscPos, _smoothedPos, aVisc);
        }

        Transform newParent = (parentGlueToHitObject && hit.collider) ? hit.collider.transform : null;
        if (_activeParent == null)
            _activeParent = newParent;

        float dt = Mathf.Max(Time.deltaTime, 0.0001f);
        float speed = 0f;
        if (_havePrevContact)
            speed = Vector3.Distance(_prevContact, _viscPos) / dt;

        _prevContact = _viscPos;
        _havePrevContact = true;

        if (_activeStrand == null)
        {
            _activeStrand = CreateNewStrand(_activeParent);
            _activeStrand.Configure(pointMinDistance, tubeSides, wetMaterial, dryMaterial, hardenSeconds);

            _activeStrand.startDropRadiusMul = startDropRadiusMul;
            _activeStrand.endDropRadiusMul = endDropRadiusMul;
            _activeStrand.startCapLength = startCapLength;
            _activeStrand.endCapLength = endCapLength;
            _activeStrand.capSegments = capSegments;

            _activeStrand.Begin();

            float r0 = ComputeRadius(speed) * 1.10f;
            _activeStrand.AddPoint(_viscPos, _smoothedNormal, r0);

            RegisterGlueSegmentOnHit(hit, _viscPos);

            UpdateGlueHaptics(true, speed);
            return;
        }

        Vector3 lastP = _activeStrand.LastPointWorld;
        float move = Vector3.Distance(lastP, _viscPos);

        UpdateGlueHaptics(true, speed);

        if (move < deadZone)
            return;

        Vector3 dir = (_viscPos - lastP).normalized;
        if (_haveLastDir)
        {
            float ang = Vector3.Angle(_lastAcceptedDir, dir);
            if (ang > maxTurnAngle)
            {
                float t = maxTurnAngle / Mathf.Max(ang, 0.0001f);
                dir = Vector3.Slerp(_lastAcceptedDir, dir, t).normalized;
                _viscPos = lastP + dir * move;
            }
        }

        _lastAcceptedDir = dir;
        _haveLastDir = true;

        float age = _activeStrand.AgeSeconds;
        float sagFactor = sagStrength * (1f - Mathf.Exp(-sagOverTime * age));
        Vector3 sag = Vector3.down * sagFactor;

        float radius = ComputeRadius(speed);
        Vector3 depositPoint = _viscPos + sag;

        _activeStrand.AddPoint(depositPoint, _smoothedNormal, radius);
        RegisterGlueSegmentOnHit(hit, depositPoint);
    }

    void RegisterGlueSegmentOnHit(RaycastHit hit, Vector3 worldPoint)
    {
        if (hit.collider == null)
            return;

        BatteryGlueState glueState = hit.collider.GetComponent<BatteryGlueState>();
        if (glueState == null)
            glueState = hit.collider.GetComponentInParent<BatteryGlueState>();

        if (glueState == null)
            return;

        glueState.RegisterGlueSegment(worldPoint);
    }

    float ComputeRadius(float speed)
    {
        float t = Mathf.InverseLerp(slowSpeed, fastSpeed, speed);
        float r = Mathf.Lerp(maxRadius, minRadius, t);
        r *= Mathf.Lerp(0.9f, flowThicknessBoost, _flow01);
        return Mathf.Clamp(r, minRadius, maxRadius);
    }

    void UpdateGlueHaptics(bool touchingSurface, float movementSpeed)
    {
        _hapticTimer -= Time.deltaTime;
        if (_hapticTimer > 0f) return;

        float amp;

        if (touchingSurface)
        {
            float speed01 = Mathf.Clamp01(Mathf.InverseLerp(0.0f, fastSpeed, movementSpeed));
            amp = Mathf.Lerp(surfaceHapticMin, surfaceHapticMax, _flow01);
            amp += speed01 * movementHapticBoost;
        }
        else
        {
            amp = Mathf.Lerp(flowHapticMin, flowHapticMax, _flow01);
        }

        amp += Mathf.Sin(Time.time * hapticOrganicSpeed) * hapticOrganicWave;
        amp = Mathf.Clamp01(amp);

        SendHaptics(amp, hapticDuration);
        _hapticTimer = hapticPulseInterval;
    }

    void ForceStopSession(bool sendBreakPulse)
    {
        bool hadActiveGlue = _sessionActive || _activeStrand != null;

        if (_activeStrand != null)
        {
            _activeStrand.End();
            _activeStrand = null;
        }

        if (sendBreakPulse && hadActiveGlue && _hadSurfaceContactThisSession)
        {
            SendHaptics(breakPulseStrength, breakPulseDuration);
        }

        _sessionActive = false;
        _activeParent = null;
        _haveSmoothed = false;
        _haveVisc = false;
        _havePrevContact = false;
        _haveLastDir = false;
        _hapticTimer = 0f;
        _hadSurfaceContactThisSession = false;

        _viscPos = Vector3.zero;
        _smoothedPos = Vector3.zero;
        _smoothedNormal = Vector3.up;
        _flow01 = 0f;
    }

    GlueStrandMesh_Final CreateNewStrand(Transform parent)
    {
        GameObject go = new GameObject("GlueStrand");
        if (parent) go.transform.SetParent(parent, true);
        return go.AddComponent<GlueStrandMesh_Final>();
    }

    XRNode GuessHandNode(XRBaseInteractor interactor)
    {
        if (!interactor) return handNode;

        string n = interactor.name.ToLower();
        if (n.Contains("left")) return XRNode.LeftHand;
        if (n.Contains("right")) return XRNode.RightHand;

        Transform t = interactor.transform;
        while (t != null)
        {
            string pn = t.name.ToLower();
            if (pn.Contains("left")) return XRNode.LeftHand;
            if (pn.Contains("right")) return XRNode.RightHand;
            t = t.parent;
        }

        return handNode;
    }

    XRNode DetermineHandFromInteractor(IXRSelectInteractor interactor)
    {
        var mb = interactor as MonoBehaviour;
        if (mb == null) return handNode;

        Transform t = mb.transform;
        while (t != null)
        {
            string nameLower = t.name.ToLower();
            if (nameLower.Contains("right")) return XRNode.RightHand;
            if (nameLower.Contains("left")) return XRNode.LeftHand;
            t = t.parent;
        }

        return handNode;
    }

    void SendHaptics(float amplitude, float duration)
    {
        if (grabInteractable == null) return;

        var interactor = grabInteractable.firstInteractorSelecting;
        if (interactor == null) return;

        XRNode node = DetermineHandFromInteractor(interactor);
        XRInputDevice device = InputDevices.GetDeviceAtXRNode(node);

        if (device.isValid)
            device.SendHapticImpulse(0, amplitude, duration);
    }

    float ReadTrigger()
    {
        if (triggerAction && triggerAction.action != null)
        {
            try { return Mathf.Clamp01(triggerAction.action.ReadValue<float>()); }
            catch { }
        }

        XRInputDevice dev = InputDevices.GetDeviceAtXRNode(_handNode);
        if (!dev.isValid)
        {
            dev = InputDevices.GetDeviceAtXRNode(XRNode.RightHand);
            if (!dev.isValid)
                dev = InputDevices.GetDeviceAtXRNode(XRNode.LeftHand);
        }

        if (dev.isValid && dev.TryGetFeatureValue(XRCommonUsages.trigger, out float trig))
            return Mathf.Clamp01(trig);

        return 0f;
    }
}