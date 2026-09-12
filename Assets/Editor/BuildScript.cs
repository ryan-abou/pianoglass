using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace PianoGlass.EditorTools
{
    /// <summary>
    /// Headless builds for PianoGlass.
    ///
    /// Unity can build from the command line, but only by calling a static
    /// method you provide — there is no built-in "just build the APK" flag.
    /// This is that method. With it, a build becomes one shell command, which
    /// means an agent, a script or a CI job can build and deploy without
    /// anyone clicking through the editor.
    ///
    /// From PowerShell:
    ///
    ///   &amp; "C:\Program Files\Unity\Hub\Editor\6000.6.0f1\Editor\Unity.exe" `
    ///       -batchmode -quit `
    ///       -projectPath "C:\Dev\PianoGlass\PianoGlass" `
    ///       -buildTarget Android `
    ///       -executeMethod PianoGlass.EditorTools.BuildScript.BuildAndroid `
    ///       -logFile "Logs\build.log"
    ///
    /// Then check $LASTEXITCODE: 0 succeeded, 1 failed.
    ///
    /// IMPORTANT: close the Unity editor first. Unity locks the project's
    /// Library folder, and a second instance on the same project fails with a
    /// message about multiple instances rather than anything about building.
    ///
    /// This file lives in Assets/Editor/ so it compiles only in the editor and
    /// never ships in the player.
    /// </summary>
    public static class BuildScript
    {
        private const string DefaultOutput = "Builds/PianoGlass.apk";

        /// <summary>Grep for this in build.log — one line, always present.</summary>
        private const string ResultTag = "[PianoGlassBuild]";

        // ------------------------------------------------------------------
        // Entry points
        // ------------------------------------------------------------------

        // No keyboard shortcuts on purpose: Ctrl+Shift+B is Unity's own Build
        // Profiles window, and shadowing it would be a daily irritation.
        [MenuItem("PianoGlass/Build Android APK")]
        public static void BuildAndroidFromMenu() => Build(development: false);

        [MenuItem("PianoGlass/Build Android APK (development)")]
        public static void BuildAndroidDevelopmentFromMenu() => Build(development: true);

        /// <summary>Called by -executeMethod for a release build.</summary>
        public static void BuildAndroid() => Build(development: false);

        /// <summary>
        /// Called by -executeMethod for a development build: enables the
        /// profiler and script debugging, and keeps Debug.Log output flowing to
        /// logcat. Slower and larger — use it while diagnosing, not for the
        /// build you hand to a test participant.
        /// </summary>
        public static void BuildAndroidDevelopment() => Build(development: true);

        // ------------------------------------------------------------------

        private static void Build(bool development)
        {
            try
            {
                string output = GetArg("-outputPath") ?? DefaultOutput;
                if (!Path.IsPathRooted(output))
                    output = Path.Combine(Directory.GetCurrentDirectory(), output);

                Directory.CreateDirectory(Path.GetDirectoryName(output));

                string[] scenes = EnabledScenes();
                if (scenes.Length == 0)
                {
                    Fail("No enabled scenes in the build list. Open File > Build Profiles, " +
                         "select the Android profile, and use Add Open Scenes.");
                    return;
                }

                if (EditorUserBuildSettings.activeBuildTarget != BuildTarget.Android)
                {
                    // Pass -buildTarget Android on the command line so Unity
                    // switches before this method runs. Switching here works but
                    // triggers a full reimport mid-build, which is slow and has
                    // surprised people with half-converted textures.
                    Debug.LogWarning($"{ResultTag} Active build target is " +
                                     $"{EditorUserBuildSettings.activeBuildTarget}, not Android. " +
                                     "Add -buildTarget Android to the command line.");
                }

                var options = new BuildPlayerOptions
                {
                    scenes = scenes,
                    locationPathName = output,
                    target = BuildTarget.Android,
                    targetGroup = BuildTargetGroup.Android,
                    options = development
                        ? BuildOptions.Development | BuildOptions.AllowDebugging
                        : BuildOptions.None
                };

                Debug.Log($"{ResultTag} building {(development ? "development" : "release")} " +
                          $"→ {output}\n{ResultTag} scenes: {string.Join(", ", scenes)}");

                BuildReport report = BuildPipeline.BuildPlayer(options);
                BuildSummary summary = report.summary;

                if (summary.result == BuildResult.Succeeded)
                {
                    double mb = summary.totalSize / 1024.0 / 1024.0;
                    Debug.Log($"{ResultTag} SUCCEEDED  {mb:0.0} MB  in {summary.totalTime.TotalSeconds:0} s  →  {output}");
                    Exit(0);
                }
                else
                {
                    // The report knows which step failed; the log alone often
                    // does not make that obvious among thousands of lines.
                    foreach (var step in report.steps)
                        foreach (var message in step.messages)
                            if (message.type == LogType.Error || message.type == LogType.Exception)
                                Debug.LogError($"{ResultTag} {step.name}: {message.content}");

                    Fail($"{summary.result} with {summary.totalErrors} error(s).");
                }
            }
            catch (Exception e)
            {
                Fail($"threw {e.GetType().Name}: {e.Message}");
            }
        }

        // ------------------------------------------------------------------

        private static string[] EnabledScenes() =>
            EditorBuildSettings.scenes
                .Where(s => s.enabled && !string.IsNullOrEmpty(s.path))
                .Select(s => s.path)
                .ToArray();

        /// <summary>Read a value that follows a flag on the command line,
        /// e.g. -outputPath Builds/test.apk</summary>
        private static string GetArg(string name)
        {
            string[] args = Environment.GetCommandLineArgs();
            for (int i = 0; i < args.Length - 1; i++)
                if (string.Equals(args[i], name, StringComparison.OrdinalIgnoreCase))
                    return args[i + 1];
            return null;
        }

        private static void Fail(string reason)
        {
            Debug.LogError($"{ResultTag} FAILED — {reason}");
            Exit(1);
        }

        /// <summary>
        /// Exit with a code the shell can test, but only in batch mode —
        /// calling this from the menu would close the editor under you.
        /// </summary>
        private static void Exit(int code)
        {
            if (Application.isBatchMode) EditorApplication.Exit(code);
        }
    }
}
