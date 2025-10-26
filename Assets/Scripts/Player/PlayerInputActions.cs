using UnityEngine;
using UnityEngine.InputSystem;

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
}
