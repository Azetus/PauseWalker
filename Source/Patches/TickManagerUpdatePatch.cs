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

                TickPawnCompEquipment(pawn);
                TickCurrentMapFlecksAroundPawn(pawn, currentMap);

            }
            catch (Exception e)
            {
                Log.Warning($"[PauseWalker] Failed ticking pawn {pawn}: {e}");
            }
        }

        // Pawn 装备相关的一些Tick需要单独调用，比如武器开火
        private static void TickPawnCompEquipment(Pawn pawn)
        {
            if (pawn != null && pawn.equipment != null)
            {
                List<ThingWithComps> equimentList = pawn.equipment.AllEquipmentListForReading;
                foreach (ThingWithComps equipment in equimentList.ToList())
                {
                    if (equipment != null && equipment.AllComps != null)
                    {
                        foreach (ThingComp comp in equipment.AllComps.ToList())
                        {
                            comp.CompTick();
                        }
                    }
                }
            }
        }

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
            
            if(AccessTools.Field(map.effecterMaintainer.GetType(), "maintainedEffecters") is { } maintainedEffectersField &&
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
