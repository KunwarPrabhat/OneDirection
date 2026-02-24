using NAudio.Wave;
using System;

namespace AudioVisualizerOverlay.src.Audio
{
    /// <summary>
    /// Handles audio capture from system loopback and processes spatial audio data.
    /// Extracts positional information (X, Y forces) and loudness from multi-channel audio.
    /// </summary>
    public class AudioProcessor : IDisposable
    {
        // Event fired when new audio data is processed
        public event EventHandler<AudioDataEventArgs>? AudioDataProcessed;

        private WasapiLoopbackCapture? _audioCapture;
        private bool _isRecording = false;
        private int _audioFrameCount = 0;

        /// <summary>
        /// Initializes the audio processor and starts capturing system audio.
        /// </summary>
        /// <exception cref="InvalidOperationException">Thrown if audio capture cannot be started.</exception>
        public void InitializeAudioCapture()
        {
            try
            {
                // Clean up any existing capture session
                StopAudioCapture();

                // Create new loopback capture (captures system audio output)
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

        /// <summary>
        /// Stops the audio capture and releases resources.
        /// </summary>
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

        /// <summary>
        /// Handles raw audio data from the loopback capture.
        /// Processes multi-channel audio to extract spatial positioning and loudness.
        /// </summary>
        private void OnAudioDataAvailable(object? sender, WaveInEventArgs e)
        {
            if (_audioCapture == null || !_isRecording)
                return;

            // Extract spatial audio metrics from the raw audio buffer
            (float xForce, float yForce, float loudness) = ProcessAudioBuffer(e.Buffer, e.BytesRecorded);

            // Debug logging (first 5 frames)
            _audioFrameCount++;
            if (_audioFrameCount <= 5)
            {
                Console.WriteLine($"[AudioProcessor] Frame {_audioFrameCount}: X={xForce:F6}, Y={yForce:F6}, Loudness={loudness:F6}");
            }

            // Trigger event for UI to consume the audio metrics
            AudioDataProcessed?.Invoke(this, new AudioDataEventArgs(xForce, yForce, loudness));
        }

        /// <summary>
        /// Processes raw audio buffer to extract X-axis (left/right), Y-axis (front/back),
        /// and loudness metrics based on multi-channel speaker configuration.
        /// </summary>
        /// <param name="buffer">Raw audio data buffer.</param>
        /// <param name="bytesRecorded">Number of bytes in the buffer.</param>
        /// <returns>Tuple of (xForce, yForce, loudness).</returns>
        private (float xForce, float yForce, float loudness) ProcessAudioBuffer(byte[] buffer, int bytesRecorded)
        {
            var waveBuffer = new WaveBuffer(buffer);
            int channelCount = _audioCapture!.WaveFormat.Channels;

            float aggregatedXForce = 0;
            float aggregatedYForce = 0;
            float aggregatedLoudness = 0;

            // Process audio in frames (each frame contains one sample per channel)
            int sampleCount = bytesRecorded / 4; // 4 bytes per float sample
            int frameCount = sampleCount / channelCount;

            for (int frameIndex = 0; frameIndex < frameCount; frameIndex++)
            {
                // Calculate base position for this frame in the sample buffer
                int bufferPosition = frameIndex * channelCount;

                // Safely extract audio from each channel
                // Standard speaker configuration: FL, FR, FC, LFE, BL, BR, SL, SR
                float frontLeftChannel = ExtractChannelSafely(waveBuffer.FloatBuffer, bufferPosition, 0, channelCount);
                float frontRightChannel = ExtractChannelSafely(waveBuffer.FloatBuffer, bufferPosition, 1, channelCount);
                float frontCenterChannel = ExtractChannelSafely(waveBuffer.FloatBuffer, bufferPosition, 2, channelCount);
                float backLeftChannel = ExtractChannelSafely(waveBuffer.FloatBuffer, bufferPosition, 4, channelCount);
                float backRightChannel = ExtractChannelSafely(waveBuffer.FloatBuffer, bufferPosition, 5, channelCount);
                float sideLeftChannel = ExtractChannelSafely(waveBuffer.FloatBuffer, bufferPosition, 6, channelCount);
                float sideRightChannel = ExtractChannelSafely(waveBuffer.FloatBuffer, bufferPosition, 7, channelCount);

                // Calculate energy groups from speaker positions
                float frontEnergy = frontLeftChannel + frontRightChannel + frontCenterChannel;
                float backEnergy = backLeftChannel + backRightChannel;
                float sideEnergy = sideLeftChannel + sideRightChannel;

                // Y-AXIS: Front vs Back (determines forward/backward direction)
                // Amplified back channel for more pronounced back detection
                float yAxisForce = (backEnergy - frontEnergy) * 2.0f;

                // X-AXIS: Left vs Right with weighted channel emphasis
                // Back and side channels weighted higher for better directional accuracy
                float leftWeightedEnergy = frontLeftChannel + (backLeftChannel * 3.0f) + (sideLeftChannel * 4.0f);
                float rightWeightedEnergy = frontRightChannel + (backRightChannel * 3.0f) + (sideRightChannel * 4.0f);
                float xAxisForce = rightWeightedEnergy - leftWeightedEnergy;

                // Total loudness from all channels
                float totalEnergy = frontEnergy + backEnergy + sideEnergy;

                aggregatedXForce += xAxisForce;
                aggregatedYForce += yAxisForce;
                aggregatedLoudness += totalEnergy;
            }

            // Average the forces and loudness across all frames for stability
            if (frameCount > 0)
            {
                aggregatedXForce /= frameCount;
                aggregatedYForce /= frameCount;
                aggregatedLoudness /= frameCount;
            }

            return (aggregatedXForce, aggregatedYForce, aggregatedLoudness);
        }

        /// <summary>
        /// Safely extracts a channel value from the float buffer, preventing out-of-bounds access.
        /// Returns 0 if the channel doesn't exist in the current audio configuration.
        /// </summary>
        /// <param name="floatBuffer">The audio float buffer.</param>
        /// <param name="baseIndex">Starting index for current frame.</param>
        /// <param name="channelIndex">Target channel index.</param>
        /// <param name="maxChannels">Total available channels.</param>
        /// <returns>Absolute value of the channel sample (0 if channel doesn't exist).</returns>
        private float ExtractChannelSafely(float[] floatBuffer, int baseIndex, int channelIndex, int maxChannels)
        {
            if (channelIndex >= maxChannels)
                return 0;

            int sampleIndex = baseIndex + channelIndex;
            if (sampleIndex >= floatBuffer.Length)
                return 0;

            return Math.Abs(floatBuffer[sampleIndex]);
        }

        /// <summary>
        /// Releases all resources held by the audio processor.
        /// </summary>
        public void Dispose()
        {
            StopAudioCapture();
            GC.SuppressFinalize(this);
        }
    }

    /// <summary>
    /// Event arguments for processed audio data (X position, Y position, loudness).
    /// </summary>
    public class AudioDataEventArgs : EventArgs
    {
        public float XForce { get; }
        public float YForce { get; }
        public float Loudness { get; }

        public AudioDataEventArgs(float xForce, float yForce, float loudness)
        {
            XForce = xForce;
            YForce = yForce;
            Loudness = loudness;
        }
    }
}
