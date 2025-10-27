# Night Cycle & Anomaly Spawn System

A comprehensive Unity system that creates a *Five Nights at Freddy's*-style night cycle with automatic anomaly spawning. Perfect for horror games, survival games, or any project requiring timed event management.

![Unity Version](https://img.shields.io/badge/Unity-2021.3+-blue)
![License](https://img.shields.io/badge/License-MIT-green)

## ✨ Features

- **🕐 FNAF-Style Night Timer**: 4-minute real-time countdown displaying as 0:00 AM → 6:00 AM
- **👻 Automatic Anomaly Spawning**: Time-based anomaly spawning with full Inspector configuration
- **🎮 Modular Design**: Three independent components that work together seamlessly
- **⚙️ Inspector-Friendly**: All settings configurable without touching code
- **🔧 Easy Integration**: Works with existing anomaly systems
- **📊 Debug Tools**: Built-in validation, gizmos, and logging
- **🚀 Quick Setup**: One-click scene setup for rapid prototyping

## 🎯 Quick Start

### Method 1: One-Click Setup (Recommended)
1. Add `NightSystemQuickSetup.cs` to any GameObject
2. Right-click the component → **"Quick Setup Night System Scene"**
3. Assign anomaly prefabs to the spawn entries
4. Press Play!

### Method 2: Manual Setup
1. Create empty GameObject named "Night System"
2. Add `NightSystemController` component
3. Assign TextMeshPro UI element for time display
4. Configure spawn entries in the Inspector
5. Set next scene index in Build Settings

## 📋 System Components

### 🕘 NightTimer.cs
- Manages the night countdown (0:00 AM → 6:00 AM)
- Configurable real-time duration (default: 4 minutes)
- Automatic scene transition at 6:00 AM
- Events for system integration

### 📋 AnomalySpawnList.cs
- Data structure for spawn configurations
- Inspector-editable spawn entries
- Visual gizmos showing spawn points and times
- Built-in validation and organization tools

### 👻 AnomalySpawnManager.cs
- Handles automatic anomaly spawning
- Integrates with existing `Anomaly.cs` script
- Tracks spawned anomalies
- Optional visual spawn effects

### 🎛️ NightSystemController.cs
- Master controller combining all components
- One-stop configuration interface
- Automatic component setup
- System validation and status monitoring

## 🔧 Configuration

### Spawn Entry Setup
Each spawn entry contains:
- **Anomaly Prefab**: GameObject to spawn
- **Spawn Point**: Transform indicating spawn location  
- **Spawn Time**: When to spawn (0.0 = 0:00 AM, 6.0 = 6:00 AM)
- **Entry Name**: Descriptive name for organization

### Example Spawn Schedule
```
Entry 1: Shadow Anomaly at 1:30 AM (Living Room)
Entry 2: Sound Anomaly at 3:00 AM (Hallway)  
Entry 3: Visual Anomaly at 4:45 AM (Kitchen)
Entry 4: Final Boss at 5:30 AM (Player Position)
```

## 🎮 Integration with Existing Anomaly System

The system automatically calls `Respond()` on spawned anomalies:

```csharp
// When spawned, your existing Anomaly.cs script receives:
public void Respond()
{
    // Your existing anomaly behavior
    StartCoroutine(DelayedRespond());
}
```

For custom integration, see `ExampleNightAnomaly.cs` which shows how to:
- Add spawn effects
- Play spawn sounds  
- Implement time-based behavior changes
- Subscribe to night system events

## 📁 File Structure

```
Assets/Scripts/GameLogic/
├── NightTimer.cs                  # ⏰ Timer system
├── AnomalySpawnList.cs           # 📋 Data management
├── AnomalySpawnManager.cs        # 👻 Spawn logic  
├── NightSystemController.cs      # 🎛️ Master controller
├── NightSystemQuickSetup.cs      # 🚀 Setup helper
├── ExampleNightAnomaly.cs        # 💡 Integration example
├── NightSystemDocumentation.md   # 📖 Full documentation
└── Anomaly.cs                    # 🔧 Your existing script
```

## 🛠️ Requirements

- **Unity 2021.3+** (for new Input System support)
- **TextMeshPro** package
- **Scene Management** access for scene transitions

## 🎨 UI Setup

The system creates FNAF-style time display:
- Monospace font recommended
- Green/amber color for authenticity  
- Top-right corner positioning
- Format: "1:30 AM"

## 🐛 Debugging & Validation

### Built-in Tools
- **Validate System Setup**: Context menu option for checking configuration
- **Visual Gizmos**: See spawn points and times in Scene view
- **Debug Logging**: Detailed information about system state
- **Runtime Status**: Monitor active spawns and system health

### Common Issues
- ❌ **No TextMeshPro assigned**: Assign UI text element
- ❌ **Invalid spawn entries**: Check prefab and spawn point assignments  
- ❌ **Scene index out of range**: Add scenes to Build Settings
- ❌ **Anomalies not spawning**: Verify spawn time range (0.0-6.0)

## 🎯 Events System

Subscribe to events for custom behavior:

```csharp
// Night Timer Events
nightTimer.OnTimeChanged += (normalizedTime) => {
    // React to time progression (0.0 to 1.0)
};

nightTimer.OnNightEnded += () => {
    // Handle night completion
};

// Spawn Manager Events  
spawnManager.OnAnomalySpawned += (anomaly, entry) => {
    // React to anomaly spawning
};
```

## 🚀 Performance

- ✅ Minimal performance impact
- ✅ Efficient spawn checking
- ✅ Automatic cleanup of destroyed references
- ✅ Optional visual effects for lower-end devices

## 🎮 Example Game Flow

1. **Night Start**: Timer begins, UI shows 0:00 AM
2. **1:30 AM**: First anomaly spawns at designated location
3. **3:00 AM**: Second anomaly spawns, first may still be active
4. **4:45 AM**: Third anomaly spawns, tension increases
5. **5:30 AM**: Final anomaly spawns for climax
6. **6:00 AM**: Night ends, next scene loads automatically

## 🤝 Contributing

This system is designed to be:
- **Modular**: Each component works independently
- **Extensible**: Easy to add new features  
- **Customizable**: Inspector-driven configuration
- **Integration-Friendly**: Works with existing code

## 📄 License

MIT License - Feel free to use in your projects!

## 🎯 Perfect For

- 🎃 Horror games with timed scares
- 🛡️ Survival games with wave-based enemies  
- 🎮 Arcade games with escalating difficulty
- 🕹️ Any project needing timed event management

---

*Created for Unity Night Cycle & Anomaly Spawn System v1.0*

**Ready to create your own Five Nights at Freddy's experience? Get started with the Quick Setup!** 🚀
