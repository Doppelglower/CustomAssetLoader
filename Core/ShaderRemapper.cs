using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;
using Il2CppInterop.Runtime;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace CustomAssetLoader.Core;

/// <summary>
/// After loading a mod GameObject prefab, optionally rebinds Material shaders to
/// official Limbus player shaders. Opt-in via Material float
/// <see cref="ReplaceWithOfficialProperty"/>:
/// <list type="bullet">
/// <item>0 — no replace</item>
/// <item>1 — <c>Shader.Find</c> only (never Source); Error if null or Find returns the stub</item>
/// <item>2 — Material tag <see cref="OfficialShaderSource.SourceTag"/> only (never Find);
/// optional |replaceTex:null|all|slots on material:</item>
/// </list>
/// Authored property values on the Material are left intact.
/// </summary>
public static class ShaderRemapper
{
    /// <summary>
    /// 0 = none, 1 = Shader.Find only, 2 = Addressable Source only.
    /// Author-written shaders that share an official name should omit it or set 0.
    /// </summary>
    public const string ReplaceWithOfficialProperty = "_ReplaceWithOfficialShader";

    private static readonly HashSet<int> RemappedPrefabIds = new();
    private static readonly HashSet<string> LoggedMissing = new(StringComparer.Ordinal);
    private static bool _dumpedLoadedShadersThisSession;

    /// <summary>
    /// Remap all Renderer materials under <paramref name="root"/> once per prefab asset.
    /// Safe to call repeatedly; already-processed instance IDs are skipped.
    /// </summary>
    public static void RemapPrefabAsset(GameObject root)
    {
        if (root == null)
        {
            return;
        }

        int id = root.GetInstanceID();
        if (!RemappedPrefabIds.Add(id))
        {
            if (BundleLog.VerboseLog)
            {
                BundleLog.Verbose(
                    $"[ShaderRemapper] Skip already-remapped instance id={id} name='{root.name}'.");
            }

            return;
        }

        if (BundleLog.VerboseLog)
        {
            LogRemapBegin(root, id);
            DumpLoadedShadersOnce();
        }

        int swapped = 0;
        int skipped = 0;
        int missing = 0;
        int optIn = 0;
        var missingNames = new HashSet<string>(StringComparer.Ordinal);
        var swappedNames = new HashSet<string>(StringComparer.Ordinal);

        Renderer[] renderers = root.GetComponentsInChildren<Renderer>(true);
        if (renderers == null || renderers.Length == 0)
        {
            if (BundleLog.VerboseLog)
            {
                BundleLog.Verbose($"[ShaderRemapper] No renderers on '{root.name}'.");
            }

            return;
        }

        Dictionary<string, Shader> loadedByName = BundleLog.VerboseLog
            ? BuildLoadedShaderIndex()
            : null;

        foreach (Renderer renderer in renderers)
        {
            if (renderer == null)
            {
                continue;
            }

            Material[] materials = renderer.sharedMaterials;
            if (materials == null || materials.Length == 0)
            {
                continue;
            }

            for (int i = 0; i < materials.Length; i++)
            {
                Material mat = materials[i];
                if (mat == null || mat.shader == null)
                {
                    skipped++;
                    continue;
                }

                int mode = GetReplaceMode(mat);
                if (mode == 0)
                {
                    skipped++;
                    continue;
                }

                optIn++;
                string shaderName = mat.shader.name;
                string rendererPath = GetTransformPath(renderer.transform);

                if (string.IsNullOrEmpty(shaderName)
                    || shaderName.Contains("InternalErrorShader", StringComparison.Ordinal))
                {
                    skipped++;
                    if (LoggedMissing.Add($"broken:{root.name}:{renderer.name}"))
                    {
                        BundleLog.Warn(
                            $"[ShaderRemapper] Broken/missing shader on '{root.name}/{renderer.name}' " +
                            $"mat='{mat.name}' (name='{shaderName}'). " +
                            "Authoring Material must reference a same-name stub shader, not InternalError.");
                    }

                    continue;
                }

                Shader stubShader = mat.shader;
                int stubId = stubShader.GetInstanceID();
                Shader playerShader = null;

                if (mode == 1)
                {
                    playerShader = Shader.Find(shaderName);
                    bool findOk = playerShader != null;
                    bool isStubInstance = findOk
                        && (ReferenceEquals(playerShader, stubShader)
                            || playerShader.GetInstanceID() == stubId);
                    bool inLoadedIndex = loadedByName != null
                        && loadedByName.ContainsKey(shaderName);
                    Shader loadedHit = null;
                    if (loadedByName != null)
                    {
                        loadedByName.TryGetValue(shaderName, out loadedHit);
                    }

                    if (BundleLog.VerboseLog)
                    {
                        BundleLog.Verbose(
                            $"[ShaderRemapper] mode=1 path='{rendererPath}' mat='{mat.name}' " +
                            $"want='{shaderName}' stubId={stubId} " +
                            $"Find={(findOk ? $"OK id={playerShader.GetInstanceID()}" : "NULL")} " +
                            $"isStub={isStubInstance} " +
                            $"LoadedIndex={(inLoadedIndex ? $"HIT id={loadedHit.GetInstanceID()}" : "MISS")}");
                    }

                    if (!findOk || isStubInstance)
                    {
                        missing++;
                        missingNames.Add(shaderName);
                        if (BundleLog.VerboseLog)
                        {
                            LogMissingDiagnostics(shaderName, loadedByName);
                        }

                        string failKey = $"mode1:{shaderName}:{mat.name}";
                        if (LoggedMissing.Add(failKey))
                        {
                            BundleLog.Error(
                                $"[ShaderRemapper] mode=1 Find failed for '{shaderName}' " +
                                $"(path='{rendererPath}' mat='{mat.name}'): " +
                                (findOk
                                    ? "Shader.Find returned the stub instance (official not loaded). " +
                                      "Use mode=2 with OfficialShaderSource, or ensure the official shader is resident."
                                    : "Shader.Find returned null. " +
                                      "Use mode=2 with OfficialShaderSource, or ensure the official shader is resident."));
                        }

                        continue;
                    }
                }
                else if (mode == 2)
                {
                    string sourceSpec = OfficialShaderSource.ReadSourceSpec(mat);
                    if (BundleLog.VerboseLog)
                    {
                        BundleLog.Verbose(
                            $"[ShaderRemapper] mode=2 path='{rendererPath}' mat='{mat.name}' " +
                            $"want='{shaderName}' source='{sourceSpec ?? "(null)"}'");
                    }

                    playerShader = OfficialShaderSource.Resolve(
                        sourceSpec, shaderName, stubId, out string sourceError);

                    if (playerShader == null)
                    {
                        Shader viaFind = Shader.Find(shaderName);
                        bool findOk = viaFind != null;
                        bool isStub = findOk
                            && (ReferenceEquals(viaFind, stubShader)
                                || viaFind.GetInstanceID() == stubId);
                        if (findOk && !isStub)
                        {
                            playerShader = viaFind;
                            if (LoggedMissing.Add($"mode2find:{mat.name}"))
                            {
                                BundleLog.VerboseWarn(
                                    $"[ShaderRemapper] mode=2 Source miss → Find OK for '{shaderName}' " +
                                    $"(mat='{mat.name}' source='{sourceSpec ?? "(null)"}': {sourceError})");
                            }
                        }
                        else
                        {
                            missing++;
                            missingNames.Add(shaderName);
                            OfficialShaderSource.LogFailureOnce(
                                $"mode2:{mat.name}:{sourceSpec}",
                                $"[ShaderRemapper] mode=2 Source+Find failed for mat='{mat.name}' " +
                                $"path='{rendererPath}' want='{shaderName}' " +
                                $"source='{sourceSpec ?? "(null)"}': {sourceError}");
                            continue;
                        }
                    }
                }
                else
                {
                    skipped++;
                    if (LoggedMissing.Add($"badmode:{mat.name}:{mode}"))
                    {
                        BundleLog.Error(
                            $"[ShaderRemapper] invalid Replace mode={mode} on mat='{mat.name}' " +
                            $"(path='{rendererPath}'); use 0, 1, or 2.");
                    }

                    continue;
                }

                if (ReferenceEquals(mat.shader, playerShader)
                    || mat.shader.GetInstanceID() == playerShader.GetInstanceID())
                {
                    if (mode == 2)
                    {
                        TryApplyReplaceTex(mat, OfficialShaderSource.ReadSourceSpec(mat), rendererPath);
                    }

                    skipped++;
                    if (BundleLog.VerboseLog)
                    {
                        BundleLog.Verbose(
                            $"[ShaderRemapper] already-official path='{rendererPath}' mat='{mat.name}' " +
                            $"shader='{shaderName}' mode={mode}");
                    }

                    continue;
                }

                mat.shader = playerShader;
                swapped++;
                swappedNames.Add(shaderName);

                string sourceSpecSwap = null;
                if (mode == 2)
                {
                    sourceSpecSwap = OfficialShaderSource.ReadSourceSpec(mat);
                    TryApplyReplaceTex(mat, sourceSpecSwap, rendererPath);
                }

                if (BundleLog.VerboseLog)
                {
                    string texNote = string.Empty;
                    if (mode == 2)
                    {
                        texNote = "(no replaceTex)";
                        if (OfficialShaderSource.TryParseSpec(
                                sourceSpecSwap, out OfficialSourceSpec parsedSwap, out _))
                        {
                            texNote = parsedSwap.WantsReplaceTex
                                ? $"replaceTex={parsedSwap.TexMode}"
                                : "replaceTex=none";
                        }

                        texNote = " " + texNote;
                    }

                    BundleLog.Verbose(
                        $"[ShaderRemapper] SWAP mode={mode} path='{rendererPath}' mat='{mat.name}' " +
                        $"'{shaderName}' -> '{playerShader.name}' id={playerShader.GetInstanceID()}{texNote}");
                }
            }
        }

        if (BundleLog.VerboseLog)
        {
            BundleLog.Verbose(
                $"[ShaderRemapper] '{root.name}': swapped={swapped} skipped={skipped} missing={missing} " +
                $"optIn={optIn} renderers={renderers.Length}");

            if (swappedNames.Count > 0)
            {
                BundleLog.Verbose(
                    $"[ShaderRemapper] swapped distinct shaders ({swappedNames.Count}): " +
                    string.Join(" | ", swappedNames));
            }

            if (missingNames.Count > 0)
            {
                BundleLog.Verbose(
                    $"[ShaderRemapper] missing distinct shaders ({missingNames.Count}): " +
                    string.Join(" | ", missingNames));
            }
        }
    }

    /// <summary>
    /// 0 = none, 1 = Find only, 2 = Source only. Unknown float → infer 2 if Source tag set.
    /// </summary>
    private static int GetReplaceMode(Material mat)
    {
        if (mat == null)
        {
            return 0;
        }

        if (mat.HasProperty(ReplaceWithOfficialProperty))
        {
            int mode = (int)Math.Round(mat.GetFloat(ReplaceWithOfficialProperty));
            if (mode < 0)
            {
                mode = 0;
            }

            if (mode > 2)
            {
                mode = 2;
            }

            if (mode > 0)
            {
                return mode;
            }
        }

        return string.IsNullOrEmpty(OfficialShaderSource.ReadSourceSpec(mat)) ? 0 : 2;
    }

    private static void TryApplyReplaceTex(Material mat, string sourceSpec, string rendererPath)
    {
        if (mat == null
            || string.IsNullOrWhiteSpace(sourceSpec)
            || !OfficialShaderSource.TryParseSpec(sourceSpec, out OfficialSourceSpec parsed, out _))
        {
            return;
        }

        if (!parsed.WantsReplaceTex)
        {
            return;
        }

        OfficialShaderSource.ApplyReplaceTexFromSource(mat, parsed, rendererPath);
    }

    /// <summary>
    /// Clears the per-prefab remap guard (e.g. after bundle reload).
    /// </summary>
    public static void ResetSessionState()
    {
        RemappedPrefabIds.Clear();
        LoggedMissing.Clear();
        _dumpedLoadedShadersThisSession = false;
        OfficialShaderSource.ResetSessionState();
    }

    private static void LogRemapBegin(GameObject root, int id)
    {
        Scene scene = root.scene;
        string sceneName = scene.IsValid() ? scene.name : "(no-scene/asset)";
        BundleLog.Verbose(
            $"[ShaderRemapper] BEGIN name='{root.name}' instanceId={id} " +
            $"scene='{sceneName}' activeScene='{SceneManager.GetActiveScene().name}' " +
            $"frame={Time.frameCount} realtime={Time.realtimeSinceStartup:F3}s");

        // Lightweight caller hint (managed frames only).
        try
        {
            var st = new StackTrace(1, false);
            var sb = new StringBuilder(128);
            int frames = Math.Min(6, st.FrameCount);
            for (int i = 0; i < frames; i++)
            {
                var m = st.GetFrame(i)?.GetMethod();
                if (m == null)
                {
                    continue;
                }

                if (sb.Length > 0)
                {
                    sb.Append(" <- ");
                }

                sb.Append(m.DeclaringType?.Name).Append('.').Append(m.Name);
            }

            if (sb.Length > 0)
            {
                BundleLog.Verbose($"[ShaderRemapper] callstack: {sb}");
            }
        }
        catch
        {
            // Diagnostics only; never fail remap because of stack capture.
        }
    }

    private static void DumpLoadedShadersOnce()
    {
        if (_dumpedLoadedShadersThisSession)
        {
            return;
        }

        _dumpedLoadedShadersThisSession = true;

        Dictionary<string, Shader> index = BuildLoadedShaderIndex();
        if (index == null || index.Count == 0)
        {
            BundleLog.VerboseWarn(
                "[ShaderRemapper] Loaded-shader dump: FindObjectsOfTypeAll<Shader> returned nothing.");
            return;
        }

        var fxTeam = new List<string>();
        var noiseOrOutline = new List<string>();
        var screen = new List<string>();
        foreach (string name in index.Keys)
        {
            if (name.StartsWith("Fx_Team/", StringComparison.Ordinal)
                || name.StartsWith("Fx_Team_", StringComparison.Ordinal))
            {
                fxTeam.Add(name);
            }

            if (name.IndexOf("Noise", StringComparison.OrdinalIgnoreCase) >= 0
                || name.IndexOf("Outline_ColorMask", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                noiseOrOutline.Add(name);
            }

            if (name.IndexOf("ScreenFliter", StringComparison.OrdinalIgnoreCase) >= 0
                || name.IndexOf("ScreenFilter", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                screen.Add(name);
            }
        }

        fxTeam.Sort(StringComparer.Ordinal);
        noiseOrOutline.Sort(StringComparer.Ordinal);
        screen.Sort(StringComparer.Ordinal);

        BundleLog.Verbose(
            $"[ShaderRemapper] Loaded-shader dump: totalUnique={index.Count} " +
            $"Fx_Team*={fxTeam.Count} Noise|Outline_ColorMask={noiseOrOutline.Count} ScreenFilt*={screen.Count}");

        LogNameList("Fx_Team*", fxTeam);
        LogNameList("Noise|Outline_ColorMask", noiseOrOutline);
        LogNameList("ScreenFilt*", screen);
    }

    private static void LogNameList(string label, List<string> names)
    {
        if (names.Count == 0)
        {
            BundleLog.Verbose($"[ShaderRemapper]   {label}: (none)");
            return;
        }

        // Chunk to keep BepInEx lines readable.
        const int chunk = 12;
        for (int i = 0; i < names.Count; i += chunk)
        {
            int count = Math.Min(chunk, names.Count - i);
            BundleLog.Verbose(
                $"[ShaderRemapper]   {label}[{i}..{i + count - 1}]: " +
                string.Join(" | ", names.GetRange(i, count)));
        }
    }

    private static void LogMissingDiagnostics(string shaderName, Dictionary<string, Shader> loadedByName)
    {
        if (loadedByName == null)
        {
            return;
        }

        // Exact (should already be MISS), then case-insensitive, then substring candidates.
        string ciHit = null;
        var substringHits = new List<string>();
        string leaf = shaderName;
        int slash = shaderName.LastIndexOf('/');
        if (slash >= 0 && slash < shaderName.Length - 1)
        {
            leaf = shaderName[(slash + 1)..];
        }

        foreach (string loaded in loadedByName.Keys)
        {
            if (string.Equals(loaded, shaderName, StringComparison.OrdinalIgnoreCase))
            {
                ciHit = loaded;
            }

            if (loaded.IndexOf(leaf, StringComparison.OrdinalIgnoreCase) >= 0
                || leaf.IndexOf(GetShaderLeaf(loaded), StringComparison.OrdinalIgnoreCase) >= 0)
            {
                if (substringHits.Count < 8 && !substringHits.Contains(loaded))
                {
                    substringHits.Add(loaded);
                }
            }
        }

        BundleLog.Verbose(
            $"[ShaderRemapper]   missing-diag '{shaderName}': " +
            $"caseInsensitive={(ciHit != null ? $"'{ciHit}'" : "none")} " +
            $"substringCandidates=[{string.Join(" | ", substringHits)}]");

        // Probe close relatives that we may fall back to later (log only, no remap change).
        string[] probes =
        {
            "Fx_Team/Fx_Grp_Outline_ColorMask_Shader_Noise_Random",
            "Fx_Team/Fx_Grp_Outline_ColorMask_Shader_N_Compact",
            "Fx_Team/Fx_Grp_Outline_ColorMask_Shader",
            "Fx_Team/Fx_Grp_MainCustom_Shader",
            "Fx_Team/Fx_Grp_Compact_Shader",
            "Shader Graphs/Fx_Grp_ScreenFliter",
        };

        foreach (string probe in probes)
        {
            Shader viaFind = Shader.Find(probe);
            bool viaIndex = loadedByName.ContainsKey(probe);
            BundleLog.Verbose(
                $"[ShaderRemapper]   probe '{probe}': Find={(viaFind != null ? "OK" : "NULL")} " +
                $"LoadedIndex={(viaIndex ? "HIT" : "MISS")}");
        }
    }

    private static string GetShaderLeaf(string name)
    {
        int slash = name.LastIndexOf('/');
        return slash >= 0 && slash < name.Length - 1 ? name[(slash + 1)..] : name;
    }

    private static Dictionary<string, Shader> BuildLoadedShaderIndex()
    {
        var index = new Dictionary<string, Shader>(StringComparer.Ordinal);
        try
        {
            Il2CppSystem.Object[] found = Resources.FindObjectsOfTypeAll(Il2CppType.Of<Shader>());
            if (found == null)
            {
                return index;
            }

            for (int i = 0; i < found.Length; i++)
            {
                Shader shader = found[i]?.TryCast<Shader>();
                if (shader == null)
                {
                    continue;
                }

                string name = shader.name;
                if (string.IsNullOrEmpty(name))
                {
                    continue;
                }

                // Prefer first resident instance; duplicates are common for stubs vs player.
                if (!index.ContainsKey(name))
                {
                    index[name] = shader;
                }
            }
        }
        catch (Exception ex)
        {
            BundleLog.VerboseWarn($"[ShaderRemapper] BuildLoadedShaderIndex failed: {ex.Message}");
        }

        return index;
    }

    private static string GetTransformPath(Transform t)
    {
        var stack = new Stack<string>();
        while (t != null)
        {
            stack.Push(t.name);
            t = t.parent;
        }

        return string.Join("/", stack);
    }
}
