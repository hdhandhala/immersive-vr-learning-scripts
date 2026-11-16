using UnityEngine;

public class BatteryGlueState : MonoBehaviour
{
    [Header("Glue Requirement")]
    [Tooltip("How many counted glue deposits/segments are needed before this battery is considered properly glued.")]
    public int requiredSegmentCount = 12;

    [Tooltip("Minimum world-space distance before another glue deposit can be counted.")]
    public float minDistanceBetweenCountedDeposits = 0.004f;

    [Header("Glue Drying")]
    [Tooltip("How long the glue stays wet after valid placement begins.")]
    public float glueDrySeconds = 20f;

    [Header("Glued Side Orientation")]
    [Tooltip("Child transform placed on glued face. Its UP axis must point outward from the glued side.")]
    public Transform glueSideMarker;

    [Tooltip("How well the glued face must point toward the grid underside direction. 1 = exact, 0 = any.")]
    [Range(0f, 1f)]
    public float bottomFacingDotThreshold = 0.75f;

    [Header("Debug")]
    public bool logGlue = false;

    int _currentSegmentCount = 0;
    bool _hasEnoughGlue = false;

    bool _hasLastCountedPoint = false;
    Vector3 _lastCountedPoint;

    bool _isDrying = false;
    float _dryStartTime = -1f;

    public bool HasGlue => _hasEnoughGlue;
    public bool HasEnoughGlue => _hasEnoughGlue;
    public int CurrentSegmentCount => _currentSegmentCount;
    public float Progress01 => requiredSegmentCount <= 0 ? 1f : Mathf.Clamp01((float)_currentSegmentCount / requiredSegmentCount);

    public bool IsDrying => _isDrying;
    public bool IsDry
    {
        get
        {
            if (!_isDrying) return false;
            return Time.time >= _dryStartTime + Mathf.Max(0.01f, glueDrySeconds);
        }
    }

    public float DryProgress01
    {
        get
        {
            if (!_isDrying) return 0f;
            float dur = Mathf.Max(0.01f, glueDrySeconds);
            return Mathf.Clamp01((Time.time - _dryStartTime) / dur);
        }
    }

    public float SecondsRemainingToDry
    {
        get
        {
            if (!_isDrying) return glueDrySeconds;
            return Mathf.Max(0f, (_dryStartTime + Mathf.Max(0.01f, glueDrySeconds)) - Time.time);
        }
    }

    public void RegisterGlueSegment(Vector3 worldPoint)
    {
        if (_hasEnoughGlue)
            return;

        if (!_hasLastCountedPoint)
        {
            CountDeposit(worldPoint);
            return;
        }

        float dist = Vector3.Distance(_lastCountedPoint, worldPoint);
        if (dist < minDistanceBetweenCountedDeposits)
            return;

        CountDeposit(worldPoint);
    }

    void CountDeposit(Vector3 worldPoint)
    {
        _currentSegmentCount++;
        _lastCountedPoint = worldPoint;
        _hasLastCountedPoint = true;

        if (logGlue)
            Debug.Log($"[BatteryGlueState:{name}] counted deposit -> {_currentSegmentCount}/{requiredSegmentCount}", this);

        if (_currentSegmentCount >= requiredSegmentCount)
        {
            _hasEnoughGlue = true;

            if (logGlue)
                Debug.Log($"[BatteryGlueState:{name}] GLUE SATISFIED", this);
        }
    }

    public bool IsGluedSideFacingBottomRelativeTo(Transform gridTransform)
    {
        if (glueSideMarker == null || gridTransform == null)
            return false;

        Vector3 gluedFaceOutward = glueSideMarker.up.normalized;
        Vector3 desiredDirection = (-gridTransform.up).normalized;

        float dot = Vector3.Dot(gluedFaceOutward, desiredDirection);
        return dot >= bottomFacingDotThreshold;
    }

    public void StartDrying()
    {
        if (!_hasEnoughGlue)
            return;

        if (_isDrying)
            return;

        _isDrying = true;
        _dryStartTime = Time.time;

        if (logGlue)
            Debug.Log($"[BatteryGlueState:{name}] Drying started. Duration={glueDrySeconds:F1}s", this);
    }

    public void StopDrying()
    {
        _isDrying = false;
        _dryStartTime = -1f;

        if (logGlue)
            Debug.Log($"[BatteryGlueState:{name}] Drying stopped.", this);
    }

    public void ResetGlue()
    {
        _currentSegmentCount = 0;
        _hasEnoughGlue = false;
        _hasLastCountedPoint = false;
        _lastCountedPoint = Vector3.zero;

        _isDrying = false;
        _dryStartTime = -1f;

        if (logGlue)
            Debug.Log($"[BatteryGlueState:{name}] ResetGlue()", this);
    }
}