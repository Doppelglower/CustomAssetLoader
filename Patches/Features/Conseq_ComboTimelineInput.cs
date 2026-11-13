using HarmonyLib;
using ModularSkillScripts;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;

namespace CustomAssetLoader.Patches.Features
{
    public class Conseq_ComboTimelineInput : IModularConsequence
    {
        public void ExecuteConsequence(ModularSA modular, string section, string circledSection, string[] circles)
        {
            // comboinput(id####) [up to 12 followers at the same time, id#### is required]
            if (circles.Length < 1 && circles.Length > 12)
                return;

            // getting a skill ID to record it in the dictionary along with the units we want to see in the combo
            // order is strict
            // Follower_0 - circles[0], Follower_1 - circles[1] etc
            BattleActionModel action = modular.modsa_selfAction;

            int currentSkillId = 0;

            if (modular.modsa_skillModel != null)
                currentSkillId = modular.modsa_skillModel.GetID();
            if (action != null)
                currentSkillId = action.Skill.GetID();

            // checking existance of the key
            if (CombosAndMore.skillids_and_followers.ContainsKey(currentSkillId.ToString()))
                return;

            // creating a new list to store only circles like id30905/id10411/id10613 etc
            Il2CppSystem.Collections.Generic.List<string> listOfFollowViews = new();
            foreach (var possibleView in circles)
            {
                
                listOfFollowViews.Add(possibleView);
                BundleLog.Verbose($"COMBO CONSEQ: ADDED {possibleView} TO SKILL {currentSkillId}");
            }
            CombosAndMore.skillids_and_followers.Add(currentSkillId.ToString(), listOfFollowViews);

        }
    }
    public class Conseq_ComboTimelineRefreshDirection : IModularConsequence
    {
        // comborefreshdir(bool,int,int,direction)

        // refreshes direction of the commander or followers
        public void ExecuteConsequence(ModularSA modular, string section, string circledSection, string[] circles)
        {
            if (circles.Length < 1)
                return;

            BattleActionModel action = modular.modsa_selfAction;

            int currentSkillId = 0;

            if (modular.modsa_skillModel != null)
                currentSkillId = modular.modsa_skillModel.GetID();
            if (action != null)
                currentSkillId = action.Skill.GetID();


            if (!CombosAndMore.skillids_and_refreshers.ContainsKey(currentSkillId.ToString())){
                Il2CppSystem.Collections.Generic.List<string> listOfFollowViews = new();
                CombosAndMore.skillids_and_refreshers.Add(currentSkillId.ToString(), listOfFollowViews);
            }
            CombosAndMore.skillids_and_refreshers[currentSkillId.ToString()].Add($"{circles[0]}/{circles[1]}/{circles[2]}/{circles[3]}/{circles[4]}");


            // use it after combo conseq!!!!

            // index - facing the certain target from _currentTargetViews list, -1 being clamped to 0 so you can just use -1 or 0

            // if: Direction None - Direction stays as it is [after clash or something]

                // setDirOfTarget - turn the actor to face the target's world position.

            // else: fixing Direction to other sides ig

        }
    }
    public static class CombosAndMore
    {
        public static Dictionary<string, Il2CppSystem.Collections.Generic.List<string>> skillids_and_followers = new();
        public static Dictionary<string, Il2CppSystem.Collections.Generic.List<string>> skillids_and_refreshers = new();
        [HarmonyPatch(typeof(BattleSkillViewBase), nameof(BattleSkillViewBase.Init))]
        [HarmonyPostfix]
        private static void Postfix_SkillModel_OnBattleStart(BattleSkillViewBase __instance, BattleActionLog actionLog, BattleSkillViewer viewer)
        {
            string skillIdtrue = viewer._currentSkillModel.GetID().ToString();

            MOTION_DETAIL backupMotion = __instance._skillViewer._currentSkillModel.GetSkillMotion();

            if (!skillids_and_followers.ContainsKey(skillIdtrue))
            {
                __instance._curSkillMotion = backupMotion;
                BundleLog.VerboseWarn($"COMBO CONSEQ: DOESNT CONTAIN {skillIdtrue}");
                return;
            }

            // list of followers
            skillids_and_followers.TryGetValue(skillIdtrue, out Il2CppSystem.Collections.Generic.List<string> listOfStringFollowViews);

            // clearing the followviews just in case
            if (__instance._followViews != null)
                __instance._followViews.Clear();
            else
                __instance._followViews = new();

            // follow direction: +1 if facing right, otherwise -1
            __instance._followDir = new();

            // adding followers in out list and checking their state
            foreach (var lmao in listOfStringFollowViews)
            {
                int unitId = int.Parse(lmao.Substring(2));
                BattleUnitView activeUnit = SingletonBehavior<BattleObjectManager>.Instance.GetViewByUnitID(unitId);
                if (!activeUnit.BattleCharacterState.IsDead)
                {
                    __instance._followViews.Add(activeUnit);
                    //__instance._followDir.Add(activeUnit._unitDirection == DIRECTION.RIGHT ? 1f : -1f);
                }
                else
                {
                    // if check is failed we're clearing the list and returning to the default skill motion we see in json 
                    __instance._followViews.Clear();
                    __instance._curSkillMotion = backupMotion;
                    BundleLog.VerboseWarn($"COMBO CONSEQ: FAILED THE CHECK");
                    return;
                }
            }

            BundleLog.Verbose($"COMBO CONSEQ: REPLACED FOLLOW VIEWS");

            // empty tween for some reason [FROM DECOMP]
            __instance.followMoveTween = new();
            __instance.followMoveTween.Add(null);

            // getting timelines from the CustomAssetLoader, I'll try to PR the public changes later
            CustomAssetLoader.Managers.BundleManager.KeyMap.TryGetValue($"Combo_{skillIdtrue}", out var bundleKey);
            CustomAssetLoader.Managers.BundleManager.TryLoadBundleForEntry(bundleKey, out var bundleAsset);
            CharacterFollowTimelineAssets comboObject = bundleAsset.LoadAsset<CharacterFollowTimelineAssets>(bundleKey.assetPath);
            BundleLog.Verbose($"COMBO CONSEQ: LOADED TIMELINES");
            // checking existance of S21 and adding obj [OTHERWISE FOR SOME REASON IT FAILS OTHER COINS] and then clearing them
            if (__instance._attackerView._curAppearance.GetMotion(MOTION_DETAIL.Empty) == null)
            {
                __instance._attackerView._curAppearance.AddMotion(MOTION_DETAIL.Empty);
                __instance._attackerView._curAppearance.GetMotion(MOTION_DETAIL.Empty).damageObj = new();
                __instance._attackerView._curAppearance.GetMotion(MOTION_DETAIL.Empty).motionObjects = new();
            }
            // replacing the timelines of S21, hoping that no enemy or id have them
            __instance._attackerView._curAppearance.GetMotion(MOTION_DETAIL.Empty).timelineAssets = comboObject.timelines;
            BundleLog.Verbose($"COMBO CONSEQ: REPLACED TIMELINES");
            // forcing the S21 and focus (ENCOUNTER VIEW), I don't want to see a lot of different combos during the BATTLE VIEW
            __instance._curSkillMotion = MOTION_DETAIL.Empty;
            __instance._skillViewType = VIEW_TYPE.ENCOUNTER;

            // adding|gettin crucial component that autobinds tracks on the timelines
            var controller = __instance._attackerView._curAppearance.GetOrAddComponent<CharacterAppearanceFollowController>();
            controller.Initialize(__instance._attackerView._curAppearance);
            controller.ResetFollowAppearance();
            __instance._attackerView._curAppearance.Initialize(__instance._attackerView._curAppearance._battleUnitView);
            controller.SetFollowAppearance(__instance._followViews);
            BundleLog.Verbose($"COMBO CONSEQ: SET FOLLOW APPEARANCE");



            DIRECTION directionEnum;
            bool directionBool;
            bool addUnitToTheFollowDir;
            if (skillids_and_refreshers.ContainsKey(skillIdtrue))
            {
                if (skillids_and_refreshers[skillIdtrue].Count > 0)
                {
                    foreach (string preparedRefresher in skillids_and_refreshers[skillIdtrue])
                    {
                        string[] circles = preparedRefresher.Split('/');
                        bool.TryParse(circles[0], out directionBool);
                        Enum.TryParse(circles[3], true, out directionEnum);
                        bool.TryParse(circles[4], out addUnitToTheFollowDir);
                        int commanderOrFollower = int.Parse(circles[2]);
                        __instance.RefreshDirection(directionBool, int.Parse(circles[1]), commanderOrFollower, directionEnum);
                        BundleLog.Verbose($"COMBO CONSEQ: REFRESHED DIRECTION");
                        if (addUnitToTheFollowDir)
                        {
                            switch (commanderOrFollower)
                            {
                                case -1:
                                    // brother you can't add your own commander to the followDir
                                    break;
                                default:
                                    __instance._followDir.Add(controller._followAppearance[commanderOrFollower]._battleUnitView._unitDirection == DIRECTION.RIGHT ? 1f : -1f);
                                    break;
                            }
                        }
                    }
                }
            }

            // positioning Follower relatively to the Commander

            //Vector3 center = __instance._attackerView.WorldPosition;
            //float scale = SingletonBehavior<BattleMapManager>.Instance._currentScaleFactor;
            //targetView.WorldPosition = center + Vector3.right * (__instance._attackDir * -2f * scale);
        }

        [HarmonyPatch(typeof(CharacterAppearanceFollowController), nameof(CharacterAppearanceFollowController.AutoBindByClipName))]
        [HarmonyPostfix]
        private static void AutoBindByClipName_Prefix(CharacterAppearanceFollowController __instance)
        {
            if (__instance.gameObject.GetComponent<CharacterApperacneResiver>().followController == null)
            {
                BundleLog.Verbose($"COMBO CONSEQ: ATTACHED FOLLOWER TO THE {__instance.gameObject.name} RESIVER");
                __instance.gameObject.GetComponent<CharacterApperacneResiver>().followController = __instance;
            }
        }

        // COMBO: clearing the list on round start clear [new battle/new turn/whatever]
        [HarmonyPatch(typeof(PassiveDetail), nameof(PassiveDetail.OnRoundStart_After_Event))]
        [HarmonyPostfix]
        private static void Postfix_PassiveDetail_OnRoundStart_After_Event1(BATTLE_EVENT_TIMING timing, PassiveDetail __instance)
        {
            skillids_and_followers.Clear();
            skillids_and_refreshers.Clear();
        }

        // COMBO: clearing the list on turn end ig just in case
        [HarmonyPatch(typeof(PassiveDetail), nameof(PassiveDetail.OnBattleEnd))]
        [HarmonyPostfix]
        private static void Postfix_PassiveDetail_OnBattleEnd1(BATTLE_EVENT_TIMING timing, PassiveDetail __instance)
        {
            skillids_and_followers.Clear();
            skillids_and_refreshers.Clear();
        }

        // for some reason requires patching NOTHING for custom EGO
        [HarmonyPatch(typeof(BattleCutScene_EGO), nameof(BattleCutScene_EGO.SetCutSceneMaterial), new Type[] { typeof(BattleSkillViewer), typeof(EgoStaticData), typeof(BattleCutSceneStaticData) })]
        [HarmonyPostfix]
        private static void BattleCutScene_EGO_PostFix(BattleSkillViewBase __instance, BattleSkillViewer viewer, EgoStaticData egoData, BattleCutSceneStaticData cutsceneData) { }

        [HarmonyPatch(typeof(BattleCutScene_EGO), nameof(BattleCutScene_EGO.SetCutSceneMaterial), new Type[] { typeof(BattleSkillViewer), typeof(EgoStaticData), typeof(BattleCutSceneStaticData) })]
        [HarmonyPrefix]
        private static void BattleCutScene_EGO_PreFix(BattleSkillViewBase __instance, BattleSkillViewer viewer, EgoStaticData egoData, BattleCutSceneStaticData cutsceneData) { }
    }
}