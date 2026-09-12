namespace CustomAssetLoader.Models;

public sealed class AssetManifest
{
    public List<BundleOverrideEntry> overrides { get; set; } = new();
}
