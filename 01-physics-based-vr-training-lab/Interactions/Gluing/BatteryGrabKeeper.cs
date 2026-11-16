using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

[DisallowMultipleComponent]
public class BatteryGrabKeeper : MonoBehaviour
{
    [Header("References")]
    public XRGrabInteractable grabInteractable;
    public Rigidbody rb;
    public Collider[] batteryColliders;

    [Header("Keep Grabbable")]
    [Tooltip("If true, the script keeps checking and restores grab state.")]
    public bool keepEnforcing = true;

    [Tooltip("How often to re-check grab state.")]
    public float checkInterval = 0.25f;

    [Header("Rigidbody State")]
    [Tooltip("Recommended true for your XR setup.")]
    public bool forceKinematic = true;

    [Tooltip("Recommended true so battery still behaves naturally when released.")]
    public bool forceUseGravity = true;

    [Header("Lock Control")]
    [Tooltip("When true, the battery is considered permanently locked and this script will not re-enable grab.")]
    public bool permanentlyLocked = false;

    [Header("Debug")]
    public bool logFixes = false;

    float _timer;

    void Reset()
    {
        if (!grabInteractable) grabInteractable = GetComponent<XRGrabInteractable>();
        if (!rb) rb = GetComponent<Rigidbody>();

        if (batteryColliders == null || batteryColliders.Length == 0)
            batteryColliders = GetComponentsInChildren<Collider>(true);
    }

    void Awake()
    {
        if (!grabInteractable) grabInteractable = GetComponent<XRGrabInteractable>();
        if (!rb) rb = GetComponent<Rigidbody>();

        if (batteryColliders == null || batteryColliders.Length == 0)
            batteryColliders = GetComponentsInChildren<Collider>(true);
    }

    void OnEnable()
    {
        EnsureState();
    }

    void Update()
    {
        if (!keepEnforcing)
            return;

        _timer += Time.deltaTime;
        if (_timer < checkInterval)
            return;

        _timer = 0f;
        EnsureState();
    }

    public void SetPermanentLock(bool locked)
    {
        permanentlyLocked = locked;
        ForceRefreshNow();
    }

    public void EnsureState()
    {
        if (permanentlyLocked)
            EnsureLockedState();
        else
            EnsureGrabbableState();
    }

    void EnsureGrabbableState()
    {
        if (grabInteractable != null && !grabInteractable.enabled)
        {
            grabInteractable.enabled = true;

            if (logFixes)
                Debug.Log($"[BatteryGrabKeeper:{name}] Re-enabled XRGrabInteractable", this);
        }

        if (rb != null)
        {
            if (forceKinematic && !rb.isKinematic)
            {
                rb.isKinematic = true;

                if (logFixes)
                    Debug.Log($"[BatteryGrabKeeper:{name}] Forced Rigidbody.isKinematic = true", this);
            }

            if (rb.useGravity != forceUseGravity)
            {
                rb.useGravity = forceUseGravity;

                if (logFixes)
                    Debug.Log($"[BatteryGrabKeeper:{name}] Restored Rigidbody.useGravity", this);
            }

            rb.detectCollisions = true;
        }

        EnsureCollidersEnabled();
    }

    void EnsureLockedState()
    {
        if (grabInteractable != null && grabInteractable.enabled)
        {
            grabInteractable.enabled = false;

            if (logFixes)
                Debug.Log($"[BatteryGrabKeeper:{name}] Disabled XRGrabInteractable because battery is locked", this);
        }

        if (rb != null)
        {
            rb.isKinematic = true;
            rb.useGravity = forceUseGravity;
            rb.detectCollisions = true;
        }

        EnsureCollidersEnabled();
    }

    void EnsureCollidersEnabled()
    {
        if (batteryColliders == null)
            return;

        for (int i = 0; i < batteryColliders.Length; i++)
        {
            Collider c = batteryColliders[i];
            if (c == null) continue;

            if (!c.enabled)
            {
                c.enabled = true;

                if (logFixes)
                    Debug.Log($"[BatteryGrabKeeper:{name}] Re-enabled collider: {c.name}", this);
            }
        }
    }

    public void ForceRefreshNow()
    {
        _timer = 0f;
        EnsureState();
    }
}