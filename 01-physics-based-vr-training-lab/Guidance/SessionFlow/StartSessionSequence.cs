using UnityEngine;

public class StartSessionSequence : MonoBehaviour
{
    [Header("UI")]
    [SerializeField] private GameObject startButton;
    [SerializeField] private GameObject buttonsGroup;

    [Header("Optional Panels")]
    [SerializeField] private GameObject introPanel;
    [SerializeField] private GameObject trainingPanel;

    [Header("XR Rig")]
    [SerializeField] private Transform xrRigRoot;
    [SerializeField] private Transform xrCamera;
    [SerializeField] private Transform sessionSpawnPoint;

    [Header("Behavior")]
    [SerializeField] private bool hideIntroPanelOnStart = true;
    [SerializeField] private bool showTrainingPanelOnStart = true;
    [SerializeField] private bool hideTrainingButtonsAtStart = true;

    [Header("Debug")]
    [SerializeField] private bool enableLogs = true;

    private bool sessionStarted = false;

    private void Start()
    {
        if (startButton != null)
            startButton.SetActive(true);

        if (buttonsGroup != null && hideTrainingButtonsAtStart)
            buttonsGroup.SetActive(false);

        if (introPanel != null)
            introPanel.SetActive(true);

        if (trainingPanel != null && showTrainingPanelOnStart)
            trainingPanel.SetActive(false);

        if (enableLogs)
            Debug.Log("[StartSessionSequence] Waiting for Start Session button.", this);
    }

    public void StartSession()
    {
        if (sessionStarted)
            return;

        sessionStarted = true;

        if (enableLogs)
            Debug.Log("[StartSessionSequence] Start Session pressed.", this);

        // UI switching
        if (startButton != null)
            startButton.SetActive(false);

        if (buttonsGroup != null)
            buttonsGroup.SetActive(true);

        if (hideIntroPanelOnStart && introPanel != null)
            introPanel.SetActive(false);

        if (showTrainingPanelOnStart && trainingPanel != null)
            trainingPanel.SetActive(true);

        // Accurate XR teleport
        TeleportRigSoHeadMatchesSpawn();
    }

    private void TeleportRigSoHeadMatchesSpawn()
    {
        if (xrRigRoot == null)
        {
            Debug.LogWarning("[StartSessionSequence] XR Rig Root is missing.", this);
            return;
        }

        if (xrCamera == null)
        {
            Debug.LogWarning("[StartSessionSequence] XR Camera is missing.", this);
            return;
        }

        if (sessionSpawnPoint == null)
        {
            Debug.LogWarning("[StartSessionSequence] Session Spawn Point is missing.", this);
            return;
        }

        // Current headset offset from rig root
        Vector3 cameraOffset = xrCamera.position - xrRigRoot.position;

        // We usually want only horizontal offset correction
        cameraOffset.y = 0f;

        // Move rig so headset ends exactly at spawn point
        xrRigRoot.position = sessionSpawnPoint.position - cameraOffset;

        // Optional: match facing direction
        Vector3 currentForward = xrCamera.forward;
        currentForward.y = 0f;

        Vector3 targetForward = sessionSpawnPoint.forward;
        targetForward.y = 0f;

        if (currentForward.sqrMagnitude > 0.0001f && targetForward.sqrMagnitude > 0.0001f)
        {
            float angle = Vector3.SignedAngle(currentForward, targetForward, Vector3.up);
            xrRigRoot.RotateAround(xrCamera.position, Vector3.up, angle);
        }

        if (enableLogs)
        {
            Debug.Log("[StartSessionSequence] Player teleported inside environment accurately.", this);
        }
    }
}