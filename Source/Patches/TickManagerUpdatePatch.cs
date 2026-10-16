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

            if (Current.Game == null || Find.CurrentMap == null || !WorldRendererUtility.DrawingMap || Find.TickManager.CurTimeSpeed != TimeSpeed.Paused)
                return;

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
            var tickType = tickListNormal.tickType;
            var thingsToRegister = tickListNormal.thingsToRegister;
            var thingsToDeregister = tickListNormal.thingsToDeregister;
            var thingLists = tickListNormal.thingLists;

            int interval = GetTickInterval(tickType);
            int index = Find.TickManager.TicksGame % interval;
            if (index >= 0 && index < thingLists.Count)
            {
                List<Thing> list2 = thingLists[index];

                for (int i = 0; i < thingsToRegister.Count; i++)
                {
                    BucketOf(thingsToRegister[i], thingLists, tickType).Add(thingsToRegister[i]);
                }
                thingsToRegister.Clear();
                for (int j = 0; j < thingsToDeregister.Count; j++)
                {
                    BucketOf(thingsToDeregister[j], thingLists, tickType).Remove(thingsToDeregister[j]);
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
                            switch (itemToTick)
                            {
                                case Pawn pawn when Utils.IsPauseWalkerPawn(pawn):
                                    pawn.DoTick();
                                    // 更新小人贴图，每次 Pawn Tick 后推进 1 Tick 的视觉状态；帧内累计次数仅用于控制外层循环。
                                    CellRect viewRect = Find.CameraDriver.CurrentViewRect.ExpandedBy(3);
                                    pawn.ProcessPostTickVisuals(1, viewRect);
                                    break;
                                // 处理投射物
                                case Projectile projectile when IsPauseProjectile(projectile):
                                    projectile.DoTick();
                                    break;
                                case ThingWithComps projectile when IsPauseProjectile(projectile):
                                    projectile.DoTick();
                                    break;
                                case Explosion explosion when IsPauseExplosion(explosion):
                                    explosion.DoTick();
                                    break;
                                case IncineratorSpray incincerSpray when IsPauseIncineratorSpray(incincerSpray):
                                    incincerSpray.DoTick();
                                    break;
                                case Building_Door door when ShouldDoorOpenInPause(door):
                                    door.DoTick();
                                    break;
                                case Mote mote when IsPauseWalkerMote(mote):
                                    mote.DoTick();
                                    break;
                                case Skyfaller sf when IsRoadRoller(sf):
                                    sf.DoTick();
                                    break;
                                case Thing thing when IsThingAfterPause(thing):
                                    thing.DoTick();
                                    break;
                            }
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

        private static int GetTickInterval(TickerType tickType)
        {
            switch (tickType)
            {
                case TickerType.Normal:
                    return 1;
                case TickerType.Rare:
                    return 250;
                case TickerType.Long:
                    return 2000;
                default:
                    return -1;
            }
        }

        private static List<Thing> BucketOf(Thing t, List<List<Thing>> thingLists, TickerType tickType)
        {
            int num = t.GetHashCode();
            if (num < 0)
            {
                num *= -1;
            }
            int index = num % GetTickInterval(tickType);
            return thingLists[index];
        }

        private static bool IsPauseProjectile(Projectile projectile)
        {
            if (projectile == null)
                return false;
            if (projectile.Launcher is Pawn launcherPawn)
            {
                Utils.IsPauseWalkerPawn(launcherPawn);
            }
            return false;
        }

        private static bool IsPauseProjectile(ThingWithComps thing)
        {
            if (thing == null)
                return false;
            //处理CE模组的 ProjectileCE
            if (AccessTools.Field(thing.GetType(), "launcher") is { } launcherField &&
                        launcherField.GetValue(thing) is Pawn launcherPawn)
            {
                return Utils.IsPauseWalkerPawn(launcherPawn);

            }
            return false;
        }

        private static bool IsPauseExplosion(Explosion explosion)
        {
            if (explosion != null && explosion.instigator is Pawn launcherPawn)
                return Utils.IsPauseWalkerPawn(launcherPawn);
            return false;
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
            return sf.Spawned && sf.Map != null && sf.def.defName.Contains("RoadRollerIncoming") && sf.spawnedTick >= Utils.GetRawTicksGameInt();
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

        // 处理枪口火光
        private static void TickPausedFlecks(Map currentMap)
        {
            if (currentMap == null)
                return;
            if (currentMap.flecks is { } curMapFlecksManager)
            {
                var currentRawTick = Utils.GetRawTicksGameInt();
                // Select systems before ticking, since Tick() can modify Fleck state.
                var targetSys = curMapFlecksManager.Systems
                    .Where(system => HasFleckAfterPause(system, currentRawTick))
                    .ToList();

                foreach (var item in targetSys)
                {
                    if (item != null)
                        item.Tick();
                }
            }
        }

        private static bool HasFleckAfterPause(FleckSystem system, int currentRawTick)
        {
            // Match generic bases so subclasses using the same Fleck data are supported too.
            switch (system)
            {
                case FleckSystemBase<FleckStatic> staticSystem:
                    foreach (var fleck in staticSystem.dataGametime)
                    {
                        if (fleck.setupTick > currentRawTick)
                            return true;
                    }
                    break;
                case FleckSystemBase<FleckThrown> thrownSystem:
                    foreach (var fleck in thrownSystem.dataGametime)
                    {
                        if (fleck.baseData.setupTick > currentRawTick)
                            return true;
                    }
                    break;
                case FleckSystemBase<FleckSplash> splashSystem:
                    foreach (var fleck in splashSystem.dataGametime)
                    {
                        if (fleck.setupTick > currentRawTick)
                            return true;
                    }
                    break;
            }

            return false;
        }

    }
}
