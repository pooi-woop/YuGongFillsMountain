// ==============================================================================
// 愚公填山 - Mod 设置
// ==============================================================================
//
// ModSettings 是 RimWorld 提供的可序列化设置类，让玩家可以在游戏内
// "选项 -> Mod 选项"中调整参数，并随存档保存/加载。
//
// 注意：ModSettings 只负责"数据"——字段和序列化。
// UI 渲染由 Mod 类的 DoSettingsWindowContents 调用此处的 DoWindowContents。
//
// 我们用这个类来允许玩家调整"工作量倍率"：
//   - 默认 1.0  -> 约 2 个游戏日
//   - 0.5       -> 约 1 个游戏日（更快）
//   - 4.0       -> 约 8 个游戏日（更慢）
// ==============================================================================

using UnityEngine;
using Verse;

namespace YGFM
{
    /// <summary>
    /// Mod 设置类。继承 Verse.ModSettings。
    /// </summary>
    public class ModSettings_YuGongFillsMountain : ModSettings
    {
        /// <summary>
        /// 工作量倍率。
        /// 默认 1.0 = XML 中定义的 WorkToBuild (24000) ≈ 2 个游戏日。
        /// 允许范围 0.1 (快) 到 10.0 (慢)。
        /// </summary>
        public float WorkAmountMultiplier = 1.0f;

        /// <summary>
        /// 渲染 Mod 设置 UI。
        /// 由 Mod.DoSettingsWindowContents 调用。
        /// </summary>
        /// <param name="inRect">UI 绘制区域（一个矩形）</param>
        public void DoWindowContents(Rect inRect)
        {
            // Listing_Standard 是 RimWorld 提供的列表式 UI 助手，方便垂直排列控件
            var listing = new Listing_Standard();
            listing.Begin(inRect);

            // 标签行：显示当前倍率对应的预估天数（XML 基础值为 2 个游戏日）
            listing.Label(
                $"工作量倍率: {WorkAmountMultiplier:F2}  (预估 {2 * WorkAmountMultiplier:F1} 个游戏日)");

            // 滑块控件，让玩家拖动调整
            // 0.1 ~ 10.0 范围
            WorkAmountMultiplier = listing.Slider(WorkAmountMultiplier, 0.1f, 10.0f);

            // 间隔
            listing.Gap();

            // 提示信息
            listing.Label("提示: 倍率=1.0 时为标准 2 个游戏日；0.5=1 日；2.0=4 日。");

            listing.End();
        }

        /// <summary>
        /// 序列化/反序列化。在存档时调用 Scribe_Values.Look 写入；
        /// 读档时由 RimWorld 自动调用并填回字段。
        /// 参数: key 字符串必须与首次写入保持一致；第三个参数是默认值（首次存档时使用）。
        /// </summary>
        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Values.Look(ref WorkAmountMultiplier, "WorkAmountMultiplier", 1.0f);
        }
    }
}
