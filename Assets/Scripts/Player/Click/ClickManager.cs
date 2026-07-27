using GameLogic;
using Report;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem; // New Input System

namespace Player.Click
{
    public class ClickManager : MonoBehaviour
{
    [SerializeField] private Camera mainCamera;
    [SerializeField] private GameObject clickEffectPrefab;
    [SerializeField] private Transform anomalyTarget; // จุดที่ anomaly จะเคลื่อนมาหา
    [SerializeField] private GameManager gameManager;
    [SerializeField] private bool autoFindGameManager = true;

    private PlayerInputActions _inputActions;

    // Unity explicitly warns against calling EventSystem.IsPointerOverGameObject() from inside
    // an InputAction callback (like OnClick below) - at that point it can only see last frame's
    // UI state, not the current one. So instead we sample it once per frame in Update() and use
    // the cached value in OnClick, which is the pattern Unity's own docs recommend.
    private bool _pointerOverUI;

    void Awake()
    {
        _inputActions = new PlayerInputActions();
    }

    void Start()
    {
        if (autoFindGameManager && gameManager == null)
            gameManager = FindObjectOfType<GameManager>();
    }

    void Update()
    {
        _pointerOverUI = EventSystem.current != null && EventSystem.current.IsPointerOverGameObject();
    }

    void OnEnable()
    {
        _inputActions.Player.Click.performed += OnClick;
        _inputActions.Player.Enable();
    }

    void OnDisable()
    {
        _inputActions.Player.Click.performed -= OnClick;
        _inputActions.Player.Disable();
    }

    private void OnClick(InputAction.CallbackContext ctx)
    {
        // Don't raycast into the game world while a modal UI (Incident Report window, etc.)
        // is up - without this, clicking a button/dropdown inside the window ALSO fired a
        // world raycast at the same screen position, which could re-trigger anomaly
        // detection underneath the window on every single click.
        if (_pointerOverUI)
            return;

        if (gameManager != null && gameManager.inputLocked)
            return;

        Vector2 mousePos = Mouse.current.position.ReadValue();
        Ray ray = mainCamera.ScreenPointToRay(mousePos);
        RaycastHit2D[] hits = Physics2D.GetRayIntersectionAll(ray);

        // Check if we hit anything
        if (hits.Length == 0)
        {
            return; // No hit, do nothing
        }

        // Check for Cancel tag first
        foreach (var hit in hits)
        {
            if (hit.collider.CompareTag("Cancel"))
            {
                return;
            }
        }
        
        // สร้างเอฟเฟกต์ตรงจุดคลิก
        Vector3 worldPos = mainCamera.ScreenToWorldPoint(mousePos);
        worldPos.z = 0;
        Instantiate(clickEffectPrefab, worldPos, Quaternion.identity);

        // Anomaly takes priority over any other collider (e.g. the room background) hit at the same point.
        foreach (var hit in hits)
        {
            if (hit.collider.CompareTag("Anomaly"))
            {
                var anomaly = hit.collider.GetComponent<Anomaly>();
                if (anomaly != null)
                    IncidentReportManager.Instance?.OpenReport(anomaly);
                break;
            }
        }
    }
}
}