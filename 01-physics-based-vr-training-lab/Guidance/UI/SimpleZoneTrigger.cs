using UnityEngine;
using UnityEngine.Video;

[RequireComponent(typeof(Collider))]
public class SimpleZoneTrigger : MonoBehaviour
{
    public enum VideoBackgroundMode
    {
        WhiteKey,
        GreenKey
    }

    [Header("Player Detection")]
    [SerializeField] private Transform xrRigRoot;
    [SerializeField] private string playerTag = "Player";

    [Header("Zone Info")]
    public int stepIndex = 0;

    [Header("Content")]
    public GameObject cardToShow;
    public VideoClip videoClip;

    [Header("Video Background")]
    public VideoBackgroundMode videoBackgroundMode = VideoBackgroundMode.WhiteKey;

    [Header("Optional Anchors")]
    public Transform panelAnchor;
    public Transform hologramAnchor;

    [Header("Presentation Extra Panel")]
    public bool useExtraPresentationPanel = false;
    public Transform extraHologramAnchor;
    public VideoClip extraVideoClip;
    public bool autoPlayExtraVideo = true;

    [Header("Manager")]
    public SimpleVRUIManager uiManager;

    [Header("Behavior")]
    [SerializeField] private bool hideWhenPlayerLeavesZone = false;

    [Header("Debug")]
    [SerializeField] private bool enableLogs = true;

    private int playerInsideCount = 0;

    private void Reset()
    {
        Collider col = GetComponent<Collider>();
        col.isTrigger = true;
    }

    private void OnTriggerEnter(Collider other)
    {
        if (!IsPlayerCollider(other))
            return;

        playerInsideCount++;

        if (enableLogs)
            Debug.Log($"[ZoneTrigger] ENTER '{name}' by {other.name}", this);

        if (uiManager != null)
            uiManager.ShowZone(this);
    }

    private void OnTriggerExit(Collider other)
    {
        if (!IsPlayerCollider(other))
            return;

        playerInsideCount = Mathf.Max(0, playerInsideCount - 1);

        if (enableLogs)
            Debug.Log($"[ZoneTrigger] EXIT '{name}' by {other.name}", this);

        if (hideWhenPlayerLeavesZone && playerInsideCount == 0 && uiManager != null)
            uiManager.HideZone(this);
    }

    private bool IsPlayerCollider(Collider other)
    {
        if (other == null)
            return false;

        if (xrRigRoot != null)
        {
            if (other.transform == xrRigRoot || other.transform.IsChildOf(xrRigRoot))
                return true;
        }

        if (other.CompareTag(playerTag))
            return true;

        if (other.transform.root != null && other.transform.root.CompareTag(playerTag))
            return true;

        return false;
    }
}