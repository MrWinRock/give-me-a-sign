using System.Collections;
using UnityEngine;
using TMPro;

public class TextShineEffect : MonoBehaviour
{
    [Header("Animation Settings")]
    [SerializeField] private AnimationType animationType = AnimationType.PulseShine;
    [SerializeField] private float animationSpeed = 1f;
    [SerializeField] private bool playOnStart = true;
    [SerializeField] private bool loop = true;
    
    [Header("Shine Colors")]
    [SerializeField] private Color baseColor = Color.white;
    [SerializeField] private Color shineColor = Color.yellow;
    [SerializeField] private Color darkenColor = Color.gray;
    
    [Header("Intensity Settings")]
    [SerializeField] private float maxShineIntensity = 2f;
    [SerializeField] private float minDarkenIntensity = 0.3f;
    
    [Header("Wave Settings (for Wave Shine)")]
    [SerializeField] private float waveFrequency = 2f;
    [SerializeField] private float waveAmplitude = 0.5f;
    
    private TextMeshProUGUI textMesh;
    private Color originalColor;
    private Coroutine animationCoroutine;
    
    public enum AnimationType
    {
        PulseShine,      // Pulses between base and shine color
        PulseDarken,     // Pulses between base and darken color
        ShineToDarken,   // Transitions from shine to darken continuously
        WaveShine,       // Wave-like shine effect
        FlickerShine,    // Random flicker between shine and base
        BreathingGlow    // Smooth breathing-like glow
    }
    
    private void Awake()
    {
        textMesh = GetComponent<TextMeshProUGUI>();
        if (textMesh == null)
        {
            Debug.LogError("TextShineEffect requires a TextMeshProUGUI component!");
            enabled = false;
            return;
        }
        
        originalColor = textMesh.color;
        if (baseColor == Color.white && originalColor != Color.white)
        {
            baseColor = originalColor;
        }
    }
    
    private void Start()
    {
        if (playOnStart)
        {
            StartAnimation();
        }
    }
    
    public void StartAnimation()
    {
        StopAnimation();
        
        switch (animationType)
        {
            case AnimationType.PulseShine:
                animationCoroutine = StartCoroutine(PulseShineAnimation());
                break;
            case AnimationType.PulseDarken:
                animationCoroutine = StartCoroutine(PulseDarkenAnimation());
                break;
            case AnimationType.ShineToDarken:
                animationCoroutine = StartCoroutine(ShineToDarkenAnimation());
                break;
            case AnimationType.WaveShine:
                animationCoroutine = StartCoroutine(WaveShineAnimation());
                break;
            case AnimationType.FlickerShine:
                animationCoroutine = StartCoroutine(FlickerShineAnimation());
                break;
            case AnimationType.BreathingGlow:
                animationCoroutine = StartCoroutine(BreathingGlowAnimation());
                break;
        }
    }
    
    public void StopAnimation()
    {
        if (animationCoroutine != null)
        {
            StopCoroutine(animationCoroutine);
            animationCoroutine = null;
        }
        
        // Reset to original color
        if (textMesh != null)
        {
            textMesh.color = originalColor;
        }
    }
    
    private IEnumerator PulseShineAnimation()
    {
        do
        {
            // Shine phase
            yield return StartCoroutine(TransitionColor(baseColor, shineColor, 1f / animationSpeed));
            // Back to base
            yield return StartCoroutine(TransitionColor(shineColor, baseColor, 1f / animationSpeed));
        } while (loop);
    }
    
    private IEnumerator PulseDarkenAnimation()
    {
        do
        {
            // Darken phase
            yield return StartCoroutine(TransitionColor(baseColor, darkenColor, 1f / animationSpeed));
            // Back to base
            yield return StartCoroutine(TransitionColor(darkenColor, baseColor, 1f / animationSpeed));
        } while (loop);
    }
    
    private IEnumerator ShineToDarkenAnimation()
    {
        do
        {
            // Shine to darken
            yield return StartCoroutine(TransitionColor(shineColor, darkenColor, 2f / animationSpeed));
            // Darken to shine
            yield return StartCoroutine(TransitionColor(darkenColor, shineColor, 2f / animationSpeed));
        } while (loop);
    }
    
    private IEnumerator WaveShineAnimation()
    {
        float time = 0f;
        
        do
        {
            time += Time.deltaTime * animationSpeed;
            
            // Create wave effect
            float wave = Mathf.Sin(time * waveFrequency) * waveAmplitude + 0.5f;
            Color currentColor = Color.Lerp(baseColor, shineColor, wave);
            
            // Apply intensity
            currentColor *= Mathf.Lerp(1f, maxShineIntensity, wave);
            currentColor.a = baseColor.a; // Preserve alpha
            
            textMesh.color = currentColor;
            
            yield return null;
        } while (loop);
    }
    
    private IEnumerator FlickerShineAnimation()
    {
        do
        {
            // Random flicker timing
            float flickerDuration = Random.Range(0.1f, 0.3f) / animationSpeed;
            float pauseDuration = Random.Range(0.2f, 0.8f) / animationSpeed;
            
            // Quick shine
            textMesh.color = shineColor;
            yield return new WaitForSeconds(flickerDuration);
            
            // Back to base
            textMesh.color = baseColor;
            yield return new WaitForSeconds(pauseDuration);
        } while (loop);
    }
    
    private IEnumerator BreathingGlowAnimation()
    {
        float time = 0f;
        
        do
        {
            time += Time.deltaTime * animationSpeed;
            
            // Smooth sine wave for breathing effect
            float breathe = (Mathf.Sin(time) + 1f) * 0.5f; // Normalize to 0-1
            
            Color currentColor = Color.Lerp(darkenColor, shineColor, breathe);
            
            // Apply intensity based on breathing
            float intensity = Mathf.Lerp(minDarkenIntensity, maxShineIntensity, breathe);
            currentColor *= intensity;
            currentColor.a = baseColor.a; // Preserve alpha
            
            textMesh.color = currentColor;
            
            yield return null;
        } while (loop);
    }
    
    private IEnumerator TransitionColor(Color fromColor, Color toColor, float duration)
    {
        float elapsed = 0f;
        
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / duration;
            
            // Smooth curve
            t = Mathf.SmoothStep(0f, 1f, t);
            
            Color currentColor = Color.Lerp(fromColor, toColor, t);
            textMesh.color = currentColor;
            
            yield return null;
        }
        
        textMesh.color = toColor;
    }
    
    // Public methods for external control
    public void SetAnimationType(AnimationType newType)
    {
        animationType = newType;
        if (animationCoroutine != null)
        {
            StartAnimation(); // Restart with new type
        }
    }
    
    public void SetColors(Color newBaseColor, Color newShineColor, Color newDarkenColor)
    {
        baseColor = newBaseColor;
        shineColor = newShineColor;
        darkenColor = newDarkenColor;
    }
    
    public void SetAnimationSpeed(float newSpeed)
    {
        animationSpeed = Mathf.Max(0.1f, newSpeed);
    }
    
    private void OnDisable()
    {
        StopAnimation();
    }
    
    private void OnDestroy()
    {
        StopAnimation();
    }
}
