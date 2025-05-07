using Verse;

namespace PauseWalker.Utilities
{
    /// <summary>
    /// Rimworld使用 TickManager.ticksGameInt 来控制游戏时间的推进
    /// 每次执行 TickManager.DoSingleTick() 都会增加 tickGameInt 来推进时间
    /// 
    /// 而每个小人的工作（Jobs）的生成与分发需要依赖ticksGameInt，这部分逻辑在 Pawn_JobTracker.JobTrackerTick() 中
    /// 试了一下直接修改ticksGameInt是不行的，会影响很多其他东西
    /// 这里新建一个变量在游戏暂停时模拟时间继续推进
    /// 在游戏暂停时所有ticksGameInt的get方法都返回_simulatedTicksGameInt
    /// </summary>
    public static class SimulatedTickManager
    {
        // 用于模拟TicksGameInt
        private static int _simulatedTicksGameInt = 0;

        public static int SimulatedTicksGameInt
        {
            get
            {
                // 如果模拟值是0，就返回原始值
                if (_simulatedTicksGameInt != 0)
                    return _simulatedTicksGameInt;
                else
                    return PauseWalkerUtils.GetRawTicksGameInt();
            }
            private set
            {
                _simulatedTicksGameInt = value;
            }
        }

        // 初始化 _simulatedTicksGameInt 等于 TickManager.TicksGame
        public static void InitSimTick()
        {
            SimulatedTicksGameInt = PauseWalkerUtils.GetRawTicksGameInt();
        }

        // 将 _simulatedTicksGameInt 清零
        public static void ClearSimTick()
        {
            SimulatedTicksGameInt = 0;
        }

        // 做完 Ticks 之后要增加 _simulatedTicksGameInt
        public static void IncreaseSimTick()
        {
            if (SimulatedTicksGameInt == 0)
                InitSimTick();


            // 这里和rimworld中TickManager.DoSingleTick()内增加TicksGameInt的逻辑一致
            if (!DebugSettings.fastEcology)
            {
                SimulatedTicksGameInt++;
            }
            else
            {
                SimulatedTicksGameInt += 2000;
            }
        }

    }
}
