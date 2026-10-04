using System;
using System.Collections.Generic;
using UnityEngine;

namespace UI
{
    public enum MessageId
    {
        OverloadWarning,
        StealthHint,
        ReportConfirmed,
        ReportNegative,
        ReportNeedRoom,
        ReportNeedWhat,
        NothingHeard,
        ModelLoading,
        RadioPass,
        RadioPassDecoy,
        RadioFailMiss,
        RadioFailMimic,
        RadioFailWrongId,
        RadioDetailPass,
        RadioDetailPassDecoy,
        RadioDetailFail,
    }

    /// <summary>
    /// Every line of text the game flashes at the player during a shift, in one asset
    /// (Resources/PlayerMessages): edit the wording and tick Blink per message. {0}/{1} are filled
    /// by the game (counts, seconds) - keep them where they make sense in your wording.
    /// </summary>
    [CreateAssetMenu(fileName = "PlayerMessages", menuName = "Give Me A Sign/Player Messages")]
    public class PlayerMessages : ScriptableObject
    {
        [Serializable]
        public class Message
        {
            public MessageId id;
            [TextArea(1, 3)] public string text;
            [Tooltip("Flashes while shown.")]
            public bool blink;
        }

        [SerializeField] private List<Message> messages = Defaults();

        private static PlayerMessages _instance;

        public static PlayerMessages Load()
        {
            if (_instance != null) return _instance;

            _instance = Resources.Load<PlayerMessages>("PlayerMessages");
            if (_instance == null)
            {
                _instance = CreateInstance<PlayerMessages>();
                _instance.messages = Defaults();
            }
            return _instance;
        }

        public static string Text(MessageId id, params object[] args)
        {
            string text = Find(id).text ?? "";
            if (args == null || args.Length == 0) return text;

            try { return string.Format(text, args); }
            catch (FormatException) { return text; } // a hand-edited message with a stray brace shouldn't break the game
        }

        public static bool Blink(MessageId id) => Find(id).blink;

        private static Message Find(MessageId id)
        {
            var all = Load().messages;
            foreach (var m in all)
            {
                if (m != null && m.id == id) return m;
            }

            foreach (var m in Defaults())
            {
                if (m.id == id) return m;
            }
            return new Message { id = id, text = id.ToString() };
        }

        // Adds any message the asset is missing (after the enum grows) without touching edited ones.
        private void OnValidate()
        {
            if (messages == null) messages = new List<Message>();

            foreach (var def in Defaults())
            {
                if (!messages.Exists(m => m != null && m.id == def.id))
                    messages.Add(def);
            }
        }

        [ContextMenu("Reset To Defaults")]
        private void ResetToDefaults() => messages = Defaults();

        private static List<Message> Defaults() => new List<Message>
        {
            new Message { id = MessageId.OverloadWarning, text = "TOO MANY ANOMALIES ({0}) - REPORT THEM BEFORE THE BUILDING FALLS", blink = true },
            new Message { id = MessageId.StealthHint, text = "MIC LIVE - WHISPER ONLY... {0}", blink = true },
            new Message { id = MessageId.ReportConfirmed, text = "COPY THAT" },
            new Message { id = MessageId.ReportNegative, text = "NEGATIVE" },
            new Message { id = MessageId.ReportNeedRoom, text = "WHICH ROOM?" },
            new Message { id = MessageId.ReportNeedWhat, text = "WHAT DID YOU SEE?" },
            new Message { id = MessageId.NothingHeard, text = "(nothing heard)" },
            new Message { id = MessageId.ModelLoading, text = "VOICE MODEL LOADING..." },
            new Message { id = MessageId.RadioPass, text = "COPY THAT" },
            new Message { id = MessageId.RadioPassDecoy, text = "GOOD CALL" },
            new Message { id = MessageId.RadioFailMiss, text = "NO RESPONSE", blink = true },
            new Message { id = MessageId.RadioFailMimic, text = "IT HEARD YOU", blink = true },
            new Message { id = MessageId.RadioFailWrongId, text = "WRONG SIGN-IN", blink = true },
            new Message { id = MessageId.RadioDetailPass, text = "HQ is satisfied." },
            new Message { id = MessageId.RadioDetailPassDecoy, text = "That wasn't HQ." },
            new Message { id = MessageId.RadioDetailFail, text = "Another anomaly got in." },
        };
    }
}
