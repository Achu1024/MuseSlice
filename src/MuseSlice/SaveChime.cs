using System;
using System.IO;
using System.Media;
using System.Threading.Tasks;

namespace MuseSlice
{
    /// <summary>保存成功时播放的快门声。</summary>
    internal static class SaveChime
    {
        private const string SoundRelativePath = "sounds\\12-快门.wav";
        private static SoundPlayer _player;
        private static MemoryStream _stream;

        /// <summary>连着存两次时，两次播放不能叠在一起——叠在一起后一次会被整个丢掉。</summary>
        private static readonly object Gate = new object();

        public static void Prepare()
        {
            try { EnsurePlayer(); }
            catch (Exception ex)
            {
                MuseSliceApp.Log("保存提示音加载失败 " + SoundPath() + "：" + ex);
            }
        }

        /// <summary>
        /// 播一声。**不阻塞调用方**——丢到后台线程上同步播。
        /// <para>
        /// 不用 <see cref="SoundPlayer.Play"/>：那条异步路实测会出现「有时候根本不响」，
        /// 而 <c>PlaySync</c> 是稳的。所以稳的那条留下，只是别占 UI 线程——它要占约 80ms，
        /// 而保存回执和全局键盘钩子都在那条线程上。
        /// </para>
        /// </summary>
        public static void Play()
        {
            MuseSliceApp.Log("SaveChime.Play() called");
            Task.Run(delegate
            {
                lock (Gate)
                {
                    try
                    {
                        MuseSliceApp.Log("SaveChime: Starting playback");
                        EnsurePlayer();
                        // 每次播放前把流定位到开头，让同一个播放器可重复播放。
                        if (_stream != null) _stream.Position = 0;
                        _player.PlaySync();
                        MuseSliceApp.Log("SaveChime: Playback completed");
                    }
                    catch (Exception ex)
                    {
                        // 音频故障不能反过来影响已经成功的保存，但必须留下可诊断信息。
                        MuseSliceApp.Log("保存提示音播放失败 " + SoundPath() + "：" + ex);
                    }
                }
            });
        }

        private static void EnsurePlayer()
        {
            if (_player != null) return;

            var path = SoundPath();
            // 这个候选文件带了试听用的前后 400ms 静音。裁掉后再加载，
            // 否则同步播放也会先等 400ms 才真正听到声音。
            _stream = new MemoryStream(TrimSilence(File.ReadAllBytes(path)), false);
            var player = new SoundPlayer(_stream);
            player.Load();
            _stream.Position = 0;
            _player = player;
        }

        private static string SoundPath()
        {
            return Path.Combine(AppContext.BaseDirectory, SoundRelativePath);
        }

        private static byte[] TrimSilence(byte[] wav)
        {
            const int headerBytes = 44;
            const int sampleBytes = 2;
            if (wav.Length <= headerBytes
                || BitConverter.ToInt16(wav, 22) != 1
                || BitConverter.ToInt16(wav, 34) != 16)
                throw new InvalidDataException("保存提示音必须是 16 位单声道 WAV");

            var first = 0;
            var last = (wav.Length - headerBytes) / sampleBytes - 1;
            while (first <= last && BitConverter.ToInt16(wav, headerBytes + first * sampleBytes) == 0) first++;
            while (last >= first && BitConverter.ToInt16(wav, headerBytes + last * sampleBytes) == 0) last--;
            if (first > last) throw new InvalidDataException("保存提示音没有可播放内容");

            var dataBytes = (last - first + 1) * sampleBytes;
            var trimmed = new byte[headerBytes + dataBytes];
            Buffer.BlockCopy(wav, 0, trimmed, 0, headerBytes);
            Buffer.BlockCopy(wav, headerBytes + first * sampleBytes, trimmed, headerBytes, dataBytes);
            Buffer.BlockCopy(BitConverter.GetBytes(36 + dataBytes), 0, trimmed, 4, 4);
            Buffer.BlockCopy(BitConverter.GetBytes(dataBytes), 0, trimmed, 40, 4);
            return trimmed;
        }
    }
}
