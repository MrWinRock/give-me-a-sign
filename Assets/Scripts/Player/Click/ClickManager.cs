using GameLogic;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem; // New Input System

namespace Player.Click
{
    public class ClickManager : MonoBehaviour
{
    [SerializeField] private Camera mainCamera;
    [SerializeField] private GameObject clickEffectPrefab;
    [SerializeField] private TextMeshProUGUI infoText;
    [SerializeField] private GameObject infoPanel;
    [SerializeField] private Transform anomalyTarget; // จุดที่ anomaly จะเคลื่อนมาหา

    private PlayerInputActions _inputActions;

    void Awake()
    {
        _inputActions = new PlayerInputActions();
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
        
        infoPanel.SetActive(true);
        // สร้างเอฟเฟกต์ตรงจุดคลิก
        Vector3 worldPos = mainCamera.ScreenToWorldPoint(mousePos);
        worldPos.z = 0;
        Instantiate(clickEffectPrefab, worldPos, Quaternion.identity);

        // Prioritize Anomaly over other colliders
        RaycastHit2D? anomalyHit = null;
        RaycastHit2D? backgroundHit = null;
        
        foreach (var hit in hits)
        {
            Debug.Log($"Hit object: {hit.collider.name} with tag: {hit.collider.tag}");
            
            if (hit.collider.CompareTag("Anomaly"))
            {
                anomalyHit = hit;
                break; // Anomaly has highest priority, stop looking
            }
            else if (hit.collider.CompareTag("LocationBackgound"))
            {
                backgroundHit = hit;
            }
        }

        // Handle based on priority
        if (anomalyHit.HasValue)
        {
            infoText.text = "Anomaly Detect!";
            anomalyHit.Value.collider.GetComponent<Anomaly>()?.Respond();
        }
        else if (backgroundHit.HasValue)
        {
            infoText.text = "No Anomaly Detect!";
        }
        else
        {
            infoText.text = "คุณคลิกโดนอย่างอื่น!";
        }
    }
}
}