using UnityEngine;

namespace GameLogic
{
    /// <summary>
    /// TEMPORARY - delete this whole file once 'Tools/Give Me A Sign/Validate Data' reports no
    /// un-migrated prefabs.
    ///
    /// This is Anomaly's configuration as it was before the class was split into
    /// Anomaly + AnomalyMovement + AnomalyPresenter + AnomalyThreatTimer. The 8 existing
    /// prefabs still hold their real values in these fields, so they are kept (hidden) and
    /// copied into the sibling components at Awake until
    /// 'Tools/Give Me A Sign/Setup/2. Migrate Anomaly Prefabs' has moved them across for good.
    ///
    /// Removing the fields before then loses the authored values silently - Unity does not warn
    /// when a serialized field disappears.
    ///
    /// To finish the job: run the migration tool, confirm the validator is clean, then delete
    /// this file and the `if (!migrated) SeedSiblingsFromLegacy(...)` line in Anomaly.Awake.
    /// </summary>
    public partial class Anomaly
    {
        [SerializeField, HideInInspector] private bool migrated;

        [HideInInspector] public RespondType respondType = RespondType.MoveToTargetThenDisappear;
        [HideInInspector] public float timeToDisappear;

        [SerializeField, HideInInspector] private Transform moveTarget;
        [SerializeField, HideInInspector] private float moveSpeed = 3f;
        [SerializeField, HideInInspector] private float scaleUpAmount = 1.5f;
        [SerializeField, HideInInspector] private float scaleAnimationSpeed = 2f;
        [SerializeField, HideInInspector] private AudioSource jumpScareAudioSource;
        [SerializeField, HideInInspector] private AudioSource fightAudioSource;
        [SerializeField, HideInInspector] private Animator anomalyAnimator;
        [SerializeField, HideInInspector] private string moveTriggerName = "StartMove";
        [SerializeField, HideInInspector] private string idleTriggerName = "Idle";

        [AnomalyOption(AnomalyOptionAttribute.OptionKind.AnomalyType)]
        [SerializeField, HideInInspector] private string correctAnomalyType;


        /// <summary>The pre-AnomalyDefinition type string. Only read as a fallback while migrating.</summary>
        public string LegacyAnomalyType => correctAnomalyType;

        /// <summary>True once this anomaly's data lives in its Definition and sibling components.</summary>
        public bool IsMigrated => migrated;

        /// <summary>Hands the pre-split values to the components that own them now.</summary>
        private void SeedSiblingsFromLegacy(AnomalyMovement movement, AnomalyPresenter presenter,
                                            AnomalyThreatTimer threatTimer)
        {
            movement.ConfigureFromLegacy(moveTarget, moveSpeed, scaleUpAmount, scaleAnimationSpeed);
            presenter.ConfigureFromLegacy(anomalyAnimator, moveTriggerName, idleTriggerName,
                                          jumpScareAudioSource, fightAudioSource);
            threatTimer.ConfigureFromLegacy(timeToDisappear);
        }
    }
}
