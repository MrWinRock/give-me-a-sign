using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace UI
{
    /// <summary>
    /// Cross-scene mouse cursor: the normal arrow, switching to the action cursor while the pointer
    /// is over an interactable UI Selectable. World-space sprites/colliders are not detected.
    /// </summary>
    public class CursorManager : MonoBehaviour
    {
        [SerializeField] private Texture2D normalCursor;
        [SerializeField] private Texture2D actionCursor;
        [SerializeField] private Vector2 normalHotspot = new Vector2(5f, 3f);
        [SerializeField] private Vector2 actionHotspot = new Vector2(14f, 3f);

        private static CursorManager instance;

        private readonly List<RaycastResult> hits = new List<RaycastResult>(16);
        private PointerEventData pointerData;
        private bool? overAction;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Bootstrap()
        {
            if (instance != null) return;

            var prefab = Resources.Load<CursorManager>("CursorManager");
            if (prefab == null) return;

            instance = Instantiate(prefab);
            instance.name = "CursorManager";
            DontDestroyOnLoad(instance.gameObject);
        }

        void OnEnable()
        {
            overAction = null;
        }

        void Update()
        {
            bool action = IsOverInteractable();
            if (overAction == action) return;

            overAction = action;
            if (action)
                Cursor.SetCursor(actionCursor, actionHotspot, CursorMode.Auto);
            else
                Cursor.SetCursor(normalCursor, normalHotspot, CursorMode.Auto);
        }

        private bool IsOverInteractable()
        {
            var eventSystem = EventSystem.current;
            var mouse = Mouse.current;
            if (eventSystem == null || mouse == null) return false;

            if (pointerData == null)
                pointerData = new PointerEventData(eventSystem);
            pointerData.position = mouse.position.ReadValue();

            hits.Clear();
            eventSystem.RaycastAll(pointerData, hits);
            if (hits.Count == 0) return false;

            if (hits[0].gameObject.GetComponentInParent<NormalCursorZone>() != null) return false;

            var selectable = hits[0].gameObject.GetComponentInParent<Selectable>();
            return selectable != null && selectable.IsInteractable();
        }
    }
}
