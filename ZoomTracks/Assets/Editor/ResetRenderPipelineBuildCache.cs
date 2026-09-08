using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEditor.Rendering;

namespace ZoomTracks {
    /// <summary>
    /// Clears stale render-pipeline build data before Unity collects assets for a new build.
    /// <para>
    /// Observed with Unity 6000.3.23f1 and URP 17.3.0: enter Play Mode, stop Play Mode,
    /// then build a Windows standalone player. URPPreprocessBuild.LogIncludedAssets throws
    /// MissingReferenceException when reading urpAsset.name from its cached asset list,
    /// because the referenced UniversalRenderPipelineAsset instance has been destroyed.
    /// The build can still report Succeeded despite this exception. Resetting the cache
    /// here was reported to resolve this reproduction after runtime asset cleanup alone
    /// had failed to eliminate it.
    /// </para>
    /// <para>
    /// Unity runs IProcessSceneWithReport callbacks when preparing scenes for Play Mode
    /// as well as during builds. In this URP version, URPProcessScene.OnProcessScene
    /// accesses URPBuildData.instance, which in turn accesses CoreBuildData.instance.
    /// This can populate the static build cache with pipeline asset references even
    /// though no player build is running. Those references are a snapshot; they do not
    /// automatically follow later changes to QualitySettings.renderPipeline or the
    /// lifetime of the referenced Unity objects.
    /// </para>
    /// <para>
    /// At build startup, CorePreprocessBuild calls m_BuildData?.Dispose() and then assigns
    /// CoreBuildData.instance to m_BuildData. Its private m_BuildData field can still be
    /// null when the singleton was created during Play Mode, so this disposal does
    /// nothing and the existing singleton is reused. URPPreprocessBuild creates a new
    /// URPBuildData object, but that object copies the references from CoreBuildData;
    /// creating it does not guarantee a fresh collection of pipeline assets.
    /// </para>
    /// <para>
    /// Our game temporarily assigns a runtime clone to QualitySettings.renderPipeline
    /// and restores the original before destroying the clone on shutdown. That cleanup
    /// repairs the active selection, but does not refresh Unity's independent build
    /// cache. The exception confirms an invalid entry in the build list; the exact
    /// instance and point of invalidation were not instrumented, so this workaround
    /// does not assume the stale entry is necessarily our runtime clone.
    /// </para>
    /// <para>
    /// CoreBuildData.Dispose clears the collected pipeline references and compute-shader
    /// cache, then resets its static instance to null. It does not destroy pipeline
    /// assets. Unity's next request for CoreBuildData.instance therefore collects fresh
    /// references from the current project settings. This must happen before Unity's
    /// CorePreprocessBuild (int.MinValue + 50) and URPPreprocessBuild (int.MinValue + 100),
    /// hence this callback's int.MinValue order. Clearing the cache after those callbacks
    /// have collected their build data would be too late.
    /// </para>
    /// <para>
    /// This is a project workaround for the installed Scriptable Render Pipeline (SRP)
    /// build-cache lifecycle. When evaluating whether a Unity upgrade makes it unnecessary,
    /// repeat Play, Stop, Build several times in one Editor session and check for both
    /// successful builds and the absence of the exception; a successful build result alone
    /// did not detect this bug.
    /// </para>
    /// </summary>
    public sealed class ResetRenderPipelineBuildCache : IPreprocessBuildWithReport {
        // Runs before Unity's CorePreprocessBuild, whose order is int.MinValue + 50.
        public int callbackOrder => int.MinValue;

        public void OnPreprocessBuild(BuildReport report) {
            CoreBuildData.instance.Dispose();
        }
    }
}
