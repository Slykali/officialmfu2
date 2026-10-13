using System.Collections.Generic;
using UnityEngine;

namespace FRC6874
{
    public enum DriverType
    {
        Human,
        AI,
    }

    public enum DriveMode
    {
        /// <summary>Stick "up" always drives along <see cref="ControlFrame.YawDegrees"/> (swerve-style).</summary>
        FieldCentric,
        /// <summary>Stick "up" drives the way the robot is facing.</summary>
        RobotCentric,
    }

    /// <summary>
    /// An FRC-style swerve robot: a physics body plus drivetrain, intake, shooter and climber, and
    /// the controller that drives it this step (the human through <see cref="PlayerDriver"/>, the
    /// scripted <see cref="AutoRoutine"/> during auto, or the <see cref="AIDriver"/>). Build robots with
    /// <see cref="RobotFactory.Create"/>. Mechanisms run in FixedUpdate; <see cref="RobotVisuals"/> animates.
    /// Local axes: +Z is the front (intake side), +X is right, the root sits on the carpet (y = 0).
    /// </summary>
    [DisallowMultipleComponent]
    public class Robot : MonoBehaviour
    {
        /// <summary>Every robot in the scene.</summary>
        public static readonly List<Robot> All = new List<Robot>();

        /// <summary>Constraints while driving: upright, glued to the carpet, free to yaw.</summary>
        public const RigidbodyConstraints DrivingConstraints =
            RigidbodyConstraints.FreezeRotationX | RigidbodyConstraints.FreezeRotationZ | RigidbodyConstraints.FreezePositionY;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            // Supports "Enter Play Mode Options" with domain reload disabled.
            All.Clear();
        }

        /// <summary>The robot whose spec says Driver == Human (null if none).</summary>
        public static Robot Human
        {
            get
            {
                for (int i = 0; i < All.Count; i++)
                {
                    Robot r = All[i];
                    if (r != null && r.Spec != null && r.Spec.Driver == DriverType.Human) return r;
                }
                return null;
            }
        }

        public RobotSpec Spec { get; private set; }
        public Alliance Alliance => Spec.Alliance;
        /// <summary>True when the controller currently drives this robot (human robot, not in attract mode).</summary>
        public bool IsHumanDriven => Spec != null && Spec.Driver == DriverType.Human && !MatchState.AttractMode;

        public int HeldFuel { get; protected set; }
        public int Capacity => GameConfig.FuelCapacity;
        public bool AutoAim { get; set; } = true;
        public DriveMode DriveMode { get; set; } = DriveMode.FieldCentric;
        /// <summary>True while a climb is in progress (arm extending or robot lifting). False again once <see cref="HasClimbed"/>.</summary>
        public bool IsClimbing { get; protected set; }
        /// <summary>True once the climb finished (the robot hangs on the tower until <see cref="ResetForMatch"/>).</summary>
        public bool HasClimbed { get; protected set; }
        public bool HasLeftStartingZone { get; protected set; }
        /// <summary>Short state text for the HUD, e.g. "INTAKING", "SHOOTING", "AUTO: SCORING PRELOAD".</summary>
        public string Status { get; protected set; } = "";
        /// <summary>Point the chase camera should follow/look at (a child about 0.45 m above the carpet).</summary>
        public Transform CameraTarget { get; protected set; }

        /// <summary>Field-space velocity (y = 0). Zero while the robot is on the tower.</summary>
        public Vector3 Velocity
        {
            get
            {
                if (Body == null || Body.isKinematic) return Vector3.zero;
                Vector3 v = Body.GetVelocity();
                v.y = 0f;
                return v;
            }
        }

        // ---- Mechanisms and drivers ---------------------------------------------------------
        public Rigidbody Body { get; private set; }
        /// <summary>The robot's physics colliders (bumper band and upper body).</summary>
        public Collider[] Colliders { get; private set; }
        public SwerveDrive Drive { get; private set; }
        public Intake Intake { get; private set; }
        public Shooter Shooter { get; private set; }
        public Climber Climber { get; private set; }
        public RobotNavigator Navigator { get; private set; }
        public PlayerDriver Player { get; private set; }
        public AIDriver AI { get; private set; }
        public AutoRoutine Auto { get; private set; }
        public RobotVisuals Visuals { get; private set; }

        /// <summary>The command executed on the last physics step (for debugging/visuals).</summary>
        public RobotCommand LastCommand { get; private set; }
        /// <summary>-1, 0 or +1: which start lane the robot was reset into (alliance-relative, used to vary auto).</summary>
        public int StartLane { get; private set; }
        /// <summary>Time.time when the hopper last became full.</summary>
        public float FullSince { get; private set; } = -10f;

        /// <summary>Bumper label, never empty.</summary>
        public string Label =>
            Spec != null && !string.IsNullOrEmpty(Spec.BumperLabel) ? Spec.BumperLabel :
            Spec != null && Spec.TeamNumber > 0 ? Spec.TeamNumber.ToString() : "BOT";

        /// <summary>True when the AI (not the controller) drives this robot outside auto.</summary>
        public bool IsAIControlled => Spec != null && !IsHumanDriven;

        /// <summary>Top speed right now: the player gets the full speed, AI-driven robots are scaled down.</summary>
        public float MaxSpeed => GameConfig.RobotMaxSpeed * (IsHumanDriven ? 1f : GameConfig.AIRobotSpeedScale);

        /// <summary>Physics position flattened to the carpet (y = 0).</summary>
        public Vector3 FlatPosition
        {
            get
            {
                Vector3 p = Body != null ? Body.position : transform.position;
                p.y = 0f;
                return p;
            }
        }

        public Quaternion PhysicsRotation => Body != null ? Body.rotation : transform.rotation;
        /// <summary>Heading in degrees (Unity convention: 0 = +Z, 90 = +X).</summary>
        public float Yaw => PhysicsRotation.eulerAngles.y;
        public Vector3 Forward => Quaternion.Euler(0f, Yaw, 0f) * Vector3.forward;
        public Vector3 Right => Quaternion.Euler(0f, Yaw, 0f) * Vector3.right;

        /// <summary>Horizontal distance from the robot center to the hub center.</summary>
        public float HubDistance
        {
            get
            {
                Vector3 d = FlatPosition - Arena.HubCenter;
                d.y = 0f;
                return d.magnitude;
            }
        }

        /// <summary>True while the robot is on (or getting onto/off) the tower.</summary>
        public bool IsOnTower => Climber != null && Climber.IsActive;

        string hint;
        float hintUntil;
        bool phaseTracked;
        MatchPhase lastPhase;
        bool lastAttract;

        /// <summary>Called once by <see cref="RobotFactory.Create"/>. Builds the body, mechanisms, drivers and visuals.</summary>
        public void Initialize(RobotSpec spec)
        {
            if (Spec != null) return;
            Spec = spec ?? new RobotSpec { BumperLabel = "BOT", DisplayName = "Robot", Driver = DriverType.AI };

            BuildBody();
            Drive = new SwerveDrive(this);
            Intake = new Intake(this);
            Shooter = new Shooter(this);
            Climber = new Climber(this);
            Navigator = new RobotNavigator(this);
            Player = new PlayerDriver(this);
            AI = new AIDriver(this);
            Auto = new AutoRoutine(this);

            Visuals = GetComponent<RobotVisuals>();
            if (Visuals == null) Visuals = gameObject.AddComponent<RobotVisuals>();
            Visuals.Build(this);
            CameraTarget = Visuals.CameraTarget != null ? Visuals.CameraTarget : transform;
        }

        /// <summary>Teleports to a start pose, stops, clears climb/leave state and sets held fuel.</summary>
        public void ResetForMatch(Vector3 position, float yawDegrees, int preloadFuel)
        {
            position.y = 0f;
            Quaternion rotation = Quaternion.Euler(0f, yawDegrees, 0f);

            if (Climber != null) Climber.ResetState();
            if (Body != null)
            {
                SetBodyKinematic(false);
                Body.position = position;
                Body.rotation = rotation;
            }
            transform.SetPositionAndRotation(position, rotation);
            if (Body != null)
            {
                Body.SetVelocity(Vector3.zero);
                Body.angularVelocity = Vector3.zero;
            }

            HeldFuel = Mathf.Clamp(preloadFuel, 0, Capacity);
            IsClimbing = HasClimbed = HasLeftStartingZone = false;
            FullSince = HeldFuel >= Capacity ? Time.time : -10f;
            hint = null;
            hintUntil = 0f;
            StartLane = ComputeLane(position);

            if (Drive != null) Drive.Reset();
            if (Intake != null) Intake.Reset();
            if (Shooter != null) Shooter.Reset();
            if (Navigator != null) Navigator.Reset();
            if (AI != null) AI.Reset();
            if (Auto != null) Auto.Reset();
            if (Player != null) Player.ClearLatched();
            AIClaims.ReleaseAll(this);
            if (Visuals != null) Visuals.ResetVisuals();
            LastCommand = default;
            Status = MatchState.RobotsEnabled ? "READY" : "DISABLED";
        }

        /// <summary>Shows a short message in <see cref="Status"/> for a moment (e.g. why a climb was refused).</summary>
        public void ShowHint(string text, float seconds = 1.6f)
        {
            hint = text;
            hintUntil = Time.time + seconds;
        }

        /// <summary>True if the robot center is inside its alliance's climb zone (XZ only).</summary>
        public bool IsInClimbZone()
        {
            if (Spec == null) return false;
            Bounds zone = Arena.ClimbZone(Alliance);
            Vector3 p = FlatPosition;
            return p.x >= zone.min.x && p.x <= zone.max.x && p.z >= zone.min.z && p.z <= zone.max.z;
        }

        /// <summary>Starts climbing if the rules allow it. Shows a hint and returns false otherwise.</summary>
        public bool TryStartClimb(bool showHints)
        {
            if (Climber == null || IsClimbing || HasClimbed || Climber.IsActive) return false;
            if (!MatchState.ClimbAllowed)
            {
                if (showHints) ShowHint("CLIMB: ENDGAME ONLY");
                return false;
            }
            if (!IsInClimbZone())
            {
                if (showHints) ShowHint("CLIMB: DRIVE INTO CLIMB ZONE");
                return false;
            }
            IsClimbing = true;
            HasClimbed = false;
            Climber.Begin();
            return true;
        }

        // ---- Called by the mechanisms ---------------------------------------------------------

        internal void AddFuel(int count)
        {
            int before = HeldFuel;
            HeldFuel = Mathf.Clamp(HeldFuel + count, 0, Capacity);
            if (HeldFuel >= Capacity && before < Capacity) FullSince = Time.time;
        }

        internal void ConsumeFuel() => HeldFuel = Mathf.Max(0, HeldFuel - 1);

        internal void OnClimbComplete()
        {
            IsClimbing = false;
            HasClimbed = true;
            Vector3 p = Body != null ? Body.position : transform.position;
            MatchState.Award(Alliance, ScoreKind.Climb, p, Label);
            GameAudio.Play(GameSound.Climb, p);
        }

        internal void OnClimberStowed()
        {
            IsClimbing = false;
            HasClimbed = false;
        }

        /// <summary>Switches between a driven (dynamic) body and a kinematic one for climbing.</summary>
        internal void SetBodyKinematic(bool kinematic)
        {
            if (Body == null) return;
            if (kinematic)
            {
                if (!Body.isKinematic)
                {
                    Body.SetVelocity(Vector3.zero);
                    Body.angularVelocity = Vector3.zero;
                }
                // Kinematic bodies only support speculative CCD; switch first to avoid a warning.
                Body.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;
                Body.constraints = RigidbodyConstraints.None;
                Body.isKinematic = true;
            }
            else
            {
                Body.isKinematic = false;
                Body.collisionDetectionMode = CollisionDetectionMode.Continuous;
                Body.constraints = DrivingConstraints;
            }
        }

        // ---- Unity messages -------------------------------------------------------------------

        protected virtual void OnEnable()
        {
            if (!All.Contains(this)) All.Add(this);
        }

        protected virtual void OnDisable()
        {
            All.Remove(this);
            AIClaims.ReleaseAll(this);
        }

        protected virtual void OnDestroy()
        {
            All.Remove(this);
            AIClaims.ReleaseAll(this);
        }

        void Update()
        {
            // Input is sampled every frame so "pressed" events are never missed; FixedUpdate applies it.
            if (Player != null) Player.Poll();
        }

        void FixedUpdate()
        {
            if (Spec == null || Body == null) return;
            float dt = Time.fixedDeltaTime;
            TrackPhase();

            bool robotsEnabled = MatchState.RobotsEnabled;
            RobotCommand cmd = default;

            if (Climber.IsActive)
            {
                // On the tower: no driving. The player may lower the robot again in practice.
                bool climbPressed = false;
                if (robotsEnabled && IsHumanDriven && MatchState.DriverControlEnabled)
                {
                    RobotCommand input = default;
                    Player.Fill(ref input);
                    climbPressed = input.Climb;
                }
                else Player.ClearLatched();

                if (Climber.State == ClimbState.Hanging && robotsEnabled)
                {
                    bool lower = (climbPressed && MatchState.Phase == MatchPhase.Practice) ||
                                 (MatchState.AttractMode && MatchState.Phase == MatchPhase.Menu && Climber.StateTime > 4f);
                    if (lower) Climber.BeginLowering();
                }
                Climber.Tick(dt, robotsEnabled);
                Intake.Tick(false, dt);
                Shooter.UpdateAim(false);
                Shooter.Tick(false, false, false, dt);
            }
            else
            {
                if (robotsEnabled)
                {
                    if (MatchState.IsAuto) Auto.Tick(ref cmd);
                    else if (IsHumanDriven)
                    {
                        if (MatchState.DriverControlEnabled) Player.Fill(ref cmd);
                        else Player.ClearLatched();
                    }
                    else AI.Tick(ref cmd, false);
                }
                else Player.ClearLatched();

                // Auto-aim: while shooting, the robot turns itself toward the hub (overrides the turn stick).
                bool aimAssist = robotsEnabled && cmd.Shoot && HeldFuel > 0 && (AutoAim || cmd.ForceAutoAim);
                Shooter.UpdateAim(aimAssist);
                if (aimAssist) cmd.FaceYaw(Shooter.AimYaw);

                Drive.Step(ref cmd, dt);
                Intake.Tick(robotsEnabled && cmd.Intake, dt);
                Shooter.Tick(robotsEnabled && cmd.Shoot, robotsEnabled && cmd.SpinUp, aimAssist, dt);
                if (robotsEnabled && cmd.Climb) TryStartClimb(IsHumanDriven);
                CheckLeftStartingZone();
            }

            LastCommand = cmd;
            Status = ComposeStatus(cmd);
        }

        // ---- Helpers --------------------------------------------------------------------------

        void BuildBody()
        {
            Body = GetComponent<Rigidbody>();
            if (Body == null) Body = gameObject.AddComponent<Rigidbody>();
            Body.mass = GameConfig.RobotMass;
            Body.useGravity = true;
            Body.SetDamping(0f, 0f);
            Body.interpolation = RigidbodyInterpolation.Interpolate;
            Body.isKinematic = false;
            Body.collisionDetectionMode = CollisionDetectionMode.Continuous;
            Body.constraints = DrivingConstraints;
            Body.maxAngularVelocity = 12f;

            // Bumper band: the full frame perimeter, slightly above the carpet so nothing drags.
            var bumper = gameObject.AddComponent<BoxCollider>();
            bumper.center = new Vector3(0f, RobotTuning.GroundClearance + RobotTuning.BumperBandHeight * 0.5f, 0f);
            bumper.size = new Vector3(GameConfig.RobotWidth, RobotTuning.BumperBandHeight, GameConfig.RobotLength);
            PhysicsCompat.SetSurface(bumper, 0.05f, 0.02f);

            // Upper body (hopper/shooter block) so balls bounce off the robot instead of passing through.
            float upperBottom = RobotTuning.GroundClearance + RobotTuning.BumperBandHeight;
            const float upperTop = 0.62f;
            var upper = gameObject.AddComponent<BoxCollider>();
            upper.center = new Vector3(0f, (upperBottom + upperTop) * 0.5f, -0.04f);
            upper.size = new Vector3(0.66f, upperTop - upperBottom, 0.74f);
            PhysicsCompat.SetSurface(upper, 0.1f, 0.1f);

            Colliders = new Collider[] { bumper, upper };
            Body.centerOfMass = new Vector3(0f, 0.15f, 0f);
        }

        int ComputeLane(Vector3 position)
        {
            if (Spec == null) return 0;
            float z = Alliance == Alliance.Blue ? -position.z : position.z;
            return Mathf.Clamp(Mathf.RoundToInt(z / 2.4f), -1, 1);
        }

        void TrackPhase()
        {
            MatchPhase phase = MatchState.Phase;
            bool attract = MatchState.AttractMode;
            if (phaseTracked && phase == lastPhase && attract == lastAttract) return;

            bool enteringAuto = phase == MatchPhase.Auto && (!phaseTracked || lastPhase != MatchPhase.Auto);
            phaseTracked = true;
            lastPhase = phase;
            lastAttract = attract;

            Navigator.Reset();
            AI.Reset();
            Player.ClearLatched();
            if (enteringAuto) Auto.Reset();
        }

        void CheckLeftStartingZone()
        {
            if (HasLeftStartingZone || !MatchState.IsAuto) return;

            Bounds zone = Arena.StartingZone(Alliance);
            Vector3 p = FlatPosition;
            Vector3 f = Forward, r = Right;
            float halfLength = GameConfig.RobotLength * 0.5f, halfWidth = GameConfig.RobotWidth * 0.5f;
            // Half extents of the (rotated) bumper footprint along world X and Z.
            float ex = halfLength * Mathf.Abs(f.x) + halfWidth * Mathf.Abs(r.x);
            float ez = halfLength * Mathf.Abs(f.z) + halfWidth * Mathf.Abs(r.z);
            bool overlapX = p.x + ex > zone.min.x && p.x - ex < zone.max.x;
            bool overlapZ = p.z + ez > zone.min.z && p.z - ez < zone.max.z;
            if (overlapX && overlapZ) return;

            HasLeftStartingZone = true;
            MatchState.Award(Alliance, ScoreKind.Leave, p, Label);
        }

        string ComposeStatus(RobotCommand cmd)
        {
            if (HasClimbed) return "CLIMBED!";
            if (IsClimbing) return "CLIMBING";
            if (Climber.State == ClimbState.Lowering) return "LOWERING";
            if (hint != null && Time.time < hintUntil) return hint;
            if (!MatchState.RobotsEnabled) return "DISABLED";
            if (MatchState.IsAuto) return "AUTO: " + Auto.Label;
            if (!IsHumanDriven) return "AI: " + AI.Label;

            if (cmd.Shoot)
            {
                if (HeldFuel <= 0) return "NO FUEL";
                if (Shooter.HubDistance > GameConfig.ShooterMaxRange) return "OUT OF RANGE";
                if (Shooter.HubDistance < GameConfig.ShooterMinRange) return "TOO CLOSE";
                return Shooter.IsFiring ? "SHOOTING" : "AIMING";
            }
            if (cmd.Intake) return HeldFuel >= Capacity ? "FULL" : "INTAKING";
            if (HeldFuel >= Capacity && Time.time - FullSince < 1.5f) return "FULL";
            if (Shooter.IsFiring) return "SHOOTING";
            if (!MatchState.DriverControlEnabled) return "DISABLED";
            return "READY";
        }
    }
}
