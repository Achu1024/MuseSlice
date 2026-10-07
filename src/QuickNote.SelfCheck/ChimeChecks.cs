using System;
using System.IO;
using QuickNote.Core;

namespace QuickNote.SelfCheck
{
    internal static class ChimeChecks
    {
        public static void Run()
        {
            Check.Section("Chime");
            WavHeaderIsWellFormed();
            WavActuallyCarriesSound();
            SecondNoteIsLowerThanFirst();
        }

        /// <summary>
        /// 头写错了播放器会静默不响——我听不到，所以只能把每个字段钉死。
        /// </summary>
        private static void WavHeaderIsWellFormed()
        {
            var wav = Chime.Wav();
            Check.That(wav.Length > 44, "文件不止一个 44 字节的头");

            using (var ms = new MemoryStream(wav))
            using (var r = new BinaryReader(ms))
            {
                Check.Equal(new string(r.ReadChars(4)), "RIFF", "开头是 RIFF");
                Check.Equal(r.ReadInt32(), wav.Length - 8,
                    "RIFF 长度 = 总长 - 8（写错的话播放器会读到不完整的文件）");
                Check.Equal(new string(r.ReadChars(4)), "WAVE", "是 WAVE 而不是别的 RIFF 类型");

                Check.Equal(new string(r.ReadChars(4)), "fmt ", "有 fmt 块");
                Check.Equal(r.ReadInt32(), 16, "fmt 块长度是 16（PCM 的标准值）");
                Check.Equal(r.ReadInt16(), (short)1, "格式是 PCM");
                Check.Equal(r.ReadInt16(), (short)1, "单声道");
                Check.Equal(r.ReadInt32(), Chime.SampleRate, "采样率");
                Check.Equal(r.ReadInt32(), Chime.SampleRate * 2, "字节率 = 采样率 × 单声道 × 2 字节");
                Check.Equal(r.ReadInt16(), (short)2, "块对齐是 2 字节");
                Check.Equal(r.ReadInt16(), (short)16, "位深 16");

                Check.Equal(new string(r.ReadChars(4)), "data", "有 data 块");
                Check.Equal(r.ReadInt32(), wav.Length - 44, "data 长度与文件总长对得上");
            }
        }

        private static void WavActuallyCarriesSound()
        {
            var wav = Chime.Wav();
            var samples = ReadSamples(wav);

            Check.That(samples.Length > Chime.SampleRate / 8,
                "长度够听（至少 125 毫秒）");
            Check.That(samples.Length < Chime.SampleRate,
                "长度克制（不到 1 秒，别拖住保存这个动作）");

            var nonZero = 0;
            var peak = 0;
            foreach (var s in samples)
            {
                var v = s < 0 ? -s : s;
                if (v > 0) nonZero++;
                if (v > peak) peak = v;
            }

            Check.That(nonZero > samples.Length / 2, "大部分采样非零——不是一段静音");
            Check.That(peak > short.MaxValue / 8, "音量不是小到听不见");
            Check.That(peak < short.MaxValue, "没有削波（削波听起来是「咔」不是「叮咚」）");

            Check.Equal(samples[0], (short)0, "第一个采样是 0：从 0 起音，否则会「啪」一声");
            Check.That(Math.Abs((int)samples[samples.Length - 1]) < short.MaxValue / 16,
                "尾巴衰减到接近 0，不会截断成「咔」");
        }

        /// <summary>「叮咚」是先高后低。只能从零交叉率上看出来，因为波形本身没法「听」。</summary>
        private static void SecondNoteIsLowerThanFirst()
        {
            var samples = ReadSamples(Chime.Wav());

            // 两段各取靠前的一段，那时衰减还没把振幅压下去
            var firstEnd = (int)(0.08 * Chime.SampleRate);
            var secondStart = (int)(0.13 * Chime.SampleRate);
            var secondEnd = (int)(0.21 * Chime.SampleRate);

            var first = ZeroCrossings(samples, 0, firstEnd);
            var second = ZeroCrossings(samples, secondStart, secondEnd);

            Check.That(first > second,
                "第一声比第二声高（「叮」在上、「咚」在下）");
            Check.That(second > 0, "第二声确实响了，不是一段静音");
        }

        private static int ZeroCrossings(short[] samples, int from, int to)
        {
            var count = 0;
            for (var i = from + 1; i < to && i < samples.Length; i++)
            {
                if ((samples[i - 1] < 0) != (samples[i] < 0)) count++;
            }
            return count;
        }

        private static short[] ReadSamples(byte[] wav)
        {
            var samples = new short[(wav.Length - 44) / 2];
            for (var i = 0; i < samples.Length; i++)
            {
                samples[i] = BitConverter.ToInt16(wav, 44 + i * 2);
            }
            return samples;
        }
    }
}
