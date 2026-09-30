using UnityEngine;

public class CarInputReader : MonoBehaviour
{
    public float Steer { get; private set; }
    public float Throttle { get; private set; }
    public float Brake { get; private set; }
    public bool Handbrake { get; private set; }
    public bool Overdrive { get; private set; }
    // -1 = look left, 1 = look right.
    public float CameraLook { get; private set; }
    // Which bound control currently drives Steer (e.g. "/XInputControllerOSX/leftStick/x"). Debug aid.
    public string SteerSourcePath => actions?.Driving.Steer.activeControl?.path ?? "-";

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
        Overdrive = actions.Driving.Boost.IsPressed(); // Input action is still named "Boost".
        CameraLook = actions.Driving.Camera.ReadValue<float>();
    }
}
