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
    /// Endgame climber: while the telescoping arm extends, the robot glides back against its tower
    /// (rear mast under the upper rung, facing downfield), then it lifts itself
    /// <see cref="GameConfig.ClimbHeight"/> over the rest of <see cref="GameConfig.ClimbDuration"/>.
    /// The body is kinematic while on the tower.
    /// The climb pauses while robots are disabled (like a real disabled robot).
    /// </summary>
    public sealed class Climber
    {
        /// <summary>Distance of the tower's upper rung from the alliance wall (see ClimbTower).</summary>
        const float UpperRungDepth = 0.16f;
        /// <summary>Distance from the robot center back to the climber mast (see RobotVisuals).</summary>
        const float MastSetback = 0.355f;

        readonly Robot robot;
        Vector3 startPosition;
        Quaternion startRotation;
        Vector3 basePosition;
        Quaternion hangRotation;
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
            startPosition = robot.Body.position;
            startPosition.y = 0f;
            startRotation = robot.Body.rotation;

            // Hang spot: mast under the upper rung, robot square to the wall, same lane as now.
            Alliance alliance = robot.Alliance;
            Bounds zone = Arena.ClimbZone(alliance);
            float halfWidth = GameConfig.RobotWidth * 0.5f;
            float z = Mathf.Clamp(startPosition.z, zone.min.z + halfWidth, zone.max.z - halfWidth);
            basePosition = new Vector3(Arena.WallX(alliance), 0f, z) +
                           Arena.AllianceForward(alliance) * (UpperRungDepth + MastSetback);
            hangRotation = Quaternion.Euler(0f, Arena.AllianceForwardYaw(alliance), 0f);

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
                {
                    float u = Smooth(timer / ExtendTime);
                    ArmExtension = RobotTuning.ClimbArmReach * u;
                    robot.Body.MovePosition(Vector3.Lerp(startPosition, basePosition, u));
                    robot.Body.MoveRotation(Quaternion.Slerp(startRotation, hangRotation, u));
                    if (timer >= ExtendTime)
                    {
                        State = ClimbState.Lifting;
                        timer = 0f;
                    }
                    break;
                }

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
