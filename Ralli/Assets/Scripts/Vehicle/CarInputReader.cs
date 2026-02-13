using UnityEngine;
using UnityEngine.InputSystem;

public class CarInputReader : MonoBehaviour
{
    public float Steer { get; private set; }
    public float Throttle { get; private set; }
    public float Brake { get; private set; }
    public bool Handbrake { get; private set; }

    private void Update()
    {
        Vector2 moveInput = ReadMove();

        Steer = Mathf.Clamp(moveInput.x, -1f, 1f);
        float accelFromMove = Mathf.Clamp(moveInput.y, -1f, 1f);
        float triggerThrottle = ReadThrottleTrigger();
        float triggerBrake = ReadBrakeTrigger();

        Throttle = Mathf.Max(Mathf.Max(0f, accelFromMove), triggerThrottle);
        Brake = Mathf.Max(Mathf.Max(0f, -accelFromMove), triggerBrake);
        Handbrake = Keyboard.current != null && Keyboard.current.spaceKey.isPressed;
        if (Gamepad.current != null)
        {
            Handbrake |= Gamepad.current.buttonSouth.isPressed;
        }
    }

    private static Vector2 ReadMove()
    {
        Vector2 value = Vector2.zero;

        if (Keyboard.current != null)
        {
            if (Keyboard.current.aKey.isPressed || Keyboard.current.leftArrowKey.isPressed) value.x -= 1f;
            if (Keyboard.current.dKey.isPressed || Keyboard.current.rightArrowKey.isPressed) value.x += 1f;
            if (Keyboard.current.wKey.isPressed || Keyboard.current.upArrowKey.isPressed) value.y += 1f;
            if (Keyboard.current.sKey.isPressed || Keyboard.current.downArrowKey.isPressed) value.y -= 1f;
        }

        if (Gamepad.current != null)
        {
            Vector2 stick = Gamepad.current.leftStick.ReadValue();
            value.x = Mathf.Abs(stick.x) > Mathf.Abs(value.x) ? stick.x : value.x;
            value.y = Mathf.Abs(stick.y) > Mathf.Abs(value.y) ? stick.y : value.y;
        }

        return value;
    }

    private static float ReadThrottleTrigger()
    {
        if (Gamepad.current == null)
        {
            return 0f;
        }

        return Gamepad.current.rightTrigger.ReadValue();
    }

    private static float ReadBrakeTrigger()
    {
        if (Gamepad.current == null)
        {
            return 0f;
        }

        return Gamepad.current.leftTrigger.ReadValue();
    }
}
