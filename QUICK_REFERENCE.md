## Quick Reference: New Code Structure

### File Locations

| Component | File | Purpose |
|-----------|------|---------|
| Audio Processing | `src/Audio/AudioProcessor.cs` | System audio capture & spatial data extraction |
| Window Management | `src/Window/WindowManager.cs` | Window interop, hotkeys, positioning |
| Visualization | `src/Visualization/RadarVisualizer.cs` | Radar display rendering & dot positioning |
| Main Coordinator | `MainWindow.xaml.cs` | Orchestrates all modules |
| UI Definition | `MainWindow.xaml` | XAML layout (unchanged) |

### Key Classes & Methods

#### AudioProcessor
```csharp
// Initialization
audioProcessor.InitializeAudioCapture();      // Start capture
audioProcessor.StopAudioCapture();            // Stop capture
audioProcessor.Dispose();                     // Cleanup

// Event (subscribe to this)
public event EventHandler<AudioDataEventArgs> AudioDataProcessed;

// AudioDataEventArgs properties
e.XForce     // Left/Right force (-1 to 1)
e.YForce     // Front/Back force (-1 to 1)
e.Loudness   // Overall energy (0 to ~1)
```

#### WindowManager
```csharp
// Setup
windowManager.MakeWindowClickThrough();              // Make transparent to clicks
windowManager.PositionWindowTopRight();              // Position overlay
windowManager.RegisterGlobalShutdownHotkey();        // Register Ctrl+Shift+X
windowManager.AttachHotKeyMessageHook();             // Activate hotkey listener
windowManager.Cleanup();                             // Cleanup on exit
```

#### RadarVisualizer
```csharp
// Update visualization
visualizer.UpdateRadarDisplay(xForce, yForce, loudness);  // Main update method
visualizer.ResetPosition();                                // Reset to center

// Configuration (edit constants in class)
SENSITIVITY_MULTIPLIER = 80.0      // Adjust dot movement range
SMOOTHING_FACTOR = 0.15            // Adjust smoothness (0 = none, 1 = max)
NOISE_THRESHOLD = 0.0001           // Minimum loudness to show dot
RADAR_MAX_RADIUS = 140             // Maximum dot distance from center
```

### Common Tasks

#### Change Hotkey
1. Open `src/Window/WindowManager.cs`
2. Modify constants (MOD_CONTROL, MOD_SHIFT, VK_X)
3. Hotkey IDs: https://docs.microsoft.com/en-us/windows/win32/inputdev/virtual-key-codes

#### Adjust Radar Sensitivity
1. Open `src/Visualization/RadarVisualizer.cs`
2. Change `SENSITIVITY_MULTIPLIER` (higher = more movement)

#### Increase Smoothing
1. Open `src/Visualization/RadarVisualizer.cs`
2. Increase `SMOOTHING_FACTOR` closer to 1.0 (0.15 → 0.25)

#### Reduce Noise Flickering
1. Open `src/Visualization/RadarVisualizer.cs`
2. Increase `NOISE_THRESHOLD` (0.0001 → 0.001)

#### Modify Audio Channel Weights
1. Open `src/Audio/AudioProcessor.cs`
2. Find method `ProcessAudioBuffer()`
3. Modify channel weights (currently: front=1x, back=3x, side=4x)

### Thread Safety Notes

⚠️ **Important**: Audio callbacks arrive on separate thread
- `AudioProcessor` handles this with `Dispatcher.Invoke()`
- UI updates automatically marshaled to main thread
- Safe to update UI elements in `OnAudioDataProcessed()` event handler

### Adding Debug Logging

Add to any method for debugging:
```csharp
Console.WriteLine($"[AudioProcessor] Detected {_audioCapture.WaveFormat.Channels} channels");
```

Output visible in Visual Studio Debug console or app console window.

### Error Handling

All modules have try-catch blocks. Check:
- `AudioProcessor.InitializeAudioCapture()` - May fail if no audio device
- `WindowManager.RegisterGlobalShutdownHotkey()` - May fail if hotkey in use
- `OnClosed()` - Cleanup called regardlessly

## Visual Architecture Diagram

```
┌─────────────────────────────────────────────────┐
│         MainWindow.xaml (UI Layer)              │
│  ┌─────────────────────────────────────────┐   │
│  │  Canvas with SoundDot (Circle Element)  │   │
│  └──────────────────┬──────────────────────┘   │
└─────────────────────┼──────────────────────────┘
                      │
        Updated by RadarVisualizer
                      │
     ┌────────────────┴─────────────────┐
     │                                  │
┌────▼────────────────┐    ┌───────────▼──────────┐
│ RadarVisualizer     │    │ WindowManager        │
│ ─────────────────   │    │ ──────────────────   │
│ • Smoothing         │    │ • Click-through      │
│ • Constraints       │    │ • Position overlay   │
│ • Dot positioning   │    │ • Global hotkeys     │
└────────────────────┘    └─────────────────────┘
        ▲
        │ Receives AudioDataEventArgs
        │
┌───────┴──────────────────┐
│  AudioProcessor          │
│  ──────────────────      │
│  • Audio capture         │
│  • Spatial processing    │
│  • Channel extraction    │
│  • Force calculation     │
└──────────────────────────┘
        ▲
        │
   System Audio
```

---

For complete details, see **REFACTORING_GUIDE.md**
