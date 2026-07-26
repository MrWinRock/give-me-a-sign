using UnityEngine;

namespace GameLogic
{
    /// <summary>
    /// Marks a string field to be edited via a dropdown populated from the shared
    /// AnomalyOptionsCatalog asset instead of free-typing. This exists purely to avoid
    /// typos that would silently break Incident Report voice/location matching (a typo
    /// in a free-typed field is invisible until you're testing with your voice and it
    /// mysteriously never matches).
    ///
    /// This is an Editor-only convenience: the underlying field stays a plain string, so
    /// Anomaly / IncidentReportManager's runtime behavior and performance are completely
    /// unaffected. The drawer that reads this attribute lives in Assets/Editor and is
    /// stripped from player builds like all editor code.
    /// </summary>
    public class AnomalyOptionAttribute : PropertyAttribute
    {
        public enum OptionKind { AnomalyType, Location }

        public readonly OptionKind Kind;

        public AnomalyOptionAttribute(OptionKind kind)
        {
            Kind = kind;
        }
    }
}
