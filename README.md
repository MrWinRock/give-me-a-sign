# Give Me A Sign 

> A First-Person Survival Horror game featuring Voice Recognition and FNAF-Style Night Cycle System

[![Unity Version](https://img.shields.io/badge/Unity-6000.2.9f1-blue)](https://unity.com/)
[![Download](https://img.shields.io/badge/Download-Itch.io-red)](https://marguro.itch.io/give-me-a-sign)

## 📖 Description

**Give Me A Sign** is a survival horror game where players must survive against supernatural anomalies that appear during the night. Players must detect anomalies in various locations through security cameras and use **Voice Recognition** to recite prayers that banish evil entities before being attacked.

The game features a real-time speech recognition system powered by **Whisper AI** that supports both English and Thai languages, and includes a FNAF-style Night Timer (0:00 AM - 6:00 AM) with configurable anomaly spawning timeline.

## 🎮 How to Play

### Controls
* **Left Click (MB1):** Inspect locations and anomalies
* **Left/Right Arrow Keys:** Switch camera views (different areas)
* **Space (Hold):** Activate microphone to recite prayer and banish demons

### Game Rules
1. **Inspect Areas:** Click to inspect different rooms through cameras to find anomalies
2. **Identify Anomalies:** When you spot something unusual, click on it immediately
3. **Pray to Banish:** When an anomaly approaches, hold Space and recite the prayer:
   - **English:** *"In the name of the father son and holy spirit"*
   - **Thai:** *"ในนามพระบิดา พระบุตร และพระจิตเจ้า"*
4. **Survive:** Banish all anomalies until 6:00 AM to win the game

### Win/Lose Conditions
* ✅ **Win:** Banish the required number of anomalies (minimum 9) and survive until 6:00 AM
* ❌ **Lose:** Get attacked by an anomaly (fail to recite prayer in time)

## ✨ Features

### 🎙️ Voice Recognition System
* **Whisper AI Integration:** Uses OpenAI Whisper AI Model for real-time speech recognition
* **Multi-language Support:** Supports both Thai and English languages
* **Prayer Detection:** Fuzzy matching system (doesn't require perfect pronunciation)
* **Sign Request System:** Detects "Give me a sign" phrase to trigger anomaly events

### 👻 Anomaly System
* **Multiple Anomaly Types:** Various types of supernatural phenomena
  - **Disappear Instantly:** Vanishes immediately when detected
  - **Move to Target:** Approaches player then attacks
  - **Move Only:** Moves but doesn't disappear (must be banished with prayer)
* **Animation Support:** Supports Animator for each anomaly
* **Audio Feedback:** Jump scare and combat sound effects
* **Prayer Banishment:** Real-time banishment through prayer recitation

### ⏰ Night Cycle System (FNAF-Style)
* **Time Display:** FNAF-style time display (0:00 AM → 6:00 AM)
* **Configurable Duration:** Adjustable real-time duration (Default: 4 minutes)
* **Automatic Anomaly Spawning:** Automatic spawning system based on timeline
* **Timeline Management:** Precise spawn timing for each anomaly

### 🎯 Score & Result System
* **Point-based Scoring:** Points awarded for successfully banishing anomalies
* **Win/Lose Conditions:** Automatic win/lose determination
* **Result Screen:** End-game summary screen
* **Special Defeat Scenes:** Special scenes when defeated by anomalies

### 🎨 UI & Visual Effects
* **Click Effects:** Visual feedback when clicking to inspect
* **Text Shine Effects:** Glowing text effects
* **Prayer Panel UI:** UI panel for prayer recitation
* **Camera System:** Multi-area camera surveillance system

### 🔊 Audio System
* **Ambient Sounds:** Creepy atmospheric soundscape
* **Microphone Feedback:** Audio cues when opening/closing microphone
* **Jump Scare Audio:** Jump scare sound effects when anomaly attacks
* **Background Music:** VHS/Retro horror-style background music

## 🛠️ Setup

### System Requirements
* **Unity Version:** `6000.2.9f1` (Unity 6)
* **Platform:** Windows (PC)
* **Microphone:** Required for voice recognition system

### Installation
1. Clone or download this project
   ```bash
   git clone https://github.com/yourusername/give-me-a-sign.git
   ```
2. Open Unity Hub
3. Click **Add** and select the project folder
4. Select Unity Editor version **6000.2.9f1**
5. Open the initial scene `Assets/Scenes/StartScene.unity`
6. Press **Play** in Unity Editor

### Whisper AI Model Setup
1. Download model from [Whisper.cpp HuggingFace](https://huggingface.co/ggerganov/whisper.cpp/tree/main)
2. Recommended models: `ggml-small.en.bin` or `ggml-base.en.bin`
3. Place model file in `Assets/StreamingAssets/Models/`
4. Configure path in `WhisperMicInput` component

### Build Settings
Scenes required in Build Settings (in order):
1. `StartScene.unity` - Game start screen
2. `GameManager.unity` - Main game scene (Night gameplay)
3. `Result.unity` - Result summary screen

## 🏗️ Project Structure

```
Assets/
├── Scenes/              # Main game scenes
├── Scripts/
│   ├── GameLogic/       # Core game systems (Anomaly, GameManager)
│   │   └── SpawnAndTime/ # Night Timer and Spawning systems
│   ├── Whisper/         # Voice Recognition system
│   ├── Player/          # Player systems (Click, Input)
│   ├── Pray/            # Prayer system and UI
│   ├── Score/           # Score and result systems
│   ├── UI/              # UI Effects and Components
│   └── Cutscene/        # Scene Transition system
├── Prefabs/             # Prefabs (Anomalies, Effects)
├── Audio/               # Sound effects and music
├── Sprites/             # Images and Textures
├── Models/              # 3D Models (if any)
└── StreamingAssets/     # Whisper AI Models
```

## 🎓 Technical Details

### Core Systems

#### 1. **Anomaly System** (`Anomaly.cs`)
- Supports 3 Response Types
- Animation Integration
- Prayer Banishment System
- Timeout & Scene Reload Logic

#### 2. **Voice Recognition** (`WhisperMicInput.cs`, `VoiceCommandRouter.cs`)
- Real-time Speech-to-Text
- Fuzzy Matching Algorithm
- Multi-language Support (Thai/English)
- Prayer Validation System

#### 3. **Night Timer** (`NightTimer.cs`)
- FNAF-style Time Display (0:00 - 6:00 AM)
- Configurable Real-time Duration
- Event-driven Architecture
- Auto Scene Transition

#### 4. **Spawn Manager** (`AnomalySpawnManager.cs`)
- Timeline-based Spawning
- Inspector Configuration
- Visual Gizmos for Timeline
- Auto-find Reference System

#### 5. **Score System** (`ScoreManager.cs`)
- Point Tracking
- Win/Lose Determination
- PlayerPrefs Data Persistence
- Result Scene Integration

## 👥 Developers

* **MrWinRock** - DevOps
* **LOVERnoey** - Lead Developer
* **Marguro** - Project Manager/Game Designer
* **Vagolf** - General Bae
* **NKantapong** - Tester

## 🤖 AI Models & Libraries

### Whisper AI Integration
* **Model Source:** [Whisper.cpp (HuggingFace)](https://huggingface.co/ggerganov/whisper.cpp/tree/main)
* **Unity Integration:** [Whisper.Unity by Macoron](https://github.com/Macoron/whisper.unity)
* **Supported Models:**
  - `ggml-tiny.bin` - Smallest, fastest
  - `ggml-base.bin` - Balanced speed and accuracy
  - `ggml-small.bin` - More accurate (Recommended)
  - `ggml-medium.bin` - Most accurate but slower

## 📝 License

This project is for educational purposes. Please check individual asset licenses.

## 🐛 Known Issues

* Voice recognition requires a relatively quiet environment
* Smaller Whisper models may not accurately recognize Thai words (recommend using small or medium models)
* Microphone permission must be granted before playing on Windows

---

**Download Game:** [Give Me A Sign on Itch.io](https://marguro.itch.io/give-me-a-sign)

