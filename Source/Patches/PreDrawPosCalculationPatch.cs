using HarmonyLib;
using PauseWalker.Utilities;
using System.Reflection;
using System.Reflection.Emit;
using UnityEngine;
using Verse;

namespace PauseWalker.Patches
{
    [HarmonyPatch(typeof(PawnTweener), nameof(PawnTweener.PreDrawPosCalculation))]
    public static class PreDrawPosCalculationPatch
    {

        private static float GetModifiedTickRate(Pawn pawn)
        {
            if (Utils.IsPauseWalkerPawn(pawn))
            {
                return 1f;
            }
            return Find.TickManager.TickRateMultiplier;
        }


        private static readonly FieldInfo f_pawn = AccessTools.Field(typeof(PawnTweener), "pawn");
        private static readonly MethodInfo m_getModifiedTickRate = AccessTools.Method(typeof(PreDrawPosCalculationPatch), nameof(GetModifiedTickRate));

        /**
         * 小人移动时渲染位置在 PreDrawPosCalculation 中修改
         * float tickRateMultiplier = Find.TickManager.TickRateMultiplier 依赖于 curTimeSpeed
         * 在特定的pawn执行这个方法的时候，让 tickRateMultiplier 等于 1f
         */
        public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            List<CodeInstruction> codes = instructions.ToList();

            // 查找 callvirt  instance float32 Verse.TickManager::get_TickRateMultiplier()
            int index = codes.FindIndex(instruction =>
            {
                return instruction.opcode == OpCodes.Callvirt &&
                    instruction.operand is MethodInfo method &&
                    method.Name == "get_TickRateMultiplier" &&
                    method.DeclaringType == typeof(Verse.TickManager);
            });
            codes.RemoveAt(index);     // remove the callvirt
            codes.RemoveAt(index - 1); // remove the call
            // 添加3条il进去
            codes.InsertRange(index - 1, new[]
            {
                new CodeInstruction(OpCodes.Ldarg_0),            // 加载 this（PawnTweener 实例）
                new CodeInstruction(OpCodes.Ldfld, f_pawn),      // 加载 this.pawn
                new CodeInstruction(OpCodes.Call, m_getModifiedTickRate) // 调用静态方法 GetModifiedTickRate
            });


            // 返回所有修改过的指令
            foreach (var code in codes)
                yield return code;

        }



        //public static bool Prefix(PawnTweener __instance)
        //{
        //    return PatchPrefixWithReflect(__instance);
        //}

        /**
         * 用prefix修改 PreDrawPosCalculation，基本逻辑和 PreDrawPosCalculation 没有区别
         * 在pawn满足特定条件的时候 float tickRateMultiplier = 1f
         * 
         * NOTE: 现在不需要用prefix修改了
         */
        private static bool PatchPrefixWithReflect(PawnTweener __instance)
        {
            if (Current.Game == null || Find.CurrentMap == null)
                return true;
            if (Find.TickManager.CurTimeSpeed == TimeSpeed.Paused)
            {
                var pawnVal = AccessTools.Field(typeof(PawnTweener), "pawn").GetValue(__instance);
                if (pawnVal is Pawn pawn)
                {
                    if (Utils.IsPauseWalkerPawn(pawn))
                    {
                        var lastDrawFrameField = AccessTools.Field(typeof(PawnTweener), "lastDrawFrame");
                        var lastDrawTickField = AccessTools.Field(typeof(PawnTweener), "lastDrawTick");
                        var tweenedPosField = AccessTools.Field(typeof(PawnTweener), "tweenedPos");
                        var lastTickSpringPosField = AccessTools.Field(typeof(PawnTweener), "lastTickSpringPos");

                        var TweenedPosRootMethod = AccessTools.Method(typeof(PawnTweener), "TweenedPosRoot");


                        int lastDrawFrame = (int)lastDrawFrameField.GetValue(__instance);
                        int lastDrawTick = (int)lastDrawTickField.GetValue(__instance);

                        if (lastDrawFrame == RealTime.frameCount)
                        {
                            return true;
                        }
                        if (!pawn.Spawned)
                        {
                            tweenedPosField.SetValue(__instance, pawn.Position.ToVector3Shifted());
                            return false;
                        }
                        if (lastDrawFrame < RealTime.frameCount - 1 && lastDrawTick < GenTicks.TicksGame - 1)
                        {
                            __instance.ResetTweenedPosToRoot();
                        }
                        else
                        {
                            lastTickSpringPosField.SetValue(__instance, tweenedPosField.GetValue(__instance));
                            float tickRateMultiplier = 1f;
                            if (tickRateMultiplier < 5f)
                            {
                                var tweenedPosRootMethod = AccessTools.Method(typeof(PawnTweener), "TweenedPosRoot");
                                Vector3 tweenedPosRoot = (Vector3)tweenedPosRootMethod.Invoke(__instance, null);
                                Vector3 tweenedPos = (Vector3)tweenedPosField.GetValue(__instance);

                                Vector3 a = tweenedPosRoot - tweenedPos;
                                float num = 0.09f * (RealTime.deltaTime * 60f * tickRateMultiplier);
                                if (RealTime.deltaTime > 0.05f)
                                {
                                    num = Mathf.Min(num, 1f);
                                }
                                tweenedPosField.SetValue(__instance, tweenedPos + a * num);

                            }
                            else
                            {
                                __instance.ResetTweenedPosToRoot();
                            }
                        }
                        lastDrawFrameField.SetValue(__instance, RealTime.frameCount);
                        lastDrawTickField.SetValue(__instance, GenTicks.TicksGame);


                        return false;
                    }
                }
            }

            return true;
        }
    }
}
