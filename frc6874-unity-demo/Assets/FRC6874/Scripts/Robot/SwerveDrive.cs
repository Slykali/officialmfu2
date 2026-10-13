using UnityEngine;

namespace FRC6874
{
    /// <summary>
    /// Holonomic (swerve) drivetrain. Each physics step it accelerates the body toward the commanded
    /// field-space velocity (limited by <see cref="GameConfig.RobotAcceleration"/>) and its yaw rate
    /// toward the commanded turn rate or heading. Collisions still act on the body between steps, so
    /// robots push balls and each other physically while the driver stays in control.
    /// </summary>
    public sealed class SwerveDrive
    {
        readonly Robot robot;

        public SwerveDrive(Robot robot)
        {
            this.robot = robot;
        }

        /// <summary>Velocity the drivetrain was asked for on the last step (after clamping).</summary>
        public Vector3 TargetVelocity { get; private set; }
        /// <summary>Yaw rate (deg/s) the drivetrain was asked for on the last step.</summary>
        public float TargetYawRate { get; private set; }

        public void Reset()
        {
            TargetVelocity = Vector3.zero;
            TargetYawRate = 0f;
        }

        /// <summary>Drives one physics step. A default command brakes to a stop.</summary>
        public void Step(ref RobotCommand command, float dt)
        {
            Rigidbody body = robot.Body;
            if (body == null || body.isKinematic || dt <= 0f) return;

            // ---- Translation
            Vector3 target = command.Velocity;
            target.y = 0f;
            target = Vector3.ClampMagnitude(target, robot.MaxSpeed);

            Vector3 v = body.GetVelocity();
            v.y = 0f;
            Vector3 dv = target - v;
            float accel = GameConfig.RobotAcceleration;
            if (target.sqrMagnitude < v.sqrMagnitude - 0.01f) accel *= RobotTuning.BrakeScale;
            float maxDv = accel * dt;
            if (dv.sqrMagnitude > maxDv * maxDv) dv = dv.normalized * maxDv;
            v += dv;
            body.SetVelocity(v);
            TargetVelocity = target;

            // ---- Rotation
            float maxRate = GameConfig.RobotTurnRate;
            float targetRate = command.HasHeading
                ? YawRateToward(robot.Yaw, command.Heading, maxRate)
                : Mathf.Clamp(command.TurnRate, -maxRate, maxRate);
            float w = body.angularVelocity.y * Mathf.Rad2Deg;
            float maxDw = RobotTuning.TurnAcceleration * dt;
            w += Mathf.Clamp(targetRate - w, -maxDw, maxDw);
            body.angularVelocity = new Vector3(0f, w * Mathf.Deg2Rad, 0f);
            TargetYawRate = targetRate;
        }

        /// <summary>
        /// Yaw rate (deg/s) that turns from <paramref name="currentYaw"/> to <paramref name="targetYaw"/>
        /// quickly without overshooting: proportional near the target, decel-limited further out.
        /// </summary>
        public static float YawRateToward(float currentYaw, float targetYaw, float maxRate)
        {
            float error = Mathf.DeltaAngle(currentYaw, targetYaw);
            float abs = Mathf.Abs(error);
            if (abs < 0.05f) return 0f;
            float rate = Mathf.Min(maxRate, RobotTuning.HeadingGain * abs);
            rate = Mathf.Min(rate, Mathf.Sqrt(2f * RobotTuning.TurnAcceleration * 0.8f * abs));
            return Mathf.Sign(error) * rate;
        }
    }
}
