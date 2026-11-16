using UnityEngine;

public class VRUIButtonAction : MonoBehaviour
{
    public enum ButtonType
    {
        Backward,
        PlayPause,
        Forward,
        Reset,
        Done
    }

    [SerializeField] private ButtonType buttonType;
    [SerializeField] private SimpleVRUIManager uiManager;

    public void TriggerAction()
    {
        if (uiManager == null)
        {
            Debug.LogWarning($"{name}: VRUIManager is not assigned.");
            return;
        }

        switch (buttonType)
        {
            case ButtonType.Backward:
                uiManager.BackwardTutorial();
                break;

            case ButtonType.PlayPause:
                uiManager.TogglePlayPause();
                break;

            case ButtonType.Forward:
                uiManager.ForwardTutorial();
                break;

            case ButtonType.Done:
                uiManager.MarkCurrentStepDone();
                break;
        }
    }
}