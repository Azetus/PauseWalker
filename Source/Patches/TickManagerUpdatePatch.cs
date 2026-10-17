using HarmonyLib;
using PauseWalker.Utilities;
using RimWorld;
using RimWorld.Planet;
using System.Diagnostics;
using UnityEngine;
using Verse;

namespace PauseWalker.Patches
{
    /// <summary>
    /// TickManagerUpdate 应该是游戏执行所有Tick的入口
    /// 如果curTimeSpeed == TimeSpeed.Paused则什么也不会做
    /// 在postfix让特定小人绕开暂停
    /// </summary>
    [HarmonyPatch(typeof(TickManager), nameof(TickManager.TickManagerUpdate))]
    public static class TickManagerUpdatePatch
    {
        // 帧时间累积量（秒），与 SimulatedTickManager 保存的模拟整数 Tick 分开管理。
        static float realTimeToTickThrough;
        static int ticksThisFrame;
        const float curTimePerTick = 1f / 60f; // 1 秒对应 60 次 Tick（假设是 1 倍速）
        static Stopwatch clock = new Stopwatch();

        public static void Postfix()
        {
            SimulateNormalSpeed();
        }


        // 模拟游戏一倍速运行，这部分和rimworld的TickManagerUpdate基本一致
        private static void SimulateNormalSpeed()
        {
            ticksThisFrame = 0;

            // 没有活跃时停 Pawn 时不进入计时循环，也不保留上一段模拟的帧时间余量。
            if (!CanSimulatePausedTicks())
            {
                realTimeToTickThrough = 0f;
                return;
            }

            if (Mathf.Abs(Time.deltaTime - curTimePerTick) < curTimePerTick * 0.1f)
            {
                realTimeToTickThrough += curTimePerTick;
            }
            else
            {
                realTimeToTickThrough += Time.deltaTime;
            }

            clock.Reset();
            clock.Start();

            while (realTimeToTickThrough > 0f && ticksThisFrame < 2f)
            {
                // 在暂停状态下执行需要做的Ticks
                DoPausedTick();

                realTimeToTickThrough -= curTimePerTick;
                ticksThisFrame++;
                // Tick 内的回调可能恢复时间、切换视角或移除最后一个时停标记。
                if (!CanSimulatePausedTicks())
                {
                    // 仅丢弃帧时间余量；保留模拟整数 Tick，返回地图后从原值继续推进。
                    realTimeToTickThrough = 0f;
                    break;
                }

                if (clock.ElapsedMilliseconds > 45.454544f)
                {
                    break;
                }
            }


            if (realTimeToTickThrough > 0f)
            {
                realTimeToTickThrough = 0f;
            }
        }

        private static bool CanSimulatePausedTicks()
        {
            if (Current.Game == null || Find.TickManager == null ||
                Find.TickManager.CurTimeSpeed != TimeSpeed.Paused || !WorldRendererUtility.DrawingMap)
                return false;

            var currentMap = Find.CurrentMap;
            return currentMap != null && Utils.CurrentMapContainsPauseWalker(currentMap);
        }

        // 绕开游戏的 TickManagerUpdate，单独执行一些 DoSingleTick() 内的内容
        private static void DoPausedTick()
        {
            var currentMap = Find.CurrentMap;
            if (currentMap == null)
                return;
            if (!Utils.CurrentMapContainsPauseWalker(currentMap))
                return;
            // MapPreTick
            TickPathFinder(currentMap);
            // 参考原本 DoSingleTick 的逻辑，在这里要增加 TicksGameInt，推进游戏时间流逝，不过这里增加的是模拟时间
            SimulatedTickManager.IncreaseSimTick();
            DoTickListNormal(currentMap);
            // MapPostTick
            TickPausedFlecks(currentMap);
        }

        private static void DoTickListNormal(Map currentMap)
        {
            var tickListNormal = Find.TickManager.tickListNormal;
            var thingsToRegister = tickListNormal.thingsToRegister;
            var thingsToDeregister = tickListNormal.thingsToDeregister;
            var thingLists = tickListNormal.thingLists;

            // 分桶和间隔计算复用原版实现；队列维护及选择性 Tick 的顺序保持不变。
            int interval = tickListNormal.TickInterval;
            int index = Find.TickManager.TicksGame % interval;
            if (index >= 0 && index < thingLists.Count)
            {
                List<Thing> list2 = thingLists[index];

                for (int i = 0; i < thingsToRegister.Count; i++)
                {
                    tickListNormal.BucketOf(thingsToRegister[i]).Add(thingsToRegister[i]);
                }

                thingsToRegister.Clear();
                for (int j = 0; j < thingsToDeregister.Count; j++)
                {
                    tickListNormal.BucketOf(thingsToDeregister[j]).Remove(thingsToDeregister[j]);
                }

                thingsToDeregister.Clear();
                for (int m = 0; m < list2.Count; m++)
                {
                    var itemToTick = list2[m];
                    // 注册/注销维护全局列表；实际 Tick 仅推进当前地图，与寻路和 Fleck 范围一致。
                    if (!itemToTick.Destroyed && itemToTick.Map == currentMap)
                    {
                        try
                        {
                            TickPausedThing(itemToTick);
                        }
                        catch (Exception ex)
                        {
                            string text = itemToTick.Spawned ? (" (at " + itemToTick.Position + ")") : "";
                            if (Prefs.DevMode)
                            {
                                Log.Error(string.Concat(new object[]
                                {
                                    "[Pause Walker] Exception ticking ",
                                    itemToTick.ToStringSafe<Thing>(),
                                    text,
                                    ": ",
                                    ex
                                }));
                            }
                            else
                            {
                                Log.ErrorOnce(string.Concat(new object[]
                                {
                                    "[Pause Walker] Exception ticking ",
                                    itemToTick.ToStringSafe<Thing>(),
                                    text,
                                    ". Suppressing further errors. Exception: ",
                                    ex
                                }), itemToTick.thingIDNumber ^ 576876901);
                            }
                        }
                    }
                }
            }
        }

        /// <summary>
        /// 推进已通过当前地图与存活检查的单个对象；调用方负责捕获并隔离异常。
        /// 类型筛选、对象 Tick 与 Pawn 的视觉更新集中在这里，保持原有执行顺序。
        /// </summary>
        private static void TickPausedThing(Thing itemToTick)
        {
            switch (itemToTick)
            {
                // Pawn 在此结束判断，不依赖通用 Thing 兜底再次排除普通角色。
                case Pawn pawn:
                    if (!Utils.IsPauseWalkerPawn(pawn))
                        break;
                    pawn.DoTick();
                    // 更新小人贴图，每次 Pawn Tick 后推进 1 Tick 的视觉状态；帧内累计次数仅用于控制外层循环。
                    CellRect viewRect = Find.CameraDriver.CurrentViewRect.ExpandedBy(3);
                    pawn.ProcessPostTickVisuals(1, viewRect);
                    break;
                // 射弹在专用分支结束判断，避免非时停角色的射弹落入生成时间兜底。
                case Projectile projectile:
                    if (IsPauseProjectile(projectile))
                        projectile.DoTick();
                    break;
                case ThingWithComps projectile when IsCombatExtendedProjectile(projectile):
                    if (IsPauseProjectile(projectile))
                        projectile.DoTick();
                    break;
                // 爆炸必须通过来源判定，不能由生成时间兜底放行。
                case Explosion explosion:
                    if (IsPauseExplosion(explosion))
                        explosion.DoTick();
                    break;
                case IncineratorSpray incincerSpray when IsPauseIncineratorSpray(incincerSpray):
                    incincerSpray.DoTick();
                    break;
                // 新建的门也必须满足触碰条件，不能仅凭生成时间获得 Tick。
                case Building_Door door:
                    if (ShouldDoorOpenInPause(door))
                        door.DoTick();
                    break;
                case Mote mote when IsPauseWalkerMote(mote):
                    mote.DoTick();
                    break;
                // 仅允许本 mod 的压路机，其他新生成的下落物也保持冻结。
                case Skyfaller sf:
                    if (IsRoadRoller(sf))
                        sf.DoTick();
                    break;
                case Thing thing when IsThingAfterPause(thing):
                    thing.DoTick();
                    break;
            }
        }

        private static void TickPathFinder(Map currentMap)
        {
            if (currentMap == null) return;

            try
            {
                currentMap.pathFinder.PathFinderTick();
            }
            catch (Exception ex3)
            {
                Log.Error(ex3.ToString());
            }
        }

        private static bool IsPauseProjectile(Projectile projectile)
        {
            if (projectile == null)
                return false;
            if (projectile.Launcher is Pawn launcherPawn)
            {
                return Utils.IsPauseWalkerPawn(launcherPawn);
            }

            return false;
        }

        private static bool IsCombatExtendedProjectile(ThingWithComps? thing)
        {
            // CE 为可选依赖，按继承链识别其射弹；普通 ThingWithComps 仍交给后续分支。
            for (Type? type = thing?.GetType(); type != null; type = type.BaseType)
            {
                if (type.FullName == "CombatExtended.ProjectileCE")
                    return true;
            }

            return false;
        }

        private static bool IsPauseProjectile(ThingWithComps thing)
        {
            if (thing == null)
                return false;
            // 仅在 switch 已识别为 CE 射弹后访问 launcher，不依赖 CE 程序集。
            if (AccessTools.Field(thing.GetType(), "launcher") is { } launcherField &&
                launcherField.GetValue(thing) is Pawn launcherPawn)
            {
                return Utils.IsPauseWalkerPawn(launcherPawn);
            }

            return false;
        }

        private static bool IsPauseExplosion(Explosion explosion)
        {
            if (explosion == null)
                return false;
            if (explosion.instigator is Pawn launcherPawn)
                return Utils.IsPauseWalkerPawn(launcherPawn);
            // 原版 Skyfaller.Impact 以空来源创建压路机爆炸，仅为本 mod 的伤害类型保留例外。
            return explosion.damType?.defName == "RoadRollerExplosion" &&
                   explosion.spawnedTick > Utils.GetRawTicksGameInt();
        }

        private static bool IsPauseIncineratorSpray(IncineratorSpray spray)
        {
            if (spray == null)
                return false;
            return spray.spawnedTick > Utils.GetRawTicksGameInt();
        }

        private static bool IsPauseWalkerMote(Mote mote)
        {
            if (mote == null) return false;
            return mote.spawnedTick > Utils.GetRawTicksGameInt();
        }

        private static bool IsRoadRoller(Skyfaller sf)
        {
            return sf != null && sf.Spawned && sf.Map != null && sf.def?.defName == "RoadRollerIncoming" &&
                   sf.spawnedTick >= Utils.GetRawTicksGameInt();
        }

        private static bool IsThingAfterPause(Thing thing)
        {
            if (thing == null)
                return false;
            if (thing is Fire)
                return false;
            if (thing is Pawn)
                return false;

            return thing.spawnedTick > Utils.GetRawTicksGameInt();
        }

        private static bool ShouldDoorOpenInPause(Building_Door door)
        {
            if (door == null) return false;
            return door.lastFriendlyTouchTick > Utils.GetRawTicksGameInt();
        }

        // 只推进符合时间戳条件的游戏时间粒子；现实时间粒子仍由原版 Update() 处理。
        private static void TickPausedFlecks(Map? currentMap)
        {
            if (Current.Game == null || Find.TickManager == null || currentMap?.flecks is not { } manager ||
                manager.parent != currentMap || manager.systems == null)
                return;

            var systems = manager.Systems?.ToList();
            if (systems == null) return;
            int currentRawTick = Utils.GetRawTicksGameInt();

            // 先取得系统快照，TimeInterval 的附带行为即使改变系统集合，也不影响本次枚举。
            foreach (var system in systems)
            {
                if (system == null) continue;
                try
                {
                    // 仅接管准确的原版类型，避免绕过自定义子类覆写的 Tick()。
                    switch (system)
                    {
                        case FleckSystemStatic staticSystem when system.GetType() == typeof(FleckSystemStatic):
                            TickSelectedFlecks(staticSystem, currentMap, currentRawTick, static fleck => fleck.setupTick);
                            break;
                        case FleckSystemThrown thrownSystem when system.GetType() == typeof(FleckSystemThrown):
                            TickSelectedFlecks(thrownSystem, currentMap, currentRawTick, static fleck => fleck.baseData.setupTick);
                            break;
                        case FleckSystemSplash splashSystem when system.GetType() == typeof(FleckSystemSplash):
                            TickSelectedFlecks(splashSystem, currentMap, currentRawTick, static fleck => fleck.setupTick);
                            break;
                    }
                }
                catch (Exception ex)
                {
                    // 单个系统失败不阻断其他系统；每种系统只记录一次，避免每帧刷屏。
                    Log.ErrorOnce($"[PauseWalker] Exception ticking paused Fleck system {system.GetType().FullName}: {ex}",
                        system.GetType().GetHashCode() ^ 0x51F1EC);
                }
            }
        }

        private static void TickSelectedFlecks<TFleck>(FleckSystemBase<TFleck>? system, Map? currentMap,
            int currentRawTick, Func<TFleck, int>? getSetupTick) where TFleck : struct, IFleck
        {
            if (system == null || currentMap == null || getSetupTick == null || system.parent?.parent != currentMap)
                return;
            var particles = system.dataGametime;
            var removeIndices = system.tmpRemoveIndices;
            if (particles == null || removeIndices == null) return;

            // 沿用原版 Tick 的倒序更新、结构体写回和批量移除，仅额外筛选生成时间。
            try
            {
                for (int i = particles.Count - 1; i >= 0; i--)
                {
                    TFleck fleck = particles[i];
                    // 保留现有时间戳规则；跨次时停的模拟时间误判仍按 R2 接受。
                    if (getSetupTick(fleck) <= currentRawTick) continue;
                    if (fleck.TimeInterval(1f / 60f, currentMap))
                        removeIndices.Add(i);
                    else
                        particles[i] = fleck;
                }

                particles.RemoveBatchUnordered(removeIndices);
            }
            finally
            {
                // 即使原版粒子更新抛出异常，也不把旧索引留给后续 Tick 或 Update。
                removeIndices.Clear();
            }
        }
    }
}