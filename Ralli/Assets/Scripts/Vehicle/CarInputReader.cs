using UnityEngine;

public class CarInputReader : MonoBehaviour
{
    public float Steer { get; private set; }
    public float Throttle { get; private set; }
    public float Brake { get; private set; }
    public bool Handbrake { get; private set; }
    public bool Boost { get; private set; }

    private InputSystem_Actions actions;

    private void OnEnable()
    {
        actions = new InputSystem_Actions();
        actions.Driving.Enable();
    }

    private void OnDisable()
    {
        actions.Driving.Disable();
        actions.Dispose();
    }

    private void Update()
    {
        Steer = actions.Driving.Steer.ReadValue<float>();
        Throttle = actions.Driving.Throttle.ReadValue<float>();
        Brake = actions.Driving.Brake.ReadValue<float>();
        Handbrake = actions.Driving.Handbrake.IsPressed();
        Boost = actions.Driving.Boost.IsPressed();
    }
}
