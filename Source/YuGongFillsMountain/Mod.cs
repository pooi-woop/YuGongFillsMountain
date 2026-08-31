// ==============================================================================
// 愚公填山 (YuGongFillsMountain) - Mod 入口
// ==============================================================================
//
// 这是 Mod 的 C# 入口点。RimWorld 加载 Mod 时会自动找到继承自 `Mod` 的类并实例化。
// 我们在构造函数中：
//   1. 加载 Mod 设置 (ModSettings)
//   2. 初始化 Harmony 补丁
//
// 同时重写两个 UI 方法让玩家能在"选项 -> Mod 选项"中调整设置:
//   - SettingsCategory() -> 显示在 Mod 选项菜单中的标题
//   - DoSettingsWindowContents() -> 渲染设置 UI
//
// 命名空间 YGFM 是 "Yu Gong Fills Mountain" 的缩写。
// ==============================================================================

using HarmonyLib;
using UnityEngine;
using Verse;

namespace YGFM
{
    /// <summary>
    /// Mod 入口类。继承自 Verse.Mod。
    /// RimWorld 启动时会自动实例化此类的对象。
    /// </summary>
    public class YuGongFillsMountainMod : Mod
    {
        /// <summary>
        /// 静态持有的设置对象，供其他类（如 Harmony patch）访问。
        /// 在构造函数中被赋值。
        /// </summary>
        public static ModSettings_YuGongFillsMountain Settings;

        /// <summary>
        /// Mod 加载时调用的构造函数。
        /// </summary>
        public YuGongFillsMountainMod(ModContentPack content) : base(content)
        {
            // 1. 加载 Mod 设置（GetSettings<T>() 是 Mod 基类提供的方法，会自动从存档读取）
            Settings = GetSettings<ModSettings_YuGongFillsMountain>();

            // 2. 初始化 Harmony 补丁
            //    "PooiWoop.YuGongFillsMountain" 是唯一 patch ID，确保不与其他 mod 冲突
            var harmony = new Harmony("PooiWoop.YuGongFillsMountain");
            harmony.PatchAll();
            Log.Message("[愚公填山] Harmony 补丁已应用。");
        }

        /// <summary>
        /// 设置窗口标题（显示在 "选项 -> Mod 选项" 列表中）。
        /// 重写 Mod 基类的方法。
        /// </summary>
        public override string SettingsCategory()
        {
            return "愚公填山 (YuGongFillsMountain)";
        }

        /// <summary>
        /// 渲染 Mod 设置 UI 窗口。
        /// RimWorld 在玩家打开 "选项 -> Mod 选项" 时调用此方法。
        /// 重写 Mod 基类的方法。
        /// </summary>
        /// <param name="inRect">UI 绘制区域（一个矩形）</param>
        public override void DoSettingsWindowContents(Rect inRect)
        {
            base.DoSettingsWindowContents(inRect);
            // 委托给 ModSettings 类的 DoWindowContents 方法来实际绘制 UI
            Settings.DoWindowContents(inRect);
        }
    }
}
