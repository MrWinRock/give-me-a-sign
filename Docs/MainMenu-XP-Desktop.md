# Main Menu — Fake Windows XP Desktop

The main menu is SEC-04's work computer. There is no "Play" button and no "Quit" button:

| Player intent | How they reach it |
|---|---|
| Start game | Double-click the **Start Shift** desktop icon, or Start ▸ Start Shift |
| Load save | Start ▸ **My Reports** |
| Settings | Start ▸ **Control Panel** |
| Credits / help | Start ▸ **Help and Support** |
| Quit | Start ▸ **Turn Off Computer** ▸ Turn Off |
| (dead end) | Start ▸ **Log Off** → "Cannot log off. Shift is not complete." |

---

## 1. Building the scene

Open `Assets/Scenes/MainMenu.unity`, then run:

**Tools ▸ Give Me A Sign ▸ Build Main Menu Scene**

That builds the whole Canvas hierarchy, generates the rounded/circular/dotted sprites into
`Assets/Sprites/GeneratedUI/`, saves one prefab per dialog into `Assets/Prefabs/MainMenu/`, and
**wires every serialized reference**. The scene is left dirty on purpose — review it, then Ctrl+S.

Re-running is safe: it deletes the `MainMenuCanvas` / `MainMenuSystems` roots it made last time
and rebuilds them. Anything else in the scene is untouched.

Two things the builder can't do for you:

1. **Add `MainMenu.unity` to File ▸ Build Settings** (it logs a warning if you haven't). Start ▸
   Turn Off Computer ▸ Restart reloads by build index, and the boot sequence loads `GameManager`
   by name — both need Build Settings entries.
2. **Assign the art.** Every icon/avatar/tray glyph is a white placeholder square. See §4.

---

## 2. Canvas hierarchy

Sibling order **is** the z-order — desktop at the back, boot overlay on top.

```
MainMenuCanvas                    Canvas (Screen Space - Overlay)
│                                 CanvasScaler: Scale With Screen Size, 1024x768, match 0.5
│                                 (XP's native resolution — every size below is authored in real
│                                  XP pixels, so this reference lands authentic proportions)
│                                 GraphicRaycaster
├── Desktop                       Image (full stretch) + UIGradient + Button (click catcher)
│                                 #1A2838 → #24384C @45% → #1E3326
│
├── DesktopIcons                  top-left (12,-12), 76 wide, VerticalLayoutGroup spacing 8
│   ├── Icon_StartShift           Image (selection highlight) + Button + DesktopIcon      76x68
│   │   ├── SelectionOutline      4 tiled 1px dotted edges — hidden unless selected
│   │   ├── IconImage             32x32
│   │   ├── LabelShadow           TMP 10px black, offset (1,-1) — drawn first = behind
│   │   └── Label                 TMP 10px white
│   ├── Icon_ReadMe               → Notepad window
│   └── Icon_RecycleBin           → Recycle Bin window
│
├── WindowLayer                   full stretch, no graphic — every dialog spawns in here,
│                                 so windows can never cover the taskbar
│
├── Taskbar                       bottom, 30px, Image + UIGradient #3C81F3 → #1E5ECC
│   │                             ← StartMenuController lives on this object
│   ├── StartButton               68x26, rounded RIGHT corners only (8px),
│   │   └── StartLabel            UIGradient #5EAC56 → #2D7D28, "start" Tahoma 13 bold italic
│   └── SystemTray                right, 92x26, #146AB8  ← TaskbarClock lives here
│       ├── SpeakerIcon           12x12
│       ├── MicrophoneIcon        12x12
│       └── ClockText             TMP 11px white — shows "2:34 AM" (in-fiction, not real time)
│
├── StartMenu                     230 wide, bottom-left, y=30 (on top of the taskbar)
│   │                             INACTIVE by default. VerticalLayoutGroup + ContentSizeFitter
│   ├── Header                    34px, UIGradient #2B6EDE → #1854BE
│   │   ├── Avatar                26x26
│   │   ├── UserName              "SEC-04", Tahoma 12 bold white
│   │   └── AccentBorder          2px #FF9D3C along the bottom
│   ├── Items                     white
│   │   ├── Item_StartShift       XPMenuItem  "Start Shift"  / "Begin monitoring"   34px
│   │   ├── Item_MyReports        XPMenuItem  "My Reports"   / "Continue / load"    34px
│   │   ├── Divider               1px #D4D0C8
│   │   ├── Item_ControlPanel     XPMenuItem  "Control Panel"/ "Settings"           34px
│   │   └── Item_Help             XPMenuItem  "Help and Support" (no subtitle)      24px
│   └── Footer                    30px, UIGradient #4A8CE8 → #2B6EDE, right-aligned
│       ├── Item_LogOff           hover = white @20%
│       └── Item_TurnOff          hover = white @20%
│
└── BootOverlay                   full stretch black + centered monospace TMP, INACTIVE
                                  ← used by ShutdownSequence for BOTH boot and shutdown

MainMenuSystems                   (scene root, no transform meaning)
├── DesktopManager
├── ShutdownSequence
└── AudioSource                   UI cue player (auto-registered by AudioManager as SFX)

EventSystem                       created if the scene has none (InputSystemUIInputModule —
                                  the project is New-Input-System only)
```

### Window prefabs → `Assets/Prefabs/MainMenu/`

Every one shares `XPWindowController`: 1px `#003C74` border, 4px radius, 24px titlebar
(`#2B6EDE → #1854BE`, Tahoma 11 bold white), red close button (`#E87A7A → #B02020`, 18x16),
body `#ECE9D8` with 14px padding.

| Prefab | Component | Width | Contents |
|---|---|---|---|
| `TurnOffWindow` | `TurnOffWindow` | 275 | 3 circular 34px buttons (Stand By / Turn Off / Restart) + Cancel |
| `ControlPanelWindow` | `ControlPanelWindow` | 320 | Audio / Display / Input tabs + OK / Cancel |
| `MyReportsWindow` | `MyReportsWindow` | 275 | save-slot list + Open / Cancel |
| `NotepadWindow` | `TextContentWindow` | 275 | white text area, 1px `#7F9DB9`, line-height ≈1.8 |
| `RecycleBinWindow` | `TextContentWindow` | 275 | "3 items." + greyed deleted-log line + Close |
| `LogOffWindow` | `TextContentWindow` | 275 | "Cannot log off." + "Shift is not complete." + OK |
| `HelpWindow` | `TextContentWindow` | 275 | controls / shift instructions / credits + OK |

---

## 3. Scripts

| Script | Path | Job |
|---|---|---|
| `DesktopManager` | `Scripts/MainMenu/` | Desktop state, icon selection + double-click, window pool & z-order, action routing, audio cues |
| `StartMenuController` | `Scripts/MainMenu/` | Start menu open/close, depressed start button, item routing |
| `XPWindowController` | `Scripts/MainMenu/` | Reusable window base: titlebar, close, Show/Hide, focus, one-at-a-time |
| `XPWindowDrag` | `Scripts/MainMenu/` | Titlebar drag handle — windows are draggable like real XP, clamped so the titlebar never leaves the screen or sinks under the taskbar |
| `ShutdownSequence` | `Scripts/MainMenu/` | Boot text sequence + shutdown screen |
| `DesktopIcon` | `Scripts/MainMenu/` | One icon: label, selection visual, click forwarding |
| `XPMenuItem` | `Scripts/MainMenu/` | One start-menu row + its hover colours |
| `TaskbarClock` | `Scripts/MainMenu/` | In-fiction clock (`useRealTime` to switch) |
| `TurnOffWindow` / `ControlPanelWindow` / `MyReportsWindow` / `TextContentWindow` | `Scripts/MainMenu/` | The four window behaviours |
| `UIGradient` | `Scripts/UI/` | Vertical 2/3-stop gradient for any uGUI Image |
| `XPPalette` | `Scripts/UI/` | `XPPalette.Hex("#316AC5")` — used only as the *default* of serialized Color fields |

**No colour or copy is hardcoded in behaviour.** Every hex above is the default of a serialized
`Color`, every string the default of a serialized `string` / `[TextArea]`. Change them in the
Inspector; the code never re-reads a literal.

`Time.timeScale` is never touched. Both sequences use `WaitForSecondsRealtime`.

---

## 4. What to wire / assign by hand

The builder wires all references. What's left is **art and audio**, all optional and null-safe:

**DesktopManager** (on `MainMenuSystems`)
- `Wallpaper` — a darkened photo of the house. When assigned it *replaces* the gradient; use
  `Wallpaper Tint` to darken further without re-exporting.
- `Start Menu Open Clip`, `Window Open Clip`, `Window Close Clip`, `Icon Click Clip`, `Error Clip`
  — leave any unassigned and that cue is simply skipped. They play through the `AudioSource` on
  `MainMenuSystems`, which `AudioManager` auto-registers on the SFX channel, so the Control
  Panel's Master slider already governs them.
- `Double Click Threshold` — 0.4s.

**Icon art** — `Icon_StartShift / Icon_ReadMe / Icon_RecycleBin ▸ IconImage ▸ Image ▸ Sprite`.
Same for `StartMenu ▸ Header ▸ Avatar` and `Taskbar ▸ SystemTray ▸ SpeakerIcon / MicrophoneIcon`.

**Fonts** — everything uses `Assets/Fonts/tahoma SDF.asset`; TMP falls back to Arial/LiberationSans
if it's missing. The two places that *want* monospace (`NotepadWindow ▸ TextArea ▸ Content` and
`BootOverlay ▸ OverlayText`) currently use Tahoma — assign a monospace TMP Font Asset there if
you want the authentic look.

**ShutdownSequence**
- `Skip Boot Sequence` — the fast-iteration switch. On = jump straight into `GameManager`.
- `Game Scene Name`, `Boot Lines`, `Line Interval` (0.8s), `Shutdown Message`, `Shutdown Hold
  Seconds` (2s) are all serialized.
- In the Editor, Turn Off logs instead of quitting, so play mode survives the test.

**TaskbarClock** — `Fictional Time` = "2:34 AM"; tick `Use Real Time` to show the wall clock.

**MyReportsWindow** — the `Slots` list is the data source (file name / detail / state). `Corrupted`
slots can't be selected. Opening a slot currently just starts the shift; the `TODO` is marked at
`MyReportsWindow.OnOpenClicked`.

---

## 5. Settings persistence

`ControlPanelWindow` reads and writes PlayerPrefs, and `DesktopManager.Awake` calls
`ControlPanelWindow.ApplySavedSettings()` so saved display settings apply even if the window is
never opened.

| Setting | Key | Owner |
|---|---|---|
| Master volume | `Vol_Master` | `AudioManager` (already existed) |
| Ambience | `Vol_Music` | `AudioManager` |
| Microphone gain | `Opt_MicGain` | `ControlPanelWindow.MicGain` |
| Enable subtitles | `Opt_Subtitles` | `ControlPanelWindow.SubtitlesEnabled` |
| Reduce flashing effects | `Opt_ReduceFlashing` | `ControlPanelWindow.ReduceFlashingEffects` |
| Resolution | `Opt_ResWidth` / `Opt_ResHeight` | applied via `Screen.SetResolution` |
| Fullscreen | `Opt_Fullscreen` | ″ |
| Push-to-Talk key | `Opt_PTTKey` | `ControlPanelWindow.PushToTalkKey` (Input System `Key`) |
| Microphone device | `Opt_MicDevice` | `ControlPanelWindow.MicrophoneDevice` |

Resolution/fullscreen are **not** applied in the Editor (forcing the Game view every play is just
a nuisance) — the `#if !UNITY_EDITOR` guard is in `ApplySavedSettings`.

**Not yet consumed by gameplay** — these four save correctly but nothing reads them:
`Opt_MicGain`, `Opt_Subtitles`, `Opt_ReduceFlashing`, `Opt_PTTKey`, `Opt_MicDevice`.
Hooking them up:
- `Opt_PTTKey` / `Opt_MicDevice` → `WhisperMicInput` (it hardcodes `Keyboard.current.spaceKey`
  in `IncidentReportManager` and takes `deviceName` from the Inspector).
- `Opt_ReduceFlashing` → `GlitchDirector` / `FormGlitchController`.

---

## 6. Debug support

- `DesktopManager` right-click ▸ **Debug/** — opens any window directly, or starts the shift.
- `ShutdownSequence` right-click ▸ **Play Boot Sequence** / **Play Shutdown Sequence**.
- `ShutdownSequence.skipBootSequence` — skip the 3.2s of boot text while iterating.
