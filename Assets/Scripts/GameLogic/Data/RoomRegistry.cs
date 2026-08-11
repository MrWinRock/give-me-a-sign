using System.Collections.Generic;
using UnityEngine;

namespace GameLogic.Data
{
    /// <summary>
    /// The one place the whole game asks "which rooms exist?". Populated automatically by
    /// every <see cref="RoomAnchor"/> in the loaded scene, sorted by
    /// <see cref="RoomDefinition.cameraOrder"/> so index 0 is always the first camera area.
    /// </summary>
    public static class RoomRegistry
    {
        private static readonly List<RoomAnchor> _anchors = new List<RoomAnchor>();

        // Rebuilt only when the anchor set changes, so opening the Incident Report window
        // doesn't allocate a fresh List<string> every time.
        private static readonly List<string> _displayNames = new List<string>();
        private static bool _namesDirty = true;

        public static IReadOnlyList<RoomAnchor> All => _anchors;

        public static int Count => _anchors.Count;

        public static event System.Action OnRoomsChanged;

        public static void Register(RoomAnchor anchor)
        {
            if (anchor == null || anchor.Room == null)
            {
                if (anchor != null)
                    Debug.LogWarning($"RoomAnchor '{anchor.name}' has no RoomDefinition assigned - it will not be part of the game's room list.", anchor);
                return;
            }

            if (_anchors.Contains(anchor)) return;

            _anchors.Add(anchor);
            Sort();
            Invalidate();
        }

        public static void Unregister(RoomAnchor anchor)
        {
            if (!_anchors.Remove(anchor)) return;
            Invalidate();
        }

        public static RoomAnchor Get(string roomId)
        {
            if (string.IsNullOrEmpty(roomId)) return null;

            for (int i = 0; i < _anchors.Count; i++)
            {
                if (_anchors[i].Room.roomId == roomId) return _anchors[i];
            }
            return null;
        }

        public static RoomAnchor Get(RoomDefinition room) => room != null ? Get(room.roomId) : null;

        public static RoomDefinition RoomAt(int index)
        {
            if (index < 0 || index >= _anchors.Count) return null;
            return _anchors[index].Room;
        }

        public static List<string> DisplayNames()
        {
            if (_namesDirty)
            {
                _displayNames.Clear();
                for (int i = 0; i < _anchors.Count; i++)
                    _displayNames.Add(_anchors[i].Room.Label);
                _namesDirty = false;
            }
            return _displayNames;
        }

        public static int IndexOfDisplayName(string displayName)
        {
            if (string.IsNullOrWhiteSpace(displayName)) return -1;

            for (int i = 0; i < _anchors.Count; i++)
            {
                if (string.Equals(_anchors[i].Room.Label, displayName, System.StringComparison.OrdinalIgnoreCase))
                    return i;
            }
            return -1;
        }

        private static void Sort() => _anchors.Sort((x, y) => x.Room.cameraOrder.CompareTo(y.Room.cameraOrder));

        private static void Invalidate()
        {
            _namesDirty = true;
            OnRoomsChanged?.Invoke();
        }
    }
}
