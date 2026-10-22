using System.Linq;
using GameLogic.Save;
using TMPro;
using UI;
using UnityEngine;
using UnityEngine.UI;

namespace MainMenu
{
    /// <summary>
    /// One desktop icon: 32x32 image with a label underneath. Pure view + click forwarding -
    /// <see cref="DesktopManager"/> owns selection state and double-click timing, so an icon
    /// never has to know about its siblings.
    /// </summary>
    public class DesktopIcon : MonoBehaviour
    {
        [Header("Identity")]
        [SerializeField] private string label = "Start Shift";
        [Tooltip("What a double-click on this icon does.")]
        [SerializeField] private DesktopAction action = DesktopAction.StartShift;

        [Header("View")]
        [SerializeField] private Button button;
        [SerializeField] private Image iconImage;
        [SerializeField] private TextMeshProUGUI labelText;
        [Tooltip("Black copy of the label sitting 1px behind, for the XP drop shadow.")]
        [SerializeField] private TextMeshProUGUI labelShadowText;

        [Header("Selection Visual")]
        [Tooltip("Highlight quad behind the label/icon. Tinted with selectedColor while selected.")]
        [SerializeField] private Image selectionBackground;
        [Tooltip("1px dotted white outline shown only while selected.")]
        [SerializeField] private GameObject selectionOutline;
        [SerializeField] private Color selectedColor = XPPalette.Hex("#316AC5", 0.4f);
        [SerializeField] private Color deselectedColor = new Color(0f, 0f, 0f, 0f);

        [Header("Unread Badge (Mail icon only)")]
        [Tooltip("Optional. Left empty, a red dot is created at runtime on the icon's top-right corner.")]
        [SerializeField] private GameObject unreadBadge;
        [SerializeField] private Vector2 badgeSize = new Vector2(10f, 10f);
        [SerializeField] private Vector2 badgeOffset = new Vector2(-5.3f, 8.5f);

        private DesktopManager _desktop;
        private float _badgeTimer;

        public DesktopAction Action => action;
        public string Label => label;
        public bool IsSelected { get; private set; }

        void Awake()
        {
            SetSelected(false);
            EnsureBadge();
            RefreshBadge();
        }

        void OnEnable() => RefreshBadge();

        void Update()
        {
            if (action != DesktopAction.OpenMail) return;

            // The mailbox can be read while the desktop is up, so re-check twice a second.
            _badgeTimer -= Time.unscaledDeltaTime;
            if (_badgeTimer > 0f) return;
            _badgeTimer = 0.5f;
            RefreshBadge();
        }

        /// <summary>Red dot on only while a found document is still unread.</summary>
        public void RefreshBadge()
        {
            if (unreadBadge == null || action != DesktopAction.OpenMail) return;

            var save = SaveManager.Current;
            bool unread = save != null && save.foundMailIds.Any(id => !save.IsEmailRead(id));
            if (unreadBadge.activeSelf != unread) unreadBadge.SetActive(unread);
        }

        private void EnsureBadge()
        {
            if (action != DesktopAction.OpenMail || unreadBadge != null || iconImage == null) return;

            const int n = 32;
            var tex = new Texture2D(n, n, TextureFormat.RGBA32, false) { filterMode = FilterMode.Bilinear };
            var px = new Color32[n * n];
            for (int y = 0; y < n; y++)
                for (int x = 0; x < n; x++)
                {
                    float d = Mathf.Sqrt((x - 15.5f) * (x - 15.5f) + (y - 15.5f) * (y - 15.5f));
                    float a = Mathf.Clamp01(16f - d);                       // soft edge
                    var c = d > 12.5f ? new Color(1f, 1f, 1f, a) : new Color(0.88f, 0.19f, 0.19f, a);
                    px[y * n + x] = c;
                }
            tex.SetPixels32(px);
            tex.Apply();

            var go = new GameObject("UnreadBadge", typeof(RectTransform), typeof(Image));
            var rt = (RectTransform)go.transform;
            rt.SetParent(iconImage.transform, false);
            rt.anchorMin = rt.anchorMax = new Vector2(1f, 0f); // bottom-right corner of the icon
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = badgeSize;
            rt.anchoredPosition = badgeOffset; // position relative to the icon's bottom-right corner
            var img = go.GetComponent<Image>();
            img.sprite = Sprite.Create(tex, new Rect(0, 0, n, n), new Vector2(0.5f, 0.5f), 100f);
            img.raycastTarget = false;
            unreadBadge = go;
            go.SetActive(false);
        }

        public void Bind(DesktopManager desktop)
        {
            _desktop = desktop;

            if (button != null)
            {
                button.onClick.RemoveListener(OnClicked);
                button.onClick.AddListener(OnClicked);
            }
        }

        private void OnClicked()
        {
            _desktop?.OnIconClicked(this);
        }

        public void SetSelected(bool selected)
        {
            IsSelected = selected;

            if (selectionBackground != null)
                selectionBackground.color = selected ? selectedColor : deselectedColor;

            if (selectionOutline != null)
                selectionOutline.SetActive(selected);
        }

        public void SetIcon(Sprite sprite)
        {
            if (iconImage != null)
                iconImage.sprite = sprite;
        }
    }
}
