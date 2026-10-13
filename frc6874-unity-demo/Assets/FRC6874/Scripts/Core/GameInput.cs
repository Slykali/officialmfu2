using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace FRC6874
{
    /// <summary>
    /// Every control the game reads, in one place. Works with the new Input System package
    /// (keyboard, mouse and any gamepad) and falls back to the legacy Input Manager
    /// (keyboard, mouse and basic gamepad buttons) when the new system is not active.
    ///
    /// Keyboard/mouse: WASD drive, Q/E or ←/→ turn, Space or LMB intake, F or RMB shoot,
    /// C climb, T auto-aim, G field/robot-centric, V camera, Left Shift precision, Esc pause,
    /// Enter confirm, H help.
    /// Gamepad: left stick drive, right stick turn, LT/RB intake, RT shoot, Y climb, X auto-aim,
    /// L3 drive mode, View/Back camera, LB precision, Menu/Start pause, A confirm.
    /// </summary>
    public static class GameInput
    {
        const float TriggerThreshold = 0.4f;
        const float StickDeadzone = 0.15f;

        /// <summary>Translation stick: x = right, y = forward. Magnitude is at most 1.</summary>
        public static Vector2 Move => Vector2.ClampMagnitude(ApplyDeadzone(RawMove()), 1f);

        /// <summary>Rotation: -1 = counter-clockwise (left), +1 = clockwise (right).</summary>
        public static float Turn => Mathf.Clamp(RawTurn(), -1f, 1f);

        public static bool IntakeHeld => IntakeHeldRaw();
        public static bool ShootHeld => ShootHeldRaw();
        public static bool PrecisionHeld => PrecisionHeldRaw();

        public static bool ClimbPressed => ClimbPressedRaw();
        public static bool AutoAimTogglePressed => AutoAimPressedRaw();
        public static bool DriveModeTogglePressed => DriveModePressedRaw();
        public static bool CameraCyclePressed => CameraPressedRaw();
        public static bool PausePressed => PausePressedRaw();
        public static bool ConfirmPressed => ConfirmPressedRaw();
        public static bool HelpTogglePressed => HelpPressedRaw();

        static Vector2 ApplyDeadzone(Vector2 v) => v.magnitude < StickDeadzone ? Vector2.zero : v;

#if ENABLE_INPUT_SYSTEM
        static bool Held(Key key) => Keyboard.current != null && Keyboard.current[key].isPressed;
        static bool Pressed(Key key) => Keyboard.current != null && Keyboard.current[key].wasPressedThisFrame;
        static Gamepad Pad => Gamepad.current;

        static Vector2 RawMove()
        {
            Vector2 v = Vector2.zero;
            if (Held(Key.W)) v.y += 1f;
            if (Held(Key.S)) v.y -= 1f;
            if (Held(Key.D)) v.x += 1f;
            if (Held(Key.A)) v.x -= 1f;
            if (Pad != null)
            {
                Vector2 stick = Pad.leftStick.ReadValue();
                if (stick.sqrMagnitude > v.sqrMagnitude) v = stick;
            }
            return v;
        }

        static float RawTurn()
        {
            float t = 0f;
            if (Held(Key.E) || Held(Key.RightArrow)) t += 1f;
            if (Held(Key.Q) || Held(Key.LeftArrow)) t -= 1f;
            if (Pad != null)
            {
                float stick = Pad.rightStick.ReadValue().x;
                if (Mathf.Abs(stick) > StickDeadzone && Mathf.Abs(stick) > Mathf.Abs(t)) t = stick;
            }
            return t;
        }

        static bool IntakeHeldRaw() =>
            Held(Key.Space) ||
            (Mouse.current != null && Mouse.current.leftButton.isPressed) ||
            (Pad != null && (Pad.leftTrigger.ReadValue() > TriggerThreshold || Pad.rightShoulder.isPressed));

        static bool ShootHeldRaw() =>
            Held(Key.F) ||
            (Mouse.current != null && Mouse.current.rightButton.isPressed) ||
            (Pad != null && Pad.rightTrigger.ReadValue() > TriggerThreshold);

        static bool PrecisionHeldRaw() =>
            Held(Key.LeftShift) || (Pad != null && Pad.leftShoulder.isPressed);

        static bool ClimbPressedRaw() =>
            Pressed(Key.C) || (Pad != null && Pad.buttonNorth.wasPressedThisFrame);

        static bool AutoAimPressedRaw() =>
            Pressed(Key.T) || (Pad != null && Pad.buttonWest.wasPressedThisFrame);

        static bool DriveModePressedRaw() =>
            Pressed(Key.G) || (Pad != null && Pad.leftStickButton.wasPressedThisFrame);

        static bool CameraPressedRaw() =>
            Pressed(Key.V) || (Pad != null && Pad.selectButton.wasPressedThisFrame);

        static bool PausePressedRaw() =>
            Pressed(Key.Escape) || Pressed(Key.P) ||
            (Pad != null && Pad.startButton.wasPressedThisFrame);

        static bool ConfirmPressedRaw() =>
            Pressed(Key.Enter) || Pressed(Key.NumpadEnter) ||
            (Pad != null && Pad.buttonSouth.wasPressedThisFrame);

        static bool HelpPressedRaw() =>
            Pressed(Key.H) || Pressed(Key.F1);
#else
        // Legacy Input Manager. Gamepad buttons follow the common XInput layout on Windows:
        // 0 A, 1 B, 2 X, 3 Y, 4 LB, 5 RB, 6 Back, 7 Start, 8 L3.
        static bool Held(KeyCode key) => Input.GetKey(key);
        static bool Pressed(KeyCode key) => Input.GetKeyDown(key);

        static Vector2 RawMove()
        {
            Vector2 v = Vector2.zero;
            bool keyboardInUse = false;
            if (Held(KeyCode.W)) { v.y += 1f; keyboardInUse = true; }
            if (Held(KeyCode.S)) { v.y -= 1f; keyboardInUse = true; }
            if (Held(KeyCode.D)) { v.x += 1f; keyboardInUse = true; }
            if (Held(KeyCode.A)) { v.x -= 1f; keyboardInUse = true; }
            bool arrowsInUse = Held(KeyCode.LeftArrow) || Held(KeyCode.RightArrow) ||
                               Held(KeyCode.UpArrow) || Held(KeyCode.DownArrow);
            if (!keyboardInUse && !arrowsInUse)
            {
                // With no keys held, the default "Horizontal"/"Vertical" axes come from the gamepad's left stick.
                v = new Vector2(Input.GetAxisRaw("Horizontal"), Input.GetAxisRaw("Vertical"));
            }
            return v;
        }

        static float RawTurn()
        {
            float t = 0f;
            if (Held(KeyCode.E) || Held(KeyCode.RightArrow) || Held(KeyCode.JoystickButton5)) t += 1f;
            if (Held(KeyCode.Q) || Held(KeyCode.LeftArrow) || Held(KeyCode.JoystickButton4)) t -= 1f;
            return t;
        }

        static bool IntakeHeldRaw() => Held(KeyCode.Space) || Input.GetMouseButton(0) || Held(KeyCode.JoystickButton0);
        static bool ShootHeldRaw() => Held(KeyCode.F) || Input.GetMouseButton(1) || Held(KeyCode.JoystickButton2);
        static bool PrecisionHeldRaw() => Held(KeyCode.LeftShift);
        static bool ClimbPressedRaw() => Pressed(KeyCode.C) || Pressed(KeyCode.JoystickButton3);
        static bool AutoAimPressedRaw() => Pressed(KeyCode.T);
        static bool DriveModePressedRaw() => Pressed(KeyCode.G) || Pressed(KeyCode.JoystickButton8);
        static bool CameraPressedRaw() => Pressed(KeyCode.V) || Pressed(KeyCode.JoystickButton6);
        static bool PausePressedRaw() => Pressed(KeyCode.Escape) || Pressed(KeyCode.P) || Pressed(KeyCode.JoystickButton7);
        static bool ConfirmPressedRaw() => Pressed(KeyCode.Return) || Pressed(KeyCode.KeypadEnter) || Pressed(KeyCode.JoystickButton0);
        static bool HelpPressedRaw() => Pressed(KeyCode.H) || Pressed(KeyCode.F1);
#endif
    }
}
