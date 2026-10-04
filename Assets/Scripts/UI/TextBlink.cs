using DG.Tweening;
using TMPro;

namespace UI
{
    /// <summary>Flashes a text's alpha. The returned tween is linked to the text, so destroying it ends the flash.</summary>
    public static class TextBlink
    {
        public const float DefaultPeriod = 0.3f;

        // loops = -1 keeps flashing until the caller kills the tween.
        public static Tween Start(TMP_Text text, int loops = -1, float period = DefaultPeriod, float lowAlpha = 0.15f)
        {
            text.alpha = 1f;
            return DOTween.To(() => text.alpha, a => text.alpha = a, lowAlpha, period)
                .SetEase(Ease.InOutSine)
                .SetLoops(loops, LoopType.Yoyo)
                .SetUpdate(true)
                .SetLink(text.gameObject);
        }
    }
}
