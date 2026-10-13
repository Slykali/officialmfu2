using UnityEngine;

namespace FRC6874
{
    /// <summary>
    /// Flywheel shooter that faces forward. Fires <see cref="GameConfig.ShotsPerSecond"/> while the
    /// shoot command is held, using <see cref="BallisticSolver"/> for a descending shot into the hub.
    /// With auto-aim the robot turns to the hub and fires only once aligned; without it the shot goes
    /// along the robot's heading with the range solution for the hub distance. The robot's own
    /// velocity is compensated, so shoot-on-the-move works. Accuracy falls off with distance.
    /// </summary>
    public sealed class Shooter
    {
        /// <summary>Where balls leave the shooter, in robot-local coordinates.</summary>
        public static readonly Vector3 MuzzleLocal = new Vector3(0f, 0.86f, -0.04f);

        readonly Robot robot;
        readonly float maxExitSpeed;
        float cooldown;

        public Shooter(Robot robot)
        {
            this.robot = robot;
            // Top exit speed: what a standing max-range shot needs, plus some headroom.
            Vector3 start = new Vector3(0f, MuzzleLocal.y, 0f);
            Vector3 target = new Vector3(GameConfig.ShooterMaxRange, Arena.HubAimPoint.y, 0f);
            Vector3 v = BallisticSolver.Solve(start, target, BallisticSolver.ApexHeightFor(GameConfig.ShooterMaxRange), out _);
            maxExitSpeed = v.magnitude * RobotTuning.MaxExitSpeedScale;
        }

        /// <summary>Flywheel speed, 0 (stopped) to 1 (ready).</summary>
        public float Flywheel01 { get; private set; }
        /// <summary>The shoot command was active on the last step.</summary>
        public bool Requested { get; private set; }
        /// <summary>Auto-aim was steering the robot on the last step.</summary>
        public bool AutoAiming { get; private set; }
        /// <summary>Heading (deg) the robot must face for the current shot solution.</summary>
        public float AimYaw { get; private set; }
        /// <summary>True when the heading is within <see cref="RobotTuning.AimToleranceDegrees"/> of <see cref="AimYaw"/> (always true without auto-aim).</summary>
        public bool IsAligned { get; private set; }
        /// <summary>Horizontal distance from the robot center to the hub center.</summary>
        public float HubDistance { get; private set; }
        public float LastShotTime { get; private set; } = -10f;
        /// <summary>Total shots fired (visuals watch this for the muzzle flash).</summary>
        public int ShotCount { get; private set; }

        public bool IsFiring => Time.time - LastShotTime < 0.3f;
        public bool InRange => HubDistance >= GameConfig.ShooterMinRange && HubDistance <= GameConfig.ShooterMaxRange;
        public float MaxExitSpeed => maxExitSpeed;

        public Vector3 MuzzlePosition => robot.Body.position + robot.Body.rotation * MuzzleLocal;

        public void Reset()
        {
            Flywheel01 = 0f;
            cooldown = 0f;
            Requested = AutoAiming = false;
            IsAligned = false;
            LastShotTime = -10f;
        }

        /// <summary>Updates range and the auto-aim heading. Call before the drivetrain step.</summary>
        public void UpdateAim(bool autoAim)
        {
            AutoAiming = autoAim;
            HubDistance = robot.HubDistance;
            if (!autoAim)
            {
                AimYaw = robot.Yaw;
                IsAligned = true;
                return;
            }

            Vector3 world = ComputeLaunchVelocity(true, false);
            // The robot must face the shooter-relative launch direction (world velocity minus robot velocity).
            Vector3 exit = world - robot.Velocity;
            exit.y = 0f;
            if (exit.sqrMagnitude < 1e-4f)
            {
                exit = Arena.HubCenter - robot.FlatPosition;
                exit.y = 0f;
            }
            AimYaw = exit.sqrMagnitude > 1e-6f ? Mathf.Atan2(exit.x, exit.z) * Mathf.Rad2Deg : robot.Yaw;
            IsAligned = Mathf.Abs(Mathf.DeltaAngle(robot.Yaw, AimYaw)) <= RobotTuning.AimToleranceDegrees;
        }

        /// <summary>Spins the flywheel and fires when ready. Call after the drivetrain step.</summary>
        public void Tick(bool shoot, bool spinUp, bool autoAim, float dt)
        {
            Requested = shoot;
            bool spin = shoot || spinUp;
            float rate = spin ? 1f / RobotTuning.FlywheelSpinUpTime : 1f / RobotTuning.FlywheelSpinDownTime;
            Flywheel01 = Mathf.MoveTowards(Flywheel01, spin ? 1f : 0f, rate * dt);

            cooldown -= dt;
            bool canFire = shoot && robot.HeldFuel > 0 && Flywheel01 >= 0.9f && (!autoAim || IsAligned);
            if (!canFire)
            {
                if (cooldown < 0f) cooldown = 0f;
                return;
            }
            if (cooldown > 1e-4f) return;

            Fire(autoAim);
            cooldown += 1f / Mathf.Max(0.1f, GameConfig.ShotsPerSecond);
        }

        /// <summary>
        /// World-space launch velocity for a shot right now. Auto-aim targets <see cref="Arena.HubAimPoint"/>;
        /// manual aim targets the point at the hub's distance straight along the robot's heading.
        /// </summary>
        public Vector3 ComputeLaunchVelocity(bool autoAim, bool withSpread)
        {
            Vector3 muzzle = MuzzlePosition;
            Vector3 aim = Arena.HubAimPoint;
            Vector3 flat = aim - muzzle;
            flat.y = 0f;
            float distance = flat.magnitude;

            Vector3 target;
            if (autoAim) target = aim;
            else
            {
                Vector3 forward = robot.Forward;
                target = new Vector3(muzzle.x + forward.x * distance, aim.y, muzzle.z + forward.z * distance);
            }

            Vector3 robotVelocity = robot.Velocity;
            if (withSpread)
            {
                float sigma = SpreadFor(robot.HubDistance, robotVelocity.magnitude);
                target.x += Gaussian() * sigma;
                target.z += Gaussian() * sigma;
            }

            Vector3 world = BallisticSolver.SolveIntoHub(muzzle, target, BallisticSolver.ApexHeightFor(distance), out _);

            // Velocity compensation: the ball inherits the robot's velocity, so the shooter only adds the difference.
            Vector3 exit = world - robotVelocity;
            if (exit.magnitude > maxExitSpeed) exit = exit.normalized * maxExitSpeed; // long shots fall short
            return exit + robotVelocity;
        }

        /// <summary>Standard deviation (m) of the landing point for a shot from <paramref name="distance"/> at <paramref name="robotSpeed"/>.</summary>
        public static float SpreadFor(float distance, float robotSpeed)
        {
            float min = GameConfig.ShooterMinRange, max = GameConfig.ShooterMaxRange;
            float sigma;
            if (distance < min) sigma = RobotTuning.SpreadTooClose;
            else if (distance <= 2f) sigma = RobotTuning.SpreadNear;
            else if (distance <= 4f) sigma = Mathf.Lerp(RobotTuning.SpreadNear, RobotTuning.SpreadMid, (distance - 2f) * 0.5f);
            else if (distance <= max) sigma = Mathf.Lerp(RobotTuning.SpreadMid, RobotTuning.SpreadAtMaxRange, Mathf.InverseLerp(4f, max, distance));
            else sigma = RobotTuning.SpreadAtMaxRange + (distance - max) * RobotTuning.SpreadBeyondPerMeter;
            return sigma + robotSpeed * RobotTuning.SpreadPerSpeed;
        }

        void Fire(bool autoAim)
        {
            Vector3 velocity = ComputeLaunchVelocity(autoAim, true);
            Vector3 muzzle = MuzzlePosition;
            GamePiece piece = GamePiece.Spawn(muzzle);
            piece.Launch(velocity, robot.Alliance, robot.Label, robot.Colliders);
            robot.ConsumeFuel();
            LastShotTime = Time.time;
            ShotCount++;
            GameAudio.Play(GameSound.Shoot, muzzle);
        }

        static float Gaussian()
        {
            // Box-Muller, clamped to ±3σ.
            float u1 = Mathf.Max(1e-6f, 1f - Random.value);
            float u2 = Random.value;
            float g = Mathf.Sqrt(-2f * Mathf.Log(u1)) * Mathf.Cos(2f * Mathf.PI * u2);
            return Mathf.Clamp(g, -3f, 3f);
        }
    }
}
