using PauseWalker.ModGameComponent;
using RimWorld;
using RimWorld.Planet;
using Verse;

namespace PauseWalker.Utilities
{
    public enum PauseWalkerState
    {
        Missing, ManualDisabled, Departed, NoAbility, NotColonist, Discarded, Invalid,
        QuestGuest, Gravship, AnomalyReturn, Borrowed, QuestReserved, Teleporting,
        Caravan, Transport, ForSale, Resurrecting, CorpsePresent, OnMap, Held, Unmanaged,
        Dead, Destroyed, Kidnapped, Lost, Unresolved
    }

    public static class PauseWalkerStateUtility
    {
        public static bool CanReturn(this PauseWalkerState state) => state is
            PauseWalkerState.Dead or PauseWalkerState.Destroyed or PauseWalkerState.Kidnapped or PauseWalkerState.Lost;

        public static bool ShouldUnregister(this PauseWalkerState state) => state is
            PauseWalkerState.Missing or PauseWalkerState.ManualDisabled or PauseWalkerState.Departed or
            PauseWalkerState.NoAbility or PauseWalkerState.NotColonist or PauseWalkerState.QuestGuest;

        public static string Description(this PauseWalkerState state) => ("PauseWalker.State." + state).Translate();

        // 所有自动回归入口共用此只读判断；前面的排除条件优先于后面的回归条件。
        public static PauseWalkerState Evaluate(Pawn? pawn, PauseWalkerManager? manager)
        {
            // 无对象时无法定位、复活或保留有效追踪。
            if (pawn == null) return PauseWalkerState.Missing;
            // 手动停用不能被技能构造、重新招募或读档自动撤销。
            if (manager?.GetExclusion(pawn) == ManagementExclusion.Manual) return PauseWalkerState.ManualDisabled;
            // 已确认的结局离队、卖出殖民地不属于异常丢失。
            if (manager?.GetExclusion(pawn) == ManagementExclusion.Departed) return PauseWalkerState.Departed;
            // 主技能被移除后，不再提供自动回归。
            if (!Utils.HasPauseWalkerAbility(pawn)) return PauseWalkerState.NoAbility;
            // Discard 可能已清除关键数据，不强行恢复其状态。
            if (pawn.Discarded) return PauseWalkerState.Discarded;
            // 缺少原版所需组件时交给人工检查，不尝试生成。
            if (!HasRequiredData(pawn)) return PauseWalkerState.Invalid;
            // 不自动夺回被放逐、出售或通过其他方式转阵营的角色。
            if (!pawn.IsColonist) return PauseWalkerState.NotColonist;
            // 暂时借给玩家的任务角色不应永久留在殖民地。
            if (pawn.IsQuestLodger()) return PauseWalkerState.QuestGuest;
            // 重力船飞行时可能暂时没有地图，但乘员并未丢失。
            if (Find.CurrentGravship?.Pawns.Contains(pawn) == true) return PauseWalkerState.Gravship;
            // 异常结局有原版暂存及自动送回流程，不能抢先拉回。
            if (ModsConfig.AnomalyActive && Find.Anomaly?.metalHellPawns.Contains(pawn) == true)
                return PauseWalkerState.AnomalyReturn;
            // 借出任务应自行处理返回及死亡，不能提前结束任务流程。
            if (pawn.IsBorrowedByAnyFaction()) return PauseWalkerState.Borrowed;
            // 任务保留和生成中的对象先交给任务系统管理。
            if (QuestUtility.IsReservedByQuestOrQuestBeingGenerated(pawn)) return PauseWalkerState.QuestReserved;
            // 传送期间的无地图状态是正常过渡状态。
            if (pawn.teleporting) return PauseWalkerState.Teleporting;
            // 正常远行队成员本来就属于 WorldPawns，不能据此判为失联。
            if (pawn.GetCaravan() != null) return PauseWalkerState.Caravan;
            // 原版运输判断也会识别残留容器；宁可漏报，由调试菜单兜底。
            if (PawnUtility.IsTravelingInTransportPodWorldObject(pawn)) return PauseWalkerState.Transport;
            // 待售角色仍由结算点管理，不能当作自由 WorldPawn 拉回。
            if (PawnUtility.ForSaleBySettlement(pawn)) return PauseWalkerState.ForSale;
            // 原版拒绝死亡正在执行复活时，不并行触发第二次复活。
            if (pawn.health.hediffSet.hediffs.OfType<Hediff_DeathRefusal>().Any(hediff => hediff.InProgress))
                return PauseWalkerState.Resurrecting;
            // 尸体在墓穴、容器或运输中也算存在，不发送远程复活信。
            if (Utils.HasUsableCorpse(pawn)) return PauseWalkerState.CorpsePresent;
            // 任意地图上的活人都不是失联对象，包括未观看的地图和休眠者。
            if (pawn.Spawned || pawn.MapHeld != null) return PauseWalkerState.OnMap;
            // 未识别的持有者可能是其他 mod 的正常流程，自动模式不拆容器。
            if (pawn.ParentHolder != null || pawn.holdingOwner != null) return PauseWalkerState.Held;
            // 旧信及未管理对象不能绕过 Manager 的当前登记状态。
            if (manager == null || !manager.IsTracked(pawn)) return PauseWalkerState.Unmanaged;
            // 仅有 Manager 引用而不在 WorldPawns 时，不推断其已死亡或丢失。
            if (Find.WorldPawns == null || !Find.WorldPawns.Contains(pawn)) return PauseWalkerState.Unresolved;
            // 真正死亡且尸体不存在时，允许通过原版 API 复活。
            if (pawn.Dead) return PauseWalkerState.Dead;
            // 保留已销毁未死亡的兜底能力，正式离队已在前面排除。
            if (pawn.Destroyed) return PauseWalkerState.Destroyed;
            // 有明确绑架记录的玩家角色可回归，发信时不提前清除记录。
            if (PawnUtility.IsKidnappedPawn(pawn)) return PauseWalkerState.Kidnapped;
            // 仅 Free 且没有地图、容器及正常任务归属的存活玩家角色自动回归。
            if (pawn.HostFaction == null && Find.WorldPawns.GetSituation(pawn) == WorldPawnSituation.Free)
                return PauseWalkerState.Lost;
            // 首领、开局遗留等其他情况不推断为异常，由人工决定。
            return PauseWalkerState.Unresolved;
        }

        public static bool HasRequiredData(Pawn pawn) => pawn.def?.race != null && pawn.health?.hediffSet != null
            && PawnUtility.InValidState(pawn)
            // needs、mindState、stances 在原版死亡清理时会置空，交由复活 API 重建。
            && pawn.story != null && pawn.ageTracker != null && pawn.relations != null;
    }
}
