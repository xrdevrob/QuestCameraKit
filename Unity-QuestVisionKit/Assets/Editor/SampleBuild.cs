using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace QuestCameraKit.Editor
{
    public static class SampleBuild
    {
        // Unity -batchmode -quit -buildTarget Android -executeMethod QuestCameraKit.Editor.SampleBuild.Build
        //       -sampleScene ColorPicker -apk /absolute/path/ColorPicker.apk
        public static void Build()
        {
            var args = Environment.GetCommandLineArgs();
            var sceneName = Argument(args, "-sampleScene", "AllSamples");
            var paths = AssetDatabase.FindAssets("t:Scene", new[] { "Assets/Samples" })
                .Select(AssetDatabase.GUIDToAssetPath).Where(p => sceneName == "AllSamples"
                    ? Path.GetFileNameWithoutExtension(p) != "WebRTC-SingleClient"
                    : Path.GetFileNameWithoutExtension(p) == sceneName).OrderBy(p => p).ToArray();
            if (paths.Length == 0 || (sceneName != "AllSamples" && paths.Length != 1)) throw new ArgumentException($"Expected exactly one sample scene named {sceneName}.");
            var apk = Path.GetFullPath(Argument(args, "-apk", Path.Combine(Application.dataPath, "../../Builds", sceneName + ".apk")));
            Directory.CreateDirectory(Path.GetDirectoryName(apk));
            var originalId = PlayerSettings.GetApplicationIdentifier(NamedBuildTarget.Android);
            var originalName = PlayerSettings.productName;
            try
            {
                PlayerSettings.SetApplicationIdentifier(NamedBuildTarget.Android,
                    "com.xrdevrob.questcamerakit." + sceneName.Replace("-", "").ToLowerInvariant());
                PlayerSettings.productName = "QuestCameraKit - " + sceneName;
                PlayerSettings.SetScriptingBackend(NamedBuildTarget.Android, ScriptingImplementation.IL2CPP);
                PlayerSettings.Android.targetArchitectures = AndroidArchitecture.ARM64;
                PlayerSettings.Android.targetSdkVersion = AndroidSdkVersions.AndroidApiLevelAuto;
                var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
                {
                    scenes = paths,
                    target = BuildTarget.Android,
                    locationPathName = apk,
                    options = BuildOptions.Development
                });
                if (report.summary.result != BuildResult.Succeeded)
                    throw new InvalidOperationException($"{sceneName} build failed: {report.summary.result}, {report.summary.totalErrors} errors.");
                Debug.Log($"Built {sceneName}: {apk} ({report.summary.totalSize} bytes).");
            }
            finally
            {
                PlayerSettings.SetApplicationIdentifier(NamedBuildTarget.Android, originalId);
                PlayerSettings.productName = originalName;
                AssetDatabase.SaveAssets();
            }
        }

        private static string Argument(string[] args, string key, string fallback)
        {
            var index = Array.IndexOf(args, key);
            if (index < 0) return fallback;
            if (index + 1 >= args.Length || args[index + 1].StartsWith("-"))
                throw new ArgumentException($"{key} requires a value.");
            return args[index + 1];
        }
    }
}
