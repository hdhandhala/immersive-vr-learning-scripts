using UnityEditor;
using UnityEngine;

public class ShaderUsageScanner
{
    [MenuItem("Tools/Build/Scan Materials and Shaders")]
    public static void ScanMaterialsAndShaders()
    {
        string[] materialGuids = AssetDatabase.FindAssets("t:Material");
        Debug.Log("===== MATERIAL / SHADER SCAN START =====");

        foreach (string guid in materialGuids)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            Material mat = AssetDatabase.LoadAssetAtPath<Material>(path);

            if (mat == null || mat.shader == null)
                continue;

            Debug.Log($"Material: {path} | Shader: {mat.shader.name} | Supported: {mat.shader.isSupported}");
        }

        Debug.Log("===== MATERIAL / SHADER SCAN END =====");
    }
}