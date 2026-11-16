using UnityEngine;

public class EndSessionSequence : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private SimpleVRUIManager uiManager;
    [SerializeField] private GameObject trainingPanel;
    [SerializeField] private GameObject endPanel;
    [SerializeField] private GameObject normalButtonsGroup;

    [Header("Behavior")]
    [SerializeField] private bool hideNormalButtonsWhenEndPanelShows = true;
    [SerializeField] private bool closeMainUIOnResume = true;

    [Header("Debug")]
    [SerializeField] private bool enableLogs = true;

    private void Start()
    {
        if (endPanel != null)
            endPanel.SetActive(false);

        if (enableLogs)
            Debug.Log("[EndSessionSequence] Ready.", this);
    }

    public void ShowEndPanel()
    {
        if (trainingPanel != null)
            trainingPanel.SetActive(true);

        if (endPanel != null)
            endPanel.SetActive(true);
        else
            Debug.LogWarning("[EndSessionSequence] End Panel is not assigned.", this);

        if (hideNormalButtonsWhenEndPanelShows && normalButtonsGroup != null)
            normalButtonsGroup.SetActive(false);

        if (enableLogs)
            Debug.Log("[EndSessionSequence] End panel shown.", this);
    }

    public void HideEndPanel()
    {
        if (endPanel != null)
            endPanel.SetActive(false);

        if (normalButtonsGroup != null)
            normalButtonsGroup.SetActive(true);

        if (enableLogs)
            Debug.Log("[EndSessionSequence] End panel hidden.", this);
    }

    public void ResumeObserving()
    {
        HideEndPanel();

        if (closeMainUIOnResume && uiManager != null)
            uiManager.CloseCurrentZoneUIButAllowReopen();

        if (enableLogs)
            Debug.Log("[EndSessionSequence] Resume selected.", this);
    }

    public void ExitExperience()
    {
        if (enableLogs)
            Debug.Log("[EndSessionSequence] Exit selected.", this);

#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }
}