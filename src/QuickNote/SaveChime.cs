using System.IO;
using System.Media;
using QuickNote.Core;

namespace QuickNote
{
    /// <summary>
    /// 保存那一声「叮咚」。波形由 <see cref="Chime"/> 合成，这里只负责播。
    /// </summary>
    internal static class SaveChime
    {
        private static SoundPlayer _player;

        /// <summary>必须留着：SoundPlayer 播放时要回头读这个流，提前释放就没声了。</summary>
        private static MemoryStream _stream;

        public static void Play()
        {
            try
            {
                if (_player == null)
                {
                    _stream = new MemoryStream(Chime.Wav());
                    _player = new SoundPlayer(_stream);
                    _player.Load();     // 先加载好：第一次保存不该因为现读流而慢半拍
                }

                // Play 是异步的（自带后台线程）。这一点很要紧——UI 线程一卡，
                // 全局键盘钩子就跟着卡，超过约 300ms 会被 Windows 静默摘掉。
                _player.Play();
            }
            catch
            {
                // 没声卡、被策略禁掉之类的情况不该影响「保存成功」这个事实
            }
        }
    }
}
