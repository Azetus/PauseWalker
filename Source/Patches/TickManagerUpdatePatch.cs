using HarmonyLib;
using Mono.Unix.Native;
using PauseWalker.Utilities;
using PauseWalker.Defs;
using System.Collections;
using System.Diagnostics;
using UnityEngine;
using Verse;
using RimWorld;
using static RimWorld.EffecterMaintainer;
using UnityEngine.UIElements;

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
            if(projectile.Launcher is Pawn launcherPawn)
            {
                PauseWalkerUtils.IsPauseWalkerPawn(launcherPawn);
            }
            return false;
        }
        private static bool IsPauseProjectile(ThingWithComps thing)
        {
            if (thing == null)
                return false;
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
            {
                return PauseWalkerUtils.IsPauseWalkerPawn(launcherPawn);

            }
            return false;
        }

        private static bool IsPauseIncineratorSpray(IncineratorSpray spray)
        {
            if (spray == null)
                return false;
            if (Find.CurrentMap != null &&
                Find.CurrentMap.mapPawns != null &&
                Find.CurrentMap.mapPawns.AllPawns != null)
            {
                if (AccessTools.Field(spray.GetType(), "positionInt") is { } positionIntField &&
                    positionIntField.GetValue(spray) is IntVec3 sprayPos)
                {
                    var pauseWalkers = Find.CurrentMap.mapPawns.AllPawns.Where(pawn =>
                    {
                        return PauseWalkerUtils.IsPauseWalkerPawn(pawn);
                    });

                    return pauseWalkers.Any(pawn =>
                    {

                        // 忽略掉y坐标
                        Vector3 a = pawn.Position.ToVector3();
                        Vector3 b = sprayPos.ToVector3();
                        if (Vector2.Distance(new Vector2(a.x, a.z), new Vector2(b.x, b.z)) <= 1f)
                            return true;
                        else
                            return false;
                    });
                }

            }
            return false;
        }

        private static void TickFlecks()
        {
            //currentMap.flecks.FleckManagerTick();
            var currentMap = Find.CurrentMap;
            if (currentMap == null)
                return;
            if (currentMap.mapPawns == null)
                return;
            List<Pawn> targetPawn = currentMap.mapPawns.AllPawnsSpawned
                .Where(pawn =>
                {
                    return PauseWalkerUtils.IsPauseWalkerPawn(pawn);
                }).ToList();

            foreach (var pawn in targetPawn)
            {
                TickCurrentMapFlecksAroundPawn(pawn, currentMap);
            }
        }

        public static bool IsPauseWalkerMote(Mote mote)
        {
            if (mote == null) return false;

            if (mote is MoteDualAttached moteDual)
            {
                Pawn? pawnB1 = null;
                Pawn? pawnB2 = null;
                if (moteDual.link1.Target.Thing is Pawn p1){
                    //Log.Message(mote.ToString() + " moteDual link1" + p1.ToString() + "is pausewalker " + PauseWalkerUtils.IsPauseWalkerPawn(p1));
                    pawnB1 = p1;
                }
                if(AccessTools.Field(moteDual.GetType(), "link2") is { } link2Field &&
                    link2Field.GetValue(moteDual) is MoteAttachLink link2 &&
                    link2.Target.Thing is Pawn p2)
                {
                    //Log.Message(mote.ToString() + " moteDual link2" + p2.ToString() + "is pausewalker " + PauseWalkerUtils.IsPauseWalkerPawn(p2));
                    pawnB2 = p2;
                }

                return PauseWalkerUtils.IsPauseWalkerPawn(pawnB1) || PauseWalkerUtils.IsPauseWalkerPawn(pawnB2);
            }
            else if (mote.link1.Target.Thing is Pawn pawnA)
            {
                //Log.Message(mote.ToString() + " mote link1" + pawnA.ToString() + "is pausewalker " + PauseWalkerUtils.IsPauseWalkerPawn(pawnA));
                return PauseWalkerUtils.IsPauseWalkerPawn(pawnA);
            }

            return false;
        }

        // 绕开游戏的 TickManagerUpdate，单独执行一些 DoSingleTick() 内的内容
        private static void DoPausedTick()
        {
            var currentMap = Find.CurrentMap;
            if (currentMap == null)
                return;
            if (!PauseWalkerUtils.CurrentMapContainsPauseWalker(currentMap))
                return;
            // 推进模拟时间
            SimulatedTickManager.IncreaseSimTick();

            TickList tickListNormal = (TickList)AccessTools.Field(Find.TickManager.GetType(), "tickListNormal").GetValue(Find.TickManager);
            TickerType tickType = (TickerType)AccessTools.Field(tickListNormal.GetType(), "tickType").GetValue(tickListNormal);

            List<Thing> thingsToRegister = (List<Thing>)AccessTools.Field(tickListNormal.GetType(), "thingsToRegister").GetValue(tickListNormal);
            List<Thing> thingsToDeregister = (List<Thing>)AccessTools.Field(tickListNormal.GetType(), "thingsToDeregister").GetValue(tickListNormal);

            List<List<Thing>> thingLists = (List<List<Thing>>)AccessTools.Field(tickListNormal.GetType(), "thingLists").GetValue(tickListNormal);
            List<Thing> list2 = thingLists[Find.TickManager.TicksGame % GetTickInterval(tickType)];

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
                                CellRect viewRect = Find.CameraDriver.CurrentViewRect.ExpandedBy(3);
                                pawn.ProcessPostTickVisuals(ticksThisFrame, viewRect);
                                break;
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
                            //case DelayedEffecterSpawner delayedEffecterSpawner:
                            //    delayedEffecterSpawner.Tick();
                            //    break;

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

            //currentMap.temporaryThingDrawer.Tick();//不需要调用
            //currentMap.effecterMaintainer.EffecterMaintainerTick(); // 帝王业火炮爆炸特效
            TickFlecks();
            

            

        }





        // 绕开游戏的 TickManagerUpdate，单独执行一些 DoSingleTick() 内的内容
        private static void DoPausedTick_old()
        {
            var currentMap = Find.CurrentMap;
            if (currentMap == null)
                return;
            if (!PauseWalkerUtils.CurrentMapContainsPauseWalker(currentMap))
                return;


            TickAllPawnsInMap(currentMap);
            TickProjectilesInMap(currentMap);
            TickExplosionInMap(currentMap);
            //TickEffecterInMap(currentMap);

            //currentMap.effecterMaintainer.EffecterMaintainerTick();
            //foreach (var item in RealTime.moteList.allMotes.ToList())
            //{
            //    item.animationPaused = false;
            //    item.paused = false;
            //    item.Tick();
            //}



            // 参考原本 DoSingleTick 的逻辑，在这里要增加 TicksGameInt，推进游戏时间流逝，不过这里增加的是模拟时间
            SimulatedTickManager.IncreaseSimTick();
        }

        // 筛选当前地图中符合条件的小人，让他们绕开游戏暂停做一些事
        private static void TickAllPawnsInMap(Map currentMap)
        {
            if (currentMap.mapPawns == null)
                return;
            List<Pawn> targetPawn = currentMap.mapPawns.AllPawnsSpawned
                .Where(pawn =>
                {
                    return PauseWalkerUtils.IsPauseWalkerPawn(pawn);
                }).ToList();

            foreach (var pawn in targetPawn)
            {
                TickPawnWhilePaused(pawn, currentMap);
            }
        }


        // 执行小人本该做的一些事，需要传入一个 Pawn
        private static void TickPawnWhilePaused(Pawn pawn, Map currentMap)
        {
            if (pawn == null)
                return;

            try
            {
                // 执行 pawn 的所有 tick
                pawn.Tick();
                pawn.TickRare();
                pawn.TickLong();
                // 更新小人贴图
                CellRect viewRect = Find.CameraDriver.CurrentViewRect.ExpandedBy(3);
                pawn.ProcessPostTickVisuals(ticksThisFrame, viewRect);
                //pawn.Drawer?.tweener.ResetTweenedPosToRoot();

                //TickPawnCompEquipment(pawn);
                TickCurrentMapFlecksAroundPawn(pawn, currentMap);

            }
            catch (Exception e)
            {
                Log.Warning($"[PauseWalker] Failed ticking pawn {pawn}: {e}");
            }
        }

        // Pawn 装备相关的一些Tick需要单独调用，比如武器开火
        //private static void TickPawnCompEquipment(Pawn pawn)
        //{
        //    if (pawn != null && pawn.equipment != null)
        //    {
        //        List<ThingWithComps> equimentList = pawn.equipment.AllEquipmentListForReading;
        //        foreach (ThingWithComps equipment in equimentList.ToList())
        //        {
        //            if (equipment != null && equipment.AllComps != null)
        //            {
        //                foreach (ThingComp comp in equipment.AllComps.ToList())
        //                {
        //                    comp.CompTick();
        //                }
        //            }
        //        }
        //    }
        //}

        // 处理枪口火光
        private static void TickCurrentMapFlecksAroundPawn(Pawn pawn, Map currentMap)
        {
            if (pawn == null || currentMap == null)
                return;

            if (currentMap.flecks is { } curMapFlecksManager &&
                AccessTools.Field(curMapFlecksManager.GetType(), "systems") is { } fleckSysField &&
                fleckSysField.GetValue(curMapFlecksManager) is Dictionary<Type, FleckSystem> fleckSystemDic
                )
            {
                var targetSys = fleckSystemDic.Values.Where(fleckSystem =>
                {
                    if (AccessTools.Field(fleckSystem.GetType(), "dataGametime") is { } dataGameTimeField &&
                        dataGameTimeField.GetValue(fleckSystem) is IList dataGameTime
                    )
                    {

                        foreach (var item in dataGameTime)
                        {
                            if (AccessTools.Field(item.GetType(), "spawnPosition") is { } spawnPostionField &&
                                spawnPostionField.GetValue(item) is Vector3 position
                            )
                            {
                                // 忽略掉y坐标
                                Vector3 a = pawn.Position.ToVector3();
                                Vector3 b = position;
                                if (Vector2.Distance(new Vector2(a.x, a.z), new Vector2(b.x, b.z)) <= 1f)
                                {
                                    return true;
                                }
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



        // 处理当前地图里的投射物，指定小人发射的投射物不受游戏暂停的影响
        private static void TickProjectilesInMap(Map map)
        {
            try
            {
                if (map == null) return;


                // 判断一下投射物的发射者，如果不是特定 Pawn 的投射物就不管了
                List<Thing> pawnProjectiles = map.listerThings.ThingsInGroup(ThingRequestGroup.Projectile)
                    .Where(thing =>
                    {
                        // 因为 Combat Extended 模组用 class ProjectileCE 代替了原本的投射物 class Projectile，这里就用反射获取 launcher
                        if (AccessTools.Field(thing.GetType(), "launcher") is { } launcherField &&
                        launcherField.GetValue(thing) is Pawn launcherPawn)
                        {
                            return PauseWalkerUtils.IsPauseWalkerPawn(launcherPawn);

                        }

                        return false;
                    })
                    .ToList();

                // 对投射物发射者是特定 Pawn 的投射物逐个调用 Tick() 方法
                foreach (Thing pawnProjectileItem in pawnProjectiles)
                {
                    var tickMethod = pawnProjectileItem.GetType().GetMethod("Tick");
                    if (tickMethod != null)
                    {
                        // 调用投射物的 Tick() 方法
                        pawnProjectileItem.Tick();
                    }
                }


            }
            catch (Exception e)
            {
                Log.Warning($"[PauseWalker] Failed ticking projectiles: {e}");
            }

        }

        // 处理特定Pawn所引发的explosions
        private static void TickExplosionInMap(Map map)
        {
            if (map == null || map.listerThings == null) return;

            List<Explosion> explosions = new List<Explosion>();
            map.listerThings.GetThingsOfType<Explosion>(explosions);

            var explosionsToBeTick = explosions.Where(item =>
            {

                if (item != null && item.instigator is Pawn launcherPawn)
                {
                    return PauseWalkerUtils.IsPauseWalkerPawn(launcherPawn);

                }
                return false;
            }).ToList();

            foreach (var item in explosionsToBeTick)
            {
                item.Tick();
            }

        }


        private static void TickEffecterInMap(Map map)
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
                //var targetEffecters = maintainedEffecters.Where(eff =>
                //{
                //    if (eff.A.Thing is Pawn pawnA && PauseWalkerUtils.IsPauseWalkerPawn(pawnA))
                //        return true;
                //    if (eff.B.Thing is Pawn pawnB && PauseWalkerUtils.IsPauseWalkerPawn(pawnB))
                //        return true;
                //    return false;
                //});
                //foreach (var item in targetEffecters.ToList())
                //{
                //    if (item.Effecter.ticksLeft > 0)
                //    {
                //        item.Effecter.EffectTick(item.A, item.B);
                //        item.Effecter.ticksLeft--;
                //    }
                //    else
                //    {
                //        item.Effecter.Cleanup();
                //        this.maintainedEffecters.RemoveAt(i);
                //    }
                //}
            }

        }
    }
}
