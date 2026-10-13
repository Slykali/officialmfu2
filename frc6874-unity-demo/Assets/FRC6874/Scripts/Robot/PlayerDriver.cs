using UnityEngine;

namespace FRC6874
{
    /// <summary>
    /// Turns <see cref="GameInput"/> into robot commands for the human robot. Input is sampled every
    /// frame in <see cref="Poll"/> (so one-frame "pressed" events are latched, never missed) and
    /// applied on the next physics step through <see cref="Fill"/>.
    /// </summary>
    public sealed class PlayerDriver
    {
        readonly Robot robot;
        Vector2 move;
        float turn;
        bool intake, shoot, precision, climbPressed;

        public PlayerDriver(Robot robot)
        {
            this.robot = robot;
        }

        /// <summary>True while the precision (slow) button is held.</summary>
        public bool PrecisionHeld => precision;

        /// <summary>Samples the controller. Called from the robot's Update.</summary>
        public void Poll()
        {
            if (!robot.IsHumanDriven || !MatchState.DriverControlEnabled)
            {
                ClearHeld();
                climbPressed = false;
                return;
            }
            if (Time.timeScale <= 0f) return; // paused: buttons belong to the pause menu

            move = GameInput.Move;
            turn = GameInput.Turn;
            intake = GameInput.IntakeHeld;
            shoot = GameInput.ShootHeld;
            precision = GameInput.PrecisionHeld;
            if (GameInput.ClimbPressed) climbPressed = true;

            if (GameInput.AutoAimTogglePressed)
            {
                robot.AutoAim = !robot.AutoAim;
                robot.ShowHint(robot.AutoAim ? "AUTO-AIM ON" : "AUTO-AIM OFF", 1.2f);
            }
            if (GameInput.DriveModeTogglePressed)
            {
                robot.DriveMode = robot.DriveMode == DriveMode.FieldCentric ? DriveMode.RobotCentric : DriveMode.FieldCentric;
                robot.ShowHint(robot.DriveMode == DriveMode.FieldCentric ? "FIELD-CENTRIC" : "ROBOT-CENTRIC", 1.2f);
            }
        }

        /// <summary>Writes the latest input into a command and consumes latched button presses.</summary>
        public void Fill(ref RobotCommand cmd)
        {
            float scale = precision ? RobotTuning.PrecisionScale : 1f;
            Vector3 direction = robot.DriveMode == DriveMode.FieldCentric
                ? ControlFrame.StickToWorld(move)
                : robot.Right * move.x + robot.Forward * move.y;
            direction.y = 0f;

            cmd.Velocity = direction * (robot.MaxSpeed * scale);
            cmd.TurnRate = turn * GameConfig.RobotTurnRate * (precision ? 0.5f : 1f);
            cmd.Intake = intake;
            cmd.Shoot = shoot;
            cmd.SpinUp = shoot;
            cmd.Climb = climbPressed;
            climbPressed = false;
        }

        /// <summary>Drops any latched button press (phase changes, disabled robot).</summary>
        public void ClearLatched() => climbPressed = false;

        void ClearHeld()
        {
            move = Vector2.zero;
            turn = 0f;
            intake = shoot = precision = false;
        }
    }
}
