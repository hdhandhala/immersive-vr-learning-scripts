using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

public class SequentialBatteryPoolSpawner : MonoBehaviour
{
    [Header("Scene Batteries In Order")]
    public List<GameObject> batteries = new List<GameObject>();

    [Header("Spawn Reference")]
    public Transform spawnPoint;
    public bool useManualSpawnRotation = true;
    public Vector3 manualSpawnEuler = Vector3.zero;
    public Vector3 spawnOffsetLocal = Vector3.zero;

    [Header("Materials")]
    public Material transitionMaterial;
    public Material originalMaterial;

    [Header("Reveal Timing")]
    public float revealDuration = 3.0f;

    [Header("Spawn Rule")]
    [Tooltip("Spawn next battery ONLY after current battery leaves this radius.")]
    public float movedAwayDistance = 0.15f;

    [Header("Debug")]
    public bool enableDebugLogs = true;
    public bool printDistanceContinuously = false;

    private int currentIndex = -1;

    private GameObject activeBattery;
    private Rigidbody activeRb;
    private XRGrabInteractable activeGrab;

    private bool nextQueued = false;
    private bool activeBatteryWasGrabbed = false;

    private Coroutine revealRoutine;
    private Coroutine monitorRoutine;

    void Awake()
    {
        Log("Awake()");
    }

    void Start()
    {
        HideAllBatteries();
        ShowNextBattery();
    }

    void HideAllBatteries()
    {
        for (int i = 0; i < batteries.Count; i++)
        {
            if (batteries[i] == null) continue;
            batteries[i].SetActive(false);
        }
    }

    public void ShowNextBattery()
    {
        currentIndex++;

        if (currentIndex >= batteries.Count)
        {
            Log("No more batteries in pool.");
            return;
        }

        nextQueued = false;
        activeBatteryWasGrabbed = false;

        activeBattery = batteries[currentIndex];
        if (activeBattery == null)
        {
            Log("Battery is null, skipping.");
            ShowNextBattery();
            return;
        }

        Quaternion spawnRotation = useManualSpawnRotation
            ? Quaternion.Euler(manualSpawnEuler)
            : spawnPoint.rotation;

        Vector3 spawnPosition = spawnPoint.TransformPoint(spawnOffsetLocal);

        activeBattery.SetActive(true);
        activeBattery.transform.SetPositionAndRotation(spawnPosition, spawnRotation);

        activeRb = activeBattery.GetComponent<Rigidbody>();
        activeGrab = activeBattery.GetComponent<XRGrabInteractable>();

        if (activeRb == null)
            Debug.LogError($"[BatteryPoolSpawner] {activeBattery.name} has no Rigidbody.", activeBattery);

        if (activeGrab == null)
            Debug.LogError($"[BatteryPoolSpawner] {activeBattery.name} has no XRGrabInteractable.", activeBattery);

        if (activeGrab != null)
        {
            activeGrab.selectEntered.RemoveListener(OnBatteryGrabbed);
            activeGrab.selectEntered.AddListener(OnBatteryGrabbed);

            // Important for your flow:
            activeGrab.throwOnDetach = false;
        }

        LockBatteryAtSpawn(activeBattery, activeRb, activeGrab, spawnPosition, spawnRotation);

        if (transitionMaterial != null)
            ApplyMaterialToBattery(activeBattery, transitionMaterial);

        if (revealRoutine != null) StopCoroutine(revealRoutine);
        revealRoutine = StartCoroutine(RevealRoutine(activeBattery, activeRb, activeGrab, spawnPosition, spawnRotation));

        if (monitorRoutine != null) StopCoroutine(monitorRoutine);
        monitorRoutine = StartCoroutine(MonitorMovedAwayRoutine(activeBattery, spawnPosition));

        Log($"ShowNextBattery -> index={currentIndex}, name={activeBattery.name}");
    }

    void OnBatteryGrabbed(SelectEnterEventArgs args)
    {
        XRGrabInteractable grabbed = args.interactableObject as XRGrabInteractable;
        if (grabbed == null) return;

        GameObject grabbedGO = grabbed.gameObject;
        Rigidbody grabbedRb = grabbedGO.GetComponent<Rigidbody>();

        activeBatteryWasGrabbed = true;
        Log($"Grabbed: {grabbedGO.name}");

        // Keep battery kinematic always
        if (grabbedRb != null)
        {
            grabbedRb.isKinematic = true;
            grabbedRb.useGravity = true;
            grabbedRb.detectCollisions = true;
        }
    }

    IEnumerator MonitorMovedAwayRoutine(GameObject battery, Vector3 spawnPosition)
    {
        while (battery != null && !nextQueued)
        {
            float dist = Vector3.Distance(battery.transform.position, spawnPosition);

            if (printDistanceContinuously)
                Log($"Distance: {dist:F3}");

            if (activeBatteryWasGrabbed && dist >= movedAwayDistance)
            {
                nextQueued = true;
                Log($"Battery moved outside radius ({dist:F3} >= {movedAwayDistance}). Spawning next.");
                ShowNextBattery();
                yield break;
            }

            yield return null;
        }
    }

    IEnumerator RevealRoutine(GameObject battery, Rigidbody rb, XRGrabInteractable grab, Vector3 spawnPosition, Quaternion spawnRotation)
    {
        float t = 0f;
        float safeReveal = Mathf.Max(0.01f, revealDuration);

        while (battery != null && t < safeReveal)
        {
            t += Time.deltaTime;

            battery.transform.SetPositionAndRotation(spawnPosition, spawnRotation);

            if (rb != null)
            {
                rb.isKinematic = true;
                rb.useGravity = true;
                rb.detectCollisions = true;
            }

            yield return null;
        }

        if (battery == null)
            yield break;

        if (originalMaterial != null)
            ApplyMaterialToBattery(battery, originalMaterial);

        EnableBatteryInteraction(rb, grab);

        Log($"Reveal complete: {battery.name}");
    }

    void LockBatteryAtSpawn(GameObject battery, Rigidbody rb, XRGrabInteractable grab, Vector3 spawnPosition, Quaternion spawnRotation)
    {
        battery.transform.SetPositionAndRotation(spawnPosition, spawnRotation);

        if (grab != null)
            grab.enabled = false;

        if (rb != null)
        {
            rb.isKinematic = true;
            rb.useGravity = true;
            rb.detectCollisions = true;
        }
    }

    void EnableBatteryInteraction(Rigidbody rb, XRGrabInteractable grab)
    {
        if (rb != null)
        {
            // Keep kinematic always
            rb.isKinematic = true;
            rb.useGravity = true;
            rb.detectCollisions = true;
        }

        if (grab != null)
        {
            grab.enabled = true;
            grab.throwOnDetach = false;
        }
    }

    void ApplyMaterialToBattery(GameObject battery, Material mat)
    {
        if (battery == null || mat == null) return;

        Renderer[] rends = battery.GetComponentsInChildren<Renderer>(true);
        for (int i = 0; i < rends.Length; i++)
        {
            if (rends[i] != null)
                rends[i].sharedMaterial = mat;
        }
    }

    void Log(string msg)
    {
        if (enableDebugLogs)
            Debug.Log($"[BatteryPoolSpawner] {msg}", this);
    }

    void OnDrawGizmosSelected()
    {
        if (spawnPoint == null) return;

        Gizmos.color = Color.cyan;
        Vector3 p = spawnPoint.TransformPoint(spawnOffsetLocal);
        Gizmos.DrawWireSphere(p, movedAwayDistance);
    }
}