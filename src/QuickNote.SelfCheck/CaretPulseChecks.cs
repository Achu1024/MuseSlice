using System;
using QuickNote.Core;

namespace QuickNote.SelfCheck
{
    internal static class CaretPulseChecks
    {
        public static void Run()
        {
            Check.Section("CaretPulse");

            Check.Equal(Math.Round(CaretPulse.LevelAt(0), 3), 0.35, "起点最暗");
            Check.Equal(Math.Round(CaretPulse.LevelAt(500), 3), 1.0, "半周期最亮");
            Check.Equal(Math.Round(CaretPulse.LevelAt(1000), 3), 0.35, "整周期回到起点");
            Check.Equal(Math.Round(CaretPulse.LevelAt(2000), 3), 0.35, "跨多个周期仍然对齐");
            Check.Equal(Math.Round(CaretPulse.LevelAt(250), 3), 0.675, "上升段线性");

            Check.That(CaretPulse.LevelAt(-1) > 0.34 && CaretPulse.LevelAt(-1) < 0.36,
                "负数毫秒被折回周期内而不是返回怪值");

            var rising = true;
            for (var t = 0; t < 500; t += 10)
            {
                if (CaretPulse.LevelAt(t + 10) < CaretPulse.LevelAt(t)) rising = false;
            }
            Check.That(rising, "前半周期单调变亮，没有抖动");

            var min = 2.0;
            for (var t = 0; t < 1000; t += 10) min = Math.Min(min, CaretPulse.LevelAt(t));
            Check.That(min >= 0.35, "永远不落到全暗——光标整个消失会像是丢了焦点");
        }
    }
}
