using UnityEngine;

[RequireComponent(typeof(Collider))]
public class BatteryCellTrigger : MonoBehaviour
{
    public PlacementGridManager gridManager;
    public string batteryTag = "Battery";
    public bool ignoreTriggerColliders = true;
    public bool logTriggerEvents = false;

    Collider _col;

    void Awake()
    {
        _col = GetComponent<Collider>();
        _col.isTrigger = true;

        if (gridManager == null)
            gridManager = GetComponentInParent<PlacementGridManager>();
    }

    void OnTriggerEnter(Collider other)
    {
        if (!IsValidBattery(other))
            return;

        if (logTriggerEvents)
            Debug.Log($"[BatteryCellTrigger:{name}] ENTER -> {other.name}", this);

        gridManager.NotifyBatteryEnteredCell(this, other);
    }

    void OnTriggerExit(Collider other)
    {
        if (!IsValidBattery(other))
            return;

        if (logTriggerEvents)
            Debug.Log($"[BatteryCellTrigger:{name}] EXIT -> {other.name}", this);

        gridManager.NotifyBatteryExitedCell(this, other);
    }

    bool IsValidBattery(Collider other)
    {
        if (gridManager == null || other == null)
            return false;

        if (ignoreTriggerColliders && other.isTrigger)
            return false;

        bool tagOk = other.CompareTag(batteryTag) || other.transform.root.CompareTag(batteryTag);
        if (!tagOk)
            return false;

        if (other.transform.root == transform.root)
            return false;

        return true;
    }
}