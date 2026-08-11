using GameLogic.Data;
using UnityEngine;

namespace GameLogic.Night
{
    /// <summary>
    /// Holds the plan for the night currently being played. The schedulers and ScoreManager all
    /// read from here, which is what makes the anomaly timeline and the win requirement two
    /// views of one object instead of two Inspector fields that have to agree.
    /// </summary>
    public static class NightPlanProvider
    {
        private static NightPlan _current;

        public static int? ForcedSeed { get; set; }

        public static event System.Action<NightPlan> OnPlanPublished;

        public static bool HasPlan => _current != null;

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

        public static void Clear() => _current = null;

        public static int NextSeed()
        {
            if (ForcedSeed.HasValue) return ForcedSeed.Value;

            // Non-zero and unrelated to any shared random state.
            return System.Environment.TickCount ^ (int)(System.DateTime.Now.Ticks & 0x7FFFFFFF);
        }

        private static NightPlan GenerateEmergencyPlan()
        {
            var library = NightContentLibrary.Load();
            var rooms = RoomsFromScene(library);

            if (library != null && library.difficulty != null && rooms.Count > 0)
            {
                var generator = new NightPlanGenerator(library, rooms, library.difficulty, library.glitch, library.haunt);
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
