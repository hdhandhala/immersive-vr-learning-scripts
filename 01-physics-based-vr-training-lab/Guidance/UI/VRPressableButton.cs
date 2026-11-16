using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class VRPressableButton : MonoBehaviour,
    IPointerEnterHandler,
    IPointerExitHandler,
    IPointerDownHandler,
    IPointerUpHandler
{
    [Header("References")]
    [SerializeField] private Graphic targetGraphic;
    [SerializeField] private RectTransform pressTarget;
    [SerializeField] private Button unityButton;
    [SerializeField] private VRUIButtonAction buttonAction;

    [Header("Press Motion")]
    [SerializeField] private float pressDepth = 6f;
    [SerializeField] private float pressSpeed = 18f;

    [Header("Visual")]
    [SerializeField] private float hoverBoost = 0.8f;
    [SerializeField] private float pressedDarken = 0.35f;

    [Header("Debug")]
    [SerializeField] private bool enableLogs = true;

    private Material runtimeMat;
    private Vector3 initialLocalPos;
    private Vector3 targetLocalPos;
    private bool isHovered;
    private bool isPressed;
    private bool pointerInside;

    private void Awake()
    {
        if (targetGraphic == null)
            targetGraphic = GetComponent<Graphic>();

        if (pressTarget == null)
            pressTarget = transform as RectTransform;

        if (unityButton == null)
            unityButton = GetComponent<Button>();

        if (buttonAction == null)
            buttonAction = GetComponent<VRUIButtonAction>();

        if (pressTarget != null)
        {
            initialLocalPos = pressTarget.localPosition;
            targetLocalPos = initialLocalPos;
        }

        if (targetGraphic != null && targetGraphic.material != null)
        {
            runtimeMat = new Material(targetGraphic.material);
            targetGraphic.material = runtimeMat;
        }
    }

    private void Update()
    {
        if (pressTarget != null)
        {
            pressTarget.localPosition = Vector3.Lerp(
                pressTarget.localPosition,
                targetLocalPos,
                Time.deltaTime * pressSpeed
            );
        }
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        pointerInside = true;
        isHovered = true;
        UpdateVisualState();

        if (enableLogs)
            Debug.Log($"[VRPressableButton] Pointer entered {name}", this);
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        pointerInside = false;
        isHovered = false;
        isPressed = false;
        targetLocalPos = initialLocalPos;
        UpdateVisualState();

        if (enableLogs)
            Debug.Log($"[VRPressableButton] Pointer exited {name}", this);
    }

    public void OnPointerDown(PointerEventData eventData)
    {
        isPressed = true;
        targetLocalPos = initialLocalPos + new Vector3(0f, 0f, pressDepth);
        UpdateVisualState();

        if (enableLogs)
            Debug.Log($"[VRPressableButton] Pointer down on {name}", this);
    }

    public void OnPointerUp(PointerEventData eventData)
    {
        bool shouldTrigger = isPressed && pointerInside;

        isPressed = false;
        targetLocalPos = initialLocalPos;
        UpdateVisualState();

        if (enableLogs)
            Debug.Log($"[VRPressableButton] Pointer up on {name} | Trigger = {shouldTrigger}", this);

        if (!shouldTrigger)
            return;

        // Call VRUIButtonAction directly
        if (buttonAction != null)
        {
            if (enableLogs)
                Debug.Log($"[VRPressableButton] Calling VRUIButtonAction on {name}", this);

            buttonAction.TriggerAction();
            return;
        }

        // Fallback: call Unity Button onClick if assigned
        if (unityButton != null)
        {
            if (enableLogs)
                Debug.Log($"[VRPressableButton] Invoking Unity Button.onClick on {name}", this);

            unityButton.onClick.Invoke();
            return;
        }

        Debug.LogWarning($"[VRPressableButton] No VRUIButtonAction or Button assigned on {name}", this);
    }

    private void UpdateVisualState()
    {
        if (runtimeMat == null)
            return;

        runtimeMat.SetFloat("_HoverBoost", isHovered ? hoverBoost : 0f);
        runtimeMat.SetFloat("_PressedDarken", isPressed ? pressedDarken : 0f);
    }
}