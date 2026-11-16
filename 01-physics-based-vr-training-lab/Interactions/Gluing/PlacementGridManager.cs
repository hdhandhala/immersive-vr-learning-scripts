using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine.XR.Interaction.Toolkit.Interactors;

public class PlacementGridManager : MonoBehaviour
{
    [Header("Grid Visuals")]
    public List<Renderer> gridTiles = new List<Renderer>();

    [Header("Shader Properties")]
    public string colorProperty = "_ChipColor";
    public string glowProperty = "_GlowIntensity";

    [Header("Colors")]
    public Color idleGreen = new Color(0f, 1f, 0.7f, 1f);
    public Color badRed = new Color(1f, 0.1f, 0.1f, 1f);
    public Color offColor = Color.black;

    [Header("Glow")]
    public float idleGlowMin = 0.6f;
    public float idleGlowMax = 1.6f;
    public float badGlowMin = 0.0f;
    public float badGlowMax = 2.4f;
    public float readyGlow = 0f;
    public float successGlow = 0f;

    [Header("Animation")]
    public float idlePulseSpeed = 2.0f;
    public float badBlinkSpeed = 5.0f;

    [Header("Validation")]
    [Tooltip("Turn black only when glue amount is enough AND glued side is facing bottom.")]
    public bool requireBottomFacingForReady = true;

    [Header("Battery Locking")]
    [Tooltip("When placement is valid, battery becomes ungrabbable and stays fixed at its current pose.")]
    public bool lockBatteryWhenReady = true;

    [Tooltip("Keep the battery grabbable while glue is still wet.")]
    public bool allowGrabWhileGlueWet = true;

    [Tooltip("Disable XR grab when finally locked.")]
    public bool disableGrabWhenLocked = true;

    [Tooltip("Make Rigidbody kinematic when locked.")]
    public bool makeKinematicWhenLocked = true;

    [Tooltip("Keep enforcing the locked pose every frame so the battery cannot drift.")]
    public bool enforceLockedPose = true;

    [Tooltip("Parent locked battery to this grid.")]
    public bool parentLockedBatteryToGrid = false;

    [Tooltip("If true, success state also stays black.")]
    public bool turnBlackOnSuccess = true;

    [Header("Ready Haptics")]
    [Tooltip("Very subtle repeating haptic while battery is in perfect placement and still wet/grabbable.")]
    public bool enableReadyPlacementHaptics = true;

    [Range(0f, 1f)]
    public float readyHapticAmplitude = 0.06f;

    public float readyHapticDuration = 0.03f;

    [Tooltip("Seconds between subtle haptic pulses.")]
    public float readyHapticInterval = 0.18f;

    [Header("Debug")]
    public bool autoCollectTileRenderers = true;
    public bool logStateChanges = false;
    public bool logBatteryLocking = false;
    public bool logDrying = false;

    enum GridState
    {
        Idle,
        MissingGlue,
        WrongOrientation,
        Ready,
        Success
    }

    GridState _state = GridState.Idle;

    MaterialPropertyBlock _mpb;
    int _colorID;
    int _glowID;

    readonly HashSet<BatteryCellTrigger> _activeCells = new HashSet<BatteryCellTrigger>();

    Transform _currentBatteryRoot;
    Collider _currentBatteryCollider;
    BatteryGlueState _currentBatteryGlue;
    BatteryGrabKeeper _currentBatteryGrabKeeper;
    XRGrabInteractable _currentBatteryGrab;
    Rigidbody _currentBatteryRb;

    bool _successLocked = false;
    bool _batteryLocked = false;

    Vector3 _lockedWorldPosition;
    Quaternion _lockedWorldRotation;

    float _readyHapticTimer = 0f;

    void Awake()
    {
        _mpb = new MaterialPropertyBlock();
        _colorID = Shader.PropertyToID(colorProperty);
        _glowID = Shader.PropertyToID(glowProperty);

        if (autoCollectTileRenderers && gridTiles.Count == 0)
            gridTiles.AddRange(GetComponentsInChildren<Renderer>(true));

        SetState(GridState.Idle, true, "Awake");
    }

    void Update()
    {
        if (!_successLocked)
            EvaluateCurrentBatteryState();

        AnimateVisuals();
        UpdateReadyPlacementHaptics();
    }

    void LateUpdate()
    {
        if (!_batteryLocked || !enforceLockedPose || _currentBatteryRoot == null)
            return;

        _currentBatteryRoot.SetPositionAndRotation(_lockedWorldPosition, _lockedWorldRotation);
    }

    public void NotifyBatteryEnteredCell(BatteryCellTrigger cell, Collider other)
    {
        if (_successLocked || other == null)
            return;

        Transform root = other.transform.root;

        if (_currentBatteryRoot != null && _currentBatteryRoot != root)
            return;

        _activeCells.Add(cell);

        _currentBatteryCollider = other;
        _currentBatteryRoot = root;
        _currentBatteryGlue = root.GetComponent<BatteryGlueState>();
        _currentBatteryGrabKeeper = root.GetComponent<BatteryGrabKeeper>();
        _currentBatteryGrab = root.GetComponent<XRGrabInteractable>();
        _currentBatteryRb = root.GetComponent<Rigidbody>();

        _readyHapticTimer = 0f;

        EvaluateCurrentBatteryState();
    }

    public void NotifyBatteryExitedCell(BatteryCellTrigger cell, Collider other)
    {
        if (_successLocked)
            return;

        _activeCells.Remove(cell);

        if (_batteryLocked)
            return;

        if (_activeCells.Count == 0)
        {
            ClearCurrentBatteryRefs();
            _readyHapticTimer = 0f;
            SetState(GridState.Idle, false, "Battery left grid");
        }
    }

    void EvaluateCurrentBatteryState()
    {
        if (_batteryLocked)
            return;

        if (_currentBatteryRoot == null || _currentBatteryCollider == null || _activeCells.Count == 0)
        {
            SetState(GridState.Idle, false, "No battery");
            return;
        }

        if (_currentBatteryGlue == null)
        {
            SetState(GridState.MissingGlue, false, "Battery has no BatteryGlueState");
            return;
        }

        if (!_currentBatteryGlue.HasEnoughGlue)
        {
            SetState(GridState.MissingGlue, false, "Battery missing glue");
            return;
        }

        if (requireBottomFacingForReady)
        {
            bool correctBottom = _currentBatteryGlue.IsGluedSideFacingBottomRelativeTo(transform);
            if (!correctBottom)
            {
                SetState(GridState.WrongOrientation, false, "Enough glue but wrong orientation");
                return;
            }
        }

        SetState(GridState.Ready, false, "Enough glue and correct bottom orientation");

        if (!_currentBatteryGlue.IsDrying)
        {
            _currentBatteryGlue.StartDrying();

            if (logDrying)
                Debug.Log($"[PlacementGridManager:{name}] Started drying for {_currentBatteryRoot.name}. " +
                          $"Will lock after {_currentBatteryGlue.glueDrySeconds:F1}s", this);
        }

        if (allowGrabWhileGlueWet && !_currentBatteryGlue.IsDry)
        {
            KeepBatteryGrabbableWhileWet();
            return;
        }

        if (lockBatteryWhenReady)
        {
            LockCurrentBattery("Placement valid and glue dried");
            NotifyPlacementSuccess();
        }
    }

    void UpdateReadyPlacementHaptics()
    {
        if (!enableReadyPlacementHaptics)
            return;

        if (_state != GridState.Ready)
        {
            _readyHapticTimer = 0f;
            return;
        }

        if (_currentBatteryGrab == null || !_currentBatteryGrab.isSelected)
        {
            _readyHapticTimer = 0f;
            return;
        }

        if (_batteryLocked)
        {
            _readyHapticTimer = 0f;
            return;
        }

        _readyHapticTimer += Time.deltaTime;
        if (_readyHapticTimer < Mathf.Max(0.02f, readyHapticInterval))
            return;

        _readyHapticTimer = 0f;
        SendReadyPlacementHaptic();
    }

    void SendReadyPlacementHaptic()
    {
        if (_currentBatteryGrab == null)
            return;

        IXRSelectInteractor interactor = _currentBatteryGrab.firstInteractorSelecting;
        if (interactor == null)
            return;

        ActionBasedController controller = (interactor as Component)?.GetComponentInParent<ActionBasedController>();
        if (controller == null)
            return;

        controller.SendHapticImpulse(readyHapticAmplitude, readyHapticDuration);
    }

    void KeepBatteryGrabbableWhileWet()
    {
        if (_currentBatteryGrabKeeper != null)
        {
            _currentBatteryGrabKeeper.SetPermanentLock(false);
            _currentBatteryGrabKeeper.ForceRefreshNow();
        }
        else
        {
            if (_currentBatteryGrab != null && !_currentBatteryGrab.enabled)
                _currentBatteryGrab.enabled = true;

            if (_currentBatteryRb != null)
            {
                _currentBatteryRb.isKinematic = true;
                _currentBatteryRb.useGravity = true;
                _currentBatteryRb.detectCollisions = true;
            }
        }

        if (logDrying && _currentBatteryGlue != null)
        {
            Debug.Log($"[PlacementGridManager:{name}] Battery still wet -> grabbable for " +
                      $"{_currentBatteryGlue.SecondsRemainingToDry:F1}s", this);
        }
    }

    public bool CurrentBatteryHasGlue()
    {
        return _currentBatteryGlue != null && _currentBatteryGlue.HasEnoughGlue;
    }

    public bool CurrentBatteryIsCorrectlyBottomOriented()
    {
        if (_currentBatteryGlue == null)
            return false;

        return _currentBatteryGlue.IsGluedSideFacingBottomRelativeTo(transform);
    }

    public void NotifyPlacementSuccess()
    {
        _successLocked = true;
        _readyHapticTimer = 0f;
        SetState(GridState.Success, true, "Placement success");
    }

    public void ResetGrid()
    {
        _activeCells.Clear();

        if (_currentBatteryGrabKeeper != null)
            _currentBatteryGrabKeeper.SetPermanentLock(false);

        ClearCurrentBatteryRefs();

        _successLocked = false;
        _batteryLocked = false;
        _lockedWorldPosition = Vector3.zero;
        _lockedWorldRotation = Quaternion.identity;
        _readyHapticTimer = 0f;

        SetState(GridState.Idle, true, "Reset");
    }

    void LockCurrentBattery(string reason)
    {
        if (_batteryLocked || _currentBatteryRoot == null)
            return;

        if (_currentBatteryGrab != null && _currentBatteryGrab.isSelected)
        {
            var interactionManager = _currentBatteryGrab.interactionManager;
            IXRSelectInteractor interactor = _currentBatteryGrab.firstInteractorSelecting;

            if (interactionManager != null && interactor != null)
                interactionManager.SelectExit(interactor, _currentBatteryGrab);
        }

        _lockedWorldPosition = _currentBatteryRoot.position;
        _lockedWorldRotation = _currentBatteryRoot.rotation;

        if (_currentBatteryRb != null)
        {
            if (!_currentBatteryRb.isKinematic)
            {
                _currentBatteryRb.linearVelocity = Vector3.zero;
                _currentBatteryRb.angularVelocity = Vector3.zero;
            }

            if (makeKinematicWhenLocked)
                _currentBatteryRb.isKinematic = true;
        }

        if (_currentBatteryGrabKeeper != null)
        {
            _currentBatteryGrabKeeper.SetPermanentLock(true);
        }
        else if (disableGrabWhenLocked && _currentBatteryGrab != null)
        {
            _currentBatteryGrab.enabled = false;
        }

        if (parentLockedBatteryToGrid && _currentBatteryRoot != null)
            _currentBatteryRoot.SetParent(transform, true);

        _batteryLocked = true;
        _readyHapticTimer = 0f;

        if (logBatteryLocking)
            Debug.Log($"[PlacementGridManager:{name}] Battery LOCKED | {reason}", this);
    }

    void ClearCurrentBatteryRefs()
    {
        _currentBatteryRoot = null;
        _currentBatteryCollider = null;
        _currentBatteryGlue = null;
        _currentBatteryGrabKeeper = null;
        _currentBatteryGrab = null;
        _currentBatteryRb = null;
    }

    void SetState(GridState newState, bool force, string reason)
    {
        if (!force && _state == newState)
            return;

        _state = newState;

        if (logStateChanges)
            Debug.Log($"[PlacementGridManager:{name}] STATE -> {_state} | {reason}", this);
    }

    void AnimateVisuals()
    {
        Color color = idleGreen;
        float glow = idleGlowMax;

        switch (_state)
        {
            case GridState.Idle:
                {
                    float t = 0.5f + 0.5f * Mathf.Sin(Time.time * idlePulseSpeed * Mathf.PI * 2f);
                    glow = Mathf.Lerp(idleGlowMin, idleGlowMax, t);
                    color = idleGreen;
                    break;
                }

            case GridState.MissingGlue:
                {
                    float t = 0.5f + 0.5f * Mathf.Sin(Time.time * badBlinkSpeed * Mathf.PI * 2f);
                    glow = Mathf.Lerp(badGlowMin, badGlowMax, t);
                    color = badRed;
                    break;
                }

            case GridState.WrongOrientation:
                {
                    float t = 0.5f + 0.5f * Mathf.Sin(Time.time * idlePulseSpeed * Mathf.PI * 2f);
                    glow = Mathf.Lerp(idleGlowMin, idleGlowMax, t);
                    color = idleGreen;
                    break;
                }

            case GridState.Ready:
                {
                    color = offColor;
                    glow = readyGlow;
                    break;
                }

            case GridState.Success:
                {
                    if (turnBlackOnSuccess)
                    {
                        color = offColor;
                        glow = successGlow;
                    }
                    else
                    {
                        color = idleGreen;
                        glow = 0f;
                    }
                    break;
                }
        }

        for (int i = 0; i < gridTiles.Count; i++)
        {
            Renderer r = gridTiles[i];
            if (!r) continue;

            r.GetPropertyBlock(_mpb);
            _mpb.SetColor(_colorID, color);
            _mpb.SetFloat(_glowID, glow);
            r.SetPropertyBlock(_mpb);
        }
    }
}