using System.Windows;
using AudioVisualizerOverlay;

namespace AudioVisualizerOverlay.src.UI
{
    public partial class SettingsWindow : System.Windows.Window
    {
        private MainWindow? _overlayWindow;

        public SettingsWindow()
        {
            InitializeComponent();
        }

        private void LaunchButton_Click(object sender, RoutedEventArgs e)
        {
            if (_overlayWindow == null || !_overlayWindow.IsLoaded)
            {
                int maxBalls = (int)MaxBallsSlider.Value;
                bool enableFilter = GunFilterCheckBox.IsChecked ?? false;

                _overlayWindow = new MainWindow(maxBalls, enableFilter);
                _overlayWindow.Closed += (s, args) => _overlayWindow = null;
                _overlayWindow.Show();
                
                LaunchButton.Content = "Close Overlay";
            }
            else
            {
                _overlayWindow.Close();
                LaunchButton.Content = "Launch Overlay";
            }
        }
    }
}
