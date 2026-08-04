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

        /// <summary>REAL minutes after the night starts - the same unit NightTimer.ElapsedMinutes uses.</summary>
        public float atMinute;

        /// <summary>When this anomaly stops being the player's problem, in seconds from the night's start.</summary>
        public float DeadlineSeconds =>
            atMinute * 60f + (definition != null ? definition.threatTimeoutSeconds : 0f);

        public float AtSeconds => atMinute * 60f;

        /// <summary>True when this anomaly can actually run the player out of time.</summary>
        public bool HasDeadline => definition != null && definition.threatTimeoutSeconds > 0f;

        public int ThreatCost => definition != null ? definition.threatCost : 0;
    }

    /// <summary>One form glitch fired at a set minute. Mirrors GlitchScheduleEntry.</summary>
    [System.Serializable]
    public struct GlitchBeat
    {
        public GlitchType type;
        public float atMinute;

        /// <summary>Optional exact text; empty means the controller picks from its word list.</summary>
        public string overrideText;

        /// <summary>Stagger so glitches scheduled close together don't all flash on one frame.</summary>
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
    ///
    /// The old setup had the anomaly timeline in AnomalyScheduler's Inspector and the win
    /// threshold in ScoreManager's - editing one and forgetting the other is exactly how the
    /// game shipped needing 9 points from 8 anomalies.
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

        /// <summary>
        /// How many anomalies must be dealt with to survive. Derived from the plan, never typed
        /// in by hand - this is the field that makes the win condition impossible to desync.
        /// </summary>
        public int requiredScore;

        public float DurationSeconds => durationMinutes * 60f;

        /// <summary>Total threat cost spent on this night - a rough difficulty readout.</summary>
        public int TotalThreatCost
        {
            get
            {
                int sum = 0;
                foreach (var placement in anomalies) sum += placement.ThreatCost;
                return sum;
            }
        }

        /// <summary>Sorts every timeline in place so the schedulers can walk them with one cursor.</summary>
        public void SortByTime()
        {
            anomalies.Sort((a, b) => a.atMinute.CompareTo(b.atMinute));
            glitches.Sort((a, b) => a.atMinute.CompareTo(b.atMinute));
            haunts.Sort((a, b) => a.atMinute.CompareTo(b.atMinute));
        }
    }
}
