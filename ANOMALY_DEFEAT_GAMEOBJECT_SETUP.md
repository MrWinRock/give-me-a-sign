# Anomaly Defeat GameObject Activation Setup Guide

## Overview
This guide explains how to set up GameObject activation in SampleScene when the player loses to an anomaly.

## Components Added

### 1. SampleSceneManager.cs
- **Purpose**: Manages GameObject activation in SampleScene based on game results
- **Location**: `Assets/Scripts/Score/SampleSceneManager.cs`
- **Functionality**: 
  - Activates specific GameObjects when player is defeated by anomaly
  - Deactivates objects for normal game results
  - Provides testing methods via context menu

### 2. Enhanced ResultDisplay.cs
- **Purpose**: Handles GameObject activation in result scenes
- **Functionality**:
  - Activates/deactivates GameObjects based on defeat type
  - Shows special UI for anomaly defeats
  - Provides testing methods

## Setup Instructions

### For SampleScene:

1. **Add SampleSceneManager Component:**
   - Open SampleScene in Unity
   - Create an empty GameObject and name it "SceneManager"
   - Add the `SampleSceneManager` component to it

2. **Configure GameObjects:**
   - **Anomaly Defeat Objects**: Drag GameObjects that should appear when player loses to anomaly
     - Examples: Special defeat screens, anomaly victory animations, dark overlays
   - **Normal Result Objects**: Drag GameObjects that should appear for normal win/lose
     - Examples: Standard UI elements, normal game result displays

3. **Setup GameObject States:**
   - Set anomaly defeat objects to **inactive** by default in the scene
   - Set normal result objects to **active** by default in the scene
   - The script will toggle them based on game result

### For Other Result Scenes:

1. **Add ResultDisplay Component:**
   - The ResultDisplay script now supports GameObject activation
   - Configure the same way as SampleSceneManager

2. **Configure Arrays:**
   - **Anomaly Defeat Objects**: Objects to show for anomaly defeats
   - **Normal Result Objects**: Objects to show for normal results

## How It Works

### Game Flow:
```
Anomaly Timeout → PlayerPrefs["AnomalyTimeout"] = 1 → Load SampleScene
                                                    ↓
                    SampleSceneManager detects flag → Activates anomaly defeat objects
                                                    ↓
                                        Shows special defeat content
```

### Normal Game Flow:
```
Night Ends → Normal Win/Lose → Load ResultScene → Shows normal results
```

## GameObject Examples

### Anomaly Defeat Objects:
- **Dark overlay panels**
- **Anomaly victory screens**
- **Special defeat messages**
- **Horror-themed UI elements**
- **Anomaly character models**
- **Creepy visual effects**

### Normal Result Objects:
- **Standard UI panels**
- **Score displays**
- **Normal win/lose screens**
- **Regular game UI elements**

## Testing

### In SampleScene:
1. **Right-click SampleSceneManager** in Inspector
2. **Select "Test Anomaly Defeat"** - Simulates anomaly timeout
3. **Select "Test Normal Result"** - Simulates normal game end
4. **Select "Clear Result Flags"** - Resets all flags

### In ResultDisplay:
1. **Right-click ResultDisplay** in Inspector
2. **Select "Test Anomaly Defeat"** - Tests anomaly defeat display
3. **Select "Test Win Result"** - Tests normal win
4. **Select "Test Lose Result"** - Tests normal lose

## Implementation Notes

- **Automatic Detection**: Scripts automatically detect anomaly timeout via PlayerPrefs
- **Scene Independence**: Each scene can have its own GameObject setup
- **Flexible Configuration**: Easy to add/remove objects via Inspector arrays
- **Debug Support**: Enable "Show Debug Info" for detailed logging
- **Memory Management**: Flags are properly cleared when starting new games

## Example Usage

```csharp
// Manual activation from code
SampleSceneManager sceneManager = FindObjectOfType<SampleSceneManager>();
sceneManager.TestAnomalyDefeat(); // Activate anomaly defeat objects
```

## Troubleshooting

- **Objects not activating**: Check that they're assigned in the Inspector arrays
- **Wrong objects showing**: Verify the objects are inactive/active by default as needed
- **No response**: Ensure SampleSceneManager is in the scene and enabled
- **Testing not working**: Check Console for debug messages when "Show Debug Info" is enabled
