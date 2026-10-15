using System;
using UnityEngine;

namespace Whisper
{
    /// <summary>The player's measured microphone levels and the thresholds derived from them.</summary>
    [Serializable]
    public class MicCalibrationData
    {
        public bool setupCompleted;
        public bool usedDefaults;
        public string deviceName;
        public float noiseFloorDb;
        public float normalVoiceDb;
        public float loudVoiceDb;
        public float minDb;
        public float maxDb;
        public float lowToNormal;
        public float normalToLoud;
        public int calibrationVersion;

        public float Normalize(float db) => Mathf.Clamp01(Mathf.InverseLerp(minDb, maxDb, db));

        public MicCalibrationData Clone() => (MicCalibrationData)MemberwiseClone();
    }
}
