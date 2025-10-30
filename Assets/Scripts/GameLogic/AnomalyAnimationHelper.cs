using UnityEngine;

namespace GameLogic
{
    /// <summary>
    /// Additional animation helper for anomaly objects
    /// This script can be added alongside the main Anomaly script for enhanced visual effects
    /// </summary>
    public class AnomalyAnimationHelper : MonoBehaviour
    {
        [Header("Visual Effects")]
        [SerializeField] private bool enableFlickerEffect = true;
        [SerializeField] private float flickerSpeed = 5f;
        [SerializeField] private float flickerIntensity = 0.3f;
        
        [Header("Movement Effects")]
        [SerializeField] private bool enableFloatEffect = true;
        [SerializeField] private float floatAmplitude = 0.1f;
        [SerializeField] private float floatSpeed = 2f;
        
        [Header("Color Effects")]
        [SerializeField] private bool enableColorShift = true;
        [SerializeField] private Color normalColor = Color.white;
        [SerializeField] private Color anomalyColor = Color.red;
        [SerializeField] private float colorShiftSpeed = 1f;
        
        private SpriteRenderer spriteRenderer;
        private Vector3 originalPosition;
        private float timeCounter;
        private bool isAnimating = false;
        
        void Start()
        {
            spriteRenderer = GetComponent<SpriteRenderer>();
            originalPosition = transform.position;
            
            if (spriteRenderer == null)
            {
                Debug.LogWarning($"AnomalyAnimationHelper on {name} requires a SpriteRenderer component!");
            }
        }
        
        void Update()
        {
            if (!isAnimating) return;
            
            timeCounter += Time.deltaTime;
            
            // Flicker effect
            if (enableFlickerEffect && spriteRenderer != null)
            {
                float alpha = 1f - (Mathf.Sin(timeCounter * flickerSpeed) * flickerIntensity);
                Color currentColor = spriteRenderer.color;
                currentColor.a = Mathf.Clamp01(alpha);
                spriteRenderer.color = currentColor;
            }
            
            // Float effect
            if (enableFloatEffect)
            {
                float yOffset = Mathf.Sin(timeCounter * floatSpeed) * floatAmplitude;
                Vector3 newPosition = originalPosition;
                newPosition.y += yOffset;
                transform.position = newPosition;
            }
            
            // Color shift effect
            if (enableColorShift && spriteRenderer != null)
            {
                float colorLerp = (Mathf.Sin(timeCounter * colorShiftSpeed) + 1f) * 0.5f;
                Color targetColor = Color.Lerp(normalColor, anomalyColor, colorLerp);
                targetColor.a = spriteRenderer.color.a; // Preserve alpha from flicker effect
                spriteRenderer.color = targetColor;
            }
        }
        
        /// <summary>
        /// Start the animation effects
        /// Call this when the anomaly begins moving
        /// </summary>
        public void StartAnimationEffects()
        {
            isAnimating = true;
            originalPosition = transform.position;
            timeCounter = 0f;
        }
        
        /// <summary>
        /// Stop the animation effects
        /// Call this when the anomaly is banished or disappears
        /// </summary>
        public void StopAnimationEffects()
        {
            isAnimating = false;
            
            // Reset to original state
            if (spriteRenderer != null)
            {
                Color resetColor = normalColor;
                resetColor.a = 1f;
                spriteRenderer.color = resetColor;
            }
            
            transform.position = originalPosition;
        }
        
        /// <summary>
        /// Fade out effect for banishing
        /// </summary>
        public void FadeOut(float duration = 1f)
        {
            StartCoroutine(FadeOutCoroutine(duration));
        }
        
        private System.Collections.IEnumerator FadeOutCoroutine(float duration)
        {
            if (spriteRenderer == null) yield break;
            
            Color startColor = spriteRenderer.color;
            float elapsedTime = 0f;
            
            while (elapsedTime < duration)
            {
                elapsedTime += Time.deltaTime;
                float alpha = Mathf.Lerp(startColor.a, 0f, elapsedTime / duration);
                
                Color currentColor = spriteRenderer.color;
                currentColor.a = alpha;
                spriteRenderer.color = currentColor;
                
                yield return null;
            }
            
            // Ensure fully transparent
            Color finalColor = spriteRenderer.color;
            finalColor.a = 0f;
            spriteRenderer.color = finalColor;
        }
    }
}
