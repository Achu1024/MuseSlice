using System;
using System.Windows.Forms;

namespace MuseSlice
{
    /// <summary>
    /// RegisterHotKey 备用路径。钩子装不上时（被杀软拦、被安全策略禁）
    /// 用它兜底，否则失败模式是「热键没反应，使用者以为是自己的手抖」。
    /// </summary>
    internal sealed class FallbackHotkey : NativeWindow, IDisposable
    {
        private const int HotkeyId = 0x5154;
        private readonly Action _onActivated;
        private bool _registered;

        public FallbackHotkey(Action onActivated)
        {
            _onActivated = onActivated;
            CreateHandle(new CreateParams());
        }

        /// <summary>组合键字符串形如 "Ctrl+Alt+N"。注册失败返回 false 并给出原因。</summary>
        public bool TryRegister(string combo, out string error)
        {
            error = null;
            uint modifiers, vk;
            if (!TryParse(combo, out modifiers, out vk))
            {
                error = "无法识别的组合键：" + combo;
                return false;
            }

            if (_registered) NativeMethods.UnregisterHotKey(Handle, HotkeyId);
            _registered = NativeMethods.RegisterHotKey(Handle, HotkeyId,
                modifiers | NativeMethods.MOD_NOREPEAT, vk);

            if (!_registered)
            {
                error = "组合键 " + combo + " 已被其他程序占用";
                return false;
            }
            return true;
        }

        private static bool TryParse(string combo, out uint modifiers, out uint vk)
        {
            modifiers = 0;
            vk = 0;
            if (string.IsNullOrWhiteSpace(combo)) return false;

            var parts = combo.Split('+');
            for (var i = 0; i < parts.Length; i++)
            {
                var p = parts[i].Trim();
                if (p.Equals("Ctrl", StringComparison.OrdinalIgnoreCase)) modifiers |= NativeMethods.MOD_CONTROL;
                else if (p.Equals("Alt", StringComparison.OrdinalIgnoreCase)) modifiers |= NativeMethods.MOD_ALT;
                else if (p.Equals("Shift", StringComparison.OrdinalIgnoreCase)) modifiers |= NativeMethods.MOD_SHIFT;
                else if (p.Equals("Win", StringComparison.OrdinalIgnoreCase)) modifiers |= NativeMethods.MOD_WIN;
                else if (p.Length == 1)
                {
                    var c = char.ToUpperInvariant(p[0]);
                    if (c < 'A' || c > 'Z') return false;
                    vk = c;
                }
                else return false;
            }
            return modifiers != 0 && vk != 0;
        }

        protected override void WndProc(ref Message m)
        {
            if (m.Msg == NativeMethods.WM_HOTKEY && m.WParam.ToInt32() == HotkeyId) _onActivated();
            base.WndProc(ref m);
        }

        public void Dispose()
        {
            if (_registered)
            {
                NativeMethods.UnregisterHotKey(Handle, HotkeyId);
                _registered = false;
            }
            DestroyHandle();
        }
    }
}
