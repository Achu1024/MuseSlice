using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using MuseSlice.Core;

namespace MuseSlice
{
    /// <summary>
    /// 低级键盘钩子。回调里只做两件事：喂状态机、投递消息。
    /// <para>
    /// 任何 IO、分配、异常都会变成使用者每次打字的延迟——更糟的是，
    /// 回调跑在 UI 线程上，UI 线程卡顿会被放大成全系统键盘卡顿，
    /// 超过 LowLevelHooksTimeout（约 300ms）后 Windows 会静默摘掉钩子，
    /// 表现是「热键突然不灵了」且没有任何提示。
    /// </para>
    /// </summary>
    internal sealed class KeyboardHook : IDisposable
    {
        private readonly DoubleTapDetector _detector;
        private readonly IntPtr _targetWindow;
        private readonly uint _showMessage;
        private readonly NativeMethods.LowLevelKeyboardProc _proc;   // 必须持有引用，否则会被 GC 回收
        private IntPtr _hookId = IntPtr.Zero;

#if DEBUG
        private int _calls;
        private long _maxTicks;
#endif

        /// <summary>
        /// 触发时只向 <paramref name="targetWindow"/> 投递 <paramref name="showMessage"/>，
        /// 绝不在回调里直接做事。
        /// <para>
        /// 这里曾经直接调用一个 Action，而那个 Action 一路做到「读当天文件算条数 →
        /// 全文着色 → 创建窗口 → 抢前台」。低级钩子的回调是在安装它的 UI 线程上执行的，
        /// 期间 Windows 会阻塞整条键盘输入链路——正是 spec 第 8 节要防的那件事。
        /// 投递消息则立刻返回，真正的活在消息循环里做。
        /// </para>
        /// </summary>
        public KeyboardHook(int thresholdMs, IntPtr targetWindow, uint showMessage)
        {
            _detector = new DoubleTapDetector(thresholdMs);
            _targetWindow = targetWindow;
            _showMessage = showMessage;
            _proc = HookProc;
        }

        public bool TryInstall()
        {
            var module = NativeMethods.GetModuleHandle(null);
            _hookId = NativeMethods.SetWindowsHookEx(NativeMethods.WH_KEYBOARD_LL, _proc, module, 0);
            return _hookId != IntPtr.Zero;
        }

        private IntPtr HookProc(int nCode, IntPtr wParam, IntPtr lParam)
        {
            if (nCode >= 0)
            {
#if DEBUG
                var start = Stopwatch.GetTimestamp();
#endif
                try
                {
                    Feed(wParam.ToInt32(), Marshal.PtrToStructure<NativeMethods.KBDLLHOOKSTRUCT>(lParam));
                }
                catch
                {
                    // 钩子回调里绝不能把异常抛出去
                }
#if DEBUG
                TrackCost(start);
#endif
            }
            return NativeMethods.CallNextHookEx(_hookId, nCode, wParam, lParam);
        }

        private void Feed(int msg, NativeMethods.KBDLLHOOKSTRUCT data)
        {
            var isDown = msg == NativeMethods.WM_KEYDOWN || msg == NativeMethods.WM_SYSKEYDOWN;
            var isUp = msg == NativeMethods.WM_KEYUP || msg == NativeMethods.WM_SYSKEYUP;
            if (!isDown && !isUp) return;

            KeyAction action;
            if (data.vkCode == NativeMethods.VK_LCONTROL)
            {
                action = isDown ? KeyAction.LeftCtrlDown : KeyAction.LeftCtrlUp;
            }
            else
            {
                if (!isDown) return;      // 其他键的抬起不关心
                action = KeyAction.OtherKey;
            }

            if (_detector.KeyEvent(action, DateTime.Now))
            {
                NativeMethods.PostMessage(_targetWindow, _showMessage, IntPtr.Zero, IntPtr.Zero);
            }
        }

#if DEBUG
        /// <summary>
        /// 用静态的 Stopwatch.GetTimestamp 而不是 new Stopwatch()：后者每次按键
        /// 都会分配一个对象，正好违背「回调里不分配」这条纪律。
        /// </summary>
        private void TrackCost(long start)
        {
            var cost = Stopwatch.GetTimestamp() - start;
            if (cost > _maxTicks) _maxTicks = cost;
            _calls++;
            if (_calls % 1000 == 0)
            {
                var ms = _maxTicks * 1000.0 / Stopwatch.Frequency;
                Debug.WriteLine("[" + Brand.Name + "] 钩子回调累计 " + _calls + " 次，单次最大 "
                    + ms.ToString("F3") + " ms");
                _maxTicks = 0;
            }
        }
#endif

        public void Dispose()
        {
            if (_hookId != IntPtr.Zero)
            {
                NativeMethods.UnhookWindowsHookEx(_hookId);
                _hookId = IntPtr.Zero;
            }
        }
    }
}
