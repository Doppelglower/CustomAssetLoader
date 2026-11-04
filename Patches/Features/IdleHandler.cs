using HarmonyLib;
using SD;
using System;
using UnityEngine;

namespace CustomAssetLoader.Patches.Features
{
    public class IdleLooper : MonoBehaviour
    {
        public IdleLooper(IntPtr ptr) : base(ptr) { }

        public bool IdleActive;
        private Animator _anim;
        private bool _failed;

        private void Update()
        {
            if (_failed || !IdleActive) return;

            try
            {
                if (_anim == null)
                {
                    _anim = GetComponentInChildren<Animator>(true);
                }

                if (_anim == null || _anim.runtimeAnimatorController == null) return;

                var st = _anim.GetCurrentAnimatorStateInfo(0);
                if (!st.loop && st.normalizedTime >= 0.98f)
                {
                    _anim.Play(st.fullPathHash, 0, 0f);
                }
            }
            catch (Exception e)
            {
                _failed = true;
                Plugin.Log.LogError($"[IdleLooper] disabled after exception: {e}");
            }
        }
    }

    internal class IdleHandler
    {
        [HarmonyPatch(typeof(CharacterAppearance), nameof(CharacterAppearance.ChangeMotion))]
        [HarmonyPostfix]
        private static void EnsureLoopedIdle(MOTION_DETAIL motiondetail, CharacterAppearance __instance)
        {
            if (__instance == null || __instance.GetComponentInParent<BundleInstanceTag>() == null)
                return;

            var spine = __instance.GetComponent<CharacterSpineSkin>();
            if (spine != null && !spine._isDisableSpine)
            {
                BundleLog.VerboseWarn($"character {__instance.GetName()} has a spine skin component that is enabled. Disable it if you are using looping on sprite renderers.");
                return;
            }

            var looper = __instance.GetComponent<IdleLooper>() ?? __instance.gameObject.AddComponent<IdleLooper>();

            looper.IdleActive = motiondetail == MOTION_DETAIL.Idle || motiondetail == MOTION_DETAIL.Default;
        }
    }
}
