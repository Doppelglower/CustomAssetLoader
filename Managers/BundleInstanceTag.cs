using Il2CppInterop.Runtime.Injection;
using UnityEngine;

namespace CustomBundleLoader.Managers;

/// <summary>
/// Runtime-only provenance marker attached exclusively by this plugin's
/// bundle instantiation path. Vanilla objects never receive this component.
/// </summary>
public sealed class BundleInstanceTag : MonoBehaviour
{
    public BundleInstanceTag(IntPtr ptr) : base(ptr) { }

    public BundleInstanceTag()
        : base(ClassInjector.DerivedConstructorPointer<BundleInstanceTag>())
    {
        ClassInjector.DerivedConstructorBody(this);
    }
}
