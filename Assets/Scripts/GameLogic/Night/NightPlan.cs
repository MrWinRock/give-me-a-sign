using System.Collections.Generic;
using GameLogic.Data;
using Report;

namespace GameLogic.Night
{
    /// <summary>One anomaly, in one room, at one minute of the night.</summary>
    [System.Serializable]
    public struct AnomalyPlacement
    {
        public AnomalyDefinition definition;
        public RoomDefinition room;

        public float atMinute;

        public float DeadlineSeconds =>
            atMinute * 60f + (definition != null ? definition.threatTimeoutSeconds : 0f);

        public float AtSeconds => atMinute * 60f;

        public bool HasDeadline => definition != null && definition.threatTimeoutSeconds > 0f;

        public int ThreatCost => definition != null ? definition.threatCost : 0;
    }

    /// <summary>One form glitch fired at a set minute. Mirrors GlitchScheduleEntry.</summary>
    [System.Serializable]
    public struct GlitchBeat
    {
        public GlitchType type;
        public float atMinute;

        public string overrideText;

        public float fireDelay;
    }

    /// <summary>Sprint 4 - ambient haunt beat. Carried through the plan so nothing has to change later.</summary>
    [System.Serializable]
    public struct HauntBeat
    {
        public HauntLoopId loop;
        public RoomDefinition room;
        public float atMinute;
    }

    /// <summary>
    /// The complete script for one night, produced by <see cref="NightPlanGenerator"/> from a
    /// seed. Everything the schedulers need is here, which is the point: the number of anomalies
    /// and the score required to survive them are computed together and cannot drift apart.
    /// </summary>
    [System.Serializable]
    public class NightPlan
    {
        public int seed;
        public int nightIndex = 1;
        public float durationMinutes = 5f;

        public List<AnomalyPlacement> anomalies = new List<AnomalyPlacement>();
        public List<GlitchBeat> glitches = new List<GlitchBeat>();
        public List<HauntBeat> haunts = new List<HauntBeat>();   // Sprint 4 fills this

        public GlitchProfile glitchProfile;

        public int requiredScore;

        public int penaltyAnomaliesPerWrongReport = 1;

        public float DurationSeconds => durationMinutes * 60f;

        public int TotalThreatCost
        {
            get
            {
                int sum = 0;
                foreach (var placement in anomalies) sum += placement.ThreatCost;
                return sum;
            }
        }

        public void SortByTime()
        {
            anomalies.Sort((a, b) => a.atMinute.CompareTo(b.atMinute));
            glitches.Sort((a, b) => a.atMinute.CompareTo(b.atMinute));
            haunts.Sort((a, b) => a.atMinute.CompareTo(b.atMinute));
        }
    }
}
