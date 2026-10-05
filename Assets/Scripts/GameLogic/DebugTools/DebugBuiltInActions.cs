using System;
using System.Collections.Generic;
using GameLogic.Data;
using GameLogic.Night;
using GameLogic.Save;
using GameLogic.SpawnAndTime;
using Report;
using UnityEngine;
using Whisper;

namespace GameLogic.DebugTools
{
    public class DebugEntry
    {
        public string group;
        public string label;
        public Action run;
        public bool refreshAfter; // rebuild the list after running (checklist ticks change their label)
    }

    /// <summary>
    /// Actions the Debug panel offers that no [ContextMenu] could express: spawning a specific
    /// anomaly, firing a specific haunt or Radio Check variant, unlocking the Field Manual, and
    /// the pre-commit QA checklist (with "how to test" steps shown in the panel's info box).
    /// </summary>
    public static class DebugBuiltInActions
    {
        private const string QaGroup = "QA  Test checklist  (click = steps, tick row = done)";
        private const string SpawnGroup = "Spawn anomaly  (random room, instant)";
        private const string HauntGroup = "Fire haunt now  (ignores tutorial night)";
        private const string RadioGroup = "Radio Check  (force a call type)";
        private const string CameraGroup = "Camera feed  (Camera Betrayal glitches)";
        private const string VoiceGroup = "Voice / Noise / Field Manual";

        private struct QaItem
        {
            public string id;
            public string title;
            public string steps;
        }

        private static readonly QaItem[] QaItems =
        {
            new QaItem { id = "ptt", title = "Hold V = walkie-talkie",
                steps = "Run a night (GamePlay). Hold V: mic-open click, then MicHold loop, HUD bottom-right shows REC (no loudness text - that is the slider's job). Release: click again. Pause menu / cutscene: V does nothing." },
            new QaItem { id = "report", title = "Report an anomaly by voice",
                steps = "Spawn: Shadow Blob. A report is WHAT + WHERE. Note the room from the spawn message, hold V and say e.g. 'shadow in bedroom' (any volume): COPY THAT and it leaves + score up. Only 'shadow' = WHICH ROOM? then say just the room (it remembers 'shadow') = COPY THAT; only 'in bedroom' = YOU SEE WHAT? then say just 'shadow'. Memory lasts 20s and resets once filed or the anomaly is gone. Mispronounce on purpose ('shado', 'kichen'): near-words count. Wrong word or room = NEGATIVE + it advances." },
            new QaItem { id = "demon", title = "Demon needs a shout",
                steps = "Spawn: Demon, pan the camera into its room until it jumpscares (camera locks). Hold V and say 'demon in <its room>' at normal volume: nothing happens (no text - watch the slider's red zone). Shout it: COPY THAT and it leaves." },
            new QaItem { id = "stealth", title = "Hooded Figure stealth mic",
                steps = "Spawn: Hooded Figure. NOTHING happens until you move the mouse over it (pan to its room first). Then the mic opens by itself (open click), HUD: MIC LIVE - WHISPER ONLY n (blinks), slider quiet zone lights up. Test 3 ways in 3 tries: (a) say nothing 8s = it leaves; (b) whisper 'figure in <room>' = it leaves via report; (c) speak/shout = full-screen jumpscare then Result (night lost). V press during it only clicks." },
            new QaItem { id = "radio", title = "Radio Check pass / fail",
                steps = "Radio Check > Normal. Say exactly what the card says ('SEC-04 copy'): COPY THAT shown ~2.5s. Only noise, only 'copy', or the wrong call sign must NOT pass. Run again and stay silent: NO RESPONSE and a penalty anomaly appears in the scene." },
            new QaItem { id = "mimic", title = "Radio Check Mimic / Wrong ID",
                steps = "Radio Check > Mimic: hint says no call sign. Stay silent = GOOD CALL. Run again and say 'all clear' = IT HEARD YOU, a penalty anomaly appears. Same for Wrong ID (saying 'copy' = WRONG SIGN-IN + penalty anomaly, silence = GOOD CALL). OwnVoice: your recorded voice plays - speaking over it must not count; answer after it ends." },
            new QaItem { id = "noise", title = "Volume slider (bottom-left)",
                steps = "Hold V and speak: the bar slides right as you get louder; coloured zones = quiet / normal / loud, no words. Spawn Hooded Figure: quiet zone lights up - stay in it (loud = jumpscare). Reveal the Demon: loud zone lights up - shout until you reach it. Mic closed: slider is faded." },
            new QaItem { id = "manual", title = "Field Manual locks + entry points",
                steps = "Field Manual > Lock all pages, press TAB: every page '???'. Spawn an anomaly: its page unlocks (Demon only when it reveals). TAB opens/closes it, but not while paused, in a cutscene, or while the Demon is out. The MainMenu icon 'Field Manual.exe' opens it too." },
            new QaItem { id = "matcher", title = "Short words don't match everything",
                steps = "Radio Check > Normal. Say just 'a' or 'in' or 'on': must NOT count as an answer. 'SEC-04 copy' (or 'copy') should." },
            new QaItem { id = "camera", title = "Camera Betrayal glitches + Mirror wallpaper",
                steps = "Camera feed > Glitch: Loop (change room: the top-left label keeps the OLD room), Frozen (SIGNAL FROZEN label and you CANNOT change room until it times out; anomaly clicks still work), Blackout (feed dies, label hidden), GhostRoom (arms itself: the NEXT room you move to shows a ghost picture + fake CAM label until you change room again; coming back is normal), Mirror (arms itself like GhostRoom: the NEXT room you move to shows black + YOUR wallpaper at its own size + 'SECURITY OFFICE' label until you change room). 'Which picture would be used?' shows Wallpaper Engine first, else the Windows wallpaper. Toggle the Control Panel switch OFF: Mirror must refuse to start. All of them end by themselves and the HUD returns to normal." },
            new QaItem { id = "levels", title = "Mic levels feel right (your mic)",
                steps = "Hold V and watch the slider: a whisper should stay in the green zone, normal speech reach yellow, a shout reach red. If not, calibrate the mic (Debug > Mic: calibrate) or adjust the Whisper/Shout Band Multipliers on NoiseMeter." },
        };

        public static List<DebugEntry> Build(Func<string> typedText, Action<string> info)
        {
            var list = new List<DebugEntry>();
            AddQa(list, info);
            AddSpawn(list, info);
            AddHaunts(list, info);
            AddRadio(list, info);
            AddCamera(list, info);
            AddVoice(list, typedText, info);
            return list;
        }

        // ── QA checklist ─────────────────────────────────────────────────────────────────

        private static string QaKey(string id) => "DebugQA_" + id;

        private static void AddQa(List<DebugEntry> list, Action<string> info)
        {
            foreach (var item in QaItems)
            {
                var captured = item;
                bool done = PlayerPrefs.GetInt(QaKey(captured.id), 0) == 1;

                list.Add(new DebugEntry
                {
                    group = QaGroup,
                    label = $"How to test:  {captured.title}",
                    run = () => info($"{captured.title}\n{captured.steps}"),
                });
                list.Add(new DebugEntry
                {
                    group = QaGroup,
                    label = (done ? "[x]  " : "[  ]  ") + captured.title,
                    refreshAfter = true,
                    run = () =>
                    {
                        PlayerPrefs.SetInt(QaKey(captured.id), done ? 0 : 1);
                        PlayerPrefs.Save();
                    },
                });
            }

            list.Add(new DebugEntry
            {
                group = QaGroup,
                label = "Reset all ticks",
                refreshAfter = true,
                run = () =>
                {
                    foreach (var item in QaItems) PlayerPrefs.DeleteKey(QaKey(item.id));
                    PlayerPrefs.Save();
                },
            });
        }

        // ── Spawn anomaly ────────────────────────────────────────────────────────────────

        private static void AddSpawn(List<DebugEntry> list, Action<string> info)
        {
            var library = NightContentLibrary.Load();
            if (library == null) return;

            foreach (var def in library.anomalies)
            {
                if (def == null || def.prefab == null) continue;

                var captured = def;
                string voice = captured.voiceResponse == VoiceResponse.None ? "" : $"  [{captured.voiceResponse}]";
                list.Add(new DebugEntry
                {
                    group = SpawnGroup,
                    label = $"Spawn: {captured.Label}{voice}",
                    run = () =>
                    {
                        var scheduler = AnomalyScheduler.Instance;
                        if (scheduler == null) { info("No AnomalyScheduler in this scene (open GamePlay)."); return; }

                        var go = scheduler.SpawnNow(captured.prefab);
                        var anomaly = go != null ? go.GetComponentInChildren<Anomaly>(true) : null;
                        string room = anomaly != null && anomaly.AssignedRoom != null ? anomaly.AssignedRoom.Label : "?";
                        info($"Spawned {captured.Label} in {room}. Report it: " + ObservationVocabulary.Load().LabelFor(captured.observation) +
                             (captured.voiceResponse == VoiceResponse.None ? "" : $"  (needs: {captured.voiceResponse})"));
                    },
                });
            }

            list.Add(new DebugEntry
            {
                group = SpawnGroup,
                label = "Spawn: one of EVERY kind (except Demon)",
                run = () =>
                {
                    var scheduler = AnomalyScheduler.Instance;
                    if (scheduler == null) { info("No AnomalyScheduler in this scene."); return; }

                    int n = 0;
                    foreach (var def in library.anomalies)
                    {
                        if (def == null || def.prefab == null) continue;
                        if (def.prefab.GetComponentInChildren<DemonAnomaly>(true) != null) continue;
                        scheduler.SpawnNow(def.prefab);
                        n++;
                    }
                    info($"Spawned {n} anomalies.");
                },
            });

            list.Add(new DebugEntry
            {
                group = SpawnGroup,
                label = "Remove ALL active anomalies",
                run = () =>
                {
                    var scheduler = AnomalyScheduler.Instance;
                    if (scheduler == null) { info("No AnomalyScheduler in this scene."); return; }

                    var spawned = scheduler.GetSpawnedAnomalies();
                    foreach (var go in spawned) UnityEngine.Object.Destroy(go);
                    info($"Removed {spawned.Count} spawned anomalies.");
                },
            });
        }

        // ── Haunts ───────────────────────────────────────────────────────────────────────

        private static void AddHaunts(List<DebugEntry> list, Action<string> info)
        {
            foreach (HauntLoopId id in Enum.GetValues(typeof(HauntLoopId)))
            {
                if (id == HauntLoopId.None) continue;

                var captured = id;
                list.Add(new DebugEntry
                {
                    group = HauntGroup,
                    label = $"Fire: {captured}",
                    run = () =>
                    {
                        var director = HauntDirector.ExistingInstance;
                        if (director == null) { info("No HauntDirector in this scene (open GamePlay)."); return; }

                        bool fired = director.TriggerNow(captured, null, ignoreTutorial: true);
                        info(fired ? $"Fired {captured}." : $"{captured} did not fire (already active, or not in the scene).");
                    },
                });
            }
        }

        // ── Radio Check variants ─────────────────────────────────────────────────────────

        private static void AddRadio(List<DebugEntry> list, Action<string> info)
        {
            foreach (RadioCheckHaunt.Variant variant in Enum.GetValues(typeof(RadioCheckHaunt.Variant)))
            {
                var captured = variant;
                list.Add(new DebugEntry
                {
                    group = RadioGroup,
                    label = $"Call: {captured}",
                    run = () =>
                    {
                        var haunt = UnityEngine.Object.FindFirstObjectByType<RadioCheckHaunt>(FindObjectsInactive.Include);
                        if (haunt == null) { info("No RadioCheckHaunt in this scene (open GamePlay)."); return; }

                        info(haunt.DebugTrigger(captured)
                            ? $"Radio Check ({captured}) started. Normal/OwnVoice: say 'SEC-04 copy'. WrongId/Mimic: stay silent."
                            : "A Radio Check is already running.");
                    },
                });
            }
        }

        // ── Camera Betrayal ──────────────────────────────────────────────────────────────

        private static void AddCamera(List<DebugEntry> list, Action<string> info)
        {
            foreach (CameraGlitchType type in Enum.GetValues(typeof(CameraGlitchType)))
            {
                var captured = type;
                list.Add(new DebugEntry
                {
                    group = CameraGroup,
                    label = $"Glitch: {captured}",
                    run = () =>
                    {
                        var haunt = UnityEngine.Object.FindFirstObjectByType<CameraBetrayalHaunt>(FindObjectsInactive.Include);
                        if (haunt == null) { info("No CameraBetrayalHaunt in this scene (open GamePlay)."); return; }

                        if (haunt.DebugTrigger(captured))
                        {
                            bool armed = captured == CameraGlitchType.GhostRoom || captured == CameraGlitchType.Mirror;
                            info(armed
                                ? $"{captured} armed: it springs on the NEXT room you move to (use the < > buttons) and ends when you change room again."
                                : $"Camera glitch {captured} started (duration from its Duration Range).");
                            return;
                        }

                        info(captured == CameraGlitchType.Mirror
                            ? "Mirror did not start - it needs the player's picture and 2+ rooms. " + PlayerWallpaper.Describe()
                            : $"{captured} did not start (already running, no CameraFeedHud, or fewer than 2 rooms).");
                    },
                });
            }

            list.Add(new DebugEntry
            {
                group = CameraGroup,
                label = "Mirror: which picture would be used?",
                run = () => info(PlayerWallpaper.Describe()),
            });

            list.Add(new DebugEntry
            {
                group = CameraGroup,
                label = "Mirror: toggle the player's Control Panel switch",
                run = () =>
                {
                    bool now = !PlayerWallpaper.Enabled;
                    PlayerPrefs.SetInt(PlayerWallpaper.PrefKey, now ? 1 : 0);
                    PlayerPrefs.Save();
                    info($"Wallpaper switch is now {(now ? "ON" : "OFF - Mirror will not happen")}.");
                },
            });

            list.Add(new DebugEntry
            {
                group = CameraGroup,
                label = "Clear all camera glitches",
                run = () =>
                {
                    var controller = UnityEngine.Object.FindFirstObjectByType<CameraFeedController>(FindObjectsInactive.Include);
                    if (controller == null) { info("No CameraFeedController in this scene."); return; }

                    controller.CancelAllGlitches();
                    info("Camera glitches cleared.");
                },
            });
        }

        // ── Voice, noise, Field Manual ───────────────────────────────────────────────────

        private static void AddVoice(List<DebugEntry> list, Func<string> typedText, Action<string> info)
        {
            foreach (VoiceLevel level in new[] { VoiceLevel.Whisper, VoiceLevel.Normal, VoiceLevel.Shout })
            {
                var captured = level;
                list.Add(new DebugEntry
                {
                    group = VoiceGroup,
                    label = $"File typed text as radio report ({captured})",
                    run = () =>
                    {
                        string text = typedText();
                        var reports = IncidentReportManager.Instance;
                        if (string.IsNullOrWhiteSpace(text)) { info("Type something in the box above first (e.g. 'shadow in the kitchen')."); return; }
                        if (reports == null) { info("No IncidentReportManager in this scene."); return; }

                        info($"'{text}' at {captured} -> {reports.FileRadioReport(text, captured)}");
                    },
                });
            }

            foreach (WhisperMicInput.VoiceLanguage language in Enum.GetValues(typeof(WhisperMicInput.VoiceLanguage)))
            {
                var captured = language;
                list.Add(new DebugEntry
                {
                    group = VoiceGroup,
                    label = $"Voice model: switch to {captured}  (reloads, a few seconds)",
                    run = () =>
                    {
                        var mic = UnityEngine.Object.FindFirstObjectByType<WhisperMicInput>();
                        if (mic == null) { info("No WhisperMicInput in this scene (open GamePlay)."); return; }

                        mic.SwitchLanguage(captured);
                        info($"Loading voice model: {mic.CurrentModelLabel}. Hold V works again once it finishes (HUD says VOICE MODEL LOADING until then).");
                    },
                });
            }

            list.Add(new DebugEntry
            {
                group = VoiceGroup,
                label = "Voice model: show which is loaded",
                run = () =>
                {
                    var mic = UnityEngine.Object.FindFirstObjectByType<WhisperMicInput>();
                    info(mic == null ? "No WhisperMicInput in this scene."
                        : $"{mic.CurrentModelLabel}  -  {(mic.IsModelReady ? "ready" : "loading...")}");
                },
            });

            list.Add(new DebugEntry
            {
                group = VoiceGroup,
                label = "Mic: calibrate noise floor (stay SILENT 3s)",
                run = () =>
                {
                    var monitor = UnityEngine.Object.FindFirstObjectByType<MicAmplitudeMonitor>();
                    var host = new GameObject("DebugMicCalibration");
                    if (monitor == null) monitor = host.AddComponent<MicAmplitudeMonitor>();

                    var runner = host.AddComponent<MicCalibrationRunner>();
                    runner.OnCompleted = floor =>
                    {
                        info($"Noise floor saved: {floor:0.0000}  (whisper <= x3, shout >= x8 of this)");
                        UnityEngine.Object.Destroy(host, 1f);
                    };
                    runner.Run(monitor);
                    info("Calibrating... stay silent for 3 seconds.");
                },
            });

            list.Add(new DebugEntry
            {
                group = VoiceGroup,
                label = "Mic: show saved noise floor",
                run = () => info($"Noise floor = {MicCalibration.NoiseFloor:0.0000}  ({(MicCalibration.HasCalibrated ? "calibrated" : "DEFAULT - never calibrated")})"),
            });

            list.Add(new DebugEntry
            {
                group = VoiceGroup,
                label = "Field Manual: unlock ALL pages",
                run = () =>
                {
                    var library = NightContentLibrary.Load();
                    int n = 0;
                    if (library != null)
                        foreach (var def in library.anomalies)
                            if (def != null && SaveManager.Current.MarkAnomalySeen(def.anomalyId)) n++;
                    SaveManager.Save();
                    info($"Unlocked {n} new page(s).");
                },
            });

            list.Add(new DebugEntry
            {
                group = VoiceGroup,
                label = "Field Manual: LOCK all pages (clear seen list)",
                run = () =>
                {
                    SaveManager.Current.seenAnomalyIds.Clear();
                    SaveManager.Save();
                    info("All pages locked again.");
                },
            });
        }
    }
}
