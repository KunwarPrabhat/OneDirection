# Audio Radar Visualizer - Refactored Architecture

## Overview

This project has been refactored from a monolithic codebase into a clean, modular architecture. All business logic has been extracted from `MainWindow.xaml.cs` into dedicated, single-responsibility modules.

## Project Structure

```
OneDirection/
├── src/
│   ├── Audio/
│   │   └── AudioProcessor.cs          # Audio capture and spatial processing
│   ├── Window/
│   │   └── WindowManager.cs           # Window interop, hotkeys, positioning
│   ├── Visualization/
│   │   └── RadarVisualizer.cs        # Radar display and dot positioning
│   └── Utilities/                     # (Future: shared utilities)
├── App.xaml                           # Application root
├── App.xaml.cs                        # Application logic
├── MainWindow.xaml                    # UI Definition
├── MainWindow.xaml.cs                 # Window coordinator (orchestrates modules)
└── OneDirection.csproj               # Project file
```

## Module Documentation

### 1. **AudioProcessor.cs** (`src/Audio/`)
**Responsibility**: Capture system audio and extract spatial positioning data

**Key Features**:
- Captures system loopback audio using NAudio
- Processes multi-channel audio (up to 8 channels)
- Extracts X-axis (left/right) and Y-axis (front/back) forces
- Calculates overall loudness/energy
- Thread-safe event-based communication

**Public Methods**:
- `InitializeAudioCapture()` - Start audio capture
- `StopAudioCapture()` - Stop and cleanup audio resources

**Events**:
- `AudioDataProcessed` - Fired with processed audio metrics (X, Y, loudness)

**How It Works**:
1. Opens loopback capture for system audio output
2. Processes incoming audio frames (typical: 1920 samples/frame)
3. For each frame:
   - Extracts channels: FL, FR, FC, BL, BR, SL, SR
   - Groups by position (front, back, side)
   - Calculates forces with weighted emphasis on back/side speakers
   - Computes average energy across all samples
4. Fires event with calculated forces

### 2. **WindowManager.cs** (`src/Window/`)
**Responsibility**: Manage window interoperability, hotkeys, and positioning

**Key Features**:
- Makes window click-through (transparent to mouse)
- Positions overlay in top-right corner of screen
- Registers global hotkey (Ctrl + Shift + X) for shutdown
- Handles Win32 API interactions safely

**Public Methods**:
- `MakeWindowClickThrough()` - Enable click-through mode
- `PositionWindowTopRight()` - Position window top-right with margin
- `RegisterGlobalShutdownHotkey()` - Register Ctrl+Shift+X hotkey
- `AttachHotKeyMessageHook()` - Attach message handler for hotkey events
- `Cleanup()` - Unregister hotkey on shutdown

**Win32 APIs Used**:
- `RegisterHotKey()` - Register global system hotkey
- `UnregisterHotKey()` - Unregister hotkey
- `GetWindowLong()` / `SetWindowLong()` - Get/set window extended styles
- `WM_HOTKEY` message - Detect hotkey presses

### 3. **RadarVisualizer.cs** (`src/Visualization/`)
**Responsibility**: Render real-time audio position on radar display

**Key Features**:
- Applies exponential smoothing to reduce jitter
- Constrains movement within circular radar boundary
- Noise threshold to prevent flickering from silence
- Configurable sensitivity and smoothing factors

**Public Methods**:
- `UpdateRadarDisplay(xForce, yForce, loudness)` - Update dot position
- `ResetPosition()` - Reset to center

**Algorithm**:
1. Check loudness against threshold (hide if silent)
2. Calculate target position: `target = force * sensitivity`
3. Apply smoothing: `smooth += (target - smooth) * smoothingFactor`
4. Constrain to circular boundary using polar coordinates
5. Update canvas position, centering dot on coordinate

**Configuration Constants**:
- `SENSITIVITY_MULTIPLIER`: 80.0 (amplifies audio forces for visibility)
- `SMOOTHING_FACTOR`: 0.15 (0 = no smoothing, 1 = instant)
- `NOISE_THRESHOLD`: 0.0001 (minimum loudness before showing)
- `RADAR_MAX_RADIUS`: 140 pixels

## Data Flow Architecture

```
System Audio Output
        ↓
[AudioProcessor] 
    Captures & Processes
        ↓ (AudioDataEventArgs: X, Y, Loudness)
[MainWindow - Event Handler]
    Coordinates updates
        ↓
[RadarVisualizer]
    Applies smoothing & constraints
        ↓
[Canvas Element - SoundDot]
    Visual output
```

## MainWindow.xaml.cs Role

The refactored `MainWindow.xaml.cs` now serves as a **coordinator** that:
1. Initializes all modules on window load
2. Connects event handlers between modules
3. Manages lifecycle (construction, event subscriptions, cleanup)
4. Handles marshaling between threads (audio callbacks → UI thread)

**Key Methods**:
- `InitializeWindowManagement()` - Setup window properties
- `InitializeAudioProcessing()` - Start audio, subscribe to events
- `InitializeVisualization()` - Setup radar display
- `OnAudioDataProcessed()` - Event that updates visualization
- `OnClosed()` - Cleanup on shutdown

## Benefits of This Architecture

✅ **Single Responsibility**: Each class has one clear purpose  
✅ **Testability**: Modules can be unit tested independently  
✅ **Reusability**: Components can be used in other projects  
✅ **Maintainability**: Changes to audio logic don't affect window management  
✅ **Readability**: Self-documenting code with clear separation  
✅ **Extensibility**: Easy to add new features (recording, settings, etc.)  

## Adding New Features

### Example: Adding a Volume Bar
1. Create `src/Visualization/VolumeBar.cs`
2. Subscribe to `AudioProcessor.AudioDataProcessed` event
3. Update UI element based on loudness value

### Example: Audio Format Switching
1. Extend `AudioProcessor` with method to select different audio devices
2. Call from settings UI or add hotkey in `WindowManager`

## Code Quality Standards

✅ **Comments**: Comprehensive XML documentation on all public members  
✅ **Naming**: Clear, descriptive variable/method/class names  
✅ **Error Handling**: Try-catch blocks with proper exception messages  
✅ **Resource Management**: Proper use of `IDisposable` pattern  
✅ **Thread Safety**: UI updates marshaled to Dispatcher  
✅ **Magic Numbers**: Named constants for configuration values  

## Dependencies

- **NAudio 2.2.1**: Audio capture and processing
- **.NET 8.0 Windows**: WPF for UI, Windows API interop

## Configuration

Edit constants in each module to customize behavior:

**Audio Processing** (`AudioProcessor.cs`):
- Channel extraction logic
- Energy grouping weights

**Window Management** (`WindowManager.cs`):
- Hotkey combination (currently Ctrl+Shift+X)
- Window positioning

**Visualization** (`RadarVisualizer.cs`):
- Sensitivity multiplier
- Smoothing factor
- Noise threshold
- Radar radius

---

This refactored codebase is now ready for team development, testing, and feature expansion!
