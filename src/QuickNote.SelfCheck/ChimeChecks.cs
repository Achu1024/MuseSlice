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
            IsATransientNotATone();
            SecondBurstIsSofterThanTheFirst();
            IsDeterministic();
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

        /// <summary>
        /// 这是撞击声，不是音：**能量必须是「一下就没」的形状**。
        /// 先前那版叮咚响 320 毫秒、二次谐波落在 2.6kHz（人耳最敏感那一段），
        /// 听着刺耳。改成快门之后要有两条相反的保证：够短、开头就最响。
        /// </summary>
        private static void IsATransientNotATone()
        {
            var samples = ReadSamples(Chime.Wav());
            var ms = samples.Length * 1000.0 / Chime.SampleRate;

            Check.That(ms > 50, "不短于 50 毫秒——太短会连播放器开音频设备的时间都不够");
            Check.That(ms < 150, "不超过 150 毫秒——超过这个长度就从「脆」变成「尖」了");

            // 峰值必须出现在开头：撞击声的定义就是这个
            var peakAt = 0;
            var peak = 0;
            for (var i = 0; i < samples.Length; i++)
            {
                var v = Math.Abs((int)samples[i]);
                if (v > peak) { peak = v; peakAt = i; }
            }

            Check.That(peak > short.MaxValue / 8, "音量不是小到听不见");
            Check.That(peak < short.MaxValue, "没有削波（削波听起来是「啪」不是「咔」）");
            Check.That(peakAt < Chime.SampleRate * 0.03,
                "峰值落在头 30 毫秒内——是瞬态，不是一段音");
            Check.That(Math.Abs((int)samples[0]) > 0 || Math.Abs((int)samples[1]) > 0,
                "一开头就有声，不是渐入");

            // 尾巴要落回接近 0，否则截断处会「咔」一声
            Check.That(Math.Abs((int)samples[samples.Length - 1]) < peak / 8,
                "结尾衰减到峰值的八分之一以下，不会被截断成第二个咔哒");
        }

        /// <summary>快门是「咔-嚓」两下，第二下更轻。用两段的能量比来验。</summary>
        private static void SecondBurstIsSofterThanTheFirst()
        {
            var samples = ReadSamples(Chime.Wav());
            var split = Chime.SecondBurstAtMs * Chime.SampleRate / 1000;

            Check.That(split < samples.Length, "第二下的起点在文件之内");

            var first = Energy(samples, 0, split);
            var second = Energy(samples, split, samples.Length);

            Check.That(second > 0, "第二下确实响了，不是一段静音");
            Check.That(first > second, "第一下比第二下响（「咔」在前、「嚓」在后）");
        }

        /// <summary>
        /// 噪声种子固定，两次合成必须逐字节一致。
        /// 这一条不是洁癖：使用者试听的候选就是这份波形，写进程序后必须是同一个声音。
        /// </summary>
        private static void IsDeterministic()
        {
            var a = Chime.Wav();
            var b = Chime.Wav();

            Check.Equal(a.Length, b.Length, "两次合成的长度一致");

            var same = a.Length == b.Length;
            for (var i = 0; same && i < a.Length; i++)
            {
                if (a[i] != b[i]) same = false;
            }
            Check.That(same, "两次合成逐字节一致——种子没有漂移");
        }

        private static double Energy(short[] samples, int from, int to)
        {
            var sum = 0.0;
            for (var i = from; i < to && i < samples.Length; i++) sum += (double)samples[i] * samples[i];
            return sum;
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
