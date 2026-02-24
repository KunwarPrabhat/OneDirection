using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Effects;
using System.Windows.Shapes;
using AudioVisualizerOverlay.src.Audio; // for AudioSource

namespace AudioVisualizerOverlay.src.Visualization
{
    public class RadarVisualizer
    {
        private const double SENSITIVITY_MULTIPLIER = 80.0;
        private const double SMOOTHING_FACTOR = 0.15;
        private const double NOISE_THRESHOLD = 0.00001;
        private const double DEFAULT_OPACITY_ACTIVE = 0.9;
        private const double DEFAULT_OPACITY_INACTIVE = 0.0;

        private const double RADAR_CENTER_X = 150;
        private const double RADAR_CENTER_Y = 150;
        private const double RADAR_MAX_RADIUS = 140;

        private class RadarDot
        {
            public Ellipse Element { get; set; } = new Ellipse();
            public double SmoothedXPosition { get; set; }
            public double SmoothedYPosition { get; set; }
        }

        private List<RadarDot> _dots;
        private int _maxBalls;
        private Canvas _radarCanvas;

        public RadarVisualizer(Canvas radarCanvas, int maxBalls)
        {
            _radarCanvas = radarCanvas ?? throw new ArgumentNullException(nameof(radarCanvas));
            _maxBalls = 1; // Force to 1 single ball
            _dots = new List<RadarDot>();

            var element = new Ellipse
            {
                Width = 24,
                Height = 24,
                Fill = new SolidColorBrush(Colors.Red),
                Opacity = DEFAULT_OPACITY_INACTIVE,
                Effect = new DropShadowEffect
                {
                    BlurRadius = 15,
                    ShadowDepth = 0,
                    Color = Colors.Red
                }
            };

            _radarCanvas.Children.Add(element);
            _dots.Add(new RadarDot { Element = element });
        }

        public void UpdateRadarDisplay(IEnumerable<AudioSource> sources)
        {
            var topSource = sources.OrderByDescending(s => s.Loudness).FirstOrDefault();

            if (topSource != null)
            {
                UpdateDot(_dots[0], topSource.XForce, topSource.YForce, topSource.Loudness);
            }
            else
            {
                _dots[0].Element.Opacity = DEFAULT_OPACITY_INACTIVE;
            }
        }

        private void UpdateDot(RadarDot dot, float xForce, float yForce, float loudness)
        {
            if (loudness < NOISE_THRESHOLD)
            {
                dot.Element.Opacity = DEFAULT_OPACITY_INACTIVE;
                return;
            }

            dot.Element.Opacity = DEFAULT_OPACITY_ACTIVE;

            double targetXPosition = xForce * SENSITIVITY_MULTIPLIER;
            double targetYPosition = yForce * SENSITIVITY_MULTIPLIER;

            dot.SmoothedXPosition += (targetXPosition - dot.SmoothedXPosition) * SMOOTHING_FACTOR;
            dot.SmoothedYPosition += (targetYPosition - dot.SmoothedYPosition) * SMOOTHING_FACTOR;

            double distanceFromCenter = Math.Sqrt(
                dot.SmoothedXPosition * dot.SmoothedXPosition + 
                dot.SmoothedYPosition * dot.SmoothedYPosition
            );

            if (distanceFromCenter > RADAR_MAX_RADIUS)
            {
                double angle = Math.Atan2(dot.SmoothedYPosition, dot.SmoothedXPosition);
                dot.SmoothedXPosition = Math.Cos(angle) * RADAR_MAX_RADIUS;
                dot.SmoothedYPosition = Math.Sin(angle) * RADAR_MAX_RADIUS;
            }

            double canvasLeft = RADAR_CENTER_X + dot.SmoothedXPosition - (dot.Element.Width / 2);
            double canvasTop = RADAR_CENTER_Y + dot.SmoothedYPosition - (dot.Element.Height / 2);

            Canvas.SetLeft(dot.Element, canvasLeft);
            Canvas.SetTop(dot.Element, canvasTop);
        }

        public void ResetPosition()
        {
            foreach (var dot in _dots)
            {
                dot.SmoothedXPosition = 0;
                dot.SmoothedYPosition = 0;
                dot.Element.Opacity = DEFAULT_OPACITY_INACTIVE;
            }
        }
    }
}
