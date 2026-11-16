using UnityEngine;
using System.Collections;

[RequireComponent(typeof(Renderer))]
public class TimedGlowStrip : MonoBehaviour
{
    [Header("Current/Start State")]
    public Color startColor = Color.green;
    public float startIntensity = 5f;

    [Header("Transition Settings (set these in Inspector)")]
    public Color targetColor = Color.blue;
    public float targetIntensity = 0.3f;
    public float durationSeconds = 5f;

    [Header("Pulse (optional)")]
    public bool pulse = false;
    public float pulseSpeed = 2f;
    public float pulseAmplitude = 1.5f;

    private Material mat;
    private Coroutine routine;

    private Color currentColor;
    private float currentIntensity;

    void Awake()
    {
        mat = GetComponent<Renderer>().material;
        mat.EnableKeyword("_EMISSION");

        currentColor = startColor;
        currentIntensity = startIntensity;
        ApplyEmission(currentColor, currentIntensity);
    }

    void Update()
    {
        if (!pulse) return;

        float p = Mathf.Sin(Time.time * pulseSpeed) * pulseAmplitude;
        ApplyEmission(currentColor, Mathf.Max(0f, currentIntensity + p));
    }

    // Call this from other scripts:
    public void TransitionTo(Color newColor, float newIntensity, float seconds)
    {
        if (routine != null) StopCoroutine(routine);
        routine = StartCoroutine(DoTransition(newColor, newIntensity, seconds));
    }

    // ✅ This one uses the Inspector values (targetColor etc.)
    public void TransitionToTarget()
    {
        TransitionTo(targetColor, targetIntensity, durationSeconds);
    }

    // ✅ Right-click component header → run from Inspector
    [ContextMenu("Run Transition To Target")]
    private void ContextMenu_RunTransition()
    {
        TransitionToTarget();
    }

    public void SetImmediate(Color c, float intensity)
    {
        if (routine != null) StopCoroutine(routine);
        currentColor = c;
        currentIntensity = intensity;
        ApplyEmission(currentColor, currentIntensity);
    }

    private IEnumerator DoTransition(Color newColor, float newIntensity, float seconds)
    {
        Color fromC = currentColor;
        float fromI = currentIntensity;

        if (seconds <= 0f)
        {
            currentColor = newColor;
            currentIntensity = newIntensity;
            ApplyEmission(currentColor, currentIntensity);
            yield break;
        }

        float t = 0f;
        while (t < 1f)
        {
            t += Time.deltaTime / seconds;

            // smooth ease-in-out
            float eased = t * t * (3f - 2f * t);

            currentColor = Color.Lerp(fromC, newColor, eased);
            currentIntensity = Mathf.Lerp(fromI, newIntensity, eased);

            if (!pulse)
                ApplyEmission(currentColor, currentIntensity);

            yield return null;
        }

        currentColor = newColor;
        currentIntensity = newIntensity;

        if (!pulse)
            ApplyEmission(currentColor, currentIntensity);

        routine = null;
    }

    private void ApplyEmission(Color c, float intensity)
    {
        mat.SetColor("_EmissionColor", c * intensity);
    }
}