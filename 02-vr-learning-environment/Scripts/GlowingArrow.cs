using UnityEngine;

[RequireComponent(typeof(Renderer))]
public class GlowingArrow : MonoBehaviour
{
    [Header("Glow Settings")]
    public Color glowColor = Color.white;
    public float glowIntensity = 1.5f; // Max glow intensity
    public float pulseSpeed = 2f;       // How fast it pulses

    private Material material;
    private Color baseEmissionColor;

    void Start()
    {
        // Get the material
        material = GetComponent<Renderer>().material;

        // Enable emission keyword if not already
        material.EnableKeyword("_EMISSION");

        // Set base emission color
        baseEmissionColor = glowColor;
    }

    void Update()
    {
        // Calculate pulsing intensity
        float emission = (Mathf.Sin(Time.time * pulseSpeed) + 1f) / 2f; // range 0 to 1
        emission *= glowIntensity;

        // Apply emission color
        Color finalEmissionColor = baseEmissionColor * emission;
        material.SetColor("_EmissionColor", finalEmissionColor);
    }
}
