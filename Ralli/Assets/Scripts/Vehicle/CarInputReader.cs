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
        float keyboardSteer = ReadKeyboardSteer();
        float keyboardAccel = ReadKeyboardAccel();
        float gamepadSteer = ReadGamepadSteer();
        float gamepadAccel = ReadGamepadAccel();

        float triggerThrottle = ReadThrottleTrigger();
        float triggerBrake = ReadBrakeTrigger();

        Steer = Mathf.Abs(gamepadSteer) > Mathf.Abs(keyboardSteer) ? gamepadSteer : keyboardSteer;

        bool usingTriggerPedals = triggerThrottle > 0.02f || triggerBrake > 0.02f;
        float accelFromMove = keyboardAccel;
        if (!usingTriggerPedals && Mathf.Abs(gamepadAccel) > Mathf.Abs(accelFromMove))
        {
            accelFromMove = gamepadAccel;
        }

        Throttle = Mathf.Max(Mathf.Max(0f, accelFromMove), triggerThrottle);
        Brake = Mathf.Max(Mathf.Max(0f, -accelFromMove), triggerBrake);
        Handbrake = Keyboard.current != null && Keyboard.current.spaceKey.isPressed;
        if (Gamepad.current != null)
        {
            Handbrake |= Gamepad.current.buttonSouth.isPressed;
        }
    }

    private static float ReadKeyboardSteer()
    {
        float value = 0f;

        if (Keyboard.current != null)
        {
            if (Keyboard.current.aKey.isPressed || Keyboard.current.leftArrowKey.isPressed) value -= 1f;
            if (Keyboard.current.dKey.isPressed || Keyboard.current.rightArrowKey.isPressed) value += 1f;
        }

        return Mathf.Clamp(value, -1f, 1f);
    }

    private static float ReadKeyboardAccel()
    {
        float value = 0f;

        if (Keyboard.current != null)
        {
            if (Keyboard.current.wKey.isPressed || Keyboard.current.upArrowKey.isPressed) value += 1f;
            if (Keyboard.current.sKey.isPressed || Keyboard.current.downArrowKey.isPressed) value -= 1f;
        }

        return Mathf.Clamp(value, -1f, 1f);
    }

    private static float ReadGamepadSteer()
    {
        if (Gamepad.current == null)
        {
            return 0f;
        }

        const float steerDeadzone = 0.15f;
        float x = Gamepad.current.leftStick.ReadValue().x;
        if (Mathf.Abs(x) < steerDeadzone)
        {
            return 0f;
        }

        return x;
    }

    private static float ReadGamepadAccel()
    {
        if (Gamepad.current == null)
        {
            return 0f;
        }

        const float accelDeadzone = 0.25f;
        const float verticalIntentMargin = 0.1f;

        Vector2 stick = Gamepad.current.leftStick.ReadValue();
        float y = stick.y;
        float absY = Mathf.Abs(y);
        float absX = Mathf.Abs(stick.x);

        if (absY < accelDeadzone)
        {
            return 0f;
        }

        if (absY < absX + verticalIntentMargin)
        {
            return 0f;
        }

        return y;
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
