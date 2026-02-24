using System;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;

namespace AudioVisualizerOverlay.src.Window
{
    /// <summary>
    /// Manages window interoperability features including global hotkeys, click-through transparency,
    /// and window positioning. Uses Win32 APIs for advanced window manipulation.
    /// </summary>
    public class WindowManager
    {
        // Win32 API Constants
        private const int HOTKEY_ID = 9000;
        private const uint MOD_CONTROL = 0x0002;     // Ctrl key modifier
        private const uint MOD_SHIFT = 0x0004;       // Shift key modifier
        private const uint VK_X = 0x58;              // 'X' virtual key code

        private const int WS_EX_TRANSPARENT = 0x00000020;  // Window style: click-through
        private const int GWL_EXSTYLE = -20;                // Get/Set window extended style

        // Win32 API Imports for window manipulation
        [DllImport("user32.dll")]
        private static extern bool RegisterHotKey(IntPtr hWnd, int id, uint fsModifiers, uint vk);

        [DllImport("user32.dll")]
        private static extern bool UnregisterHotKey(IntPtr hWnd, int id);

        [DllImport("user32.dll")]
        public static extern int GetWindowLong(IntPtr hwnd, int index);

        [DllImport("user32.dll")]
        public static extern int SetWindowLong(IntPtr hwnd, int index, int newStyle);

        private IntPtr _windowHandle;
        private System.Windows.Window? _window;

        /// <summary>
        /// Initializes the window manager with a reference to the main window.
        /// </summary>
        /// <param name="mainWindow">The WPF window to manage.</param>
        public WindowManager(System.Windows.Window mainWindow)
        {
            _window = mainWindow;
            _windowHandle = new WindowInteropHelper(mainWindow).Handle;
        }

        /// <summary>
        /// Makes the window click-through (transparent to mouse clicks) so it doesn't interfere with gameplay.
        /// </summary>
        public void MakeWindowClickThrough()
        {
            int currentStyle = GetWindowLong(_windowHandle, GWL_EXSTYLE);
            SetWindowLong(_windowHandle, GWL_EXSTYLE, currentStyle | WS_EX_TRANSPARENT);
        }

        /// <summary>
        /// Positions the window in the top-right corner of the primary screen with a small margin.
        /// </summary>
        public void PositionWindowTopRight()
        {
            if (_window == null)
                return;

            double screenWidth = SystemParameters.PrimaryScreenWidth;
            const double rightMargin = 20;
            const double topMargin = 20;

            _window.Left = screenWidth - _window.Width - rightMargin;
            _window.Top = topMargin;
        }

        /// <summary>
        /// Registers a global hotkey that triggers application shutdown.
        /// Hotkey: Ctrl + Shift + X
        /// </summary>
        /// <returns>True if hotkey registration was successful.</returns>
        public bool RegisterGlobalShutdownHotkey()
        {
            try
            {
                bool result = RegisterHotKey(_windowHandle, HOTKEY_ID, MOD_CONTROL | MOD_SHIFT, VK_X);
                if (result)
                {
                    Console.WriteLine("Global hotkey registered: Ctrl + Shift + X");
                }
                return result;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Failed to register hotkey: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Sets up message hook to listen for global hotkey events.
        /// Must be called after RegisterGlobalShutdownHotkey().
        /// </summary>
        public void AttachHotKeyMessageHook()
        {
            HwndSource? source = HwndSource.FromHwnd(_windowHandle);
            if (source != null)
            {
                source.AddHook(HandleWindowMessage);
            }
        }

        /// <summary>
        /// Handles window messages including hotkey activation.
        /// Triggers application shutdown when the registered hotkey is pressed.
        /// </summary>
        private IntPtr HandleWindowMessage(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
        {
            const int WM_HOTKEY = 0x0312;

            // Check if this is a hotkey message and it matches our registered hotkey ID
            if (msg == WM_HOTKEY && wParam.ToInt32() == HOTKEY_ID)
            {
                Application.Current.Shutdown();
                handled = true;
            }

            return IntPtr.Zero;
        }

        /// <summary>
        /// Unregisters the global hotkey and cleans up window resources.
        /// Call this when the application is closing.
        /// </summary>
        public void Cleanup()
        {
            try
            {
                UnregisterHotKey(_windowHandle, HOTKEY_ID);
                Console.WriteLine("Global hotkey unregistered.");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error during hotkey cleanup: {ex.Message}");
            }
        }
    }
}
