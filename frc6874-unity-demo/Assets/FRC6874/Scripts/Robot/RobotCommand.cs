using UnityEngine;

namespace FRC6874
{
    /// <summary>
    /// What a driver (human, autonomous routine or AI) asks the robot to do during one physics step.
    /// The robot's mechanisms execute it in <c>FixedUpdate</c>.
    /// </summary>
    public struct RobotCommand
    {
        /// <summary>Desired field-space velocity in m/s (y is ignored). Clamped to the robot's max speed.</summary>
        public Vector3 Velocity;
        /// <summary>Desired yaw rate in deg/s (positive = clockwise seen from above). Ignored when <see cref="HasHeading"/>.</summary>
        public float TurnRate;
        /// <summary>When true the drivetrain turns toward <see cref="Heading"/> instead of using <see cref="TurnRate"/>.</summary>
        public bool HasHeading;
        /// <summary>Desired yaw in degrees (Unity convention: 0 = +Z, 90 = +X).</summary>
        public float Heading;
        public bool Intake;
        public bool Shoot;
        /// <summary>Keep the flywheel spinning without firing (so the first shot is instant).</summary>
        public bool SpinUp;
        /// <summary>Aim at the hub while shooting even if the driver turned auto-aim off (auto/AI).</summary>
        public bool ForceAutoAim;
        /// <summary>Request to start climbing this step.</summary>
        public bool Climb;

        /// <summary>Turns toward a yaw (degrees).</summary>
        public void FaceYaw(float yawDegrees)
        {
            HasHeading = true;
            Heading = yawDegrees;
        }

        /// <summary>Turns toward a world-space direction (ignored if it is nearly zero).</summary>
        public void FaceDirection(Vector3 direction)
        {
            direction.y = 0f;
            if (direction.sqrMagnitude < 1e-4f) return;
            FaceYaw(Mathf.Atan2(direction.x, direction.z) * Mathf.Rad2Deg);
        }
    }
}
