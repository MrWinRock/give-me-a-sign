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
        RaycastHit2D hit = Physics2D.GetRayIntersection(ray);
        
        if (hit.collider.CompareTag("Cancel"))
        {
            return;    
        }
        
        infoPanel.SetActive(true);
        // สร้างเอฟเฟกต์ตรงจุดคลิก
        Vector3 worldPos = mainCamera.ScreenToWorldPoint(mousePos);
        worldPos.z = 0;
        Instantiate(clickEffectPrefab, worldPos, Quaternion.identity);

        if (hit.collider.CompareTag("LocationBackgound"))
        {
            infoText.text = "No Anomaly Detect!";
        }
        else if (hit.collider.CompareTag("Anomaly"))
        {
            infoText.text = "Anomaly Detect!";
            hit.collider.GetComponent<Anomaly>()?.Respond();
        }
        else
        {
            infoText.text = "คุณคลิกโดนอย่างอื่น!";
        }
    }
}
}
