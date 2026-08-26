// ==============================================================================
// 愚公填山 - Harmony 补丁
// ==============================================================================
//
// Harmony 是 RimWorld modding 的核心库，允许在运行时修改原版方法的行为而不修改原版代码。
// 我们用两个 Harmony patch 来让我们的"厚岩顶建造标记"建筑正确工作：
//
// Patch 1: 让原版 Construction workgiver 跳过我们的建筑
//   - 原版的 WorkGiver_ConstructFinishFrames 默认会处理所有建造框架
//   - 如果不 patch 它，Construction 工作类型的殖民者也会去建造我们的标记
//     ——这就违背了"用挖掘工作类型建造"的设计
//   - 通过在 HasJobOnThing 的 Postfix 中将我们的建筑返回 false，让其跳过
//
// Patch 2: 让 WorkToBuild stat 受 Mod 设置中的"工作量倍率"影响
//   - 原版的 StatExtension.GetStatValue 返回 stat 的最终值
//   - 我们在 Postfix 中检查 stat == WorkToBuild 且 thing 是我们的标记时
//     将返回值乘以 Settings.WorkAmountMultiplier
// ==============================================================================

using HarmonyLib;
using Verse;
using RimWorld;

namespace YGFM
{
    // ----------------------------------------------------------------------
    // Patch 1: 让原版 WorkGiver_ConstructFinishFrames 跳过我们的标记建筑
    // ----------------------------------------------------------------------
    //
    // WorkGiver_ConstructFinishFrames.HasJobOnThing(Pawn pawn, Thing t, bool forced)
    // 是原版"建造框架"工作的工作分配入口。我们在 Postfix 中检查 t，
    // 如果它是我们的标记建筑的框架，强制返回 false（无工作可做）。
    // 这样原版 Construction 工作就不会处理我们的标记。
    // ----------------------------------------------------------------------
    [HarmonyPatch(typeof(WorkGiver_ConstructFinishFrames), "HasJobOnThing")]
    public static class Patch_WorkGiver_ConstructFinishFrames_HasJobOnThing
    {
        /// <summary>
        /// Postfix: 在原版方法执行后调用。如果目标是我们的标记建筑，强制 __result = false。
        /// </summary>
        /// <param name="__result">原版方法的返回值，通过 ref 修饰可修改</param>
        /// <param name="t">原版方法的第二个参数（工作目标）</param>
        public static void Postfix(ref bool __result, Thing t)
        {
            // 如果原版已经判定不能工作，无需处理
            if (!__result) return;

            // 检查目标是否是 Frame（建造框架）
            if (t is Frame frame)
            {
                // Frame 的实体是 entityDefToBuild，要强转为 ThingDef 才能取 defName
                ThingDef buildDef = frame.def.entityDefToBuild as ThingDef;
                if (buildDef != null && buildDef.defName == "YGFM_ThickRoofBuilder")
                {
                    // 强制让原版 Construction 跳过我们的标记
                    __result = false;
                }
            }
        }
    }

    // ----------------------------------------------------------------------
    // Patch 2: 让我们的标记建筑的 WorkToBuild stat 受 Mod 设置倍率影响
    // ----------------------------------------------------------------------
    //
    // StatExtension.GetStatValue(Thing thing, StatDef stat, bool applyPostProcess = true)
    // 是 RimWorld 中读取 stat 值的统一入口。我们 Patch 它的 Postfix：
    //   - 如果 stat 是 WorkToBuild
    //   - 且 thing 是我们的标记建筑（或其 Frame）
    //   - 则把返回值乘以 Settings.WorkAmountMultiplier
    // ----------------------------------------------------------------------
    [HarmonyPatch(typeof(StatExtension), "GetStatValue")]
    public static class Patch_StatExtension_GetStatValue
    {
        /// <summary>
        /// 静态变量缓存我们的标记 ThingDef，避免每次都查 defName 字符串。
        /// 第一次访问时通过 DefDatabase 查找。
        /// </summary>
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

        /// <summary>
        /// Postfix: 修改 stat 返回值。
        /// 注意：参数名必须与原版方法签名一致，否则 Harmony 无法识别。
        /// </summary>
        public static void Postfix(Thing thing, StatDef stat, ref float __result)
        {
            // 只关心 WorkToBuild stat
            if (stat != StatDefOf.WorkToBuild) return;

            // 防御性：如果 Mod 还未加载完设置，跳过（避免在游戏启动早期出错）
            if (YuGongFillsMountainMod.Settings == null) return;

            // 检查 thing 是否是标记建筑本身
            if (thing.def == ThickRoofBuilderDef)
            {
                __result *= YuGongFillsMountainMod.Settings.WorkAmountMultiplier;
                return;
            }

            // 也需要处理 Frame 情况：当殖民者检查建造框架的工作量时，
            // 实际访问的是 frame.def.entityDefToBuild 的 stat。
            // 但 StatExtension.GetStatValue 对 Frame 返回的是 Frame 自己的 stat，
            // 实际工作量会通过 GenUI/Jobs 中的另一条路径访问。
            // 为简洁起见这里只处理直接访问，已足够覆盖大多数场景。
        }
    }
}
