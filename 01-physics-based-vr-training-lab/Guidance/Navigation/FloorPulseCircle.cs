using UnityEngine;

[ExecuteAlways]
[DisallowMultipleComponent]
[RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
public class FloorPulseCircle : MonoBehaviour
{
    [Header("Shape")]
    [Min(0.02f)] public float radius = 0.12f;
    [Min(0.002f)] public float ringWidth = 0.025f;
    [Min(24)] public int segments = 96;
    [Min(0f)] public float floorOffset = 0.004f;

    [Header("Visual")]
    public Color ringColor = new Color(0.1f, 0.7f, 1f, 1f);
    [Range(0f, 1f)] public float alpha = 1f;
    public bool pulse = true;

    [Header("Material")]
    public Material circleMaterialTemplate;

    MeshFilter _mf;
    MeshRenderer _mr;
    Mesh _mesh;
    Material _instMat;

    bool _needsRebuild;

    void Awake()
    {
        CacheComponents();
        EnsureRuntimeObjects();
        ApplyAll();
    }

    void OnEnable()
    {
        CacheComponents();
        EnsureRuntimeObjects();
        ApplyAll();
    }

    void OnValidate()
    {
        CacheComponents();
        _needsRebuild = true;
    }

    void Update()
    {
#if UNITY_EDITOR
        if (!Application.isPlaying)
        {
            if (_needsRebuild)
            {
                EnsureRuntimeObjects();
                ApplyAll();
                _needsRebuild = false;
            }
        }
#endif
    }

    void CacheComponents()
    {
        if (_mf == null) _mf = GetComponent<MeshFilter>();
        if (_mr == null) _mr = GetComponent<MeshRenderer>();
    }

    void EnsureRuntimeObjects()
    {
        if (_mf == null) _mf = gameObject.AddComponent<MeshFilter>();
        if (_mr == null) _mr = gameObject.AddComponent<MeshRenderer>();

        if (_mesh == null)
        {
            _mesh = new Mesh();
            _mesh.name = "FloorPulseCircleMesh";
            _mesh.MarkDynamic();
        }

        if (_mf.sharedMesh != _mesh)
            _mf.sharedMesh = _mesh;

        if (circleMaterialTemplate != null)
        {
            bool needNewMaterial =
                _instMat == null ||
                _mr.sharedMaterial == null ||
                _mr.sharedMaterial.shader != circleMaterialTemplate.shader;

            if (needNewMaterial)
            {
                _instMat = new Material(circleMaterialTemplate);
                _instMat.name = circleMaterialTemplate.name + " (Instance)";
                _mr.sharedMaterial = _instMat;
            }
            else
            {
                _instMat = _mr.sharedMaterial;
            }
        }

        _mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        _mr.receiveShadows = false;
        _mr.lightProbeUsage = UnityEngine.Rendering.LightProbeUsage.Off;
        _mr.reflectionProbeUsage = UnityEngine.Rendering.ReflectionProbeUsage.Off;
    }

    void ApplyAll()
    {
        RebuildMesh();
        UpdateMaterial();
        UpdateTransform();
    }

    void UpdateTransform()
    {
        Vector3 p = transform.position;
        transform.position = new Vector3(p.x, floorOffset, p.z);
        transform.rotation = Quaternion.Euler(90f, 0f, 0f);
    }

    void UpdateMaterial()
    {
        if (_instMat == null)
            return;

        if (_instMat.HasProperty("_RingColor"))
            _instMat.SetColor("_RingColor", ringColor);

        if (_instMat.HasProperty("_GlowColor"))
            _instMat.SetColor("_GlowColor", ringColor * 1.2f);

        if (_instMat.HasProperty("_Alpha"))
            _instMat.SetFloat("_Alpha", alpha);

        if (_instMat.HasProperty("_PulseEnabled"))
            _instMat.SetFloat("_PulseEnabled", pulse ? 1f : 0f);
    }

    void RebuildMesh()
    {
        if (_mesh == null)
            return;

        float outer = Mathf.Max(0.001f, radius);
        float inner = Mathf.Max(0.0005f, radius - ringWidth);

        int vertCount = segments * 2;
        Vector3[] vertices = new Vector3[vertCount];
        Vector2[] uvs = new Vector2[vertCount];
        int[] tris = new int[segments * 6];
        Vector3[] normals = new Vector3[vertCount];

        for (int i = 0; i < segments; i++)
        {
            float t = i / (float)segments;
            float a = t * Mathf.PI * 2f;

            float cos = Mathf.Cos(a);
            float sin = Mathf.Sin(a);

            int outerIndex = i * 2;
            int innerIndex = i * 2 + 1;

            vertices[outerIndex] = new Vector3(cos * outer, sin * outer, 0f);
            vertices[innerIndex] = new Vector3(cos * inner, sin * inner, 0f);

            uvs[outerIndex] = new Vector2(1f, t);
            uvs[innerIndex] = new Vector2(0f, t);

            normals[outerIndex] = Vector3.forward;
            normals[innerIndex] = Vector3.forward;
        }

        int ti = 0;
        for (int i = 0; i < segments; i++)
        {
            int next = (i + 1) % segments;

            int o0 = i * 2;
            int i0 = i * 2 + 1;
            int o1 = next * 2;
            int i1 = next * 2 + 1;

            tris[ti++] = o0;
            tris[ti++] = o1;
            tris[ti++] = i0;

            tris[ti++] = i0;
            tris[ti++] = o1;
            tris[ti++] = i1;
        }

        _mesh.Clear();
        _mesh.vertices = vertices;
        _mesh.uv = uvs;
        _mesh.triangles = tris;
        _mesh.normals = normals;
        _mesh.RecalculateBounds();
        _mesh.bounds = new Bounds(Vector3.zero, new Vector3(radius * 4f, radius * 4f, 1f));
    }
}