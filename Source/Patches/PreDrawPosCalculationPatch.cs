using HarmonyLib;
using PauseWalker.Utilities;
using System.Reflection;
using System.Reflection.Emit;
using Verse;

namespace PauseWalker.Patches
{
    /// <summary>
    /// 修正时停 Pawn 的移动位置补间，使其在游戏暂停时仍能平滑移动。
    /// 原版 PreDrawPosCalculation 使用 TickRateMultiplier 计算补间步长，
    /// 而暂停时该倍率为 0，即使自定义 tick 已推进 Pawn 的寻路状态，补间也无法正常跟进。
    /// 本补丁仅修正当前方法内这一处 getter 的返回值，保留原版的其余位置计算逻辑。
    /// </summary>
    [HarmonyPatch(typeof(PawnTweener), nameof(PawnTweener.PreDrawPosCalculation))]
    public static class PreDrawPosCalculationPatch
    {
        // 缓存构造 IL 所需的方法元数据：前者用于准确定位 getter，后者作为新增 call 指令的目标。
        // 此处反射仅用于补丁装配；每次实际修正倍率时，通过 Publicizer 直接读取 tweener.pawn。
        private static readonly MethodInfo tickRateGetter = AccessTools.PropertyGetter(typeof(TickManager), nameof(TickManager.TickRateMultiplier));
        private static readonly MethodInfo adjustTickRateMethod = AccessTools.Method(typeof(PreDrawPosCalculationPatch), nameof(AdjustTickRate));

        /// <summary>
        /// 接收原 getter 已计算出的倍率，仅为暂停中的时停 Pawn 返回 1 倍速的补间倍率。
        /// </summary>
        /// <param name="originalValue">原 getter 的实际返回值，可能已被其他 mod 的补丁调整。</param>
        /// <param name="tweener">当前正在执行 PreDrawPosCalculation 的实例，用于确定其所属 Pawn。</param>
        private static float AdjustTickRate(float originalValue, PawnTweener tweener)
        {
            // 同时要求游戏暂停、Pawn 拥有主技能及 PauseWalkerHediff。
            // 仅仅暂停游戏不会让普通 Pawn 获得该修正；恢复时间后，即使 Hediff 尚未清理也不强制为 1。
            if (Current.Game != null && Find.TickManager?.CurTimeSpeed == TimeSpeed.Paused &&
                Utils.IsPauseWalkerPawn(tweener.pawn))
            {
                return 1f;
            }

            // 沿用传入的数值，不再次读取 getter，避免重复计算或重复触发其他 mod 的 getter 补丁。
            return originalValue;
        }

        /// <summary>
        /// 保留原 getter 调用，在其返回后插入一次局部修正。
        /// 概念上将原来的赋值改为：
        /// float tickRateMultiplier = AdjustTickRate(Find.TickManager.TickRateMultiplier, this);
        /// 不复制原方法、不替换全局 getter，也不依赖局部变量编号或 getter 前一条指令的形式。
        /// </summary>
        public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            // Transpiler 在 Harmony 装配补丁时执行，补丁链重建时也可能再次执行，并非每帧执行。
            // 先将当前输入序列保存为列表，完成所有匹配检查后再插入指令。
            var codes = instructions.ToList();
            if (tickRateGetter == null || adjustTickRateMethod == null)
            {
                // 所需方法无法解析时，不尝试构造缺失调用目标的 IL。
                // 当前补丁不生效可能影响时停移动的平滑度，但输入指令保持原样。
                Log.Error("[PauseWalker] Cannot resolve movement interpolation methods. Input IL is unchanged; paused movement interpolation may be affected.");
                return codes;
            }

            // 按准确的 MethodInfo 匹配，而不是仅比较方法名；Calls 同时识别 call 与 callvirt。
            // 扫描整个序列，确保目标调用唯一，避免原版或其他 Transpiler 改写方法后误选某一处调用。
            int matchIndex = -1;
            int matchCount = 0;
            for (int i = 0; i < codes.Count; i++)
            {
                if (codes[i].Calls(tickRateGetter))
                {
                    matchIndex = i;
                    matchCount++;
                }
            }

            if (matchCount != 1)
            {
                // 零匹配意味着原先的调用已不存在，多匹配则无法确认哪一处对应移动补间。
                // 两种情况均明确记录原因并返回输入 IL，不使用未经验证的位置继续修改。
                Log.Error($"[PauseWalker] Expected exactly one TickRateMultiplier getter call in PawnTweener.PreDrawPosCalculation, found {matchCount}. Input IL is unchanged; paused movement interpolation may be affected.");
                return codes;
            }

            // 原 getter 已消费 TickManager 实例，并将一个 float 返回值压入求值栈。
            // 只在其后增加两条指令，栈顶写在右侧：
            //   getter 返回后：[originalValue]
            //   ldarg.0 后：   [originalValue, this(PawnTweener)]
            //   call 后：      [adjustedValue]
            // 静态修正函数消费这两个参数，再返回一个 float，供原来的 stloc 等后续指令继续使用。
            // 不删除或改写任何原指令，它们携带的跳转标签与异常块标记也保留在原指令上。
            codes.InsertRange(matchIndex + 1, new[]
            {
                // 实例方法的参数 0 是当前 PawnTweener，也就是 C# 中的 this。
                new CodeInstruction(OpCodes.Ldarg_0),
                // 将栈上的原始倍率与当前实例传给修正函数，返回修正后的倍率。
                new CodeInstruction(OpCodes.Call, adjustTickRateMethod)
            });

            return codes;
        }
    }
}
