using NAudio.Wave;
using NAudio.Dsp;
using System;
using System.Collections.Generic;

namespace AudioVisualizerOverlay.src.Audio
{
    public class AudioSource
    {
        public int Id { get; set; }
        public float XForce { get; set; }
        public float YForce { get; set; }
        public float Loudness { get; set; }
    }

    public class AudioDataEventArgs : EventArgs
    {
        public IEnumerable<AudioSource> Sources { get; }

        public AudioDataEventArgs(IEnumerable<AudioSource> sources)
        {
            Sources = sources;
        }
    }

    public class AudioProcessor : IDisposable
    {
        public event EventHandler<AudioDataEventArgs>? AudioDataProcessed;

        private WasapiLoopbackCapture? _audioCapture;
        private bool _isRecording = false;
        private int _audioFrameCount = 0;
        private bool _enableGunFilter;

        public AudioProcessor(bool enableGunFilter = false)
        {
            _enableGunFilter = enableGunFilter;
        }

        public void InitializeAudioCapture()
        {
            try
            {
                StopAudioCapture();

                _audioCapture = new WasapiLoopbackCapture();
                _audioCapture.DataAvailable += OnAudioDataAvailable;
                _audioCapture.StartRecording();
                _isRecording = true;

                Console.WriteLine($"Audio capture started. Channels detected: {_audioCapture.WaveFormat.Channels}");
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException("Failed to initialize audio capture.", ex);
            }
        }

        public void StopAudioCapture()
        {
            if (_audioCapture != null)
            {
                _audioCapture.StopRecording();
                _audioCapture.Dispose();
                _audioCapture = null;
                _isRecording = false;
            }
        }

        private void OnAudioDataAvailable(object? sender, WaveInEventArgs e)
        {
            if (_audioCapture == null || !_isRecording)
                return;

            IEnumerable<AudioSource> sources = ProcessAudioBuffer(e.Buffer, e.BytesRecorded);

            _audioFrameCount++;
            if (_audioFrameCount <= 5)
            {
                Console.WriteLine($"[AudioProcessor] Frame {_audioFrameCount} processed.");
            }

            AudioDataProcessed?.Invoke(this, new AudioDataEventArgs(sources));
        }

        private IEnumerable<AudioSource> ProcessAudioBuffer(byte[] buffer, int bytesRecorded)
        {
            var waveBuffer = new WaveBuffer(buffer);
            int channelCount = _audioCapture!.WaveFormat.Channels;
            int sampleRate = _audioCapture.WaveFormat.SampleRate;

            int sampleCount = bytesRecorded / 4;
            int frameCount = sampleCount / channelCount;

            if (_enableGunFilter && frameCount > 0)
            {
                if (!IsGunshot(waveBuffer.FloatBuffer, 0, frameCount, channelCount, sampleRate))
                {
                    return new List<AudioSource>(); // Filtered out
                }
            }

            float avgFL = 0, avgFR = 0, avgFC = 0, avgBL = 0, avgBR = 0, avgSL = 0, avgSR = 0;

            for (int frameIndex = 0; frameIndex < frameCount; frameIndex++)
            {
                int bufferPosition = frameIndex * channelCount;

                avgFL += ExtractChannelSafely(waveBuffer.FloatBuffer, bufferPosition, 0, channelCount);
                avgFR += ExtractChannelSafely(waveBuffer.FloatBuffer, bufferPosition, 1, channelCount);
                avgFC += ExtractChannelSafely(waveBuffer.FloatBuffer, bufferPosition, 2, channelCount);
                avgBL += ExtractChannelSafely(waveBuffer.FloatBuffer, bufferPosition, 4, channelCount);
                avgBR += ExtractChannelSafely(waveBuffer.FloatBuffer, bufferPosition, 5, channelCount);
                avgSL += ExtractChannelSafely(waveBuffer.FloatBuffer, bufferPosition, 6, channelCount);
                avgSR += ExtractChannelSafely(waveBuffer.FloatBuffer, bufferPosition, 7, channelCount);
            }

            if (frameCount > 0)
            {
                avgFL /= frameCount; avgFR /= frameCount; avgFC /= frameCount;
                avgBL /= frameCount; avgBR /= frameCount;
                avgSL /= frameCount; avgSR /= frameCount;
            }

            // 1. Store channels in radial order (clockwise)
            // 0: FC, 1: FR, 2: SR, 3: BR, 4: BL, 5: SL, 6: FL
            float[] E = new float[7] { avgFC, avgFR, avgSR, avgBR, avgBL, avgSL, avgFL };

            // 2. Define unit vectors for each speaker direction (Y is up/negative)
            (float x, float y)[] V = new (float, float)[7] {
                (0f, -1f),          // 0: Front Center
                (0.707f, -0.707f),  // 1: Front Right
                (1f, 0f),           // 2: Side Right
                (0.707f, 0.707f),   // 3: Back Right
                (-0.707f, 0.707f),  // 4: Back Left
                (-1f, 0f),          // 5: Side Left
                (-0.707f, -0.707f)  // 6: Front Left
            };

            var sources = new List<AudioSource>();
            float sBoost = 12.0f; // Adjusted sensitivity multiplier
            float noiseThreshold = 0.00002f;

            // 3. Find local peaks and calculate exact interpolated vector
            for (int i = 0; i < 7; i++)
            {
                int prev = (i + 6) % 7;
                int next = (i + 1) % 7;

                // Local peak detection (strictly greater on one side prevents double-counting equal adjacent channels)
                if (E[i] > noiseThreshold && E[i] >= E[prev] && E[i] > E[next])
                {
                    // Weighted vector sum of the peak and its immediate neighbors
                    float vecX = (E[prev] * V[prev].x + E[i] * V[i].x + E[next] * V[next].x);
                    float vecY = (E[prev] * V[prev].y + E[i] * V[i].y + E[next] * V[next].y);

                    // Normalize the interpolated direction
                    float mag = (float)Math.Sqrt(vecX * vecX + vecY * vecY);
                    if (mag > 0)
                    {
                        vecX /= mag;
                        vecY /= mag;
                    }
                    else
                    {
                        vecX = V[i].x;
                        vecY = V[i].y;
                    }

                    // Apply magnitude (loudness)
                    float forceMag = E[i] * sBoost;

                    // Boost back channels slightly if they are naturally quieter
                    if (i == 3 || i == 4) 
                    {
                        forceMag *= 1.5f;
                    }

                    sources.Add(new AudioSource { 
                        Id = i, 
                        XForce = vecX * forceMag, 
                        YForce = vecY * forceMag, 
                        Loudness = E[i] 
                    });
                }
            }

            return sources;
        }

        private float ExtractChannelSafely(float[] floatBuffer, int baseIndex, int channelIndex, int maxChannels)
        {
            if (channelIndex >= maxChannels)
                return 0;

            int sampleIndex = baseIndex + channelIndex;
            if (sampleIndex >= floatBuffer.Length)
                return 0;

            return Math.Abs(floatBuffer[sampleIndex]);
        }

        private bool IsGunshot(float[] buffer, int start, int frameCount, int channelCount, int sampleRate)
        {
            int fftLength = 512;
            int m = (int)Math.Log(fftLength, 2.0);
            var complexBuffer = new Complex[fftLength];
            
            for (int i = 0; i < fftLength; i++)
            {
                if (i < frameCount && (start + i * channelCount) < buffer.Length)
                {
                    // Mix front left and right for analysis
                    complexBuffer[i].X = buffer[start + i * channelCount]; 
                }
                else
                {
                    complexBuffer[i].X = 0;
                }
                complexBuffer[i].Y = 0;
            }

            // Apply FFT
            FastFourierTransform.FFT(true, m, complexBuffer);

            float lowEnergy = 0;
            float midEnergy = 0; // 1kHz-4kHz is typical transient gun crack

            for (int i = 1; i < fftLength / 2; i++)
            {
                double freq = (i * sampleRate) / (double)fftLength;
                float mag = (float)Math.Sqrt(complexBuffer[i].X * complexBuffer[i].X + complexBuffer[i].Y * complexBuffer[i].Y);

                if (freq < 1000)
                    lowEnergy += mag;
                else if (freq <= 4000)
                    midEnergy += mag;
            }

            // Gunshots have significant transient hit in mid frequencies
            // Wind / rumble is mostly lowEnergy
            return midEnergy > (lowEnergy * 0.15f);
        }

        public void Dispose()
        {
            StopAudioCapture();
            GC.SuppressFinalize(this);
        }
    }
}
