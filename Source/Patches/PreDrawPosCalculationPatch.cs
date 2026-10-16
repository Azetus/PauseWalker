using HarmonyLib;
using PauseWalker.Utilities;
using System.Reflection;
using System.Reflection.Emit;
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
    }
}
