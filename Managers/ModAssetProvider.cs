using Il2CppInterop.Runtime.Injection;
using UnityEngine.ResourceManagement.ResourceLocations;
using UnityEngine.ResourceManagement.ResourceProviders;

namespace CustomBundleLoader.Managers;

/// <summary>
/// Serves mod AssetBundle assets through Addressables.
/// Location PrimaryKey / InternalId is the Addressable key (label/id.suffix).
/// </summary>
public sealed class ModAssetProvider : ResourceProviderBase
{
    static ModAssetProvider()
    {
        ClassInjector.RegisterTypeInIl2Cpp<ModAssetProvider>();
    }

    public ModAssetProvider(IntPtr ptr) : base(ptr) { }

    public ModAssetProvider()
        : base(ClassInjector.DerivedConstructorPointer<ModAssetProvider>())
    {
        ClassInjector.DerivedConstructorBody(this);
        m_ProviderId = "CustomBundleLoader.ModAssetProvider";
    }

    public override void Provide(ProvideHandle provideHandle)
    {
        try
        {
            IResourceLocation location = provideHandle.Location;
            if (location == null)
            {
                provideHandle.Complete<UnityEngine.Object>(
                    null,
                    false,
                    new Il2CppSystem.Exception("ModAssetProvider: location is null"));
                return;
            }

            string key = location.PrimaryKey;
            if (string.IsNullOrEmpty(key))
            {
                key = location.InternalId;
            }

            if (string.IsNullOrEmpty(key)
                || !BundleManager.TryLoadUnityObject(key, location.ResourceType, out UnityEngine.Object asset)
                || asset == null)
            {
                BundleLog.Error($"[CustomBundleLoader] Provide FAILED: {key}");
                provideHandle.Complete<UnityEngine.Object>(
                    null,
                    false,
                    new Il2CppSystem.Exception($"ModAssetProvider: asset not found: {key}"));
                return;
            }

            BundleLog.Verbose(
                $"[CustomBundleLoader] Provide OK: {key} → {asset.name} ({asset.GetIl2CppType().Name})");

            provideHandle.Complete(asset, true, (Il2CppSystem.Exception)null);
        }
        catch (Exception ex)
        {
            provideHandle.Complete<UnityEngine.Object>(
                null,
                false,
                new Il2CppSystem.Exception($"ModAssetProvider.Provide failed: {ex.Message}"));
        }
    }

    public override void Release(IResourceLocation location, Il2CppSystem.Object obj)
    {
        // Bundle-owned assets; do not Destroy.
    }
}
