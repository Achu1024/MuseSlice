using System;
using MuseSlice.Core;

namespace MuseSlice.SelfCheck
{
    internal static class DoubleTapDetectorChecks
    {
        private static readonly DateTime T0 = new DateTime(2026, 10, 5, 14, 0, 0, 0);

        public static void Run()
        {
            Check.Section("DoubleTapDetector");
            TriggersWithinThreshold();
            IgnoresBeyondThreshold();
            CtrlCDoesNotTrigger();
            HoldingCtrlDoesNotTrigger();
            OtherKeyBetweenTapsDoesNotTrigger();
            FirstTapAloneDoesNotTrigger();
            TapAfterTriggerStartsFresh();
        }

        private static DoubleTapDetector New() { return new DoubleTapDetector(300); }

        private static void TriggersWithinThreshold()
        {
            var d = New();
            Check.That(!d.KeyEvent(KeyAction.LeftCtrlDown, T0), "第一次按下不触发");
            Check.That(!d.KeyEvent(KeyAction.LeftCtrlUp, T0.AddMilliseconds(50)), "第一次抬起不触发");
            Check.That(!d.KeyEvent(KeyAction.LeftCtrlDown, T0.AddMilliseconds(150)), "第二次按下不触发");
            Check.That(d.KeyEvent(KeyAction.LeftCtrlUp, T0.AddMilliseconds(180)),
                "两次轻点间隔 130ms（< 300ms）→ 第二次抬起时触发");
        }

        private static void IgnoresBeyondThreshold()
        {
            var d = New();
            d.KeyEvent(KeyAction.LeftCtrlDown, T0);
            d.KeyEvent(KeyAction.LeftCtrlUp, T0.AddMilliseconds(50));
            d.KeyEvent(KeyAction.LeftCtrlDown, T0.AddMilliseconds(600));
            Check.That(!d.KeyEvent(KeyAction.LeftCtrlUp, T0.AddMilliseconds(650)),
                "两次轻点间隔 600ms（> 300ms）→ 不触发");
        }

        private static void CtrlCDoesNotTrigger()
        {
            var d = New();
            d.KeyEvent(KeyAction.LeftCtrlDown, T0);
            d.KeyEvent(KeyAction.OtherKey, T0.AddMilliseconds(30));      // 期间按了 C
            d.KeyEvent(KeyAction.LeftCtrlUp, T0.AddMilliseconds(60));

            d.KeyEvent(KeyAction.LeftCtrlDown, T0.AddMilliseconds(120));
            d.KeyEvent(KeyAction.OtherKey, T0.AddMilliseconds(140));      // 又一次 Ctrl+C
            Check.That(!d.KeyEvent(KeyAction.LeftCtrlUp, T0.AddMilliseconds(170)),
                "两次 Ctrl+C 不触发（Ctrl 抬起前夹了其他键）");
        }

        private static void HoldingCtrlDoesNotTrigger()
        {
            var d = New();
            Check.That(!d.KeyEvent(KeyAction.LeftCtrlDown, T0), "长按初期不触发");
            Check.That(!d.KeyEvent(KeyAction.LeftCtrlDown, T0.AddMilliseconds(40)),
                "长按产生的重复 KeyDown 被忽略");
            Check.That(!d.KeyEvent(KeyAction.LeftCtrlDown, T0.AddMilliseconds(80)),
                "继续重复 KeyDown 仍被忽略");
            Check.That(!d.KeyEvent(KeyAction.LeftCtrlUp, T0.AddMilliseconds(1500)),
                "长按 1.5 秒后抬起不触发");
        }

        private static void OtherKeyBetweenTapsDoesNotTrigger()
        {
            var d = New();
            d.KeyEvent(KeyAction.LeftCtrlDown, T0);
            d.KeyEvent(KeyAction.LeftCtrlUp, T0.AddMilliseconds(50));
            d.KeyEvent(KeyAction.OtherKey, T0.AddMilliseconds(100));      // 中间敲了别的键
            d.KeyEvent(KeyAction.LeftCtrlDown, T0.AddMilliseconds(150));
            Check.That(!d.KeyEvent(KeyAction.LeftCtrlUp, T0.AddMilliseconds(180)),
                "两次轻点之间夹了其他键 → 不触发");
        }

        private static void FirstTapAloneDoesNotTrigger()
        {
            var d = New();
            d.KeyEvent(KeyAction.LeftCtrlDown, T0);
            Check.That(!d.KeyEvent(KeyAction.LeftCtrlUp, T0.AddMilliseconds(50)),
                "只轻点一次 Ctrl 不触发");
        }

        private static void TapAfterTriggerStartsFresh()
        {
            var d = New();
            d.KeyEvent(KeyAction.LeftCtrlDown, T0);
            d.KeyEvent(KeyAction.LeftCtrlUp, T0.AddMilliseconds(50));
            d.KeyEvent(KeyAction.LeftCtrlDown, T0.AddMilliseconds(150));
            Check.That(d.KeyEvent(KeyAction.LeftCtrlUp, T0.AddMilliseconds(180)), "先触发一次");

            // 紧接着第三次轻点不应立刻再触发一次——触发后状态必须清空
            d.KeyEvent(KeyAction.LeftCtrlDown, T0.AddMilliseconds(200));
            Check.That(!d.KeyEvent(KeyAction.LeftCtrlUp, T0.AddMilliseconds(230)),
                "触发后紧接着的第三次轻点不重复触发");
        }
    }
}
