using System;
using MuseSlice.Core;

namespace MuseSlice
{
    /// <summary>
    /// 串起主路径（全局键盘钩子 + 双击左 Ctrl）与备用路径（RegisterHotKey）。
    /// 钩子装不上时自动降级并说明原因——静默失效是最难排查的失败模式。
    /// </summary>
    internal sealed class HotkeyManager : IDisposable
    {
        private readonly AppConfig _config;
        private KeyboardHook _hook;
        private FallbackHotkey _fallback;
        private Action _onActivated;
        private IntPtr _targetWindow;
        private uint _showMessage;

        /// <summary>钩子装不上、已降级到备用组合键时触发，参数是给使用者看的原因。</summary>
        public event Action<string> Degraded;

        public HotkeyManager(AppConfig config)
        {
            _config = config;
        }

        /// <summary>
        /// <paramref name="targetWindow"/> 与 <paramref name="showMessage"/> 用于主路径：
        /// 钩子回调只投递这条消息，具体工作回到消息循环里做。
        /// </summary>
        public void Start(Action onActivated, IntPtr targetWindow, uint showMessage)
        {
            _onActivated = onActivated;
            _targetWindow = targetWindow;
            _showMessage = showMessage;

            if (_config.HotkeyMode == AppConfig.ModeDoubleTap)
            {
                _hook = new KeyboardHook(_config.DoubleTapMs, targetWindow, showMessage);
                if (_hook.TryInstall()) return;

                _hook.Dispose();
                _hook = null;
                EnsureFallback("全局键盘钩子安装失败（可能被安全软件拦截），已改用备用组合键。");
            }
            else
            {
                EnsureFallback(null);
            }
        }

        private void EnsureFallback(string reason)
        {
            if (_fallback == null) _fallback = new FallbackHotkey(_onActivated);

            string error;
            if (!_fallback.TryRegister(_config.FallbackHotkey, out error))
            {
                if (Degraded != null) Degraded(error);
                return;
            }

            if (reason != null && Degraded != null) Degraded(reason);
        }

        public void ApplyConfigChange()
        {
            Dispose();
            Start(_onActivated, _targetWindow, _showMessage);
        }

        public void Dispose()
        {
            if (_hook != null) { _hook.Dispose(); _hook = null; }
            if (_fallback != null) { _fallback.Dispose(); _fallback = null; }
        }
    }
}
