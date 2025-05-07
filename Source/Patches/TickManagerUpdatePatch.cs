using HarmonyLib;
using Mono.Unix.Native;
using PauseWalker.Utilities;
using PauseWalker.Defs;
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
            TickAllPawnsInMap();
            TickProjectilesInMap(Find.CurrentMap);

            // 参考原本 DoSingleTick 的逻辑，在这里要增加 TicksGameInt，推进游戏时间流逝，不过这里增加的是模拟时间
            SimulatedTickManager.IncreaseSimTick();
        }

        // 筛选当前地图中符合条件的小人，让他们绕开游戏暂停做一些事
        private static void TickAllPawnsInMap()
        {
            List<Pawn> targetPawn = Find.CurrentMap.mapPawns.AllPawnsSpawned
                .Where(pawn =>
                {
                    return PauseWalkerUtils.IsPauseWalkerPawn(pawn);
                }).ToList();

            foreach (var pawn in targetPawn)
            {
                TickPawnWhilePaused(pawn);
            }
        }


        // 执行小人本该做的一些事，需要传入一个 Pawn
        private static void TickPawnWhilePaused(Pawn pawn)
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

                // Pawn 装备相关的一些Tick需要单独调用，比如武器开火
                if (pawn.equipment != null)
                {
                    List<ThingWithComps> equimentList = pawn.equipment.AllEquipmentListForReading;
                    foreach (ThingWithComps equipment in equimentList)
                    {
                        if (equipment != null)
                        {
                            foreach (ThingComp comp in equipment.AllComps)
                            {
                                comp.CompTick();
                            }
                        }
                    }
                }


                // 处理枪口火光
                if (Current.Game?.CurrentMap?.flecks is { } curMapFlecksManager &&
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


                //if (Current.Game?.CurrentMap?.flecks != null) {
                //    var curMapFlecksManager = Current.Game.CurrentMap.flecks;
                //    Dictionary<Type, FleckSystem> fleckSys = (Dictionary<Type, FleckSystem>)AccessTools
                //        .Field(curMapFlecksManager.GetType(), "systems")
                //        .GetValue(curMapFlecksManager);

                //    var targetSys = fleckSys.Values.Where(fleckSystems =>
                //    {
                //        var dataGameTimeField = AccessTools.Field(fleckSystems.GetType(), "dataGametime");
                //        if (dataGameTimeField != null)
                //        {
                //            var dataGameTime = dataGameTimeField.GetValue(fleckSystems) as IList;

                //            if (dataGameTime != null)
                //            {
                //                foreach (var item in dataGameTime)
                //                {
                //                    var spawnPosition = AccessTools.Field(item.GetType(), "spawnPosition").GetValue(item);
                //                    if (spawnPosition is Vector3 position)
                //                    {
                //                        var distance = Vector3.Distance(pawn.Position.ToVector3(), position);
                //                        if (Vector3.Distance(pawn.Position.ToVector3(), position) <= 20f)
                //                        {
                //                            return true;
                //                        }
                //                    }
                //                }
                //            }

                //        }
                //        return false;
                //    }).ToList();


                //    foreach (var item in targetSys)
                //    {
                //        if (item != null)
                //            item.Tick();
                //    }
                //}


                //foreach (var fleckSystems in fleckSys.Values)
                //{
                //    var dataGameTimeField = AccessTools.Field(fleckSystems.GetType(), "dataGametime");
                //    if (dataGameTimeField != null)
                //    {
                //        var dataGameTime = dataGameTimeField.GetValue(fleckSystems) as IList<IFleck>;
                //        if (dataGameTime != null)
                //        {
                //            foreach (var item in dataGameTime)
                //            {
                //                var spawnPosition = AccessTools.Field(item.GetType(), "spawnPosition").GetValue(item);
                //                if (spawnPosition is Vector3 position)
                //                {
                //                    Log.Message("Fleck pos: " + spawnPosition.ToString());
                //                    if (Vector3.Distance(pawn.Position.ToVector3(), position) <= 20f)
                //                    {
                //                        fleckSystems.Tick();
                //                    }
                //                }
                //            }
                //        }


                //    }
                //}
            }
            catch (Exception e)
            {
                Log.Warning($"[PauseWalker] Failed ticking pawn {pawn}: {e}");
            }
        }

        // 处理当前地图里的投射物，指定小人发射的投射物不受游戏暂停的影响
        private static void TickProjectilesInMap(Map map)
        {
            try
            {
                if (map == null) return;



                //来自MapPostTick 更新光照动画特效
                //map.MapPostTick();
                //try
                //{
                //    map.flecks.FleckManagerTick();
                //}
                //catch (Exception ex17)
                //{
                //    Log.Error(ex17.ToString());
                //}
                //try
                //{
                //    map.effecterMaintainer.EffecterMaintainerTick();
                //}
                //catch (Exception ex18)
                //{
                //    Log.Error(ex18.ToString());
                //}


                // 判断一下投射物的发射者，如果不是特定 Pawn 的投射物就不管了
                List<Thing> pawnProjectiles = map.listerThings.ThingsInGroup(ThingRequestGroup.Projectile)
                    .Where(thing =>
                    {
                        // 因为 Combat Extended 模组用 class ProjectileCE 代替了原本的投射物 class Projectile，这里就用反射获取 launcher
                        object launcher = AccessTools.Field(thing.GetType(), "launcher").GetValue(thing);
                        if (launcher is Pawn launcherPawn)
                        {
                            return launcherPawn.story?.traits?.HasTrait(PauseWalkerTraitDefOf.PauseWalker) == true;

                        }
                        //FieldInfo launcherField = thing.GetType().GetField("launcher", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                        //if (launcherField != null)
                        //{
                        //    var launcher = launcherField.GetValue(thing);
                        //    if (launcher is Pawn launcherPawn)
                        //    {
                        //        return launcherPawn.story?.traits?.HasTrait(PauseWalkerTraitDefOf.PauseWalker) == true;

                        //    }

                        //}

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
    }
}
