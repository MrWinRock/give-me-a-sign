using UnityEngine.InputSystem;

namespace Player
{
    [System.Serializable]
    public class PlayerInputActions
    {
        public PlayerActions Player { get; private set; }
    
        public PlayerInputActions()
        {
            Player = new PlayerActions();
        }
    
        public void Enable()
        {
            Player.Enable();
        }
    
        public void Disable()
        {
            Player.Disable();
        }

        public void Dispose()
        {
            Player.Dispose();
        }
    }

    [System.Serializable]
    public class PlayerActions
    {
        public InputAction Click { get; private set; }
    
        public PlayerActions()
        {
            Click = new InputAction(binding: "<Mouse>/leftButton");
        }
    
        public void Enable()
        {
            Click.Enable();
        }
    
        public void Disable()
        {
            Click.Disable();
        }

        public void Dispose()
        {
            Click.Dispose();
        }
    }
}