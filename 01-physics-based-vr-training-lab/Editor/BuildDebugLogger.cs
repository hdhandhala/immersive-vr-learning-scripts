using System;
using System.IO;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

public class BuildDebugLogger : IPreprocessBuildWithReport, IPostprocessBuildWithReport
{
    public int callbackOrder => 0;

    private static string LogPath =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Desktop), "UnityBuildDebugLog.txt");

    public void OnPreprocessBuild(BuildReport report)
    {
        WriteLine("====================================================");
        WriteLine("BUILD STARTED");
        WriteLine("Time: " + DateTime.Now);
        WriteLine("Platform: " + report.summary.platform);
        WriteLine("Output Path: " + report.summary.outputPath);
        WriteLine("Unity Version: " + Application.unityVersion);
        WriteLine("Product Name: " + PlayerSettings.productName);
        WriteLine("Company Name: " + PlayerSettings.companyName);
        WriteLine("Bundle Identifier: " + PlayerSettings.GetApplicationIdentifier(BuildTargetGroup.Android));
        WriteLine("Scripting Backend: " + PlayerSettings.GetScriptingBackend(BuildTargetGroup.Android));
        WriteLine("Target Architectures: " + PlayerSettings.Android.targetArchitectures);
        WriteLine("Min SDK Version: " + PlayerSettings.Android.minSdkVersion);
        WriteLine("Target SDK Version: " + PlayerSettings.Android.targetSdkVersion);
        WriteLine("Graphics APIs Auto: " + PlayerSettings.GetUseDefaultGraphicsAPIs(BuildTarget.Android));
        WriteGraphicsAPIs();
        WriteEnabledScenes();
        WriteLine("====================================================");
    }

    public void OnPostprocessBuild(BuildReport report)
    {
        WriteLine("BUILD FINISHED");
        WriteLine("Time: " + DateTime.Now);
        WriteLine("Result: " + report.summary.result);
        WriteLine("Total Errors: " + report.summary.totalErrors);
        WriteLine("Total Warnings: " + report.summary.totalWarnings);
        WriteLine("Build Time: " + report.summary.totalTime);
        WriteLine("Build Size: " + report.summary.totalSize + " bytes");

        if (report.steps != null)
        {
            WriteLine("--------------- BUILD STEPS ---------------");
            foreach (var step in report.steps)
            {
                WriteLine("Step: " + step.name + " | Duration: " + step.duration);
                if (step.messages != null)
                {
                    foreach (var msg in step.messages)
                    {
                        WriteLine($"  [{msg.type}] {msg.content}");
                    }
                }
            }
        }

        WriteLine("====================================================");
        Debug.Log("Build debug log written to: " + LogPath);
    }

    [MenuItem("Tools/Build/Write Android Build Environment Info")]
    public static void WriteEnvironmentInfo()
    {
        WriteLine("====================================================");
        WriteLine("MANUAL ENVIRONMENT DUMP");
        WriteLine("Time: " + DateTime.Now);
        WriteLine("Unity Version: " + Application.unityVersion);
        WriteLine("Active Build Target: " + EditorUserBuildSettings.activeBuildTarget);
        WriteLine("Development Build: " + EditorUserBuildSettings.development);
        WriteLine("Connect Profiler: " + EditorUserBuildSettings.connectProfiler);
        WriteLine("Allow Debugging: " + EditorUserBuildSettings.allowDebugging);
        WriteLine("Install In Build Folder: " + EditorUserBuildSettings.installInBuildFolder);
        WriteLine("Build App Bundle: " + EditorUserBuildSettings.buildAppBundle);
        WriteLine("Android Keystore Use Custom: " + PlayerSettings.Android.useCustomKeystore);
        WriteLine("Package Name: " + PlayerSettings.GetApplicationIdentifier(BuildTargetGroup.Android));
        WriteLine("Scripting Backend: " + PlayerSettings.GetScriptingBackend(BuildTargetGroup.Android));
        WriteLine("API Compatibility: " + PlayerSettings.GetApiCompatibilityLevel(BuildTargetGroup.Android));
        WriteLine("Color Space: " + PlayerSettings.colorSpace);
        WriteLine("Graphics Jobs: " + PlayerSettings.graphicsJobs);
        WriteGraphicsAPIs();
        WriteEnabledScenes();
        WriteLine("====================================================");

        Debug.Log("Environment info written to: " + LogPath);
        EditorUtility.RevealInFinder(LogPath);
    }

    private static void WriteGraphicsAPIs()
    {
        try
        {
            var apis = PlayerSettings.GetGraphicsAPIs(BuildTarget.Android);
            WriteLine("Graphics APIs:");
            if (apis == null || apis.Length == 0)
            {
                WriteLine("  None found");
                return;
            }

            foreach (var api in apis)
            {
                WriteLine("  - " + api);
            }
        }
        catch (Exception e)
        {
            WriteLine("Could not read Graphics APIs: " + e.Message);
        }
    }

    private static void WriteEnabledScenes()
    {
        WriteLine("Enabled Scenes:");
        foreach (var scene in EditorBuildSettings.scenes)
        {
            if (scene.enabled)
                WriteLine("  - " + scene.path);
        }
    }

    private static void WriteLine(string text)
    {
        try
        {
            File.AppendAllText(LogPath, text + Environment.NewLine);
        }
        catch (Exception e)
        {
            Debug.LogError("Failed to write build debug log: " + e.Message);
        }
    }
}