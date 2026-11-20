using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

/// <summary>All keyboard / mouse / gamepad input in one place (works with the new Input System).</summary>
public static class GameInput
{
    public static Vector2 Move
    {
        get
        {
#if ENABLE_INPUT_SYSTEM
            Vector2 v = Vector2.zero;
            Keyboard k = Keyboard.current;
            if (k != null)
            {
                if (k.leftArrowKey.isPressed || k.aKey.isPressed) v.x -= 1f;
                if (k.rightArrowKey.isPressed || k.dKey.isPressed) v.x += 1f;
                if (k.downArrowKey.isPressed || k.sKey.isPressed) v.y -= 1f;
                if (k.upArrowKey.isPressed || k.wKey.isPressed) v.y += 1f;
            }
            Gamepad g = Gamepad.current;
            if (g != null)
            {
                Vector2 s = g.leftStick.ReadValue();
                if (s.sqrMagnitude > v.sqrMagnitude) v = s;
            }
            return Vector2.ClampMagnitude(v, 1f);
#else
            return Vector2.ClampMagnitude(new Vector2(Input.GetAxisRaw("Horizontal"), Input.GetAxisRaw("Vertical")), 1f);
#endif
        }
    }

    public static bool FireHeld
    {
        get
        {
#if ENABLE_INPUT_SYSTEM
            Keyboard k = Keyboard.current;
            Mouse m = Mouse.current;
            Gamepad g = Gamepad.current;
            return (k != null && k.spaceKey.isPressed)
                || (m != null && m.leftButton.isPressed)
                || (g != null && g.buttonSouth.isPressed);
#else
            return Input.GetKey(KeyCode.Space) || Input.GetMouseButton(0);
#endif
        }
    }

    public static bool MissilePressed
    {
        get
        {
#if ENABLE_INPUT_SYSTEM
            Keyboard k = Keyboard.current;
            Mouse m = Mouse.current;
            Gamepad g = Gamepad.current;
            return (k != null && (k.mKey.wasPressedThisFrame || k.leftCtrlKey.wasPressedThisFrame))
                || (m != null && m.rightButton.wasPressedThisFrame)
                || (g != null && g.buttonEast.wasPressedThisFrame);
#else
            return Input.GetKeyDown(KeyCode.M) || Input.GetKeyDown(KeyCode.LeftControl) || Input.GetMouseButtonDown(1);
#endif
        }
    }

    public static bool RestartPressed
    {
        get
        {
#if ENABLE_INPUT_SYSTEM
            Keyboard k = Keyboard.current;
            Gamepad g = Gamepad.current;
            return (k != null && (k.rKey.wasPressedThisFrame || k.enterKey.wasPressedThisFrame))
                || (g != null && g.startButton.wasPressedThisFrame);
#else
            return Input.GetKeyDown(KeyCode.R) || Input.GetKeyDown(KeyCode.Return);
#endif
        }
    }
}
