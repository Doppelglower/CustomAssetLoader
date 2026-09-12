using System;
using System.Collections.Generic;

namespace CustomAssetLoader.Models;

/// <summary>
/// Parsed OfficialShaderSource tag. Grammar:
/// shader:key | material:key [|replaceTex:null|all|slot,...]
/// </summary>
public sealed class OfficialSourceSpec
{
    public enum ReplaceTexMode
    {
        None = 0,
        NullOnly = 1,
        All = 2,
        Whitelist = 3,
    }

    public string RawSpec { get; private set; }
    public string Kind { get; private set; }
    public string PrimaryKey { get; private set; }
    public ReplaceTexMode TexMode { get; private set; }
    public IReadOnlyList<string> TexSlots { get; private set; }

    /// <summary>Cache / Addressables key: "{kind}:{primaryKey}".</summary>
    public string LoadKey => $"{Kind}:{PrimaryKey}";

    public bool IsMaterial =>
        string.Equals(Kind, "material", StringComparison.OrdinalIgnoreCase);

    public bool IsShader =>
        string.Equals(Kind, "shader", StringComparison.OrdinalIgnoreCase);

    public bool WantsReplaceTex => TexMode != ReplaceTexMode.None;

    public static bool TryParse(string spec, out OfficialSourceSpec parsed, out string error)
    {
        parsed = null;
        error = null;

        if (string.IsNullOrWhiteSpace(spec))
        {
            error = "empty OfficialShaderSource tag";
            return false;
        }

        string raw = spec.Trim();
        string head = raw;
        ReplaceTexMode texMode = ReplaceTexMode.None;
        List<string> texSlots = null;

        int pipe = raw.IndexOf('|');
        if (pipe >= 0)
        {
            head = raw.Substring(0, pipe).Trim();
            string suffix = raw.Substring(pipe + 1).Trim();
            const string texPrefix = "replaceTex:";
            if (!suffix.StartsWith(texPrefix, StringComparison.OrdinalIgnoreCase))
            {
                error =
                    $"unsupported Source suffix '{suffix}' " +
                    "(expected |replaceTex:null|all|slot,... ; prefab: and |mat: removed)";
                return false;
            }

            string texArg = suffix.Substring(texPrefix.Length).Trim();
            if (string.IsNullOrEmpty(texArg))
            {
                error = "empty |replaceTex: value";
                return false;
            }

            if (string.Equals(texArg, "null", StringComparison.OrdinalIgnoreCase))
            {
                texMode = ReplaceTexMode.NullOnly;
            }
            else if (string.Equals(texArg, "all", StringComparison.OrdinalIgnoreCase))
            {
                texMode = ReplaceTexMode.All;
            }
            else
            {
                texMode = ReplaceTexMode.Whitelist;
                texSlots = new List<string>();
                foreach (string part in texArg.Split(','))
                {
                    string slot = part.Trim();
                    if (!string.IsNullOrEmpty(slot))
                    {
                        texSlots.Add(slot);
                    }
                }

                if (texSlots.Count == 0)
                {
                    error = "empty |replaceTex slot list";
                    return false;
                }
            }
        }

        int colon = head.IndexOf(':');
        if (colon <= 0)
        {
            error = $"invalid Source '{raw}' — expected shader:/material: prefix";
            return false;
        }

        string kind = head.Substring(0, colon).Trim().ToLowerInvariant();
        string primaryKey = head.Substring(colon + 1).Trim();
        if (string.IsNullOrEmpty(primaryKey))
        {
            error = $"empty primary key in Source '{raw}'";
            return false;
        }

        if (string.Equals(kind, "prefab", StringComparison.OrdinalIgnoreCase))
        {
            error =
                "prefab: Source is removed — use material:Assets/.../MatName.mat per slot";
            return false;
        }

        if (!string.Equals(kind, "shader", StringComparison.OrdinalIgnoreCase)
            && !string.Equals(kind, "material", StringComparison.OrdinalIgnoreCase))
        {
            error = $"unknown Source kind '{kind}' — use shader: or material:";
            return false;
        }

        if (texMode != ReplaceTexMode.None
            && !string.Equals(kind, "material", StringComparison.OrdinalIgnoreCase))
        {
            error = "|replaceTex: is only valid with material: (not shader:)";
            return false;
        }

        parsed = new OfficialSourceSpec
        {
            RawSpec = raw,
            Kind = kind,
            PrimaryKey = primaryKey,
            TexMode = texMode,
            TexSlots = texSlots ?? (IReadOnlyList<string>)Array.Empty<string>(),
        };
        return true;
    }
}
