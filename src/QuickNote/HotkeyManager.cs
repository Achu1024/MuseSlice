using System;
using QuickNote.Core;

namespace QuickNote
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

        /// <summary>钩子装不上、已降级到备用组合键时触发，参数是给使用者看的原因。</summary>
        public event Action<string> Degraded;

        public HotkeyManager(AppConfig config)
        {
            _config = config;
        }

        public void Start(Action onActivated)
        {
            _onActivated = onActivated;

            if (_config.HotkeyMode == AppConfig.ModeDoubleTap)
            {
                _hook = new KeyboardHook(_config.DoubleTapMs, onActivated);
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
            Start(_onActivated);
        }

        public void Dispose()
        {
            if (_hook != null) { _hook.Dispose(); _hook = null; }
            if (_fallback != null) { _fallback.Dispose(); _fallback = null; }
        }
    }
}
