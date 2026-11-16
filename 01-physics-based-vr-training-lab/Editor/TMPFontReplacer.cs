using UnityEditor;
using UnityEngine;
using TMPro;

public class TMPFontReplacer
{
    [MenuItem("Tools/TMP/Replace Fonts In Scene")]
    static void ReplaceFontsInScene()
    {
        TMP_FontAsset newFont = Selection.activeObject as TMP_FontAsset;
        if (newFont == null)
        {
            Debug.LogError("Select a TMP_FontAsset in Project window first.");
            return;
        }

        var texts = Object.FindObjectsByType<TMP_Text>(FindObjectsSortMode.None);
        int count = 0;

        foreach (var t in texts)
        {
            t.font = newFont;
            EditorUtility.SetDirty(t);
            count++;
        }

        Debug.Log($"Replaced font on {count} TMP_Text objects.");
    }
}