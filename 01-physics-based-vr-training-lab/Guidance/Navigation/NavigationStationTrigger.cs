using UnityEngine;

[RequireComponent(typeof(Collider))]
public class NavigationStationTrigger : MonoBehaviour
{
    [Header("References")]
    public FloorPathRibbon navigationPath;
    public Transform xrRigRoot;

    [Header("Path Progress")]
    [Tooltip("When player enters this station, path will start from this waypoint index.")]
    public int newStartPointIndex = 0;

    [Header("Fallback Filter")]
    public string playerTag = "Player";

    [Header("One Shot")]
    public bool triggerOnlyOnce = true;

    bool _triggered;

    void Reset()
    {
        Collider col = GetComponent<Collider>();
        if (col != null)
            col.isTrigger = true;
    }

    void OnTriggerEnter(Collider other)
    {
        if (_triggered && triggerOnlyOnce)
            return;

        if (!IsXRPlayer(other))
            return;

        if (navigationPath == null)
        {
            Debug.LogWarning($"NavigationStationTrigger on {name} has no NavigationPath assigned.");
            return;
        }

        navigationPath.SetStartPointIndex(newStartPointIndex);
        _triggered = true;

        Debug.Log($"{name}: advanced path start to index {newStartPointIndex}");
    }

    bool IsXRPlayer(Collider other)
    {
        if (other == null)
            return false;

        if (xrRigRoot != null)
        {
            Transform t = other.transform;
            return t == xrRigRoot || t.IsChildOf(xrRigRoot);
        }

        Transform current = other.transform;
        while (current != null)
        {
            if (current.CompareTag(playerTag))
                return true;

            current = current.parent;
        }

        return false;
    }
}