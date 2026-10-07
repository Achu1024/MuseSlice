using System;
using System.Collections.Generic;
using System.IO;

namespace QuickNote.Core
{
    /// <summary>
    /// 保存那一下的「咔嚓」——相机快门那个声音。
    /// <para>
    /// 这是**撞击声，不是音**，所以合成方式与一段旋律完全不同：一段极短的噪声瞬态，
    /// 加一两个快速衰减的谐振（撞击声的模态模型）。关键在于**时长**——3kHz 响 3 毫秒
    /// 是「脆」，响 100 毫秒才是「尖」。先前那版 E6→C6 的叮咚响 320 毫秒，而且二次
    /// 谐波正好落在 2.6kHz——人耳最敏感的那一段，听着就是刺耳。
    /// </para>
    /// <para>
    /// 自己合成而不是用系统提示音：SystemSounds 那几种里没有这类声音，而带一个 wav
    /// 文件或引一个音频库都违反零依赖。合成还有个好处——波形完全可控，也能用断言把
    /// WAV 头钉死（头写错会静默不响，那是肉眼和耳朵都很难归因的失败）。
    /// </para>
    /// <para>
    /// 噪声用**固定种子**：每次合成结果逐采样一致。这一条不是洁癖——使用者试听的
    /// 候选就是这份波形，写进程序后必须还是同一个声音。
    /// </para>
    /// </summary>
    public static class Chime
    {
        public const int SampleRate = 44100;

        /// <summary>噪声的随机种子。写死，保证每次合成完全一样。</summary>
        public const int NoiseSeed = 20261007;

        /// <summary>两下之间的间隔（毫秒）。快门是「咔-嚓」两声。</summary>
        public const int SecondBurstAtMs = 35;

        private const double LowpassCoef = 0.55;
        private const double FullScale = 32767.0;

        /// <summary>合成好的 16 位单声道 PCM WAV。</summary>
        public static byte[] Wav()
        {
            var samples = new List<short>();

            // 第一声「咔」：响、亮，几乎全是噪声
            AppendBurst(samples, 35, 0.26, 0.90, 0.0025, 1800, 0.008, 0.20);

            // 第二声「嚓」：轻一点、低一点
            AppendBurst(samples, 45, 0.20, 0.70, 0.0030, 1200, 0.012, 0.25);

            return Pack(samples);
        }

        /// <summary>
        /// 一下撞击。
        /// </summary>
        /// <param name="ms">这一段多长。</param>
        /// <param name="amp">输出总缩放。</param>
        /// <param name="noiseAmp">噪声量——撞击的那一下「嚓」。</param>
        /// <param name="noiseTau">噪声衰减时间常数，越小越干脆。</param>
        /// <param name="modeHz">谐振频率。</param>
        /// <param name="modeTau">谐振衰减，越小越「敲」。</param>
        /// <param name="modeWeight">谐振量。</param>
        private static void AppendBurst(List<short> into, int ms, double amp,
            double noiseAmp, double noiseTau, double modeHz, double modeTau, double modeWeight)
        {
            // 必须用 Math.Round（默认就是「四舍六入五成双」），不能截断：
            // PowerShell 的 [int] 转换走的正是这个规则（[int]1543.5 得 1544），
            // 而 C# 的 (int) 会截断成 1543——差一个采样，波形就对不上试听文件了。
            var count = (int)Math.Round(ms * SampleRate / 1000.0);

            // 每一下都从头开始取噪声：两下用的是同一段噪声序列。
            // 这是生成脚本的行为，也是使用者试听时听到的东西，不能改。
            var rng = new Random(NoiseSeed);

            var lowpass = 0.0;
            var phase = 0.0;
            var phaseStep = 2 * Math.PI * modeHz / SampleRate;

            for (var i = 0; i < count; i++)
            {
                var t = (double)i / SampleRate;
                var v = 0.0;

                // 噪声先过一道一阶低通：纯白噪听起来是「嘶」，低通之后才像敲到东西
                var white = rng.NextDouble() * 2 - 1;
                lowpass += LowpassCoef * (white - lowpass);
                v += noiseAmp * lowpass * Math.Exp(-t / noiseTau);

                phase += phaseStep;
                v += modeWeight * Math.Sin(phase) * Math.Exp(-t / modeTau);

                into.Add(Sample(amp * v));
            }
        }

        private static short Sample(double v)
        {
            var s = (int)Math.Round(v * FullScale);
            if (s > 32767) return 32767;
            if (s < -32767) return -32767;
            return (short)s;
        }

        private static byte[] Pack(List<short> samples)
        {
            var dataBytes = samples.Count * 2;

            using (var ms = new MemoryStream(44 + dataBytes))
            using (var w = new BinaryWriter(ms))
            {
                WriteTag(w, "RIFF");
                w.Write(36 + dataBytes);          // 之后的全部字节数
                WriteTag(w, "WAVE");

                WriteTag(w, "fmt ");
                w.Write(16);                       // PCM 的 fmt 块固定 16
                w.Write((short)1);                 // PCM
                w.Write((short)1);                 // 单声道
                w.Write(SampleRate);
                w.Write(SampleRate * 2);           // 字节率 = 采样率 × 声道 × 位深/8
                w.Write((short)2);                 // 块对齐
                w.Write((short)16);                // 位深

                WriteTag(w, "data");
                w.Write(dataBytes);
                foreach (var s in samples) w.Write(s);

                w.Flush();
                return ms.ToArray();
            }
        }

        /// <summary>四个 ASCII 字节的块标识。不用 Write(string)——那会走编码，长度不可控。</summary>
        private static void WriteTag(BinaryWriter w, string tag)
        {
            foreach (var c in tag) w.Write((byte)c);
        }
    }
}
