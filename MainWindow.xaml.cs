using System.Windows;
using AudioVisualizerOverlay.src.Audio;
using AudioVisualizerOverlay.src.Window;
using AudioVisualizerOverlay.src.Visualization;

namespace AudioVisualizerOverlay
{
    /// <summary>
    /// Main application window for the Audio Radar Visualizer overlay.
    /// 
    /// ARCHITECTURE OVERVIEW:
    /// - AudioProcessor: Captures system audio and extracts spatial positioning data
    /// - WindowManager: Handles window hover-through, positioning, and global hotkeys
    /// - RadarVisualizer: Renders real-time audio position on radar display
    /// 
    /// WORKFLOW:
    /// 1. Window loads and initializes all components
    /// 2. Audio capture starts, feeding spatial data to processor
    /// 3. Processor calculates X/Y forces and loudness from multi-channel audio
    /// 4. Visualizer updates radar display in real-time
    /// 5. Global hotkey (Ctrl+Shift+X) allows quick exit
    /// </summary>
    public partial class MainWindow : Window
    {
        // Core components
        private AudioProcessor? _audioProcessor;
        private WindowManager? _windowManager;
        private RadarVisualizer? _radarVisualizer;
        
        private int _maxBalls;
        private bool _enableGunFilter;

        public MainWindow(int maxBalls = 1, bool enableGunFilter = false)
        {
            _maxBalls = maxBalls;
            _enableGunFilter = enableGunFilter;
            InitializeComponent();
        }

        /// <summary>
        /// Window loaded event - initializes all components and starts audio capture.
        /// Called after XAML is loaded and before window is shown.
        /// </summary>
        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            try
            {
                Console.WriteLine("[MainWindow] ===== INITIALIZATION START =====");
                InitializeWindowManagement();
                InitializeAudioProcessing();
                InitializeVisualization();
                Console.WriteLine("[MainWindow] ===== INITIALIZATION COMPLETE =====");
                Console.WriteLine("[MainWindow] App is ready. Play audio and check if red dot appears.");
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Initialization Error: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                Close();
            }
        }

        /// <summary>
        /// Initializes window properties: makes it click-through, positions it correctly,
        /// and sets up global hotkey for shutdown.
        /// </summary>
        private void InitializeWindowManagement()
        {
            Console.WriteLine("[MainWindow] Initializing window management...");
            _windowManager = new WindowManager(this);

            // Make window transparent to mouse clicks (doesn't interfere with gameplay)
            _windowManager.MakeWindowClickThrough();

            // Position in top-right corner of screen
            _windowManager.PositionWindowTopRight();

            // Register Ctrl+Shift+X hotkey for application exit
            if (_windowManager.RegisterGlobalShutdownHotkey())
            {
                _windowManager.AttachHotKeyMessageHook();
            }
            Console.WriteLine("[MainWindow] Window management initialized.");
        }

        /// <summary>
        /// Initializes audio capture from system loopback (speaker output).
        /// Subscribes to audio data events for radar updates.
        /// </summary>
        private void InitializeAudioProcessing()
        {
            _audioProcessor = new AudioProcessor(_enableGunFilter);

            // Subscribe to audio data events
            _audioProcessor.AudioDataProcessed += OnAudioDataProcessed;

            // Start capturing system audio
            try
            {
                _audioProcessor.InitializeAudioCapture();
                Console.WriteLine("[MainWindow] Audio processor initialized successfully");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[MainWindow] Audio initialization error: {ex.Message}");
                MessageBox.Show($"Audio Error: {ex.Message}\n\nMake sure you have audio playing from your system speakers.", "Audio Capture Failed", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }

        /// <summary>
        /// Initializes the radar visualization with the canvas dot element.
        /// </summary>
        private void InitializeVisualization()
        {
            if (RadarCanvas == null)
            {
                Console.WriteLine("[MainWindow] ERROR: RadarCanvas element not found in XAML!");
                MessageBox.Show("Critical Error: RadarCanvas element not found in XAML.", "Fatal Error", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            _radarVisualizer = new RadarVisualizer(RadarCanvas, _maxBalls);
            Console.WriteLine("[MainWindow] Visualization initialized successfully");
        }

        /// <summary>
        /// Handles audio data events from the processor.
        /// Updates the radar visualization with new audio position data.
        /// </summary>
        /// <param name="sender">Audio processor raising the event.</param>
        /// <param name="e">Audio data (X position, Y position, loudness).</param>
        private void OnAudioDataProcessed(object? sender, AudioDataEventArgs e)
        {
            // Update radar display on UI thread
            Dispatcher.Invoke(() =>
            {
                if (_radarVisualizer == null)
                {
                    Console.WriteLine("[MainWindow] WARNING: Visualizer is null!");
                    return;
                }

                _radarVisualizer.UpdateRadarDisplay(e.Sources);
            });
        }

        /// <summary>
        /// Window closed event - cleans up resources including audio capture and hotkey.
        /// </summary>
        protected override void OnClosed(EventArgs e)
        {
            // Clean up audio processor
            _audioProcessor?.Dispose();

            // Unregister global hotkey
            _windowManager?.Cleanup();

            base.OnClosed(e);
        }
    }
}