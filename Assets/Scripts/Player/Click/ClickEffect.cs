using UnityEngine;

public class ClickEffect : MonoBehaviour
{
    public float duration = 0.5f; // เวลาที่ใช้ก่อนหายไป
    public float scaleUp = 1.5f;

    private float timer = 0f;
    private SpriteRenderer sr;
    private Color startColor;

    void Start()
    {
        sr = GetComponent<SpriteRenderer>();
        startColor = sr.color;
    }

    void Update()
    {
        timer += Time.deltaTime;
        float t = timer / duration;

        // ขยายขนาด
        transform.localScale = Vector3.Lerp(Vector3.one, Vector3.one * scaleUp, t);

        // ค่อย ๆ จางหาย
        sr.color = new Color(startColor.r, startColor.g, startColor.b, 1 - t);

        if (timer >= duration)
        {
            Destroy(gameObject);
        }
    }
}