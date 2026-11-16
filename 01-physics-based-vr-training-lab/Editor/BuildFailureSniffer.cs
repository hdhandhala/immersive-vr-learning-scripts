using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

public class BuildFailureSniffer : IPreprocessBuildWithReport, IPostprocessBuildWithReport
{
    public int callbackOrder => 0;

    private static string DesktopLogPath =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Desktop), "BuildFailureSniffer.txt");

    private static string UnityEditorLogPath =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), @"Unity\Editor\Editor.log");

    public void OnPreprocessBuild(BuildReport report)
    {
        WriteLine("====================================================");
        WriteLine("BUILD START");
        WriteLine("Time: " + DateTime.Now);
        WriteLine("Output: " + report.summary.outputPath);
        WriteLine("Platform: " + report.summary.platform);
        WriteLine("Unity Version: " + Application.unityVersion);
        WriteLine("Product Name: " + PlayerSettings.productName);
        WriteLine("Company Name: " + PlayerSettings.companyName);
        WriteLine("Active Build Target: " + EditorUserBuildSettings.activeBuildTarget);
        WriteLine("Development Build: " + EditorUserBuildSettings.development);
        WriteLine("Build App Bundle: " + EditorUserBuildSettings.buildAppBundle);
        WriteLine("Android Target Architectures: " + PlayerSettings.Android.targetArchitectures);
        WriteLine("Android Min SDK: " + PlayerSettings.Android.minSdkVersion);
        WriteLine("Android Target SDK: " + PlayerSettings.Android.targetSdkVersion);
        WriteLine("Graphics Jobs: " + PlayerSettings.graphicsJobs);
        WriteLine("Color Space: " + PlayerSettings.colorSpace);
        WriteLine("====================================================");
    }

    public void OnPostprocessBuild(BuildReport report)
    {
        WriteLine("====================================================");
        WriteLine("BUILD END");
        WriteLine("Time: " + DateTime.Now);
        WriteLine("Result: " + report.summary.result);
        WriteLine("Errors: " + report.summary.totalErrors);
        WriteLine("Warnings: " + report.summary.totalWarnings);
        WriteLine("Size: " + report.summary.totalSize + " bytes");
        WriteLine("Duration: " + report.summary.totalTime);

        if (report.steps != null)
        {
            WriteLine("--------------- BUILD STEPS ---------------");
            foreach (var step in report.steps)
            {
                WriteLine("Step: " + step.name + " | " + step.duration);

                if (step.messages != null)
                {
                    foreach (var msg in step.messages)
                    {
                        WriteLine($"  [{msg.type}] {msg.content}");
                    }
                }
            }
        }

        WriteLastLinesFromEditorLog(250);

        WriteLine("====================================================");
        Debug.Log("BuildFailureSniffer log written to: " + DesktopLogPath);
    }

    [MenuItem("Tools/Build/Write Last 250 Editor Log Lines")]
    public static void DumpEditorLogOnly()
    {
        WriteLine("====================================================");
        WriteLine("EDITOR LOG DUMP");
        WriteLine("Time: " + DateTime.Now);
        WriteLastLinesFromEditorLog(250);
        WriteLine("====================================================");
        Debug.Log("Editor log dump written to: " + DesktopLogPath);
    }

    private static void WriteLastLinesFromEditorLog(int lineCount)
    {
        WriteLine("--------------- LAST EDITOR.LOG LINES ---------------");

        try
        {
            if (!File.Exists(UnityEditorLogPath))
            {
                WriteLine("Editor.log not found at: " + UnityEditorLogPath);
                return;
            }

            using (var fs = new FileStream(UnityEditorLogPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
            using (var sr = new StreamReader(fs))
            {
                var text = sr.ReadToEnd();
                var lines = text.Split(new[] { "\r\n", "\n" }, StringSplitOptions.None);
                int start = Math.Max(0, lines.Length - lineCount);

                for (int i = start; i < lines.Length; i++)
                {
                    WriteLine(lines[i]);
                }
            }
        }
        catch (Exception e)
        {
            WriteLine("Failed reading Editor.log: " + e.Message);
        }
    }

    private static void WriteLine(string text)
    {
        try
        {
            File.AppendAllText(DesktopLogPath, text + Environment.NewLine);
        }
        catch (Exception e)
        {
            Debug.LogError("Failed writing sniffer log: " + e.Message);
        }
    }
}