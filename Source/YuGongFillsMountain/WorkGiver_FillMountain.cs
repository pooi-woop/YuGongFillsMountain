// ==============================================================================
// 愚公填山 - 自定义 WorkGiver: 用"挖掘"(Mining) 工作类型建造标记
// ==============================================================================
//
// RimWorld 的工作 AI 由"WorkGiver"驱动：
//   - 每个 WorkGiver 关联一个 WorkType（如 Mining、Construction、Cooking）
//   - 殖民者根据其工作标签启用情况，依次询问每个 WorkGiver"有没有事可做"
//   - WorkGiver_Scanner 是其中一种扫描型 WorkGiver，会扫描地图寻找可工作的目标
//
// 我们的实现：
//   - 继承自原版 WorkGiver_Scanner（而非具体的 WorkGiver_ConstructFinishFrames）
//     这样我们不依赖原版具体类的私有字段，便于跨版本兼容
//   - 重写 PotentialWorkThingsGlobal: 返回地图上所有"厚岩顶建造标记"的建造框架（Frame）
//   - 重写 HasJobOnThing: 判断殖民者是否能工作于某个 Frame
//   - 重写 JobOnThing: 返回一个"完成建造框架"的 Job
//
// 同时通过 Harmony patch 让原版 WorkGiver_ConstructFinishFrames 跳过我们的建筑，
// 否则 Construction 工作类型的殖民者也会重复尝试建造我们的标记（造成冲突）。
// ==============================================================================

using System.Collections.Generic;
using System.Linq;
using Verse;
using Verse.AI;
using RimWorld;

namespace YGFM
{
    /// <summary>
    /// 自定义 WorkGiver_Scanner。
    /// 在 XML (WorkGiverDefs.xml) 中通过 giverClass="YGFM.WorkGiver_FillMountain" 引用，
    /// 并设置 workType=Mining，让殖民者用"挖掘"工作类型完成此工作。
    /// </summary>
    public class WorkGiver_FillMountain : WorkGiver_Scanner
    {
        /// <summary>
        /// 此 WorkGiver 扫描的目标类型。
        /// - ThingRequestGroup.BuildingArtificial: 建筑/建造框架
        /// 我们要找的目标是"建造框架 (Frame)"，它属于 BuildingArtificial 类。
        /// </summary>
        public override ThingRequest PotentialWorkThingRequest
        {
            get
            {
                // 用 ThingRequest.For(ThingDef) 不行——Frame 是动态的，需要更宽泛的请求
                return ThingRequest.ForGroup(ThingRequestGroup.BuildingArtificial);
            }
        }

        /// <summary>
        /// 返回此 WorkGiver 在整个地图上所有可能的工作目标。
        /// RimWorld 会遍历这些目标，找到最近的可工作目标分配给殖民者。
        /// </summary>
        public override IEnumerable<Thing> PotentialWorkThingsGlobal(Pawn pawn)
        {
            // 遍历地图上所有"建筑类"实体，筛选出我们的建造框架
            // 注意：listerThings.ThingsMatching 返回所有匹配 ThingRequest 的 Thing
            var things = pawn.Map.listerThings.ThingsMatching(ThingRequest.ForGroup(ThingRequestGroup.BuildingArtificial));

            foreach (Thing t in things)
            {
                // 关键判断：如果是 Frame（建造框架），并且其 BuildDef 是我们的标记建筑
                if (t is Frame frame)
                {
                    ThingDef buildDef = frame.def.entityDefToBuild as ThingDef;
                    if (buildDef != null && buildDef.defName == "YGFM_ThickRoofBuilder")
                    {
                        yield return t;
                    }
                }
                // 兼容：如果是已生成的标记建筑本身（理论上正常情况下不会发生）
                else if (t.def.defName == "YGFM_ThickRoofBuilder")
                {
                    yield return t;
                }
            }
        }

        /// <summary>
        /// 判断殖民者是否能对此目标执行工作。
        /// 必须满足：
        ///   - 目标未禁止(forbidden)
        ///   - 殖民者能到达
        ///   - 殖民者能预订(reserve)此目标
        ///   - 还有未完成的建造工作（避免反复触发）
        /// </summary>
        public override bool HasJobOnThing(Pawn pawn, Thing t, bool forced = false)
        {
            // 只对 Frame 类型有效
            if (!(t is Frame frame)) return false;

            // 检查 BuildDef 是否是我们的标记建筑
            ThingDef buildDef = frame.def.entityDefToBuild as ThingDef;
            if (buildDef == null || buildDef.defName != "YGFM_ThickRoofBuilder") return false;

            // 已禁止的目标不能自动工作（除非玩家强制 forced）
            if (frame.IsForbidden(pawn) && !forced) return false;

            // 殖民者无法到达此格子 -> 不能工作
            if (!pawn.CanReach(frame, PathEndMode.Touch, Danger.Deadly)) return false;

            // 检查是否已被他人预订（避免多个殖民者抢同一个工作）
            // 1 个名额，预留 -1 表示不限数量
            if (!pawn.CanReserve(frame, 1, -1, null, forced)) return false;

            return true;
        }

        /// <summary>
        /// 返回一个 Job 对象，让殖民者去执行"完成建造框架"的工作。
        /// 复用原版的 JobDefOf.ConstructFinishFrame（原版"完成建造"作业）。
        /// </summary>
        public override Job JobOnThing(Pawn pawn, Thing t, bool forced = false)
        {
            // 再次检查（防御性编程）
            if (!HasJobOnThing(pawn, t, forced)) return null;

            Frame frame = (Frame)t;

            // 创建一个新 Job，使用原版 FinishFrame JobDef（JobDefOf.FinishFrame）
            // 参数1: JobDef
            // 参数2: 工作目标 (targetA = 建造框架本身)
            Job job = JobMaker.MakeJob(JobDefOf.FinishFrame, t);

            // 允许此工作被玩家强制执行时使用 (allowOpportunistic = 顺手做)
            job.expiryInterval = 2000;       // 2000 tick (≈33秒) 后过期
            job.checkOverrideOnExpire = false;

            return job;
        }

        /// <summary>
        /// 路径到达模式: TOUCH 表示殖民者站在格子边缘即可（不需要进入格子内部）。
        /// </summary>
        public override PathEndMode PathEndMode
        {
            get { return PathEndMode.Touch; }
        }
    }
}
