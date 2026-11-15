// Editor-only build entry points for command-line builds.
// Usage (Unity closed):
//   Unity -batchmode -quit -projectPath . -buildTarget iOS \
//         -executeMethod BuildScript.BuildIOS -logFile -
// Output: Builds/iOS (an Xcode project; open or build it with xcodebuild).

using System;
using System.Linq;
using UnityEditor;
using UnityEditor.Build.Reporting;

public static class BuildScript
{
    const string IOSOutputPath = "Builds/iOS";

    public static void BuildIOS()
    {
        string[] scenes = EditorBuildSettings.scenes
            .Where(s => s.enabled)
            .Select(s => s.path)
            .ToArray();

        if (scenes.Length == 0)
            throw new Exception("No enabled scenes in Build Settings.");

        var options = new BuildPlayerOptions
        {
            scenes = scenes,
            locationPathName = IOSOutputPath,
            target = BuildTarget.iOS,
            options = BuildOptions.None,
        };

        BuildReport report = BuildPipeline.BuildPlayer(options);
        BuildSummary summary = report.summary;

        if (summary.result != BuildResult.Succeeded)
        {
            // A non-zero exit code makes the command-line build fail visibly.
            EditorApplication.Exit(1);
        }
    }
}
