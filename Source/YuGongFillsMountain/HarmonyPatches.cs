// ==============================================================================
// 愚公填山 - Harmony 补丁
// ==============================================================================
//
// Patch 1: 让原版 Construction workgiver 跳过我们的标记建筑
// Patch 2: 让标记建筑的 WorkToBuild stat 受 Mod 设置倍率影响
//   - 注意：Frame.WorkToBuild 走的是 BuildableDef.GetStatValueAbstract
//     （而非 StatExtension.GetStatValue），所以倍率补丁必须打在
//     GetStatValueAbstract 上才能同时影响显示与实际建造工作量。
// Patch 3: 为标记的建造框架（Frame）绘制进度条
// ==============================================================================

using System.Collections.Generic;
using HarmonyLib;
using Verse;
using Verse.AI;
using RimWorld;
using UnityEngine;

namespace YGFM
{
    /// <summary>
    /// 作业节流器：限制单个殖民者在同一 tick 内从本 mod 领取的作业数量。
    /// 用于抑制原版"X started 10 jobs in one tick"保护警告——当一次性框选大量
    /// 标记、深水/注水改造导致地形变化时，小人可能在单 tick 内被反复派发
    /// 立刻失败的作业，触发该警告并造成卡顿。这里把单个 tick 内本 mod 派给
    /// 同一小人的作业数封顶在 MaxPerTick，避免任务风暴。
    /// </summary>
    internal static class JobThrottle
    {
        private const int MaxPerTick = 2;

        private static readonly Dictionary<Pawn, int> lastTick = new Dictionary<Pawn, int>();
        private static readonly Dictionary<Pawn, int> count = new Dictionary<Pawn, int>();

        /// <summary>
        /// 询问本 tick 是否还能给该小人派发事件作业。
        /// </summary>
        internal static bool TryAllow(Pawn pawn)
        {
            if (pawn == null) return true;

            int now = GenTicks.TicksGame;
            if (!lastTick.TryGetValue(pawn, out int lt) || lt != now)
            {
                // 新的一 tick：重置计数并放行
                lastTick[pawn] = now;
                count[pawn] = 1;
                return true;
            }

            int c = count[pawn];
            if (c >= MaxPerTick) return false;
            count[pawn] = c + 1;
            return true;
        }
    }

    // Patch 1: 让原版 WorkGiver_ConstructFinishFrames 跳过我们的标记建筑
    // 注意：在 RimWorld 1.6 中 WorkGiver_ConstructFinishFrames 并未重写 HasJobOnThing，
    // 该方法由基类 WorkGiver_Scanner 提供。按方法名补丁基类虚方法在 Harmony 2.x 下
    // 会解析失败（Undefined target method）。因此这里改在 1.6 由该类直接声明的
    // JobOnThing 上打补丁：若目标是我们的标记建筑建造框架，返回 null（无作业），
    // 从而让原版 Construction 工作跳过它。
    [HarmonyPatch(typeof(WorkGiver_ConstructFinishFrames), "JobOnThing")]
    public static class Patch_WorkGiver_ConstructFinishFrames_JobOnThing
    {
        public static Job Postfix(Job __result, Thing t)
        {
            if (t is Frame frame)
            {
                ThingDef buildDef = frame.def.entityDefToBuild as ThingDef;
                if (buildDef != null && buildDef.defName == "YGFM_ThickRoofBuilder")
                {
                    return null;
                }
            }
            return __result;
        }
    }

    // Patch 2: 让标记建筑的 WorkToBuild stat 受 Mod 设置倍率影响
    // 注意：RimWorld 1.6 中 BuildableDef 上已不存在实例方法 GetStatValueAbstract，
    // 该方法被移到了静态方法 RimWorld.StatExtension.GetStatValueAbstract(BuildableDef, StatDef, ThingDef)。
    // Frame.WorkToBuild / 信息面板显示的工作量都走这条路径，因此必须补丁它。
    [HarmonyPatch(typeof(StatExtension), "GetStatValueAbstract", new[]
    {
        typeof(BuildableDef),
        typeof(StatDef),
        typeof(ThingDef)
    })]
    public static class Patch_StatExtension_GetStatValueAbstract
    {
        private static ThingDef _thickRoofBuilderDef;
        private static ThingDef ThickRoofBuilderDef
        {
            get
            {
                if (_thickRoofBuilderDef == null)
                {
                    _thickRoofBuilderDef = DefDatabase<ThingDef>.GetNamed("YGFM_ThickRoofBuilder");
                }
                return _thickRoofBuilderDef;
            }
        }

        public static void Postfix(BuildableDef def, StatDef stat, ref float __result)
        {
            if (stat != StatDefOf.WorkToBuild) return;

            if (YuGongFillsMountainMod.Settings == null) return;

            if (def == ThickRoofBuilderDef)
            {
                __result *= YuGongFillsMountainMod.Settings.WorkAmountMultiplier;
            }
        }
    }

    // Patch 3: 为标记的建造框架绘制进度条
    // 材质必须在主线程创建，否则 RimWorld 会告警，故用 [StaticConstructorOnStartup]
    // 在启动时（主线程）于静态构造函数中初始化。
    [HarmonyPatch(typeof(Frame), "DrawAt")]
    [StaticConstructorOnStartup]
    public static class Patch_Frame_DrawAt_ProgressBar
    {
        private static readonly Material BarFilledMat;
        private static readonly Material BarUnfilledMat;

        static Patch_Frame_DrawAt_ProgressBar()
        {
            BarFilledMat = SolidColorMaterials.SimpleSolidColorMaterial(new Color(0.5f, 0.9f, 0.5f));
            BarUnfilledMat = SolidColorMaterials.SimpleSolidColorMaterial(new Color(0.2f, 0.2f, 0.2f));
        }

        public static void Postfix(Frame __instance)
        {
            if (!(__instance.def.entityDefToBuild is ThingDef buildDef)) return;
            if (buildDef.defName != "YGFM_ThickRoofBuilder") return;
            if (__instance.WorkToBuild <= 0f) return;

            GenDraw.DrawFillableBar(new GenDraw.FillableBarRequest
            {
                center = __instance.DrawPos + Vector3.up * 0.3f,
                size = new Vector2(0.9f, 0.12f),
                fillPercent = Mathf.Clamp01(__instance.PercentComplete),
                filledMat = BarFilledMat,
                unfilledMat = BarUnfilledMat,
                margin = 0.05f,
                rotation = Rot4.North
            });
        }
    }
}
