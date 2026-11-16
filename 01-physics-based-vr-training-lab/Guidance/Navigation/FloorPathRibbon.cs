using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[ExecuteAlways]
[RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
public class FloorPathRibbon : MonoBehaviour
{
    [Header("Path Points")]
    public List<Transform> controlPoints = new List<Transform>();

    [Header("Progress")]
    [Min(0)] public int startPointIndex = 0;

    [Header("Shape")]
    [Min(0.001f)] public float ribbonWidth = 0.08f;
    [Min(2)] public int subdivisionsPerSegment = 12;
    [Min(0f)] public float floorOffset = 0.003f;

    [Header("UV")]
    public float uvTiling = 3f;

    [Header("Build")]
    public bool rebuildEveryFrameInEditor = true;

    [Header("Fade")]
    public bool smoothFadeCoveredRibbon = true;
    [Min(0.01f)] public float fadeDuration = 0.6f;

    MeshFilter _meshFilter;
    MeshRenderer _meshRenderer;
    Mesh _mesh;
    bool _needsRebuild;

    void OnEnable()
    {
        CacheComponents();
        EnsureMesh();
        Rebuild();
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
            if (rebuildEveryFrameInEditor || _needsRebuild)
            {
                EnsureMesh();
                Rebuild();
                _needsRebuild = false;
            }
        }
#endif
    }

    void CacheComponents()
    {
        if (_meshFilter == null) _meshFilter = GetComponent<MeshFilter>();
        if (_meshRenderer == null) _meshRenderer = GetComponent<MeshRenderer>();
    }

    void EnsureMesh()
    {
        if (_meshFilter == null) _meshFilter = GetComponent<MeshFilter>();
        if (_meshRenderer == null) _meshRenderer = GetComponent<MeshRenderer>();

        if (_mesh == null)
        {
            _mesh = new Mesh();
            _mesh.name = "FloorPathRibbonMesh";
            _mesh.MarkDynamic();
        }

        if (_meshFilter.sharedMesh != _mesh)
            _meshFilter.sharedMesh = _mesh;
    }

    public void SetStartPointIndex(int index)
    {
        int clamped = Mathf.Clamp(index, 0, Mathf.Max(0, controlPoints.Count - 1));

        if (clamped == startPointIndex)
            return;

        if (Application.isPlaying && smoothFadeCoveredRibbon && clamped > startPointIndex)
        {
            CreateFadeMesh(startPointIndex, clamped);
        }

        startPointIndex = clamped;
        Rebuild();
    }

    public void Rebuild()
    {
        EnsureMesh();

        List<Vector3> pts = GetValidPointsFromStart();

        if (pts.Count < 2)
        {
            _mesh.Clear();
            return;
        }

        List<Vector3> sampled = BuildSampledCurve(pts);

        if (sampled.Count < 2)
        {
            _mesh.Clear();
            return;
        }

        BuildRibbonMesh(_mesh, sampled);
    }

    List<Vector3> GetValidPointsFromStart()
    {
        List<Vector3> pts = new List<Vector3>();

        int safeStart = Mathf.Clamp(startPointIndex, 0, Mathf.Max(0, controlPoints.Count - 1));

        for (int i = safeStart; i < controlPoints.Count; i++)
        {
            if (controlPoints[i] != null)
                pts.Add(controlPoints[i].position);
        }

        return pts;
    }

    List<Vector3> GetValidPointsInRange(int fromIndexInclusive, int toIndexInclusive)
    {
        List<Vector3> pts = new List<Vector3>();

        int from = Mathf.Clamp(fromIndexInclusive, 0, Mathf.Max(0, controlPoints.Count - 1));
        int to = Mathf.Clamp(toIndexInclusive, 0, Mathf.Max(0, controlPoints.Count - 1));

        if (to <= from)
            return pts;

        for (int i = from; i <= to; i++)
        {
            if (i >= 0 && i < controlPoints.Count && controlPoints[i] != null)
                pts.Add(controlPoints[i].position);
        }

        return pts;
    }

    List<Vector3> BuildSampledCurve(List<Vector3> pts)
    {
        List<Vector3> result = new List<Vector3>();

        if (pts.Count == 2)
        {
            for (int i = 0; i <= subdivisionsPerSegment; i++)
            {
                float t = i / (float)subdivisionsPerSegment;
                result.Add(Vector3.Lerp(pts[0], pts[1], t));
            }
            return result;
        }

        for (int i = 0; i < pts.Count - 1; i++)
        {
            Vector3 p0 = (i == 0) ? pts[i] : pts[i - 1];
            Vector3 p1 = pts[i];
            Vector3 p2 = pts[i + 1];
            Vector3 p3 = (i + 2 < pts.Count) ? pts[i + 2] : pts[i + 1];

            for (int j = 0; j < subdivisionsPerSegment; j++)
            {
                float t = j / (float)subdivisionsPerSegment;
                result.Add(CatmullRom(p0, p1, p2, p3, t));
            }
        }

        result.Add(pts[pts.Count - 1]);
        return result;
    }

    Vector3 CatmullRom(Vector3 p0, Vector3 p1, Vector3 p2, Vector3 p3, float t)
    {
        float t2 = t * t;
        float t3 = t2 * t;

        return 0.5f * (
            (2f * p1) +
            (-p0 + p2) * t +
            (2f * p0 - 5f * p1 + 4f * p2 - p3) * t2 +
            (-p0 + 3f * p1 - 3f * p2 + p3) * t3
        );
    }

    void BuildRibbonMesh(Mesh targetMesh, List<Vector3> sampled)
    {
        int count = sampled.Count;
        if (count < 2)
        {
            targetMesh.Clear();
            return;
        }

        Vector3[] vertices = new Vector3[count * 2];
        Vector2[] uvs = new Vector2[count * 2];
        int[] triangles = new int[(count - 1) * 6];

        float totalLength = 0f;
        for (int i = 1; i < count; i++)
            totalLength += Vector3.Distance(sampled[i - 1], sampled[i]);

        totalLength = Mathf.Max(totalLength, 0.0001f);
        float accumulated = 0f;

        for (int i = 0; i < count; i++)
        {
            Vector3 current = sampled[i];
            Vector3 forward;

            if (i == 0)
                forward = (sampled[i + 1] - current).normalized;
            else if (i == count - 1)
                forward = (current - sampled[i - 1]).normalized;
            else
                forward = (sampled[i + 1] - sampled[i - 1]).normalized;

            Vector3 right = Vector3.Cross(Vector3.up, forward).normalized;
            if (right.sqrMagnitude < 0.0001f)
                right = Vector3.right;

            Vector3 raised = current + Vector3.up * floorOffset;
            float halfWidth = ribbonWidth * 0.5f;

            Vector3 leftPos = raised - right * halfWidth;
            Vector3 rightPos = raised + right * halfWidth;

            vertices[i * 2 + 0] = transform.InverseTransformPoint(leftPos);
            vertices[i * 2 + 1] = transform.InverseTransformPoint(rightPos);

            if (i > 0)
                accumulated += Vector3.Distance(sampled[i - 1], current);

            float v = (accumulated / totalLength) * uvTiling;
            uvs[i * 2 + 0] = new Vector2(0f, v);
            uvs[i * 2 + 1] = new Vector2(1f, v);
        }

        int ti = 0;
        for (int i = 0; i < count - 1; i++)
        {
            int i0 = i * 2 + 0;
            int i1 = i * 2 + 1;
            int i2 = (i + 1) * 2 + 0;
            int i3 = (i + 1) * 2 + 1;

            triangles[ti++] = i0;
            triangles[ti++] = i2;
            triangles[ti++] = i1;

            triangles[ti++] = i1;
            triangles[ti++] = i2;
            triangles[ti++] = i3;
        }

        targetMesh.Clear();
        targetMesh.vertices = vertices;
        targetMesh.uv = uvs;
        targetMesh.triangles = triangles;

        Vector3[] normals = new Vector3[vertices.Length];
        for (int i = 0; i < normals.Length; i++)
            normals[i] = Vector3.up;

        targetMesh.normals = normals;
        targetMesh.RecalculateBounds();
    }

    void CreateFadeMesh(int oldStartIndex, int newStartIndex)
    {
        List<Vector3> fadePts = GetValidPointsInRange(oldStartIndex, newStartIndex);
        if (fadePts.Count < 2)
            return;

        List<Vector3> sampled = BuildSampledCurve(fadePts);
        if (sampled.Count < 2)
            return;

        GameObject fadeObj = new GameObject("RibbonFadeTemp");
        fadeObj.transform.SetParent(transform, false);
        fadeObj.transform.localPosition = Vector3.zero;
        fadeObj.transform.localRotation = Quaternion.identity;
        fadeObj.transform.localScale = Vector3.one;

        MeshFilter mf = fadeObj.AddComponent<MeshFilter>();
        MeshRenderer mr = fadeObj.AddComponent<MeshRenderer>();

        Mesh fadeMesh = new Mesh();
        fadeMesh.name = "RibbonFadeMesh";
        BuildRibbonMesh(fadeMesh, sampled);
        mf.sharedMesh = fadeMesh;

        if (_meshRenderer != null && _meshRenderer.sharedMaterial != null)
        {
            Material fadeMat = new Material(_meshRenderer.sharedMaterial);
            fadeMat.name = _meshRenderer.sharedMaterial.name + "_FadeTemp";
            mr.sharedMaterial = fadeMat;
            StartCoroutine(FadeAndDestroy(fadeObj, mr, fadeMat));
        }
        else
        {
            Destroy(fadeObj);
        }

        mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        mr.receiveShadows = false;
        mr.lightProbeUsage = UnityEngine.Rendering.LightProbeUsage.Off;
        mr.reflectionProbeUsage = UnityEngine.Rendering.ReflectionProbeUsage.Off;
    }

    IEnumerator FadeAndDestroy(GameObject fadeObj, MeshRenderer mr, Material fadeMat)
    {
        if (fadeObj == null || fadeMat == null)
            yield break;

        float startAlpha = 1f;
        if (fadeMat.HasProperty("_Alpha"))
            startAlpha = fadeMat.GetFloat("_Alpha");

        float t = 0f;

        while (t < fadeDuration)
        {
            t += Time.deltaTime;
            float k = 1f - Mathf.Clamp01(t / fadeDuration);

            if (fadeMat != null && fadeMat.HasProperty("_Alpha"))
                fadeMat.SetFloat("_Alpha", startAlpha * k);

            yield return null;
        }

        if (fadeObj != null)
            Destroy(fadeObj);

        if (fadeMat != null)
            Destroy(fadeMat);
    }

    void OnDrawGizmos()
    {
        if (controlPoints == null) return;

        Gizmos.color = Color.cyan;

        for (int i = 0; i < controlPoints.Count; i++)
        {
            if (controlPoints[i] == null) continue;
            Gizmos.DrawSphere(controlPoints[i].position, 0.03f);

            if (i < controlPoints.Count - 1 && controlPoints[i + 1] != null)
                Gizmos.DrawLine(controlPoints[i].position, controlPoints[i + 1].position);
        }
    }
}