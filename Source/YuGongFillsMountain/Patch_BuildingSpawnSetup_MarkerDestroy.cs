// ==============================================================================
// 愚公填山 - 在 Building.SpawnSetup 完成后销毁标记（修复 NRE 红字）
// ==============================================================================
//
// 背景与根因：与精卫填海完全一致，不再重复，参见同目录注释。
//
// 原版 Building.SpawnSetup 在跑完 ThingWithComps.SpawnSetup（内部执行所有 comp 的
// PostSpawnSetup）后，紧接着执行 base.Map.listerBuildings.Add(this)。
// 旧代码在 Comp 的 PostSpawnSetup 里同步 parent.Destroy()，把 this.Map 置 null，
// 导致原版那一行解引用 null 抛 NullReferenceException——每放置/建造一个标记就
// 爆一次 "Root level exception in OnGUI()"。
//
// 修复：改为在 Building.SpawnSetup 的 Harmony Postfix 中，等 spawn 完整结束后再销毁。
// ==============================================================================

using HarmonyLib;
using RimWorld;
using Verse;

namespace YGFM
{
    /// <summary>
    /// 在厚岩顶建造标记完成后销毁它（保留厚岩顶）。
    /// </summary>
    [HarmonyPatch(typeof(Verse.Building), nameof(Verse.Building.SpawnSetup))]
    public static class Patch_BuildingSpawnSetup_MarkerDestroy
    {
        public static void Postfix(Building __instance, bool respawningAfterLoad)
        {
            // 读档恢复生成时不销毁（厚岩顶只在新建阶段放置一次）
            if (respawningAfterLoad) return;

            // 防御：若在 spawn 过程中已被销毁则跳过
            if (__instance.Destroyed) return;

            // 只处理本 mod 的标记建筑
            if (__instance.def.defName != "YGFM_ThickRoofBuilder") return;

            CompPlaceThickRoof comp = __instance.TryGetComp<CompPlaceThickRoof>();
            if (comp != null && comp.Props.destroySelfOnComplete)
            {
                // DestroyMode.Vanish 表示"消失"，不留建筑残骸
                __instance.Destroy(DestroyMode.Vanish);
            }
        }
    }
}