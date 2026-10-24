using System.Collections.Generic;
using GameLogic.Night;

namespace GameLogic.Data
{
    /// <summary>Which sector a day is played in, derived from the rooms' roomId prefix ("s032_cam02" -> S032) and activeNights.</summary>
    public static class SectorLabels
    {
        public static string IdOf(RoomDefinition room)
        {
            if (room == null || string.IsNullOrEmpty(room.roomId)) return null;
            int i = room.roomId.IndexOf('_');
            return i > 0 ? room.roomId.Substring(0, i).ToUpperInvariant() : null;
        }

        /// <summary>"SECTOR_S032" for a single-sector day, "ALL SECTORS" when several are open, "" if unknown.</summary>
        public static string ForDay(int day)
        {
            var library = NightContentLibrary.Load();
            if (library == null || library.rooms == null) return "";

            var ids = new List<string>();
            foreach (var room in library.rooms)
            {
                if (room == null || !room.IsActiveOn(day)) continue;
                string id = IdOf(room);
                if (id != null && !ids.Contains(id)) ids.Add(id);
            }
            if (ids.Count == 0) return "";
            return ids.Count == 1 ? "SECTOR_" + ids[0] : "ALL SECTORS";
        }
    }
}
