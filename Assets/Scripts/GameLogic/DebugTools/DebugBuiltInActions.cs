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
                steps = "Run a night (GamePlay). Hold V: mic-open click, then MicHold loop, HUD bottom-right shows REC + WHISPER/NORMAL/SHOUT. Release: click again. Pause menu / cutscene: V does nothing." },
            new QaItem { id = "report", title = "Report an anomaly by voice",
                steps = "Spawn: Shadow Blob. A report is WHAT + WHERE. Note the room from the spawn message, hold V and say e.g. 'shadow in bedroom' (any volume): COPY THAT and it leaves + score up. Only 'shadow' = WHICH ROOM? (not filed, no penalty); only 'in bedroom' = WHAT DID YOU SEE?; wrong word or wrong room = NEGATIVE + it advances." },
            new QaItem { id = "demon", title = "Demon needs a shout",
                steps = "Spawn: Demon, pan the camera into its room until it jumpscares (camera locks). Hold V and say 'demon in <its room>' at normal volume: STATIC - SPEAK UP, nothing else happens. Shout it: COPY THAT and it leaves." },
            new QaItem { id = "stealth", title = "Hooded Figure stealth mic",
                steps = "Spawn: Hooded Figure. Mic opens by itself (open click), HUD: MIC LIVE - WHISPER ONLY n. Test 3 ways in 3 tries: (a) say nothing 8s = it leaves; (b) whisper 'figure in <room>' = it leaves via report; (c) speak/shout = full-screen jumpscare then Result (night lost). V press during it only clicks." },
            new QaItem { id = "radio", title = "Radio Check pass / fail / noise",
                steps = "Radio Check > Normal. Answer 'SEC-04 copy': COPY THAT shown ~2.5s. Run it again and stay silent: NO RESPONSE (NOISE +25) + 'Missed n/3', Noise bar jumps. Miss 3 in a row: HQ sends someone = next scheduled haunt fires immediately." },
            new QaItem { id = "mimic", title = "Radio Check Mimic / Wrong ID",
                steps = "Radio Check > Mimic: hint says no call sign. Stay silent = GOOD CALL. Run again and say 'all clear' = IT HEARD YOU, a penalty anomaly appears, Noise +40. Same for Wrong ID (answering = WRONG SIGN-IN, silence = GOOD CALL)." },
            new QaItem { id = "noise", title = "Noise Meter -> Listener",
                steps = "Hold V and talk loudly for ~10s: bar bottom-left fills (whisper barely, shout fast). Full = 'Too loud. Something heard you...' and Silence Protocol starts. Stay quiet: bar drains after ~2.5s." },
            new QaItem { id = "manual", title = "Field Manual locks + entry points",
                steps = "Field Manual > Lock all pages, press TAB: every page '???'. Spawn an anomaly: its page unlocks (Demon only when it reveals). TAB opens/closes it, but not while paused, in a cutscene, or while the Demon is out. The MainMenu icon 'Field Manual.exe' opens it too." },
            new QaItem { id = "matcher", title = "Short words don't match everything",
                steps = "Radio Check > Normal. Say just 'a' or 'in' or 'on': must NOT count as an answer. 'SEC-04 copy' (or 'copy') should." },
            new QaItem { id = "levels", title = "Mic levels feel right (your mic)",
                steps = "Hold V and speak: whisper -> REC WHISPER, normal voice -> REC NORMAL, shout -> REC SHOUT. If whisper reads NORMAL, lower Noise Meter 'Whisper Band Multiplier' or recalibrate the mic in Control Panel." },
        };

        public static List<DebugEntry> Build(Func<string> typedText, Action<string> info)
        {
            var list = new List<DebugEntry>();
            AddQa(list, info);
            AddSpawn(list, info);
            AddHaunts(list, info);
            AddRadio(list, info);
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

            foreach (float amount in new[] { 25f, 50f })
            {
                float captured = amount;
                list.Add(new DebugEntry
                {
                    group = VoiceGroup,
                    label = $"Noise Meter +{captured:0}",
                    run = () =>
                    {
                        var meter = NoiseMeter.Instance;
                        if (meter == null) { info("No NoiseMeter in this scene."); return; }
                        meter.AddNoise(captured);
                        info($"Noise now {meter.Level:0}/100.");
                    },
                });
            }

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
