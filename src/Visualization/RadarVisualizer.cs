using System;
using System.Windows.Controls;
using System.Windows.Shapes;

namespace AudioVisualizerOverlay.src.Visualization
{
    /// <summary>
    /// Manages the radar visualization on the canvas. Handles smoothing, positioning,
    /// and rendering of the audio-reactive sound dot based on spatial audio data.
    /// </summary>
    public class RadarVisualizer
    {
        // Visualization Configuration
        private const double SENSITIVITY_MULTIPLIER = 80.0;      // Amplifies audio forces for visibility
        private const double SMOOTHING_FACTOR = 0.15;             // Exponential smoothing (0-1, lower = more smooth)
        private const double NOISE_THRESHOLD = 0.00001;           // LOWERED FOR DEBUG: Minimum loudness to show dot (prevents flickering)
        private const double DEFAULT_OPACITY_ACTIVE = 1.0;       // Dot opacity when audio is detected
        private const double DEFAULT_OPACITY_INACTIVE = 0.0;     // Dot opacity when silent

        // Radar Configuration
        private const double RADAR_CENTER_X = 150;
        private const double RADAR_CENTER_Y = 150;
        private const double RADAR_MAX_RADIUS = 140;

        // Smoothed position values
        private double _smoothedXPosition = 0;
        private double _smoothedYPosition = 0;

        private Ellipse? _soundDotElement;

        // Debug logging
        private bool _hasLoggedDebugInfo = false;
        private int _debugLogCount = 0;

        /// <summary>
        /// Initializes the radar visualizer with the sound dot UI element.
        /// </summary>
        /// <param name="soundDot">The ellipse element representing the sound position on the radar.</param>
        public RadarVisualizer(Ellipse soundDot)
        {
            _soundDotElement = soundDot ?? throw new ArgumentNullException(nameof(soundDot));
        }

        /// <summary>
        /// Updates the radar visualization based on audio spatial data.
        /// Applies smoothing, constrains movement to radar bounds, and updates dot position on canvas.
        /// </summary>
        /// <param name="xForce">Horizontal force from audio (left/right).</param>
        /// <param name="yForce">Vertical force from audio (front/back).</param>
        /// <param name="loudness">Overall loudness/energy level of audio.</param>
        public void UpdateRadarDisplay(float xForce, float yForce, float loudness)
        {
            if (_soundDotElement == null)
            {
                Console.WriteLine("[RadarVisualizer] ERROR: SoundDot element is null!");
                return;
            }

            // Debug logging (first 10 updates)
            if (!_hasLoggedDebugInfo)
            {
                Console.WriteLine($"[RadarVisualizer] Audio Data - X: {xForce:F6}, Y: {yForce:F6}, Loudness: {loudness:F6}");
                _debugLogCount++;
                if (_debugLogCount >= 10)
                    _hasLoggedDebugInfo = true;
            }

            // Hide dot if audio is below noise threshold (prevents flickering from dead air/noise)
            if (loudness < NOISE_THRESHOLD)
            {
                _soundDotElement.Opacity = DEFAULT_OPACITY_INACTIVE;
                return;
            }

            _soundDotElement.Opacity = DEFAULT_OPACITY_ACTIVE;

            // Calculate target position by amplifying audio forces with sensitivity multiplier
            double targetXPosition = xForce * SENSITIVITY_MULTIPLIER;
            double targetYPosition = yForce * SENSITIVITY_MULTIPLIER;

            // Apply exponential smoothing to reduce jittery movement
            // Smoothing interpolates between current and target: smooth = smooth + (target - smooth) * factor
            _smoothedXPosition += (targetXPosition - _smoothedXPosition) * SMOOTHING_FACTOR;
            _smoothedYPosition += (targetYPosition - _smoothedYPosition) * SMOOTHING_FACTOR;

            // Constrain dot movement to stay within radar circle boundary
            ConstrainPositionToRadarBoundary();

            // Update dot visual position on the canvas
            PositionDotOnCanvas();
        }

        /// <summary>
        /// Constrains the smoothed position to stay within the maximum radar radius.
        /// Uses polar coordinates (distance and angle) to maintain circular boundary.
        /// </summary>
        private void ConstrainPositionToRadarBoundary()
        {
            // Calculate distance from radar center using Euclidean distance
            double distanceFromCenter = Math.Sqrt(
                _smoothedXPosition * _smoothedXPosition + 
                _smoothedYPosition * _smoothedYPosition
            );

            // If within bounds, no adjustment needed
            if (distanceFromCenter <= RADAR_MAX_RADIUS)
                return;

            // Position exceeds radius, scale it back proportionally
            // Calculate angle to maintain direction
            double angle = Math.Atan2(_smoothedYPosition, _smoothedXPosition);

            // Set position to boundary while preserving direction
            _smoothedXPosition = Math.Cos(angle) * RADAR_MAX_RADIUS;
            _smoothedYPosition = Math.Sin(angle) * RADAR_MAX_RADIUS;
        }

        /// <summary>
        /// Updates the canvas position of the sound dot based on smoothed coordinates.
        /// Positions dot at center plus offset, then adjusts for dot size to center it.
        /// </summary>
        private void PositionDotOnCanvas()
        {
            if (_soundDotElement == null)
            {
                Console.WriteLine("[RadarVisualizer] ERROR in PositionDotOnCanvas: Element is null!");
                return;
            }

            // Calculate canvas position: center + smoothed offset - (dot size / 2 for centering)
            double canvasLeft = RADAR_CENTER_X + _smoothedXPosition - (_soundDotElement.Width / 2);
            double canvasTop = RADAR_CENTER_Y + _smoothedYPosition - (_soundDotElement.Height / 2);

            Canvas.SetLeft(_soundDotElement, canvasLeft);
            Canvas.SetTop(_soundDotElement, canvasTop);

            // Debug: Log first few positioning updates
            if (_debugLogCount < 3)
            {
                Console.WriteLine($"[RadarVisualizer] Dot positioned - Left: {canvasLeft:F2}, Top: {canvasTop:F2}");
            }
        }

        /// <summary>
        /// Resets the radar visualization to center position (no audio).
        /// </summary>
        public void ResetPosition()
        {
            _smoothedXPosition = 0;
            _smoothedYPosition = 0;
            PositionDotOnCanvas();
        }
    }
}
