using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
public class GlueStrandMesh_Final : MonoBehaviour
{
    [Header("Surface Irregularity")]
    [Range(0f, 1f)] public float surfaceTensionNoise = 0.010f;
    public float noiseScale = 1.20f;
    public float wobbleSpeed = 0.10f;

    [Header("Rounded Ends")]
    public float startDropRadiusMul = 1.12f;
    public float endDropRadiusMul = 1.06f;
    public float startCapLength = 0.00115f;
    public float endCapLength = 0.00105f;
    [Range(3, 16)] public int capSegments = 8;

    [Header("Cap Shape Tuning")]
    [Range(0.25f, 1.25f)] public float capShapePower = 0.58f;
    [Range(0.7f, 1.1f)] public float edgeBodyBlend = 0.88f;

    [Header("Cooling")]
    public Material wetMaterial;
    public Material dryMaterial;
    public float hardenSeconds = 20f;

    MeshFilter _mf;
    MeshRenderer _mr;
    Mesh _mesh;
    Material _instMat;

    readonly List<Vector3> _pts = new();
    readonly List<Vector3> _nrm = new();
    readonly List<float> _rad = new();

    float _birthTime;
    float _minPointDist = 0.0035f;
    int _sides = 10;

    public Vector3 LastPointWorld { get; private set; }
    public float LastRadius { get; private set; }
    public float AgeSeconds => Time.time - _birthTime;

    static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
    static readonly int SmoothnessId = Shader.PropertyToID("_Smoothness");

    public void Configure(float minPointDistance, int sides, Material wet, Material dry, float harden)
    {
        _minPointDist = Mathf.Max(0.00035f, minPointDistance);
        _sides = Mathf.Clamp(sides, 3, 24);
        wetMaterial = wet;
        dryMaterial = dry;
        hardenSeconds = Mathf.Max(0.01f, harden);

        Ensure();

        if (_instMat != null)
            Destroy(_instMat);

        if (wetMaterial != null)
        {
            _instMat = new Material(wetMaterial);
            _mr.sharedMaterial = _instMat;
        }
    }

    public void Begin()
    {
        _birthTime = Time.time;
        _pts.Clear();
        _nrm.Clear();
        _rad.Clear();

        Ensure();

        if (_mesh != null)
            _mesh.Clear();

        LastRadius = 0.0012f;
    }

    public void AddPoint(Vector3 worldPos, Vector3 worldNormal, float radius)
    {
        radius = Mathf.Max(0.0001f, radius);
        worldNormal = worldNormal.sqrMagnitude > 0.0001f ? worldNormal.normalized : Vector3.up;

        if (_pts.Count > 0)
        {
            float d = Vector3.Distance(_pts[_pts.Count - 1], worldPos);
            if (d < _minPointDist)
                return;
        }

        _pts.Add(worldPos);
        _nrm.Add(worldNormal);
        _rad.Add(radius);

        LastPointWorld = worldPos;
        LastRadius = radius;

        if (_pts.Count >= 2)
            RebuildMesh();
    }

    public void End()
    {
        if (_pts.Count >= 2)
            RebuildMesh();
    }

    void Awake()
    {
        Ensure();
    }

    void Ensure()
    {
        if (_mf == null) _mf = GetComponent<MeshFilter>();
        if (_mr == null) _mr = GetComponent<MeshRenderer>();

        if (_mesh == null)
        {
            _mesh = new Mesh();
            _mesh.name = "GlueStrandMesh";
            _mesh.MarkDynamic();
            _mf.sharedMesh = _mesh;
        }
    }

    void Update()
    {
        if (_instMat == null)
            return;

        float t = hardenSeconds <= 0.001f ? 1f : Mathf.Clamp01((Time.time - _birthTime) / hardenSeconds);

        if (_instMat.HasProperty("_DryProgress"))
            _instMat.SetFloat("_DryProgress", t);

        if (_instMat.HasProperty(BaseColorId))
        {
            Color c = _instMat.GetColor(BaseColorId);
            c.a = Mathf.Lerp(0.55f, 0.92f, t);
            _instMat.SetColor(BaseColorId, c);
        }

        if (_instMat.HasProperty(SmoothnessId))
            _instMat.SetFloat(SmoothnessId, Mathf.Lerp(0.92f, 0.35f, t));
    }

    struct RingData
    {
        public Vector3 pos;
        public Vector3 normal;
        public Vector3 tangent;
        public float radius;
        public float v;
    }

    void RebuildMesh()
    {
        if (_pts.Count < 2)
            return;

        Ensure();

        List<RingData> rings = BuildRings();
        if (rings.Count < 2)
            return;

        int ringCount = rings.Count;
        int vertsPerRing = _sides;
        int vertCount = ringCount * vertsPerRing;
        int triCount = (ringCount - 1) * vertsPerRing * 6;

        Vector3[] verts = new Vector3[vertCount];
        Vector2[] uvs = new Vector2[vertCount];
        int[] tris = new int[triCount];

        for (int i = 0; i < ringCount; i++)
        {
            RingData ring = rings[i];

            Vector3 tangent = ring.tangent.sqrMagnitude > 0.000001f ? ring.tangent.normalized : Vector3.forward;
            Vector3 right = Vector3.Cross(ring.normal, tangent);

            if (right.sqrMagnitude < 1e-6f)
                right = Vector3.Cross(Vector3.up, tangent);

            if (right.sqrMagnitude < 1e-6f)
                right = Vector3.right;

            right.Normalize();
            Vector3 up = Vector3.Cross(tangent, right).normalized;

            for (int s = 0; s < _sides; s++)
            {
                float a = (s / (float)_sides) * Mathf.PI * 2f;

                float wob = Mathf.Sin((_birthTime * 0.4f) + i * 0.3f + s * 0.5f);
                float pn = Mathf.PerlinNoise(i * 0.15f * noiseScale, s * 0.20f * noiseScale);
                float noise = (pn - 0.5f) * 2f;

                float rr = ring.radius * (1f + surfaceTensionNoise * noise + surfaceTensionNoise * 0.5f * wob);
                rr = Mathf.Max(0.00001f, rr);

                Vector3 offset = (Mathf.Cos(a) * right + Mathf.Sin(a) * up) * rr;
                int idx = i * vertsPerRing + s;

                verts[idx] = transform.InverseTransformPoint(ring.pos + offset);
                uvs[idx] = new Vector2(s / (float)_sides, ring.v);
            }
        }

        int t = 0;
        for (int i = 0; i < ringCount - 1; i++)
        {
            int a0 = i * vertsPerRing;
            int b0 = (i + 1) * vertsPerRing;

            for (int s = 0; s < vertsPerRing; s++)
            {
                int s0 = s;
                int s1 = (s + 1) % vertsPerRing;

                int i0 = a0 + s0;
                int i1 = a0 + s1;
                int i2 = b0 + s0;
                int i3 = b0 + s1;

                tris[t++] = i0; tris[t++] = i3; tris[t++] = i2;
                tris[t++] = i0; tris[t++] = i1; tris[t++] = i3;
            }
        }

        _mesh.Clear(false);
        _mesh.vertices = verts;
        _mesh.uv = uvs;
        _mesh.triangles = tris;
        _mesh.RecalculateNormals();
        _mesh.RecalculateBounds();
    }

    List<RingData> BuildRings()
    {
        List<RingData> rings = new List<RingData>(_pts.Count + capSegments * 2);

        float totalLen = 0f;
        for (int i = 1; i < _pts.Count; i++)
            totalLen += Vector3.Distance(_pts[i - 1], _pts[i]);
        totalLen = Mathf.Max(totalLen, 0.0001f);

        Vector3 startDir = (_pts[1] - _pts[0]).normalized;
        Vector3 endDir = (_pts[_pts.Count - 1] - _pts[_pts.Count - 2]).normalized;

        float startBaseRadius = Mathf.Max(0.00005f, _rad[0] * startDropRadiusMul);
        float endBaseRadius = Mathf.Max(0.00005f, _rad[_rad.Count - 1] * endDropRadiusMul);

        for (int i = capSegments; i >= 1; i--)
        {
            float t = i / (float)(capSegments + 1);
            float shaped = Mathf.Pow(t, capShapePower);
            float capR = startBaseRadius * Mathf.Sqrt(Mathf.Max(0f, 1f - shaped * shaped));

            Vector3 pos = _pts[0] - startDir * (startCapLength * t);

            rings.Add(new RingData
            {
                pos = pos,
                normal = _nrm[0],
                tangent = startDir,
                radius = capR,
                v = 0f
            });
        }

        float accLen = 0f;
        for (int i = 0; i < _pts.Count; i++)
        {
            if (i > 0)
                accLen += Vector3.Distance(_pts[i - 1], _pts[i]);

            Vector3 tangent;
            if (i == 0) tangent = (_pts[1] - _pts[0]).normalized;
            else if (i == _pts.Count - 1) tangent = (_pts[i] - _pts[i - 1]).normalized;
            else tangent = (_pts[i + 1] - _pts[i - 1]).normalized;

            float radius = _rad[i];

            if (i == 0)
                radius *= edgeBodyBlend;

            if (i == _pts.Count - 1)
                radius *= edgeBodyBlend;

            rings.Add(new RingData
            {
                pos = _pts[i],
                normal = _nrm[i],
                tangent = tangent,
                radius = radius,
                v = accLen / totalLen
            });
        }

        for (int i = 1; i <= capSegments; i++)
        {
            float t = i / (float)(capSegments + 1);
            float shaped = Mathf.Pow(t, capShapePower);
            float capR = endBaseRadius * Mathf.Sqrt(Mathf.Max(0f, 1f - shaped * shaped));

            Vector3 pos = _pts[_pts.Count - 1] + endDir * (endCapLength * t);

            rings.Add(new RingData
            {
                pos = pos,
                normal = _nrm[_nrm.Count - 1],
                tangent = endDir,
                radius = capR,
                v = 1f
            });
        }

        return rings;
    }
}