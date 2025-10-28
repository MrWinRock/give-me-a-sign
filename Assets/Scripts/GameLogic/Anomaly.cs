using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using System.Collections;

public class Anomaly : MonoBehaviour
{
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
    [SerializeField] private bool destroyAfterDisappear = false; // จะลบ object ทิ้งไหม
    
    [Header("Scale Animation")]
    [SerializeField] private float scaleUpAmount = 1.5f; // ขยายเป็น 1.5 เท่า
    [SerializeField] private float scaleAnimationSpeed = 2f; // ความเร็วการขยาย

    [Header("Audio")]
    [SerializeField] private AudioSource jumpScareAudioSource; // AudioSource สำหรับเสียง anomaly
    
    private bool isMoving = false;
    private Vector3 originalScale;
    private bool canPrayDisappear = false; // Can disappear with spacebar
    private PrayUiManager prayManager;
    public float timeToDisappear;
    
    // Event fired when anomaly disappears (for scoring system)
    public System.Action<Anomaly> OnAnomalyDisappeared;

    void Start()
    {
        originalScale = transform.localScale;
        prayManager = FindObjectOfType<PrayUiManager>();
    }

    void Update()
    {
        // Check for spacebar input when anomaly is moving to target and can pray disappear
        if (canPrayDisappear && Keyboard.current.spaceKey.wasPressedThisFrame)
        {
            StopAllCoroutines();
            HandlePrayDisappear();
        }
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
        isMoving = true;
        
        // Enable prayer disappearing only for MoveToTargetThenDisappear type
        if (respondType == RespondType.MoveToTargetThenDisappear)
        {
            canPrayDisappear = true;
            // Show prayer UI
            if (prayManager != null)
            {
                prayManager.ShowPrayPanel();
                jumpScareAudioSource.Play();
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

        isMoving = false;
        
        if (disappearAfter)
        {
            // Keep prayer UI active and wait for spacebar input
            if (respondType == RespondType.MoveToTargetThenDisappear)
            {
                canPrayDisappear = true;
                // Wait 7 seconds then reload scene
                float timer = 0f;
                while (timer < timeToDisappear && canPrayDisappear)
                {
                    timer += Time.deltaTime;
                    yield return null;
                }
                
                // If 7 seconds passed without spacebar press, reload scene
                if (canPrayDisappear)
                {
                    SceneManager.LoadScene(SceneManager.GetActiveScene().name);
                    yield break;
                }
            }
            else
            {
                canPrayDisappear = false;
                yield return new WaitForSeconds(disappearDelay);
                HandleDisappear();
            }
        }
        else
        {
            canPrayDisappear = false;
            // For MoveOnly type, fire the disappear event for scoring even though it doesn't actually disappear
            if (respondType == RespondType.MoveOnly)
            {
                OnAnomalyDisappeared?.Invoke(this);
            }
        }
    }

    private IEnumerator ScaleUpAnimation()
    {
        Vector3 targetScale = originalScale * scaleUpAmount;
        
        while (Vector3.Distance(transform.localScale, targetScale) > 0.01f)
        {
            transform.localScale = Vector3.Lerp(transform.localScale, targetScale, scaleAnimationSpeed * Time.deltaTime);
            yield return null;
        }
        
        transform.localScale = targetScale;
    }

    private void HandleDisappear()
    {
        // Hide prayer UI
        if (prayManager != null)
            prayManager.HidePrayPanel();
            
        // Fire event before disappearing (for scoring system)
        OnAnomalyDisappeared?.Invoke(this);
        
        if (destroyAfterDisappear)
            Destroy(gameObject);
        else
            gameObject.SetActive(false);
    }

    private void HandlePrayDisappear()
    {
        canPrayDisappear = false;
        isMoving = false;
        HandleDisappear();
    }
}
