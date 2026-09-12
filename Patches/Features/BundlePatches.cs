using System.Reflection;
using Addressable;
using HarmonyLib;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;
using UnityEngine.ResourceManagement.ResourceLocations;

namespace CustomAssetLoader.Patches.Features;

public static class BundlePatches
{
    public static void Apply(Harmony harmony)
    {
        MethodInfo getInitialized = AccessTools.PropertyGetter(
            typeof(AddressableManager),
            nameof(AddressableManager.IsInitializedAddressable));
        MethodInfo assetExists = AccessTools.Method(
            typeof(AddressableManager),
            nameof(AddressableManager.AssetExists),
            new[] { typeof(Il2CppSystem.Object) });
        MethodInfo loadUserDataAndSetScene = AccessTools.Method(
            typeof(GlobalGameManager),
            nameof(GlobalGameManager.LoadUserDataAndSetScene));
        MethodInfo changePhaseAppearance = AccessTools.Method(
            typeof(BattleUnitView),
            nameof(BattleUnitView.ChangePhaseAppearance),
            new[] { typeof(string) });
        MethodInfo createEnemyWave = AccessTools.Method(
            typeof(StageController),
            nameof(StageController.CreateEnemyWave),
            new[] { typeof(int) });
        MethodInfo instantiateByObject = AccessTools.Method(
            typeof(Addressables),
            nameof(Addressables.InstantiateAsync),
            new[]
            {
                typeof(Il2CppSystem.Object),
                typeof(Transform),
                typeof(bool),
                typeof(bool),
            });
        MethodInfo instantiateByObjectPos = AccessTools.Method(
            typeof(Addressables),
            nameof(Addressables.InstantiateAsync),
            new[]
            {
                typeof(Il2CppSystem.Object),
                typeof(Vector3),
                typeof(Quaternion),
                typeof(Transform),
                typeof(bool),
            });
        MethodInfo instantiateByLocation = AccessTools.Method(
            typeof(Addressables),
            nameof(Addressables.InstantiateAsync),
            new[]
            {
                typeof(IResourceLocation),
                typeof(Transform),
                typeof(bool),
                typeof(bool),
            });
        MethodInfo instantiateByLocationPos = AccessTools.Method(
            typeof(Addressables),
            nameof(Addressables.InstantiateAsync),
            new[]
            {
                typeof(IResourceLocation),
                typeof(Vector3),
                typeof(Quaternion),
                typeof(Transform),
                typeof(bool),
            });

        if (assetExists == null)
        {
            BundleLog.Error("Bundle patch target missing: AddressableManager.AssetExists");
            return;
        }

        harmony.Patch(
            assetExists,
            prefix: new HarmonyMethod(typeof(BundlePatches), nameof(Prefix_EnsureCatalog)),
            postfix: new HarmonyMethod(typeof(BundlePatches), nameof(Postfix_AssetExists)));

        if (getInitialized != null)
        {
            harmony.Patch(
                getInitialized,
                postfix: new HarmonyMethod(typeof(BundlePatches), nameof(Postfix_GetIsInitializedAddressable)));
        }

        if (loadUserDataAndSetScene != null)
        {
            harmony.Patch(
                loadUserDataAndSetScene,
                postfix: new HarmonyMethod(typeof(BundlePatches), nameof(Postfix_LoadUserDataAndSetScene)));
        }
        else
        {
            BundleLog.Error("Bundle patch target missing: GlobalGameManager.LoadUserDataAndSetScene");
        }

        if (changePhaseAppearance != null)
        {
            harmony.Patch(
                changePhaseAppearance,
                prefix: new HarmonyMethod(typeof(BundlePatches), nameof(Prefix_EnsureCatalog)));
        }

        if (createEnemyWave != null)
        {
            harmony.Patch(
                createEnemyWave,
                prefix: new HarmonyMethod(typeof(BundlePatches), nameof(Prefix_EnsureCatalog)));
        }

        if (instantiateByObject != null)
        {
            harmony.Patch(
                instantiateByObject,
                prefix: new HarmonyMethod(typeof(BundlePatches), nameof(Prefix_InstantiateAsync_Object)));
        }
        else
        {
            BundleLog.Warn("Missing Addressables.InstantiateAsync(object, Transform, ...)");
        }

        if (instantiateByObjectPos != null)
        {
            harmony.Patch(
                instantiateByObjectPos,
                prefix: new HarmonyMethod(typeof(BundlePatches), nameof(Prefix_InstantiateAsync_Object_PosRot)));
        }
        else
        {
            BundleLog.Warn(
                "Missing Addressables.InstantiateAsync(object, Vector3, Quaternion, ...)");
        }

        if (instantiateByLocation != null)
        {
            harmony.Patch(
                instantiateByLocation,
                prefix: new HarmonyMethod(typeof(BundlePatches), nameof(Prefix_InstantiateAsync_Location)));
        }

        if (instantiateByLocationPos != null)
        {
            harmony.Patch(
                instantiateByLocationPos,
                prefix: new HarmonyMethod(typeof(BundlePatches), nameof(Prefix_InstantiateAsync_Location_PosRot)));
        }

        // NOTE: Never Harmony-patch generic methods here (e.g. Addressables.LoadAssetAsync<T>,
        // AddressableManager.LoadAssetSync<T>). Under IL2CPP all reference-type instantiations
        // share one native body, so patching the <Sprite> instantiation corrupts every other T
        // (observed: DUI TextAsset loads re-typed to Sprite → InvalidKeyException).
        // Sprite loading is served natively via BundleManager's locator + ModAssetProvider.
        //
        // Full reload at plugin load (covers the window before LoadUserDataAndSetScene),
        // and again from LoadUserDataAndSetScene postfix (first load + Lethe hot reload).
        BundleManager.RequestReload();
        BundleLog.Verbose($"Bundle patches applied. Overrides: {BundleManager.OverrideCount}");
    }

    public static void Postfix_LoadUserDataAndSetScene()
    {
        BundleManager.RequestReload();
    }

    public static void Prefix_EnsureCatalog()
    {
        BundleManager.EnsureAddressablesRegisteredFromReadyGate();
    }

    public static void Postfix_AssetExists(Il2CppSystem.Object key, ref bool __result)
    {
        if (key == null)
        {
            return;
        }

        string keyStr = key.ToString();
        if (!BundleManager.IsModAddressableKey(keyStr))
        {
            return;
        }

        BundleManager.EnsureAddressablesRegisteredFromReadyGate();
        bool ready = BundleManager.IsReadyModAddressableKey(keyStr);
        BundleLog.Verbose(
            $"[CustomAssetLoader] AssetExists key={keyStr} native={__result} → ready={ready}");

        // Only upgrade false→true for keys we can actually serve; never force a
        // native key to false just because it resembles one of our resourceIds.
        if (ready)
        {
            __result = true;
        }
    }

    public static void Postfix_GetIsInitializedAddressable(ref bool __result)
    {
        if (!__result)
        {
            return;
        }

        BundleManager.EnsureAddressablesRegisteredFromReadyGate();
    }

    public static bool Prefix_InstantiateAsync_Object(
        Il2CppSystem.Object key,
        Transform parent,
        bool instantiateInWorldSpace,
        bool trackHandle,
        ref AsyncOperationHandle<GameObject> __result)
    {
        if (!BundleManager.TryGetModAddressableKey(key, out string addressableKey))
        {
            return true;
        }

        return TryCompleteWithDirectInstantiate(
            addressableKey,
            parent,
            setPose: false,
            default,
            default,
            ref __result);
    }

    public static bool Prefix_InstantiateAsync_Object_PosRot(
        Il2CppSystem.Object key,
        Vector3 position,
        Quaternion rotation,
        Transform parent,
        bool trackHandle,
        ref AsyncOperationHandle<GameObject> __result)
    {
        if (!BundleManager.TryGetModAddressableKey(key, out string addressableKey))
        {
            return true;
        }

        return TryCompleteWithDirectInstantiate(
            addressableKey,
            parent,
            setPose: true,
            position,
            rotation,
            ref __result);
    }

    public static bool Prefix_InstantiateAsync_Location(
        IResourceLocation location,
        Transform parent,
        bool instantiateInWorldSpace,
        bool trackHandle,
        ref AsyncOperationHandle<GameObject> __result)
    {
        if (!TryGetKeyFromLocation(location, out string key))
        {
            return true;
        }

        return TryCompleteWithDirectInstantiate(
            key,
            parent,
            setPose: false,
            default,
            default,
            ref __result);
    }

    public static bool Prefix_InstantiateAsync_Location_PosRot(
        IResourceLocation location,
        Vector3 position,
        Quaternion rotation,
        Transform parent,
        bool trackHandle,
        ref AsyncOperationHandle<GameObject> __result)
    {
        if (!TryGetKeyFromLocation(location, out string key))
        {
            return true;
        }

        return TryCompleteWithDirectInstantiate(
            key,
            parent,
            setPose: true,
            position,
            rotation,
            ref __result);
    }

    private static bool TryGetKeyFromLocation(IResourceLocation location, out string key)
    {
        key = null;
        if (location == null)
        {
            return false;
        }

        Il2CppSystem.Object locObj = location.Cast<Il2CppSystem.Object>();
        return BundleManager.TryGetModAddressableKey(locObj, out key);
    }

    private static bool TryCompleteWithDirectInstantiate(
        string addressableKey,
        Transform parent,
        bool setPose,
        Vector3 position,
        Quaternion rotation,
        ref AsyncOperationHandle<GameObject> __result)
    {
        BundleManager.EnsureAddressablesRegisteredFromReadyGate();

        BundleLog.Verbose(
            $"[CustomAssetLoader] Direct Instantiate bypass: {addressableKey} setPose={setPose}");

        if (!BundleManager.TryInstantiateByAddressableKey(addressableKey, parent, out GameObject instance)
            || instance == null)
        {
            BundleLog.Error($"[CustomAssetLoader] Direct Instantiate FAILED: {addressableKey}");
            return true;
        }

        if (setPose)
        {
            instance.transform.SetPositionAndRotation(position, rotation);
        }

        __result = Addressables.ResourceManager.CreateCompletedOperation(instance, string.Empty);
        BundleLog.Verbose(
            $"[CustomAssetLoader] Direct Instantiate OK: {addressableKey} go={instance.name}");

        return false;
    }

}
