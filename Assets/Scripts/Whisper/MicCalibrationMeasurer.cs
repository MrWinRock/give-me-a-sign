using System.Collections.Generic;
using UnityEngine;

namespace Whisper
{
    /// <summary>
    /// The measurement maths of the microphone wizard, with no UI and no microphone access: feed it dB frames,
    /// read back the noise floor / utterance levels, and derive the thresholds.
    /// </summary>
    public class MicCalibrationMeasurer
    {
        public struct Utterance
        {
            public bool longEnough;
            public bool hasSpeech;
            public float peakDb;
            public float levelDb; // average of the loudest part of the speech frames
            public float seconds;
        }

        private readonly MicCalibrationConfig _config;
        private readonly List<float> _quietDb = new List<float>(256);
        private readonly List<float> _speechDb = new List<float>(256);
        private float _quietSeconds;
        private float _utteranceSeconds;
        private float _utterancePeak = float.NegativeInfinity;

        public MicCalibrationMeasurer(MicCalibrationConfig config) => _config = config;

        public float QuietProgress01 => Mathf.Clamp01(_quietSeconds / Mathf.Max(0.1f, _config.quietSeconds));
        public bool QuietDone => _quietSeconds >= _config.quietSeconds;

        public float ToDb(float rms) => rms <= 0.00001f ? _config.silenceDb : Mathf.Max(_config.silenceDb, 20f * Mathf.Log10(rms));

        public void BeginQuiet()
        {
            _quietDb.Clear();
            _quietSeconds = 0f;
        }

        public void AddQuiet(float db, float seconds)
        {
            _quietDb.Add(db);
            _quietSeconds += seconds;
        }

        // A high percentile, so an occasional click or cough doesn't set the floor but steady room noise does.
        public float QuietFloorDb()
        {
            if (_quietDb.Count == 0) return _config.silenceDb;

            var sorted = new List<float>(_quietDb);
            sorted.Sort();
            int index = Mathf.Clamp(Mathf.CeilToInt(sorted.Count * _config.noiseFloorPercentile) - 1, 0, sorted.Count - 1);
            return sorted[index];
        }

        public void BeginUtterance()
        {
            _speechDb.Clear();
            _utteranceSeconds = 0f;
            _utterancePeak = float.NegativeInfinity;
        }

        public void AddUtterance(float db, float seconds, float noiseFloorDb)
        {
            _utteranceSeconds += seconds;
            _utterancePeak = Mathf.Max(_utterancePeak, db);
            if (db >= noiseFloorDb + _config.speechGateAboveFloorDb) _speechDb.Add(db);
        }

        // The utterance level is the mean of the loudest `louderPortionFraction` of its speech frames, so pauses and soft
        // word endings don't drag a shout down.
        public Utterance EndUtterance()
        {
            var result = new Utterance
            {
                seconds = _utteranceSeconds,
                longEnough = _utteranceSeconds >= _config.minUtteranceSeconds,
                hasSpeech = _speechDb.Count > 0,
                peakDb = float.IsNegativeInfinity(_utterancePeak) ? _config.silenceDb : _utterancePeak,
                levelDb = _config.silenceDb,
            };

            if (!result.hasSpeech) return result;

            var sorted = new List<float>(_speechDb);
            sorted.Sort((a, b) => b.CompareTo(a));
            int take = Mathf.Clamp(Mathf.CeilToInt(sorted.Count * _config.louderPortionFraction), 1, sorted.Count);

            float sum = 0f;
            for (int i = 0; i < take; i++) sum += sorted[i];
            result.levelDb = sum / take;
            return result;
        }

        public bool NormalIsClearlyAboveFloor(float normalDb, float floorDb) => normalDb >= floorDb + _config.minNormalOverFloorDb;

        // The raw mic clips regardless of Mic volume, so the headroom check removes the gain first.
        public bool LoudHasRoomAbove(float normalDb, float gainDb = 0f) => normalDb - gainDb + _config.minLoudOverNormalDb <= _config.maxMeasurableDb;

        public bool LoudIsClearlyAboveNormal(float loudDb, float normalDb) => loudDb >= normalDb + _config.minLoudOverNormalDb;

        // minDb..maxDb maps dB to the meter's 0..1; the two thresholds sit between floor/normal and normal/loud, clamped to safe ranges.
        public MicCalibrationData Derive(float floorDb, float normalDb, float loudDb, string deviceName)
        {
            var c = _config;
            float minDb = floorDb + c.minDbOffset;
            float maxDb = Mathf.Max(loudDb + c.maxDbHeadroom, minDb + c.minRangeDb);

            var data = new MicCalibrationData
            {
                setupCompleted = true,
                usedDefaults = false,
                deviceName = deviceName ?? string.Empty,
                noiseFloorDb = floorDb,
                normalVoiceDb = normalDb,
                loudVoiceDb = loudDb,
                minDb = minDb,
                maxDb = maxDb,
                calibrationVersion = c.currentCalibrationVersion,
            };

            float lowDb = floorDb + c.lowToNormalWeight * (normalDb - floorDb);
            float highDb = normalDb + c.normalToLoudWeight * (loudDb - normalDb);

            float low = Mathf.Clamp(data.Normalize(lowDb), c.lowToNormalClamp.x, c.lowToNormalClamp.y);
            float high = Mathf.Clamp(data.Normalize(highDb), c.normalToLoudClamp.x, c.normalToLoudClamp.y);

            if (high - low < c.minThresholdGap)
            {
                high = Mathf.Min(low + c.minThresholdGap, c.normalToLoudClamp.y);
                low = Mathf.Max(high - c.minThresholdGap, c.lowToNormalClamp.x);
            }

            data.lowToNormal = low;
            data.normalToLoud = high;
            return data;
        }
    }
}
