using GameLogic.Data;
using UnityEngine;

namespace GameLogic.Night
{
    /// <summary>
    /// Holds the plan for the night currently being played. The schedulers and ScoreManager all
    /// read from here, which is what makes the anomaly timeline and the win requirement two
    /// views of one object instead of two Inspector fields that have to agree.
    ///
    /// <see cref="NightPlanRunner"/> publishes the plan in Awake, before any scheduler's Start.
    /// If something goes wrong and nothing published, <see cref="Current"/> generates one on
    /// demand and says so loudly rather than returning null and taking the scene down.
    /// </summary>
    public static class NightPlanProvider
    {
        private static NightPlan _current;

        /// <summary>
        /// Seed to use for the next night instead of a random one. Set from the debug menu to
        /// replay a specific night; null means "roll a fresh seed".
        /// </summary>
        public static int? ForcedSeed { get; set; }

        /// <summary>Fired whenever a new plan is published, for HUD/debug overlays.</summary>
        public static event System.Action<NightPlan> OnPlanPublished;

        /// <summary>True when a plan has actually been published for this night.</summary>
        public static bool HasPlan => _current != null;

        /// <summary>
        /// The night's plan. Generates an emergency one if nothing published, so a missing
        /// NightPlanRunner is a console error rather than a null-reference cascade.
        /// </summary>
        public static NightPlan Current
        {
            get
            {
                if (_current != null) return _current;

                Debug.LogError(
                    "NightPlanProvider: something asked for the night's plan before one was published. " +
                    "Add a NightPlanRunner to the gameplay scene - generating an emergency plan for now.");

                Publish(GenerateEmergencyPlan());
                return _current;
            }
        }

        public static void Publish(NightPlan plan)
        {
            _current = plan;

            if (plan != null)
                OnPlanPublished?.Invoke(plan);
        }

        /// <summary>Drops the current plan so the next night generates a fresh one.</summary>
        public static void Clear() => _current = null;

        /// <summary>A brand new seed, from the clock rather than UnityEngine.Random.</summary>
        public static int NextSeed()
        {
            if (ForcedSeed.HasValue) return ForcedSeed.Value;

            // Non-zero and unrelated to any shared random state.
            return System.Environment.TickCount ^ (int)(System.DateTime.Now.Ticks & 0x7FFFFFFF);
        }

        /// <summary>
        /// Last resort so the game still runs with no library configured: a single anomaly in
        /// whatever room the scene has. Deliberately trivial - it exists to keep the scene alive
        /// while the real problem is fixed, not to be playable content.
        /// </summary>
        private static NightPlan GenerateEmergencyPlan()
        {
            var library = NightContentLibrary.Load();
            var rooms = RoomsFromScene(library);

            if (library != null && library.difficulty != null && rooms.Count > 0)
            {
                var generator = new NightPlanGenerator(library, rooms, library.difficulty, library.glitch);
                return generator.GenerateValid(nightIndex: 1, durationMinutes: 5f, seed: NextSeed());
            }

            return new NightPlan { seed = 0, nightIndex = 1, durationMinutes = 5f, requiredScore = 0 };
        }

        private static System.Collections.Generic.List<RoomDefinition> RoomsFromScene(NightContentLibrary library)
        {
            // Prefer the rooms the camera can actually reach; fall back to the library's list
            // (which is what the editor batch tools use, with no scene loaded).
            var rooms = new System.Collections.Generic.List<RoomDefinition>();

            foreach (var anchor in RoomRegistry.All)
            {
                if (anchor.Room != null) rooms.Add(anchor.Room);
            }

            if (rooms.Count == 0 && library != null)
                rooms.AddRange(library.rooms);

            return rooms;
        }
    }
}
