using System;
using System.Collections.Generic;
using System.Linq;
using Il2CppInterop.Runtime;
using UnityEngine;
using UnityEngine.AddressableAssets;
using Object = UnityEngine.Object;

namespace CustomBundleLoader.Managers;

/// <summary>
/// Resolve mode=2 OfficialShaderSource tags to a live Shader via Addressables.
/// Remapper SWAPs the shader onto the authored Material (parameters stay authored).
/// Tag grammar: shader:key | material:key [|replaceTex:null|all|slot,...]
/// </summary>
public static class OfficialShaderSource
{
    public const string SourceTag = "OfficialShaderSource";

    private static readonly Dictionary<string, Shader> ShaderCache =
        new(StringComparer.Ordinal);

    /// <summary>Source Material cached when kind is material: (key = LoadKey).</summary>
    private static readonly Dictionary<string, Material> MaterialCache =
        new(StringComparer.Ordinal);

    private static readonly HashSet<string> LoggedFailures =
        new(StringComparer.Ordinal);

    private static readonly HashSet<string> LoggedWarns =
        new(StringComparer.Ordinal);

    private static readonly HashSet<string> LoggedTexBind =
        new(StringComparer.Ordinal);

    public static void ResetSessionState()
    {
        ShaderCache.Clear();
        MaterialCache.Clear();
        LoggedFailures.Clear();
        LoggedWarns.Clear();
        LoggedTexBind.Clear();
    }

    /// <summary>
    /// Returns the Addressables Material for a previously resolved <c>material:</c> Source
    /// (null for shader: or if not cached yet).
    /// </summary>
    public static Material TryGetCachedSourceMaterial(string sourceSpec)
    {
        if (string.IsNullOrWhiteSpace(sourceSpec)
            || !OfficialSourceSpec.TryParse(sourceSpec, out OfficialSourceSpec parsed, out _)
            || !parsed.IsMaterial)
        {
            return null;
        }

        return MaterialCache.TryGetValue(parsed.LoadKey, out Material mat) ? mat : null;
    }

    public static bool TryParseSpec(
        string sourceSpec,
        out OfficialSourceSpec parsed,
        out string error)
    {
        return OfficialSourceSpec.TryParse(sourceSpec, out parsed, out error);
    }

    public static string ReadSourceSpec(Material mat)
    {
        if (mat == null)
        {
            return null;
        }

        try
        {
            string s = mat.GetTag(SourceTag, false);
            return string.IsNullOrWhiteSpace(s) ? null : s.Trim();
        }
        catch (Exception ex)
        {
            BundleLog.VerboseWarn(
                $"[OfficialShaderSource] GetTag('{SourceTag}') failed on mat='{mat.name}': {ex.Message}");
            return null;
        }
    }

    public static Shader Resolve(
        string sourceSpec,
        string stubShaderNameHint,
        out string error)
    {
        return Resolve(sourceSpec, stubShaderNameHint, excludeStubId: 0, out error);
    }

    public static Shader Resolve(
        string sourceSpec,
        string stubShaderNameHint,
        int excludeStubId,
        out string error)
    {
        error = null;
        if (string.IsNullOrWhiteSpace(sourceSpec))
        {
            error = "empty OfficialShaderSource tag";
            return null;
        }

        if (!OfficialSourceSpec.TryParse(sourceSpec, out OfficialSourceSpec parsed, out error))
        {
            return null;
        }

        if (ShaderCache.TryGetValue(parsed.LoadKey, out Shader cached) && cached != null)
        {
            return cached;
        }

        Object asset = LoadAddressable(parsed.PrimaryKey, out error);
        if (asset == null)
        {
            return null;
        }

        Shader shader = Extract(
            asset, parsed.Kind, stubShaderNameHint, out Material sourceMat, out error);
        if (shader == null)
        {
            return null;
        }

        string cacheKey = parsed.LoadKey;
        if (!string.IsNullOrEmpty(stubShaderNameHint)
            && !string.Equals(shader.name, stubShaderNameHint, StringComparison.Ordinal)
            && LoggedWarns.Add($"namehint:{cacheKey}:{stubShaderNameHint}"))
        {
            BundleLog.VerboseWarn(
                $"[OfficialShaderSource] stub name '{stubShaderNameHint}' != loaded '{shader.name}' " +
                $"(source='{parsed.RawSpec}'); applying Source shader anyway.");
        }

        if (LoggedWarns.Add($"resolve:{cacheKey}:{shader.name}"))
        {
            string texNote = parsed.IsMaterial && parsed.WantsReplaceTex
                ? $" replaceTex={DescribeReplaceTex(parsed)}"
                : parsed.IsMaterial ? " replaceTex=none" : string.Empty;
            BundleLog.Verbose(
                $"[OfficialShaderSource] {parsed.Kind}: Addressables shader id={shader.GetInstanceID()} " +
                $"name='{shader.name}'{texNote}");
        }

        ShaderCache[parsed.LoadKey] = shader;
        if (sourceMat != null)
        {
            MaterialCache[parsed.LoadKey] = sourceMat;
        }

        return shader;
    }

    private static string DescribeReplaceTex(OfficialSourceSpec parsed)
    {
        switch (parsed.TexMode)
        {
            case OfficialSourceSpec.ReplaceTexMode.NullOnly:
                return "null";
            case OfficialSourceSpec.ReplaceTexMode.All:
                return "all";
            case OfficialSourceSpec.ReplaceTexMode.Whitelist:
                return string.Join(",", parsed.TexSlots);
            default:
                return "none";
        }
    }

    public static void LogFailureOnce(string key, string message)
    {
        if (LoggedFailures.Add(key))
        {
            BundleLog.Error(message);
        }
    }

    private static Object LoadAddressable(string primaryKey, out string error)
    {
        error = null;
        if (string.IsNullOrEmpty(primaryKey))
        {
            error = "empty key";
            return null;
        }

        try
        {
            var handle = Addressables.LoadAssetAsync<Object>((Il2CppSystem.Object)primaryKey);
            Object asset = handle.WaitForCompletion();
            if (asset == null)
            {
                error = $"Addressables load failed key='{primaryKey}' status={handle.Status}";
                return null;
            }

            return asset;
        }
        catch (Exception ex)
        {
            error = $"Addressables load exception key='{primaryKey}': {ex.Message}";
            return null;
        }
    }

    private static Shader Extract(
        Object asset,
        string kind,
        string stubShaderNameHint,
        out Material sourceMat,
        out string error)
    {
        error = null;
        sourceMat = null;
        switch (kind)
        {
            case "shader":
            {
                Shader s = asset.TryCast<Shader>();
                if (s == null)
                {
                    error =
                        $"shader: key loaded {asset.GetType().Name} '{asset.name}', not Shader";
                    return null;
                }

                return s;
            }
            case "material":
            {
                Material mat = asset.TryCast<Material>();
                if (mat?.shader == null)
                {
                    error =
                        $"material: key loaded {asset.GetType().Name} '{asset.name}', not Material with shader";
                    return null;
                }

                sourceMat = mat;
                return mat.shader;
            }
            default:
                error = $"internal: unknown kind '{kind}'";
                return null;
        }
    }

    private static readonly string[] FxTextureProperties =
    {
        "_MainTex",
        "_Color_Tex",
        "_Noise_Tex",
        "_Dissolve_Tex",
        "_Mask_Tex",
        "_Normal_Tex",
        "_Distortion_Tex",
        "_Outline_Tex",
        "_Secondary_Tex",
        "_Ramp_Tex",
        "_Flow_Tex",
        "_Alpha_Tex",
    };

    /// <summary>
    /// Apply |replaceTex: from a parsed material: Source onto <paramref name="target"/>.
    /// </summary>
    public static int ApplyReplaceTexFromSource(
        Material target,
        OfficialSourceSpec parsed,
        string logPath = null)
    {
        if (target == null || parsed == null || !parsed.WantsReplaceTex)
        {
            return 0;
        }

        Material source = TryGetCachedSourceMaterial(parsed.RawSpec);
        if (source == null)
        {
            Resolve(
                parsed.RawSpec,
                target.shader != null ? target.shader.name : null,
                target.shader != null ? target.shader.GetInstanceID() : 0,
                out _);
            source = TryGetCachedSourceMaterial(parsed.RawSpec);
        }

        if (source == null || ReferenceEquals(source, target))
        {
            return 0;
        }

        IEnumerable<string> props = EnumerateTextureProps(parsed);
        int bound = 0;
        foreach (string prop in props)
        {
            try
            {
                if (!target.HasProperty(prop) || !source.HasProperty(prop))
                {
                    continue;
                }

                if (parsed.TexMode == OfficialSourceSpec.ReplaceTexMode.NullOnly
                    && target.GetTexture(prop) != null)
                {
                    continue;
                }

                Texture tex = source.GetTexture(prop);
                if (tex == null)
                {
                    continue;
                }

                target.SetTexture(prop, tex);
                bound++;
            }
            catch
            {
                // ignore per-property failures under IL2CPP
            }
        }

        if (bound > 0)
        {
            string key = $"{parsed.RawSpec}:{target.name}:{logPath}";
            if (LoggedTexBind.Add(key))
            {
                BundleLog.Verbose(
                    $"[OfficialShaderSource] replaceTex {DescribeReplaceTex(parsed)} " +
                    $"bound {bound} on mat='{target.name}' source='{parsed.RawSpec}'" +
                    (string.IsNullOrEmpty(logPath) ? "" : $" path='{logPath}'"));
            }
        }

        return bound;
    }

    private static IEnumerable<string> EnumerateTextureProps(OfficialSourceSpec parsed)
    {
        if (parsed.TexMode == OfficialSourceSpec.ReplaceTexMode.Whitelist)
        {
            return parsed.TexSlots;
        }

        return FxTextureProperties;
    }
}
