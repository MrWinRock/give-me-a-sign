using UnityEngine;
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

    private bool isMoving = false;
    private Vector3 originalScale;

    void Start()
    {
        originalScale = transform.localScale;
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
            yield return new WaitForSeconds(disappearDelay);
            HandleDisappear();
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
        if (destroyAfterDisappear)
            Destroy(gameObject);
        else
            gameObject.SetActive(false);
    }
}
