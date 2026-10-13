using UnityEngine;

namespace FRC6874
{
    public enum ClimbState
    {
        /// <summary>Arm down, robot driving normally.</summary>
        Stowed,
        /// <summary>Arm reaching up (robot stopped, body kinematic).</summary>
        Extending,
        /// <summary>Robot pulling itself up.</summary>
        Lifting,
        /// <summary>Climb complete; the robot hangs until the match is reset.</summary>
        Hanging,
        /// <summary>Coming back down (practice / attract mode only).</summary>
        Lowering,
    }

    /// <summary>
    /// Endgame climber: extends a telescoping arm, then lifts the robot <see cref="GameConfig.ClimbHeight"/>
    /// over the rest of <see cref="GameConfig.ClimbDuration"/>. The body is kinematic while on the tower.
    /// The climb pauses while robots are disabled (like a real disabled robot).
    /// </summary>
    public sealed class Climber
    {
        readonly Robot robot;
        Vector3 basePosition;
        float timer;
        float lowerStartExtension;

        public Climber(Robot robot)
        {
            this.robot = robot;
        }

        public ClimbState State { get; private set; } = ClimbState.Stowed;
        /// <summary>Seconds spent in the current state.</summary>
        public float StateTime => timer;
        /// <summary>Meters the arm's inner stage is raised above its stowed height.</summary>
        public float ArmExtension { get; private set; }
        /// <summary>Meters the robot is lifted off the carpet.</summary>
        public float Lift { get; private set; }
        /// <summary>True whenever the climber has left the stowed state (the robot cannot drive).</summary>
        public bool IsActive => State != ClimbState.Stowed;

        float ExtendTime => Mathf.Max(0.05f, GameConfig.ClimbDuration * RobotTuning.ClimbExtendFraction);
        float LiftTime => Mathf.Max(0.05f, GameConfig.ClimbDuration - ExtendTime);

        /// <summary>Stops the robot, makes it kinematic and starts extending the arm.</summary>
        public void Begin()
        {
            if (State != ClimbState.Stowed) return;
            basePosition = robot.Body.position;
            basePosition.y = 0f;
            robot.SetBodyKinematic(true);
            State = ClimbState.Extending;
            timer = 0f;
            Lift = 0f;
        }

        /// <summary>Starts lowering a hanging robot back onto the carpet.</summary>
        public void BeginLowering()
        {
            if (State != ClimbState.Hanging) return;
            State = ClimbState.Lowering;
            timer = 0f;
            lowerStartExtension = ArmExtension;
        }

        public void Tick(float dt, bool robotsEnabled)
        {
            if (State == ClimbState.Stowed || !robotsEnabled) return;
            timer += dt;

            switch (State)
            {
                case ClimbState.Extending:
                    ArmExtension = RobotTuning.ClimbArmReach * Smooth(timer / ExtendTime);
                    if (timer >= ExtendTime)
                    {
                        State = ClimbState.Lifting;
                        timer = 0f;
                    }
                    break;

                case ClimbState.Lifting:
                {
                    float u = Smooth(timer / LiftTime);
                    Lift = GameConfig.ClimbHeight * u;
                    // The hook stays put while the robot pulls itself up toward it.
                    ArmExtension = Mathf.Max(0f, RobotTuning.ClimbArmReach - Lift);
                    robot.Body.MovePosition(basePosition + Vector3.up * Lift);
                    if (timer >= LiftTime)
                    {
                        State = ClimbState.Hanging;
                        timer = 0f;
                        robot.OnClimbComplete();
                    }
                    break;
                }

                case ClimbState.Lowering:
                {
                    float duration = Mathf.Max(0.05f, RobotTuning.ClimbLowerDuration);
                    float u = Smooth(timer / duration);
                    Lift = GameConfig.ClimbHeight * (1f - u);
                    ArmExtension = Mathf.Lerp(lowerStartExtension, 0f, u);
                    robot.Body.MovePosition(basePosition + Vector3.up * Lift);
                    if (timer >= duration)
                    {
                        State = ClimbState.Stowed;
                        timer = 0f;
                        Lift = 0f;
                        ArmExtension = 0f;
                        robot.SetBodyKinematic(false);
                        robot.Body.position = basePosition;
                        robot.OnClimberStowed();
                    }
                    break;
                }
            }
        }

        /// <summary>Back to stowed (the robot restores its body itself in ResetForMatch).</summary>
        public void ResetState()
        {
            State = ClimbState.Stowed;
            timer = 0f;
            Lift = 0f;
            ArmExtension = 0f;
        }

        static float Smooth(float x) => Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(x));
    }
}
