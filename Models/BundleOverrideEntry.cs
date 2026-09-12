namespace CustomAssetLoader.Models;

public sealed class BundleOverrideEntry
{
    public string label { get; set; } = string.Empty;
    public string resourceId { get; set; } = string.Empty;
    public string bundle { get; set; } = string.Empty;
    public string assetPath { get; set; } = string.Empty;

    /// <summary>GameObject, Sprite, or BattleEffectList</summary>
    public string assetType { get; set; } = "GameObject";

    public string GetKey() => $"{label}\0{resourceId}";
}
