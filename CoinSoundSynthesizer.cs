using System;
using System.IO;
using NAudio.Wave;

namespace rans0m
{
    public static class CoinSoundSynthesizer
    {
        private static readonly byte[][] cachedCoinWavs;
        private static readonly byte[] cachedSlamWav;
        private static readonly byte[] cachedVaseBreakWav;
        private static readonly byte[] cachedGlitchProcessingWav;
        private static readonly byte[] cachedInstallWav;
        private static WaveOut? activeProcessingWaveOut;

        static CoinSoundSynthesizer()
        {
            // Pre-generate ascending crunchy digital glitch coin chimes (authentic A-90 / Doors corrupted arcade sound)
            double[] baseFreqs = new double[] { 2300.0, 2650.0, 3050.0, 3500.0, 4050.0, 4600.0 };
            cachedCoinWavs = new byte[baseFreqs.Length][];

            for (int i = 0; i < baseFreqs.Length; i++)
            {
                cachedCoinWavs[i] = GenerateGlitchCoinWav(baseFreqs[i], 0.068);
            }

            cachedSlamWav = GenerateSlamGlitchWav();
            cachedVaseBreakWav = GenerateVaseBreakWav();
            cachedGlitchProcessingWav = GenerateProcessingGlitchWav();

            try
            {
                using var stream = Properties.Resources.install;
                if (stream != null)
                {
                    using var ms = new MemoryStream();
                    stream.CopyTo(ms);
                    cachedInstallWav = ms.ToArray();
                }
                else
                {
                    cachedInstallWav = Array.Empty<byte>();
                }
            }
            catch
            {
                cachedInstallWav = Array.Empty<byte>();
            }
        }

        public static void PlayVaseBreak(float volume = 0.90f)
        {
            try
            {
                var ms = new MemoryStream(cachedVaseBreakWav);
                var reader = new WaveFileReader(ms);
                var waveOut = new WaveOut { Volume = Math.Clamp(volume, 0f, 1f) };

                waveOut.Init(reader);
                waveOut.PlaybackStopped += (s, e) =>
                {
                    try
                    {
                        waveOut.Dispose();
                        reader.Dispose();
                        ms.Dispose();
                    }
                    catch { }
                };

                waveOut.Play();
            }
            catch { }
        }

        public static void PlayAchievementSound(float volume = 0.90f)
        {
            try
            {
                using var stream = Properties.Resources.achievement;
                if (stream == null) return;
                var ms = new MemoryStream();
                stream.CopyTo(ms);
                ms.Position = 0;

                var reader = new Mp3FileReader(ms);
                var waveOut = new WaveOut { Volume = Math.Clamp(volume, 0f, 1f) };
                waveOut.Init(reader);
                waveOut.PlaybackStopped += (s, e) =>
                {
                    try
                    {
                        waveOut.Dispose();
                        reader.Dispose();
                        ms.Dispose();
                    }
                    catch { }
                };
                waveOut.Play();
            }
            catch { }
        }

        public static void PlayDamageSound(float volume = 0.85f)
        {
            try
            {
                using var stream = Properties.Resources.damage;
                if (stream == null) return;
                var ms = new MemoryStream();
                stream.CopyTo(ms);
                ms.Position = 0;

                var reader = new NAudio.Vorbis.VorbisWaveReader(ms);
                var waveOut = new WaveOut { Volume = Math.Clamp(volume, 0f, 1f) };
                waveOut.Init(reader);
                waveOut.PlaybackStopped += (s, e) =>
                {
                    try
                    {
                        waveOut.Dispose();
                        reader.Dispose();
                        ms.Dispose();
                    }
                    catch { }
                };
                waveOut.Play();
            }
            catch { }
        }

        public static void PlayWindowSlamGlitch(float volume = 0.85f)
        {
            try
            {
                var ms = new MemoryStream(cachedSlamWav);
                var reader = new WaveFileReader(ms);
                var waveOut = new WaveOut { Volume = Math.Clamp(volume, 0f, 1f) };

                waveOut.Init(reader);
                waveOut.PlaybackStopped += (s, e) =>
                {
                    try
                    {
                        waveOut.Dispose();
                        reader.Dispose();
                        ms.Dispose();
                    }
                    catch { }
                };

                waveOut.Play();
            }
            catch { }
        }

        public static void PlayCoinDing(int pitchIndex = 0, float volume = 0.70f)
        {
            try
            {
                int idx = Math.Clamp(pitchIndex, 0, cachedCoinWavs.Length - 1);
                byte[] wavData = cachedCoinWavs[idx];

                var ms = new MemoryStream(wavData);
                var reader = new WaveFileReader(ms);
                var waveOut = new WaveOut { Volume = Math.Clamp(volume, 0f, 1f) };

                waveOut.Init(reader);
                waveOut.PlaybackStopped += (s, e) =>
                {
                    try
                    {
                        waveOut.Dispose();
                        reader.Dispose();
                        ms.Dispose();
                    }
                    catch { }
                };

                waveOut.Play();
            }
            catch { }
        }

        public static void PlayProcessingGlitchSfx(float volume = 0.85f)
        {
            try
            {
                var ms = new MemoryStream(cachedGlitchProcessingWav);
                var reader = new WaveFileReader(ms);
                var waveOut = new WaveOut { Volume = Math.Clamp(volume, 0f, 1f) };

                waveOut.Init(reader);
                waveOut.PlaybackStopped += (s, e) =>
                {
                    try
                    {
                        waveOut.Dispose();
                        reader.Dispose();
                        ms.Dispose();
                    }
                    catch { }
                };

                waveOut.Play();
            }
            catch { }
        }

        public static void PlayProcessingSound(float volume = 0.85f)
        {
            try
            {
                StopProcessingSound();
                if (cachedInstallWav == null || cachedInstallWav.Length == 0) return;

                var ms = new MemoryStream(cachedInstallWav);
                var reader = new WaveFileReader(ms);
                var waveOut = new WaveOut { Volume = Math.Clamp(volume, 0f, 1f) };

                waveOut.Init(reader);
                waveOut.PlaybackStopped += (s, e) =>
                {
                    try
                    {
                        waveOut.Dispose();
                        reader.Dispose();
                        ms.Dispose();
                    }
                    catch { }
                };

                activeProcessingWaveOut = waveOut;
                waveOut.Play();
            }
            catch { }
        }

        public static void StopProcessingSound()
        {
            try
            {
                if (activeProcessingWaveOut != null)
                {
                    activeProcessingWaveOut.Volume = 0f;
                    activeProcessingWaveOut.Stop();
                    activeProcessingWaveOut.Dispose();
                    activeProcessingWaveOut = null;
                }
            }
            catch { }
        }

        private static byte[] GenerateGlitchCoinWav(double fundamentalFreq, double durationSeconds)
        {
            int sampleRate = 44100;
            int numSamples = (int)(sampleRate * durationSeconds);
            byte[] bytes = new byte[44 + numSamples * 2];

            // RIFF Header
            System.Text.Encoding.ASCII.GetBytes("RIFF").CopyTo(bytes, 0);
            BitConverter.GetBytes(bytes.Length - 8).CopyTo(bytes, 4);
            System.Text.Encoding.ASCII.GetBytes("WAVE").CopyTo(bytes, 8);
            System.Text.Encoding.ASCII.GetBytes("fmt ").CopyTo(bytes, 12);
            BitConverter.GetBytes(16).CopyTo(bytes, 16); // Subchunk1Size (16 for PCM)
            BitConverter.GetBytes((short)1).CopyTo(bytes, 20); // AudioFormat 1 = PCM
            BitConverter.GetBytes((short)1).CopyTo(bytes, 22); // NumChannels = 1 (Mono)
            BitConverter.GetBytes(sampleRate).CopyTo(bytes, 24);
            BitConverter.GetBytes(sampleRate * 2).CopyTo(bytes, 28); // ByteRate
            BitConverter.GetBytes((short)2).CopyTo(bytes, 32); // BlockAlign
            BitConverter.GetBytes((short)16).CopyTo(bytes, 34); // BitsPerSample
            System.Text.Encoding.ASCII.GetBytes("data").CopyTo(bytes, 36);
            BitConverter.GetBytes(numSamples * 2).CopyTo(bytes, 40);

            var rnd = new Random((int)fundamentalFreq);
            double f0 = fundamentalFreq;

            for (int i = 0; i < numSamples; i++)
            {
                double t = (double)i / sampleRate;

                // Snappy exponential falloff (55/s)
                double env = Math.Exp(-t * 55.0);

                // Initial 3.5ms digital crunch glitch pop
                double glitchNoise = 0.0;
                if (t < 0.0035)
                {
                    glitchNoise = (rnd.NextDouble() - 0.5) * 0.65;
                }

                // Frequency with micro digital jitter
                double fMod = f0 + (rnd.NextDouble() - 0.5) * 35.0;

                // Chime with bit-buzz harmonic and inharmonic ring
                double s0 = Math.Sin(2.0 * Math.PI * fMod * t);
                double s1 = Math.Sign(Math.Sin(2.0 * Math.PI * (fMod * 2.23) * t)) * 0.32; // Digital square overtone
                double s2 = Math.Sin(2.0 * Math.PI * (fMod * 3.86) * t) * 0.20; // Metallic inharmonic ping

                double raw = (0.55 * s0 + s1 + s2) * env + glitchNoise;

                // Digital 7-bit quantization / bitcrush for corrupted A-90 feel
                double crushed = Math.Round(raw * 40.0) / 40.0;

                short pcmVal = (short)Math.Clamp((int)(crushed * 26000.0), -32767, 32767);
                BitConverter.GetBytes(pcmVal).CopyTo(bytes, 44 + i * 2);
            }

            return bytes;
        }

        private static byte[] GenerateSlamGlitchWav()
        {
            int sampleRate = 44100;
            double duration = 0.16; // 160ms
            int numSamples = (int)(sampleRate * duration);
            byte[] bytes = new byte[44 + numSamples * 2];

            // RIFF Header
            System.Text.Encoding.ASCII.GetBytes("RIFF").CopyTo(bytes, 0);
            BitConverter.GetBytes(bytes.Length - 8).CopyTo(bytes, 4);
            System.Text.Encoding.ASCII.GetBytes("WAVE").CopyTo(bytes, 8);
            System.Text.Encoding.ASCII.GetBytes("fmt ").CopyTo(bytes, 12);
            BitConverter.GetBytes(16).CopyTo(bytes, 16);
            BitConverter.GetBytes((short)1).CopyTo(bytes, 20);
            BitConverter.GetBytes((short)1).CopyTo(bytes, 22);
            BitConverter.GetBytes(sampleRate).CopyTo(bytes, 24);
            BitConverter.GetBytes(sampleRate * 2).CopyTo(bytes, 28);
            BitConverter.GetBytes((short)2).CopyTo(bytes, 32);
            BitConverter.GetBytes((short)16).CopyTo(bytes, 34);
            System.Text.Encoding.ASCII.GetBytes("data").CopyTo(bytes, 36);
            BitConverter.GetBytes(numSamples * 2).CopyTo(bytes, 40);

            var rnd = new Random(9090);

            for (int i = 0; i < numSamples; i++)
            {
                double t = (double)i / sampleRate;

                // Fast bass thud envelope: punchy attack, exponential decay
                double env = Math.Exp(-t * 26.0);

                // Initial cyber burst noise (first 18ms)
                double burst = 0.0;
                if (t < 0.018)
                {
                    burst = (rnd.NextDouble() - 0.5) * 0.85 * (1.0 - t / 0.018);
                }

                // Pitch envelope drops from 140Hz down to 55Hz
                double pitch = 55.0 + 85.0 * Math.Exp(-t * 35.0);
                double sine = Math.Sin(2.0 * Math.PI * pitch * t);
                double sub = Math.Sin(2.0 * Math.PI * (pitch * 0.5) * t) * 0.4;
                double dist = Math.Sign(sine) * 0.25;

                double raw = (sine * 0.65 + sub + dist) * env + burst;
                double crushed = Math.Round(raw * 32.0) / 32.0;

                short pcmVal = (short)Math.Clamp((int)(crushed * 28000.0), -32767, 32767);
                BitConverter.GetBytes(pcmVal).CopyTo(bytes, 44 + i * 2);
            }

            return bytes;
        }

        private static byte[] GenerateVaseBreakWav()
        {
            int sampleRate = 44100;
            double duration = 0.35; // 350ms
            int numSamples = (int)(sampleRate * duration);
            byte[] bytes = new byte[44 + numSamples * 2];

            // RIFF Header
            System.Text.Encoding.ASCII.GetBytes("RIFF").CopyTo(bytes, 0);
            BitConverter.GetBytes(bytes.Length - 8).CopyTo(bytes, 4);
            System.Text.Encoding.ASCII.GetBytes("WAVE").CopyTo(bytes, 8);
            System.Text.Encoding.ASCII.GetBytes("fmt ").CopyTo(bytes, 12);
            BitConverter.GetBytes(16).CopyTo(bytes, 16);
            BitConverter.GetBytes((short)1).CopyTo(bytes, 20); // PCM
            BitConverter.GetBytes((short)1).CopyTo(bytes, 22); // Mono
            BitConverter.GetBytes(sampleRate).CopyTo(bytes, 24);
            BitConverter.GetBytes(sampleRate * 2).CopyTo(bytes, 28);
            BitConverter.GetBytes((short)2).CopyTo(bytes, 32);
            BitConverter.GetBytes((short)16).CopyTo(bytes, 34);
            System.Text.Encoding.ASCII.GetBytes("data").CopyTo(bytes, 36);
            BitConverter.GetBytes(numSamples * 2).CopyTo(bytes, 40);

            var rnd = new Random(12345);

            for (int i = 0; i < numSamples; i++)
            {
                double t = (double)i / sampleRate;

                // 1. Heavy hollow clay jar acoustic body thump (deep punchy physical impact)
                double thumpFreq = 175.0 * Math.Exp(-t * 22.0) + 65.0;
                double thumpEnv = Math.Exp(-t * 18.0);
                double thump = Math.Sin(2.0 * Math.PI * thumpFreq * t) * 0.80 * thumpEnv;

                // 2. Sharp brittle ceramic fracture snap (first 16ms)
                double snap = 0.0;
                if (t < 0.016)
                {
                    double sEnv = 1.0 - t / 0.016;
                    snap = (rnd.NextDouble() - 0.5) * 1.35 * sEnv;
                }

                // 3. Earthenware clay harmonic resonance (hollow pot body ring)
                double ringEnv = Math.Exp(-t * 30.0);
                double ring = (Math.Sin(2.0 * Math.PI * 440.0 * t) * 0.35 +
                               Math.Sin(2.0 * Math.PI * 720.0 * t) * 0.25 +
                               Math.Sin(2.0 * Math.PI * 1150.0 * t) * 0.20) * ringEnv;

                // 4. Shard clatter & miniature ceramic fractures
                double shards = 0.0;
                double[] shardTimes = { 0.020, 0.042, 0.070, 0.105, 0.150, 0.205 };
                double[] shardFreqs = { 2400.0, 3100.0, 1950.0, 2800.0, 3600.0, 2200.0 };
                for (int s = 0; s < shardTimes.Length; s++)
                {
                    if (t >= shardTimes[s])
                    {
                        double dt = t - shardTimes[s];
                        if (dt < 0.035)
                        {
                            double sEnv = Math.Exp(-dt * 110.0);
                            shards += Math.Sin(2.0 * Math.PI * shardFreqs[s] * dt) * 0.22 * sEnv;
                            shards += (rnd.NextDouble() - 0.5) * 0.15 * sEnv;
                        }
                    }
                }

                // 5. Golden coin eruption chime (light sparkling coin jingles in the burst!)
                double coins = 0.0;
                double[] coinTimes = { 0.014, 0.035, 0.060, 0.090 };
                double[] coinFreqs = { 3200.0, 3850.0, 4400.0, 2900.0 };
                for (int c = 0; c < coinTimes.Length; c++)
                {
                    if (t >= coinTimes[c])
                    {
                        double dt = t - coinTimes[c];
                        if (dt < 0.050)
                        {
                            double cEnv = Math.Exp(-dt * 70.0);
                            coins += Math.Sin(2.0 * Math.PI * coinFreqs[c] * dt) * 0.18 * cEnv;
                        }
                    }
                }

                double raw = thump + snap + ring + shards + coins;
                // Warm acoustic soft clipping (no harsh 5-bit crushed distortion)
                double softClipped = Math.Tanh(raw * 0.95);
                short pcmVal = (short)Math.Clamp((int)(softClipped * 29000.0), -32767, 32767);
                BitConverter.GetBytes(pcmVal).CopyTo(bytes, 44 + i * 2);
            }

            return bytes;
        }

        private static byte[] GenerateProcessingGlitchWav()
        {
            int sampleRate = 44100;
            double duration = 1.38; // 1380ms of cyber digital telemetry & glitch stream
            int numSamples = (int)(sampleRate * duration);
            byte[] bytes = new byte[44 + numSamples * 2];

            // RIFF Header
            System.Text.Encoding.ASCII.GetBytes("RIFF").CopyTo(bytes, 0);
            BitConverter.GetBytes(bytes.Length - 8).CopyTo(bytes, 4);
            System.Text.Encoding.ASCII.GetBytes("WAVE").CopyTo(bytes, 8);
            System.Text.Encoding.ASCII.GetBytes("fmt ").CopyTo(bytes, 12);
            BitConverter.GetBytes(16).CopyTo(bytes, 16);
            BitConverter.GetBytes((short)1).CopyTo(bytes, 20); // PCM
            BitConverter.GetBytes((short)1).CopyTo(bytes, 22); // Mono
            BitConverter.GetBytes(sampleRate).CopyTo(bytes, 24);
            BitConverter.GetBytes(sampleRate * 2).CopyTo(bytes, 28);
            BitConverter.GetBytes((short)2).CopyTo(bytes, 32);
            BitConverter.GetBytes((short)16).CopyTo(bytes, 34);
            System.Text.Encoding.ASCII.GetBytes("data").CopyTo(bytes, 36);
            BitConverter.GetBytes(numSamples * 2).CopyTo(bytes, 40);

            var rnd = new Random(4242);
            double[] bleepFreqs = { 2200.0, 2750.0, 1850.0, 3300.0, 2400.0, 2900.0, 3800.0, 1950.0, 2600.0, 3500.0 };

            for (int i = 0; i < numSamples; i++)
            {
                double t = (double)i / sampleRate;

                // 1. Digital carrier buzz (subtle rhythmic 110Hz square wave)
                double carrier = Math.Sign(Math.Sin(2.0 * Math.PI * 110.0 * t)) * 0.07;

                // 2. High-speed packet telemetry chirps (frequency hopping every 52ms)
                int packetIdx = (int)(t / 0.052);
                double fPacket = bleepFreqs[packetIdx % bleepFreqs.Length];
                double packetPhase = (t % 0.052) / 0.052;
                double packetEnv = packetPhase < 0.72 ? Math.Sin(Math.PI * (packetPhase / 0.72)) : 0.0;
                double bleep = Math.Sin(2.0 * Math.PI * fPacket * t) * packetEnv * 0.32;
                double bleepHarmonic = Math.Sign(Math.Sin(2.0 * Math.PI * (fPacket * 1.5) * t)) * packetEnv * 0.10;

                // 3. Fast cryptographic tick clicks (seek pulses every 20ms)
                double tickPhase = (t % 0.020) / 0.020;
                double tick = tickPhase < 0.15 ? (rnd.NextDouble() - 0.5) * 0.40 * (1.0 - tickPhase / 0.15) : 0.0;

                // 4. Glitch static bursts at progress leaps (at ~0.3s, ~0.68s, ~1.05s)
                double glitchBurst = 0.0;
                if ((t >= 0.28 && t <= 0.34) || (t >= 0.65 && t <= 0.72) || (t >= 1.02 && t <= 1.08))
                {
                    glitchBurst = (rnd.NextDouble() - 0.5) * 0.48;
                }

                // 5. High-voltage charging sweep in final 320ms building up to morph slam
                double chargeSweep = 0.0;
                if (t > 1.06)
                {
                    double cProgress = (t - 1.06) / 0.32;
                    double chargeFreq = 900.0 + 3100.0 * (cProgress * cProgress);
                    chargeSweep = Math.Sin(2.0 * Math.PI * chargeFreq * t) * (0.20 + 0.35 * cProgress);
                }

                double raw = carrier + bleep + bleepHarmonic + tick + glitchBurst + chargeSweep;
                double crushed = Math.Tanh(raw * 1.25);

                short pcmVal = (short)Math.Clamp((int)(crushed * 26000.0), -32767, 32767);
                BitConverter.GetBytes(pcmVal).CopyTo(bytes, 44 + i * 2);
            }

            return bytes;
        }
    }
}
