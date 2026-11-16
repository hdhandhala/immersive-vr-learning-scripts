using UnityEngine;

[RequireComponent(typeof(LineRenderer))]
public class GlowingNavigationLine : MonoBehaviour
{
    public Transform[] pathPoints;
    public bool animate = true;
    public float animSpeed = 0.5f;
    public Color glowColor = Color.cyan;
    public float lineHeight = 0.25f;
    public float lineWidth = 0.2f;

    private LineRenderer line;
    private float animTime;

    void Start()
    {
        line = GetComponent<LineRenderer>();

        if (pathPoints.Length < 2)
        {
            Debug.LogWarning("Not enough points for navigation line!");
            return;
        }

        // Setup Line Renderer
        line.positionCount = pathPoints.Length;
        line.startWidth = lineWidth;
        line.endWidth = lineWidth;
        line.textureMode = LineTextureMode.Tile;

        for (int i = 0; i < pathPoints.Length; i++)
        {
            Vector3 pos = pathPoints[i].position;
            pos.y = lineHeight;
            line.SetPosition(i, pos);
        }
    }

    void Update()
    {
        if (animate)
        {
            AnimateGlow();
        }
    }

    void AnimateGlow()
    {
        Gradient newGrad = new Gradient();

        GradientColorKey[] colorKeys = new GradientColorKey[1];
        GradientAlphaKey[] alphaKeys = new GradientAlphaKey[2];

        colorKeys[0] = new GradientColorKey(glowColor, 0f);

        alphaKeys[0] = new GradientAlphaKey(0.2f, animTime);
        alphaKeys[1] = new GradientAlphaKey(1f, 1f);

        newGrad.SetKeys(colorKeys, alphaKeys);
        newGrad.mode = GradientMode.Blend;

        line.colorGradient = newGrad;
        animTime += (Time.unscaledDeltaTime * animSpeed);

        if (animTime >= 1f)
            animTime = 0f;
    }
}
