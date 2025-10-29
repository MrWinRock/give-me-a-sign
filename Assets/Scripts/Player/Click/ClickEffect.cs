using UnityEngine;

namespace Player.Click
{
    public class ClickEffect : MonoBehaviour
    {
        public float duration = 0.5f; // เวลาที่ใช้ก่อนหายไป
        public float scaleUp = 1.5f;

        private float _timer;
        private SpriteRenderer _sr;
        private Color _startColor;

        void Start()
        {
            _sr = GetComponent<SpriteRenderer>();
            _startColor = _sr.color;
        }

        void Update()
        {
            _timer += Time.deltaTime;
            float t = _timer / duration;

            // ขยายขนาด
            transform.localScale = Vector3.Lerp(Vector3.one, Vector3.one * scaleUp, t);

            // ค่อย ๆ จางหาย
            _sr.color = new Color(_startColor.r, _startColor.g, _startColor.b, 1 - t);

            if (_timer >= duration)
            {
                Destroy(gameObject);
            }
        }
    }
}