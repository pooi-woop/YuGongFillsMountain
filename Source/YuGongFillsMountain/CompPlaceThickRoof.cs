// ==============================================================================
// 愚公填山 - ThingComp: 完成建造时放置厚岩顶
// ==============================================================================
//
// 作用：当殖民者完成"厚岩顶建造标记"建筑的建造后，将这一格的屋顶设为"厚岩顶"，
//      然后销毁标记本身（保持地图整洁，仅留下厚岩顶）。
//
// 实现要点：
//   - CompProperties_PlaceThickRoof 是 XML 中可配置的参数类
//   - CompPlaceThickRoof 是运行时执行的 ThingComp
//   - 重写 PostSpawnSetup：在建筑生成（即建造完成）后触发
//   - 调用 map.roofGrid.SetRoof 将格子设为 RoofDefOf.RoofRockThick
// ==============================================================================

using Verse;
using RimWorld;

namespace YGFM
{
    /// <summary>
    /// XML 中可配置的参数类。
    /// 在 ThingDef 的 <comps> 中通过 Class="YGFM.CompProperties_PlaceThickRoof" 引用。
    /// </summary>
    public class CompProperties_PlaceThickRoof : CompProperties
    {
        /// <summary>
        /// 完成后是否销毁标记本身。设为 true 表示只留下厚岩顶，标记消失。
        /// </summary>
        public bool destroySelfOnComplete = true;

        /// <summary>
        /// 构造函数必须将 compClass 设为对应的 ThingComp 类型。
        /// </summary>
        public CompProperties_PlaceThickRoof()
        {
            compClass = typeof(CompPlaceThickRoof);
        }
    }

    /// <summary>
    /// ThingComp 实现类。负责在建筑生成（建造完成）后修改屋顶。
    /// </summary>
    public class CompPlaceThickRoof : ThingComp
    {
        /// <summary>
        /// 便捷访问器：把父类的 props 强转为我们的 CompProperties 子类。
        /// 这样可以方便地访问 XML 中配置的字段（如 destroySelfOnComplete）。
        /// </summary>
        public CompProperties_PlaceThickRoof Props => (CompProperties_PlaceThickRoof)props;

        /// <summary>
        /// 在 Thing 生成到地图上之后调用。
        /// 对于建筑来说，这就是"建造完成"的时刻。
        /// </summary>
        /// <param name="respawningAfterLoad">
        /// true 表示这是读档时的"恢复生成"，不是新建。
        /// 我们在读档时不应该重复执行屋顶生成（否则读档会让标记所在格的屋顶被重置）。
        /// </param>
        public override void PostSpawnSetup(bool respawningAfterLoad)
        {
            base.PostSpawnSetup(respawningAfterLoad);

            // 仅在新建（即建造完成）时执行；读档时跳过
            if (respawningAfterLoad) return;

            // 获取当前建筑所在格子
            IntVec3 cell = parent.Position;
            // 获取所在地图
            Map map = parent.Map;

            if (map == null)
            {
                Log.Warning("[愚公填山] CompPlaceThickRoof: map 为 null，无法设置屋顶。");
                return;
            }

            // 把这一格的屋顶设为"厚岩顶" (RoofDefOf.RoofRockThick = 原版的 overhead mountain)
            // 注意：SetRoof 会自动处理屋顶网格的更新、相关可视化刷新等
            map.roofGrid.SetRoof(cell, RoofDefOf.RoofRockThick);

            // 生成一些视觉效果（土石崩落效果）让玩家直观感受到
            // 这里使用原版的 Filth_RubbleRock 作为视觉提示（散落石屑）
            if (ThingDefOf.Filth_RubbleRock != null)
            {
                FilthMaker.TryMakeFilth(cell, map, ThingDefOf.Filth_RubbleRock);
            }

            // 可选：在调试日志中确认
            Log.Message($"[愚公填山] 已在 {cell} 设置厚岩顶。");

            // 注意：这里【不能】同步 destroy 自身！
            // 原版 Building.SpawnSetup 在跑完 ThingWithComps.SpawnSetup（内部会调用本 comp 的
            // PostSpawnSetup）后，紧接着执行 base.Map.listerBuildings.Add(this)。
            // 若在此销毁，thing.Map 会变成 null，原版那一行 ldfld 就会抛 NullReferenceException
            // （表现为每放置/建造一个标记就爆一次 "Root level exception in OnGUI()"）。
            // 销毁已交由 Patch_BuildingSpawnSetup_MarkerDestroy（Building.SpawnSetup 的
            // Harmony Postfix）在 spawn 流程完整结束后再执行。
        }
    }
}
