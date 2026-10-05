using System;
using System.Windows.Forms;

namespace QuickNote
{
    /// <summary>
    /// 一个不可见的消息窗口。第二个实例启动时会向 HWND_BROADCAST 投递一个
    /// 由 RegisterWindowMessage 得来的自定义消息；已经有实例在跑的话由这里收到，
    /// 转成事件弹小条。没有这个窗口，广播就石沉大海，
    /// 表现是「双击 exe 什么也没发生」。
    /// </summary>
    internal sealed class MessageWindow : NativeWindow, IDisposable
    {
        private readonly uint _message;
        public event Action ShowRequested;

        public MessageWindow(uint message)
        {
            _message = message;
            CreateHandle(new CreateParams());
        }

        protected override void WndProc(ref Message m)
        {
            if ((uint)m.Msg == _message)
            {
                var h = ShowRequested;
                if (h != null) h();
            }
            base.WndProc(ref m);
        }

        public void Dispose()
        {
            DestroyHandle();
        }
    }
}
