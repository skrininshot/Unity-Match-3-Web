using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace Match3.EditorTools
{
    /// <summary>Produces the Web build from the command line.</summary>
    public static class BuildTool
    {
        public static void BuildWebGL()
        {
            SceneBuilder.RegisterInBuildSettings();

            string output = ArtifactPaths.WebGLBuild;

            PlayerSettings.SetScriptingBackend(NamedBuildTarget.WebGL, ScriptingImplementation.IL2CPP);
            // Going past Low risks stripping a method that is only ever reached dynamically
            // (delegate/interface dispatch), producing a "RuntimeError: null function" WASM trap at
            // runtime instead of a build-time error. Low is the most aggressive level observed safe.
            PlayerSettings.SetManagedStrippingLevel(NamedBuildTarget.WebGL, ManagedStrippingLevel.Low);
            // OptimizeSize trades a bit of runtime speed for materially smaller generated code --
            // a good trade for a casual 2D board game with no tight per-frame budget.
            PlayerSettings.SetIl2CppCodeGeneration(NamedBuildTarget.WebGL, Il2CppCodeGeneration.OptimizeSize);
            PlayerSettings.WebGL.compressionFormat = WebGLCompressionFormat.Brotli;
            // The local test server (Python's http.server) doesn't send Content-Encoding for .br
            // files, so without the fallback a locally-served build would fail to load entirely.
            PlayerSettings.WebGL.decompressionFallback = true;
            PlayerSettings.WebGL.dataCaching = true;

            // ExplicitlyThrownExceptionsOnly only wraps explicit `throw` sites in try/catch; an
            // implicit one (e.g. a null reference) skips straight past managed code and surfaces as
            // an opaque "RuntimeError: null function" wasm trap with no C# stack trace at all -- which
            // is exactly what turned a real bug into a guessing game once already on this project.
            // Full trades some size/speed for getting an actual catchable exception with a managed
            // stack trace in its place; worth it while effects/UI are still being tuned and need to
            // be debuggable.
            PlayerSettings.WebGL.exceptionSupport = WebGLExceptionSupport.FullWithStacktrace;
            PlayerSettings.runInBackground = true;
            PlayerSettings.defaultWebScreenWidth = 1280;
            PlayerSettings.defaultWebScreenHeight = 720;
            PlayerSettings.companyName = "Match3";
            PlayerSettings.productName = "Match Three";

            var options = new BuildPlayerOptions
            {
                scenes = EditorBuildSettings.scenes.Where(s => s.enabled).Select(s => s.path).ToArray(),
                locationPathName = output,
                target = BuildTarget.WebGL,
                targetGroup = BuildTargetGroup.WebGL,
                options = BuildOptions.None,
            };

            Debug.Log($"[TOOL] building WebGL into {output}");
            BuildReport report = BuildPipeline.BuildPlayer(options);
            BuildSummary summary = report.summary;

            Debug.Log($"[TOOL] build result: {summary.result}, " +
                      $"size: {summary.totalSize / (1024 * 1024)} MB, " +
                      $"time: {summary.totalTime.TotalSeconds:0} s, " +
                      $"errors: {summary.totalErrors}, warnings: {summary.totalWarnings}");

            if (summary.result != BuildResult.Succeeded)
            {
                foreach (BuildStep step in report.steps)
                foreach (BuildStepMessage message in step.messages)
                    if (message.type == LogType.Error || message.type == LogType.Exception)
                        Debug.LogError($"[TOOL] {step.name}: {message.content}");

                EditorApplication.Exit(1);
            }

            // Fail loudly if the index page is missing; a "successful" build with no entry point
            // would otherwise look fine until someone tried to open it.
            string indexPath = Path.Combine(output, "index.html");
            if (!File.Exists(indexPath))
            {
                Debug.LogError($"[TOOL] build finished but {indexPath} is missing");
                EditorApplication.Exit(1);
            }

            Debug.Log($"[TOOL] WebGL build ready: {indexPath}");
        }
    }
}
