using System;

namespace MuseSlice.Core
{
    /// <summary>
    /// 双击左 Ctrl 的判定。纯状态机：调用方喂它按键事件与时间戳，
    /// 它回答是否触发。完全不知道键盘钩子的存在，因此可以离线测试。
    /// </summary>
    public sealed class DoubleTapDetector
    {
        private readonly int _thresholdMs;
        private bool _ctrlDown;
        private bool _cleanTap;
        private DateTime? _lastTapAt;

        public DoubleTapDetector(int thresholdMs)
        {
            if (thresholdMs <= 0) throw new ArgumentOutOfRangeException("thresholdMs");
            _thresholdMs = thresholdMs;
        }

        public int ThresholdMs { get { return _thresholdMs; } }

        public bool KeyEvent(KeyAction action, DateTime now)
        {
            switch (action)
            {
                case KeyAction.LeftCtrlDown:
                    if (_ctrlDown) return false;   // 长按产生的重复 KeyDown，忽略
                    _ctrlDown = true;
                    _cleanTap = true;
                    return false;

                case KeyAction.LeftCtrlUp:
                    if (!_ctrlDown) return false;
                    _ctrlDown = false;
                    if (!_cleanTap) return false;  // Ctrl+C 之类，不是轻点

                    if (_lastTapAt.HasValue &&
                        (now - _lastTapAt.Value).TotalMilliseconds < _thresholdMs)
                    {
                        _lastTapAt = null;
                        return true;
                    }
                    _lastTapAt = now;
                    return false;

                default:
                    // 任何其他键都会打断待定的轻点序列，否则每个 Ctrl+C
                    // 都会和下一次 Ctrl 轻点凑成一次「双击」。
                    _cleanTap = false;
                    _lastTapAt = null;
                    return false;
            }
        }
    }
}
