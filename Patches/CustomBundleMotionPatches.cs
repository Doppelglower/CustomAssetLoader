using System.Reflection;
using HarmonyLib;
using Spine.Unity;
using UnityEngine.Playables;

namespace CustomBundleLoader.Patches;

/// <summary>
/// Bridges CharacterAppearance.StopMotion's root-speed pause to a real
/// PlayableDirector pause for Spine timelines instantiated by this plugin.
/// </summary>
public static class CustomBundleMotionPatches
{
    private static readonly Dictionary<int, float> ForcedPauseScales = new();

    public static void Apply(Harmony harmony)
    {
        MethodInfo stopMotion = AccessTools.Method(
            typeof(SD.CharacterAppearance),
            nameof(SD.CharacterAppearance.StopMotion),
            new[] { typeof(bool), typeof(float) });

        if (stopMotion == null)
        {
            BundleLog.Error("Custom bundle motion patch target missing: CharacterAppearance.StopMotion");
            return;
        }

        harmony.Patch(
            stopMotion,
            postfix: new HarmonyMethod(
                typeof(CustomBundleMotionPatches),
                nameof(Postfix_StopMotion)));

        MethodInfo skeletonUpdate = AccessTools.Method(
            typeof(SkeletonAnimation),
            nameof(SkeletonAnimation.Update),
            Type.EmptyTypes);
        if (skeletonUpdate == null)
        {
            BundleLog.Warn(
                "Custom bundle motion sync target missing: SkeletonAnimation.Update()");
            return;
        }

        harmony.Patch(
            skeletonUpdate,
            prefix: new HarmonyMethod(
                typeof(CustomBundleMotionPatches),
                nameof(Prefix_SkeletonAnimationUpdate)));
    }

    public static void Postfix_StopMotion(SD.CharacterAppearance __instance, bool value)
    {
        if (__instance == null
            || __instance.GetComponentInParent<BundleInstanceTag>() == null)
        {
            return;
        }

        try
        {
            CharacterSpineSkin spineSkin = __instance.currentSpineSkin;
            bool spineVisible = spineSkin != null
                && spineSkin._spine_renderer != null
                && spineSkin._spine_renderer.enabled;
            if (!spineVisible)
            {
                return;
            }

            PlayableDirector director = __instance._playableDirector;
            if (director == null)
            {
                return;
            }

            if (value)
            {
                if (director.state == PlayState.Playing)
                {
                    director.Pause();
                }
            }
            else if (director.state == PlayState.Paused)
            {
                director.Resume();
            }
        }
        catch (Exception ex)
        {
            BundleLog.VerboseWarn(
                $"Custom bundle Spine pause bridge skipped for {__instance.name}: {ex.Message}");
        }
    }

    public static void Prefix_SkeletonAnimationUpdate(SkeletonAnimation __instance)
    {
        if (__instance == null
            || __instance.GetComponentInParent<BundleInstanceTag>() == null)
        {
            return;
        }

        int id = __instance.GetInstanceID();
        try
        {
            var appearance = __instance.GetComponentInParent<SD.CharacterAppearance>();
            PlayableDirector director = appearance?._playableDirector;
            var entry = __instance.AnimationState?.GetCurrent(0);
            if (director == null || entry == null)
            {
                RestoreForcedScale(id, entry, "missing-director-or-entry");
                return;
            }

            bool graphValid = director.playableGraph.IsValid()
                && director.playableGraph.GetRootPlayableCount() > 0;
            if (!graphValid)
            {
                RestoreForcedScale(id, entry, "invalid-graph");
                return;
            }

            double rootSpeed = director.playableGraph.GetRootPlayable(0).GetSpeed();
            bool paused = director.state == PlayState.Paused
                || Math.Abs(rootSpeed) < 0.0001;

            if (paused)
            {
                if (!ForcedPauseScales.ContainsKey(id))
                {
                    float originalScale = entry.TimeScale;
                    ForcedPauseScales[id] = originalScale > 0.0001f
                        ? originalScale
                        : 1f;
                    BundleLog.Verbose(
                        $"[CustomBundleLoader.SpineSync] PAUSE appearance={appearance.name} " +
                        $"directorState={director.state} rootSpeed={rootSpeed:F3} " +
                        $"trackTime={entry.TrackTime:F4} savedScale={ForcedPauseScales[id]:F3}");
                }
                else if (entry.TimeScale > 0.0001f)
                {
                    ForcedPauseScales[id] = entry.TimeScale;
                }

                entry.TimeScale = 0f;
            }
            else
            {
                RestoreForcedScale(id, entry, "director-running");
            }
        }
        catch (Exception ex)
        {
            BundleLog.VerboseWarn(
                $"[CustomBundleLoader.SpineSync] skipped for {__instance.name}: {ex.Message}");
        }
    }

    private static void RestoreForcedScale(int id, Spine.TrackEntry entry, string reason)
    {
        if (!ForcedPauseScales.TryGetValue(id, out float savedScale))
        {
            return;
        }

        if (entry != null && entry.TimeScale <= 0.0001f)
        {
            entry.TimeScale = savedScale;
        }

        ForcedPauseScales.Remove(id);
        BundleLog.Verbose(
            $"[CustomBundleLoader.SpineSync] RESUME reason={reason} restoredScale={savedScale:F3}");
    }
}
