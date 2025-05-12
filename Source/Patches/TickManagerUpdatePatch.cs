using HarmonyLib;
using PauseWalker.Utilities;
using RimWorld;
using System.Collections;
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

            if (Current.Game == null || Find.CurrentMap == null || Find.TickManager.CurTimeSpeed != TimeSpeed.Paused)
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
            if (!PauseWalkerUtils.CurrentMapContainsPauseWalker(currentMap))
                return;
            // 参考原本 DoSingleTick 的逻辑，在这里要增加 TicksGameInt，推进游戏时间流逝，不过这里增加的是模拟时间
            SimulatedTickManager.IncreaseSimTick();

            if (AccessTools.Field(Find.TickManager.GetType(), "tickListNormal")?.GetValue(Find.TickManager) is TickList tickListNormal &&
                AccessTools.Field(tickListNormal.GetType(), "tickType")?.GetValue(tickListNormal) is TickerType tickType &&
                AccessTools.Field(tickListNormal.GetType(), "thingsToRegister")?.GetValue(tickListNormal) is List<Thing> thingsToRegister &&
                AccessTools.Field(tickListNormal.GetType(), "thingsToDeregister")?.GetValue(tickListNormal) is List<Thing> thingsToDeregister &&
                AccessTools.Field(tickListNormal.GetType(), "thingLists")?.GetValue(tickListNormal) is List<List<Thing>> thingLists
                )
            {
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
                        if (!itemToTick.Destroyed)
                        {
                            try
                            {
                                switch (itemToTick)
                                {
                                    case Pawn pawn when PauseWalkerUtils.IsPauseWalkerPawn(pawn):
                                        pawn.Tick();
                                        // 更新小人贴图
                                        CellRect viewRect = Find.CameraDriver.CurrentViewRect.ExpandedBy(3);
                                        pawn.ProcessPostTickVisuals(ticksThisFrame, viewRect);
                                        break;
                                    // 处理投射物
                                    case Projectile projectile when IsPauseProjectile(projectile):
                                        projectile.Tick();
                                        break;
                                    case ThingWithComps projectile when IsPauseProjectile(projectile):
                                        projectile.Tick();
                                        break;
                                    case Explosion explosion when IsPauseExplosion(explosion):
                                        explosion.Tick();
                                        break;
                                    case IncineratorSpray incincerSpray when IsPauseIncineratorSpray(incincerSpray):
                                        incincerSpray.Tick();
                                        break;

                                    case Mote mote when IsPauseWalkerMote(mote):
                                        mote.Tick();
                                        break;
                                    case Thing thing when IsThingAfterPause(thing):
                                        thing.Tick();
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
            TickPausedFlecks(currentMap);
            TickPausedEffecterInMap(currentMap);
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
                PauseWalkerUtils.IsPauseWalkerPawn(launcherPawn);
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
                return PauseWalkerUtils.IsPauseWalkerPawn(launcherPawn);

            }
            return false;
        }

        private static bool IsPauseExplosion(Explosion explosion)
        {
            if (explosion != null && explosion.instigator is Pawn launcherPawn)
                return PauseWalkerUtils.IsPauseWalkerPawn(launcherPawn);
            return false;
        }

        private static bool IsPauseIncineratorSpray(IncineratorSpray spray)
        {
            if (spray == null)
                return false;
            return spray.spawnedTick > PauseWalkerUtils.GetRawTicksGameInt();
        }

        private static bool IsPauseWalkerMote(Mote mote)
        {
            if (mote == null) return false;
            return mote.spawnedTick > PauseWalkerUtils.GetRawTicksGameInt();
        }

        private static bool IsThingAfterPause(Thing thing)
        {
            if (thing == null)
                return false;
            if (thing is Fire)
                return false;
            if (thing is Pawn)
                return false;

            return thing.spawnedTick > PauseWalkerUtils.GetRawTicksGameInt();
        }


        // 处理枪口火光
        private static void TickPausedFlecks(Map currentMap)
        {
            if (currentMap == null)
                return;
            if (currentMap.flecks is { } curMapFlecksManager &&
                AccessTools.Field(curMapFlecksManager.GetType(), "systems") is { } fleckSysField &&
                fleckSysField.GetValue(curMapFlecksManager) is Dictionary<Type, FleckSystem> fleckSystemDic
                )
            {
                var currentRawTick = PauseWalkerUtils.GetRawTicksGameInt();
                var targetSys = fleckSystemDic.Values.Where(fleckSystem =>
                {
                    if (AccessTools.Field(fleckSystem.GetType(), "dataGametime") is { } dataGameTimeField &&
                        dataGameTimeField.GetValue(fleckSystem) is IList dataGameTime
                    )
                    {

                        foreach (var item in dataGameTime)
                        {
                            if (AccessTools.Field(item.GetType(), "setupTick") is { } setupTickField &&
                                setupTickField.GetValue(item) is int setupTick
                            )
                            {
                                if (setupTick > currentRawTick)
                                    return true;
                            }
                            if (AccessTools.Field(item.GetType(), "baseData") is { } baseDataField &&
                               baseDataField.GetValue(item) is FleckStatic baseData)
                            {
                                if (baseData.setupTick > currentRawTick)
                                    return true;
                            }
                        }

                    }

                    return false;
                }).ToList();


                foreach (var item in targetSys)
                {
                    if (item != null)
                        item.Tick();
                }
            }
        }


        private static void TickPausedEffecterInMap(Map map)
        {
            if (map == null || map.effecterMaintainer == null)
                return;

            if (AccessTools.Field(map.effecterMaintainer.GetType(), "maintainedEffecters") is { } maintainedEffectersField &&
                maintainedEffectersField.GetValue(map.effecterMaintainer) is List<EffecterMaintainer.MaintainedEffecter> maintainedEffecters
                )
            {
                for (int i = maintainedEffecters.Count - 1; i >= 0; i--)
                {
                    EffecterMaintainer.MaintainedEffecter maintainedEffecter = maintainedEffecters[i];
                    if(maintainedEffecter.Effecter.spawnTick > PauseWalkerUtils.GetRawTicksGameInt())
                    {
                        if (maintainedEffecter.Effecter.ticksLeft > 0)
                        {
                            maintainedEffecter.Effecter.EffectTick(maintainedEffecter.A, maintainedEffecter.B);
                            maintainedEffecter.Effecter.ticksLeft--;
                        }
                        else
                        {
                            maintainedEffecter.Effecter.Cleanup();
                            maintainedEffecters.RemoveAt(i);
                        }
                    }

                }
            }

        }
    }
}
