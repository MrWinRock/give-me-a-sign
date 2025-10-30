using System.Collections;
using System.Collections.Generic;
using Pray;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace GameLogic
{
    public class Anomaly : MonoBehaviour
    {
        // Static collection to track all active anomalies
        private static readonly List<Anomaly> _activeAnomalies = new List<Anomaly>();
        public static IReadOnlyList<Anomaly> ActiveAnomalies => _activeAnomalies;

        public enum RespondType
        {
            DisappearInstantly,  // หายทันที
            MoveToTargetThenDisappear, // เคลื่อนไปหา target แล้วค่อยหาย
            MoveOnly             // แค่เคลื่อนไปหา target ไม่หาย
        }

        [Header("Respond Settings")]
        public RespondType respondType = RespondType.MoveToTargetThenDisappear;

        [SerializeField] private Transform moveTarget; // Empty GameObject ที่กำหนดจาก Hierarchy
        [SerializeField] private float moveSpeed = 3f; // ความเร็วในการเคลื่อน
        [SerializeField] private float disappearDelay = 0.5f; // เวลาหลังถึงเป้าหมายก่อนหาย (ถ้ามี)
        [SerializeField] private bool destroyAfterDisappear ; // จะลบ object ทิ้งไหม
    
        [Header("Scale Animation")]
        [SerializeField] private float scaleUpAmount = 1.5f; // ขยายเป็น 1.5 เท่า
        [SerializeField] private float scaleAnimationSpeed = 2f; // ความเร็วการขยาย

        public GameObject cutsceneCheck;
        
    [Header("Audio")]
    [SerializeField] private AudioSource jumpScareAudioSource;
    [SerializeField] private AudioSource fightAudioSource;// AudioSource สำหรับเสียง anomaly
    
    [Header("Animation")]
    [SerializeField] private Animator anomalyAnimator; // Animator component for anomaly animations
    [SerializeField] private string moveTriggerName = "StartMove"; // Animation trigger name when starting to move
    [SerializeField] private string idleTriggerName = "Idle"; // Animation trigger name when idle/banished
    [SerializeField] private AnomalyAnimationHelper animationHelper; // Optional helper for additional effects
    
    private bool _isMoving;
        private Vector3 _originalScale;
        private bool _canPrayDisappear; // Can disappear with voice prayer
        private PrayUiManager _prayManager;
        public float timeToDisappear;
    
        // Event fired when anomaly disappears (for scoring system)
        public System.Action<Anomaly> OnAnomalyDisappeared;

        void Start()
        {
            _originalScale = transform.localScale;
            _prayManager = FindObjectOfType<PrayUiManager>();
            
            // Get animator component if not assigned
            if (anomalyAnimator == null)
                anomalyAnimator = GetComponent<Animator>();
                
            // Get animation helper if not assigned
            if (animationHelper == null)
                animationHelper = GetComponent<AnomalyAnimationHelper>();
        }

        void OnEnable()
        {
            // Add this anomaly to the active list when enabled
            if (!_activeAnomalies.Contains(this))
            {
                _activeAnomalies.Add(this);
            }
        }

        void OnDisable()
        {
            // Remove this anomaly from the active list when disabled
            _activeAnomalies.Remove(this);
        }

        void OnDestroy()
        {
            // Remove this anomaly from the active list when destroyed
            _activeAnomalies.Remove(this);
        }

        public void Respond()
        {
            StartCoroutine(DelayedRespond());
        }

        private IEnumerator DelayedRespond()
        {
            yield return new WaitForSeconds(4f); // Wait 4 seconds

            switch (respondType)
            {
                case RespondType.DisappearInstantly:
                    HandleDisappear();
                    break;

                case RespondType.MoveToTargetThenDisappear:
                    if (moveTarget != null)
                        StartCoroutine(MoveToTargetCoroutine(true));
                    else
                        Debug.LogWarning($"{name} has no target assigned!");
                    break;

                case RespondType.MoveOnly:
                    if (moveTarget != null)
                        StartCoroutine(MoveToTargetCoroutine(false));
                    else
                        Debug.LogWarning($"{name} has no target assigned!");
                    break;
            }
        }

        private IEnumerator MoveToTargetCoroutine(bool disappearAfter)
        {
            _isMoving = true;
            
            // Trigger movement animation
            if (anomalyAnimator != null && !string.IsNullOrEmpty(moveTriggerName))
            {
                anomalyAnimator.SetTrigger(moveTriggerName);
                Debug.Log($"Triggered animation: {moveTriggerName} for anomaly {name}");
            }
            
            // Start additional animation effects
            if (animationHelper != null)
            {
                animationHelper.StartAnimationEffects();
            }
        
            // Enable prayer disappearing only for MoveToTargetThenDisappear type
            if (respondType == RespondType.MoveToTargetThenDisappear)
            {
                _canPrayDisappear = true;
                // Show prayer UI
                if (_prayManager != null)
                {
                    _prayManager.ShowPrayPanel();
                    jumpScareAudioSource.Play();
                    yield return new WaitForSeconds(0.2f);
                    fightAudioSource.Play();
                }
            }
        
            // Start scale up animation
            StartCoroutine(ScaleUpAnimation());

            while (moveTarget != null && Vector3.Distance(transform.position, moveTarget.position) > 0.05f)
            {
                transform.position = Vector3.MoveTowards(
                    transform.position,
                    moveTarget.position,
                    moveSpeed * Time.deltaTime
                );
                yield return null;
            }

            _isMoving = false;
        
            if (disappearAfter)
            {
                // Keep prayer UI active and wait for voice input (prayer)
                if (respondType == RespondType.MoveToTargetThenDisappear)
                {
                    _canPrayDisappear = true;
                
                    // Wait for timeToDisappear seconds then reload scene if not banished by prayer
                    float timer = 0f;
                    while (timer < timeToDisappear && _canPrayDisappear && gameObject.activeInHierarchy)
                    {
                        timer += Time.deltaTime;
                        yield return null;
                    }
                
                    // Double check: If canPrayDisappear is still true and object is still active, reload scene
                    if (_canPrayDisappear && gameObject.activeInHierarchy)
                    {
                        Debug.Log($"Anomaly {name} timeout reached. Reloading scene...");
                        SceneManager.LoadScene(SceneManager.GetActiveScene().name);
                    }
                }
                else
                {
                    _canPrayDisappear = false;
                    yield return new WaitForSeconds(disappearDelay);
                    HandleDisappear();
                }
            }
            else
            {
                _canPrayDisappear = false;
                // For MoveOnly type, fire the disappear event for scoring even though it doesn't actually disappear
                if (respondType == RespondType.MoveOnly)
                {
                    OnAnomalyDisappeared?.Invoke(this);
                }
            }
        }

        private IEnumerator ScaleUpAnimation()
        {
            Vector3 targetScale = _originalScale * scaleUpAmount;
        
            while (Vector3.Distance(transform.localScale, targetScale) > 0.01f)
            {
                transform.localScale = Vector3.Lerp(transform.localScale, targetScale, scaleAnimationSpeed * Time.deltaTime);
                yield return null;
            }
        
            transform.localScale = targetScale;
        }

        private void HandleDisappear()
        {
            // Trigger idle/banished animation
            if (anomalyAnimator != null && !string.IsNullOrEmpty(idleTriggerName))
            {
                anomalyAnimator.SetTrigger(idleTriggerName);
                Debug.Log($"Triggered animation: {idleTriggerName} for anomaly {name} - Banished");
            }
            
            // Stop additional animation effects and fade out
            if (animationHelper != null)
            {
                animationHelper.StopAnimationEffects();
                animationHelper.FadeOut(0.5f); // Fade out over 0.5 seconds
            }
            
            
            // Hide prayer UI
            if (_prayManager != null)
                _prayManager.HidePrayPanel();
            
            // Fire event before disappearing (for scoring system)
            OnAnomalyDisappeared?.Invoke(this);
        
            if (destroyAfterDisappear)
                Destroy(gameObject, 0.6f); // Delay destruction to allow fade out
            else
                StartCoroutine(DelayedDeactivate(0.6f));
        }
        
        private System.Collections.IEnumerator DelayedDeactivate(float delay)
        {
            yield return new WaitForSeconds(delay);
            gameObject.SetActive(false);
        }

        /// <summary>
        /// Public method for VoiceCommandRouter to call when prayer is successful
        /// </summary>
        public void OnPrayerSuccessful()
        {
            if (_canPrayDisappear && respondType == RespondType.MoveToTargetThenDisappear)
            {
                Debug.Log($"Prayer successful for anomaly {name}. Banishing...");
            
                // Set flag first to prevent scene reload
                _canPrayDisappear = false;
            
                // Stop all coroutines to prevent timeout
                StopAllCoroutines();
                
                fightAudioSource.Stop();
                // Handle disappearing
                HandleDisappear();
            }
        }

        /// <summary>
        /// Check if this anomaly can be banished by prayer
        /// </summary>
        public bool CanBePrayerBanished()
        {
            return _canPrayDisappear && respondType == RespondType.MoveToTargetThenDisappear;
        }
    }
}
