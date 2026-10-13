using System.Collections.Generic;
using UnityEngine;

namespace FRC6874
{
    /// <summary>
    /// Builds a robot's look from primitives (no colliders) and animates it every frame: swerve
    /// modules steer toward their travel direction and spin with speed, the intake deploys and its
    /// roller spins, the hopper shows held fuel, the flywheel spins with a muzzle flash per shot,
    /// LEDs show state (pulse while shooting, green when full, rainbow once climbed), the climber
    /// extends, and the human robot gets a bobbing marker overhead.
    /// </summary>
    [DisallowMultipleComponent]
    public class RobotVisuals : MonoBehaviour
    {
        sealed class SwerveModuleVisual
        {
            public Transform Steer;
            public Transform Wheel;
            public Vector3 LocalPosition;
            public float SteerAngle;
            public float SpinAngle;
        }

        const float WheelRadius = 0.05f;
        const float WheelWidth = 0.04f;
        const float IntakeDeployedAngle = 25f;
        const float IntakeStowedAngle = -50f;
        const float HopperBallDiameter = 0.15f;
        const float ClimberZ = -0.355f;
        const float FlashDuration = 0.09f;
        const float MarkerHeight = 1.5f;

        static readonly Color Gold = new Color(1f, 0.78f, 0.15f);
        static readonly Color FullGreen = new Color(0.15f, 1f, 0.3f);

        Robot robot;
        readonly SwerveModuleVisual[] modules = new SwerveModuleVisual[4];
        Transform intakePivot, intakeRoller, flywheel, climberStage, marker, muzzleFlash;
        GameObject[] hopperBalls;
        float[] hopperPop;
        Material ledMaterial;
        Mesh markerMesh;

        float intakeAngle = IntakeStowedAngle;
        float rollerSpeed, rollerAngle, flywheelAngle;
        int lastShotCount;
        float flashTimer;
        Color lastLedColor;
        float lastLedIntensity = -1f;

        /// <summary>Child the chase camera follows (about 0.45 m above the carpet).</summary>
        public Transform CameraTarget { get; private set; }
        /// <summary>Visual muzzle at <see cref="Shooter.MuzzleLocal"/>.</summary>
        public Transform Muzzle { get; private set; }
        /// <summary>Number of renderers created (kept under ~80 per robot).</summary>
        public int RendererCount { get; private set; }

        /// <summary>Creates every visual part. Called once by <see cref="Robot.Initialize"/>.</summary>
        public void Build(Robot owner)
        {
            if (robot != null) return;
            robot = owner;
            RobotSpec spec = owner.Spec;
            Transform root = transform;

            float length = GameConfig.RobotLength, width = GameConfig.RobotWidth;
            float bt = RobotTuning.BumperThickness;
            float frameHalfL = length * 0.5f - bt, frameHalfW = width * 0.5f - bt;

            Material alu = MaterialFactory.Lit(new Color(0.74f, 0.76f, 0.79f), 0.65f, 0.85f);
            Material dark = MaterialFactory.Lit(new Color(0.13f, 0.13f, 0.15f), 0.35f);
            Material black = MaterialFactory.Lit(new Color(0.05f, 0.05f, 0.05f), 0.15f);
            Material orange = MaterialFactory.Lit(new Color(1f, 0.45f, 0.05f), 0.35f);
            Material bumperMat = MaterialFactory.Lit(spec.Alliance.ToColor(), 0.12f);
            Material poly = MaterialFactory.Unlit(new Color(0.78f, 0.88f, 1f, 0.22f));
            Material fuel = MaterialFactory.Lit(GameConfig.FuelColor, 0.15f);
            Material plate = MaterialFactory.Lit(Color.Lerp(spec.AccentColor, new Color(0.2f, 0.2f, 0.22f), 0.45f), 0.4f, 0.3f);
            Material white = MaterialFactory.Lit(new Color(0.92f, 0.92f, 0.92f), 0.3f);

            // ---- Bumpers + labels
            float bumperBottom = 0.03f, bumperHeight = 0.22f;
            float bumperY = bumperBottom + bumperHeight * 0.5f;
            Part(PrimitiveType.Cube, "Bumper Front", root, new Vector3(0f, bumperY, length * 0.5f - bt * 0.5f), new Vector3(width, bumperHeight, bt), bumperMat);
            Part(PrimitiveType.Cube, "Bumper Back", root, new Vector3(0f, bumperY, -(length * 0.5f - bt * 0.5f)), new Vector3(width, bumperHeight, bt), bumperMat);
            Part(PrimitiveType.Cube, "Bumper Left", root, new Vector3(-(width * 0.5f - bt * 0.5f), bumperY, 0f), new Vector3(bt, bumperHeight, length - 2f * bt), bumperMat);
            Part(PrimitiveType.Cube, "Bumper Right", root, new Vector3(width * 0.5f - bt * 0.5f, bumperY, 0f), new Vector3(bt, bumperHeight, length - 2f * bt), bumperMat);

            string label = owner.Label;
            const float textHeight = 0.11f, gap = 0.004f;
            // TextFactory: identity reads from local -Z; for a face with outward normal n use LookRotation(-n).
            Label(label, root, new Vector3(0f, bumperY, length * 0.5f + gap), Quaternion.LookRotation(Vector3.back), textHeight);
            Label(label, root, new Vector3(0f, bumperY, -length * 0.5f - gap), Quaternion.LookRotation(Vector3.forward), textHeight);
            Label(label, root, new Vector3(width * 0.5f + gap, bumperY, 0f), Quaternion.LookRotation(Vector3.left), textHeight);
            Label(label, root, new Vector3(-width * 0.5f - gap, bumperY, 0f), Quaternion.LookRotation(Vector3.right), textHeight);

            // ---- Frame
            const float railY = 0.27f, rail = 0.04f;
            float railOffsetZ = frameHalfL - rail * 0.5f, railOffsetX = frameHalfW - rail * 0.5f;
            Part(PrimitiveType.Cube, "Rail Front", root, new Vector3(0f, railY, railOffsetZ), new Vector3(frameHalfW * 2f, rail, rail), alu);
            Part(PrimitiveType.Cube, "Rail Back", root, new Vector3(0f, railY, -railOffsetZ), new Vector3(frameHalfW * 2f, rail, rail), alu);
            Part(PrimitiveType.Cube, "Rail Left", root, new Vector3(-railOffsetX, railY, 0f), new Vector3(rail, rail, frameHalfL * 2f - 2f * rail), alu);
            Part(PrimitiveType.Cube, "Rail Right", root, new Vector3(railOffsetX, railY, 0f), new Vector3(rail, rail, frameHalfL * 2f - 2f * rail), alu);
            Part(PrimitiveType.Cube, "Belly Pan", root, new Vector3(0f, 0.075f, 0f), new Vector3(frameHalfW * 2f - 0.02f, 0.012f, frameHalfL * 2f - 0.02f), dark);

            // ---- Swerve modules
            float moduleX = frameHalfW - 0.09f, moduleZ = frameHalfL - 0.09f;
            int index = 0;
            for (int sz = 1; sz >= -1; sz -= 2)
            {
                for (int sx = -1; sx <= 1; sx += 2)
                {
                    var module = new SwerveModuleVisual { LocalPosition = new Vector3(sx * moduleX, 0f, sz * moduleZ) };
                    Transform mount = Empty("Swerve Module", root, module.LocalPosition);
                    module.Steer = Empty("Steer", mount, Vector3.zero);
                    module.Wheel = Empty("Wheel", module.Steer, new Vector3(0f, WheelRadius, 0f));
                    Part(PrimitiveType.Cylinder, "Tread", module.Wheel, Vector3.zero,
                        new Vector3(WheelRadius * 2f, WheelWidth * 0.5f, WheelRadius * 2f), black, Quaternion.Euler(0f, 0f, 90f));
                    Part(PrimitiveType.Cube, "Hub Mark", module.Wheel, new Vector3(sx * (WheelWidth * 0.5f + 0.003f), WheelRadius * 0.5f, 0f),
                        new Vector3(0.006f, 0.02f, 0.02f), orange);
                    Part(PrimitiveType.Cube, "Fork", module.Steer, new Vector3(0f, WheelRadius * 2f + 0.015f, 0f), new Vector3(0.07f, 0.022f, 0.12f), alu);
                    Part(PrimitiveType.Cylinder, "Steer Motor", mount, new Vector3(0f, 0.175f, 0f), new Vector3(0.055f, 0.045f, 0.055f), dark);
                    modules[index++] = module;
                }
            }

            // ---- Intake (front): pivoting arms with an orange roller
            intakePivot = Empty("Intake Pivot", root, new Vector3(0f, 0.30f, frameHalfL - 0.01f));
            float armX = frameHalfW + 0.035f;
            Part(PrimitiveType.Cube, "Intake Arm L", intakePivot, new Vector3(-armX, 0f, 0.07f), new Vector3(0.015f, 0.04f, 0.17f), alu);
            Part(PrimitiveType.Cube, "Intake Arm R", intakePivot, new Vector3(armX, 0f, 0.07f), new Vector3(0.015f, 0.04f, 0.17f), alu);
            intakeRoller = Empty("Intake Roller", intakePivot, new Vector3(0f, 0f, 0.145f));
            float rollerLength = armX * 2f - 0.03f;
            Part(PrimitiveType.Cylinder, "Roller", intakeRoller, Vector3.zero, new Vector3(0.07f, rollerLength * 0.5f, 0.07f), orange, Quaternion.Euler(0f, 0f, 90f));
            Part(PrimitiveType.Cube, "Roller Grip A", intakeRoller, new Vector3(0f, 0.036f, 0f), new Vector3(rollerLength - 0.02f, 0.01f, 0.014f), black);
            Part(PrimitiveType.Cube, "Roller Grip B", intakeRoller, new Vector3(0f, -0.036f, 0f), new Vector3(rollerLength - 0.02f, 0.01f, 0.014f), black);
            intakePivot.localRotation = Quaternion.Euler(intakeAngle, 0f, 0f);

            // ---- Hopper (polycarbonate box showing held fuel)
            const float hopperHalfX = 0.29f, hopperBack = -0.04f, hopperFront = 0.33f, hopperBottom = 0.29f, hopperTop = 0.58f;
            float hopperMidZ = (hopperBack + hopperFront) * 0.5f, hopperDepth = hopperFront - hopperBack;
            float hopperMidY = (hopperBottom + hopperTop) * 0.5f, hopperHeight = hopperTop - hopperBottom;
            Part(PrimitiveType.Cube, "Hopper Floor", root, new Vector3(0f, hopperBottom - 0.005f, hopperMidZ), new Vector3(hopperHalfX * 2f, 0.01f, hopperDepth), dark);
            Part(PrimitiveType.Cube, "Hopper Front", root, new Vector3(0f, hopperMidY, hopperFront), new Vector3(hopperHalfX * 2f, hopperHeight, 0.008f), poly, null, false);
            Part(PrimitiveType.Cube, "Hopper Back", root, new Vector3(0f, hopperMidY, hopperBack), new Vector3(hopperHalfX * 2f, hopperHeight, 0.008f), poly, null, false);
            Part(PrimitiveType.Cube, "Hopper Left", root, new Vector3(-hopperHalfX, hopperMidY, hopperMidZ), new Vector3(0.008f, hopperHeight, hopperDepth), poly, null, false);
            Part(PrimitiveType.Cube, "Hopper Right", root, new Vector3(hopperHalfX, hopperMidY, hopperMidZ), new Vector3(0.008f, hopperHeight, hopperDepth), poly, null, false);

            int shownCapacity = Mathf.Clamp(owner.Capacity, 0, 12);
            hopperBalls = new GameObject[shownCapacity];
            hopperPop = new float[shownCapacity];
            for (int i = 0; i < shownCapacity; i++)
            {
                int layer = i / 6, slot = i % 6;
                int row = slot / 3, column = slot % 3;
                var position = new Vector3((column - 1) * 0.16f, hopperBottom + HopperBallDiameter * 0.5f + 0.005f + layer * 0.14f,
                    hopperBack + 0.1f + row * 0.17f);
                hopperBalls[i] = Part(PrimitiveType.Sphere, "Held Fuel", root, position, Vector3.one * HopperBallDiameter, fuel);
                hopperBalls[i].SetActive(false);
            }

            // ---- Shooter (rear, firing forward over the hopper)
            Part(PrimitiveType.Cube, "Shooter Plate L", root, new Vector3(-0.21f, 0.54f, -0.18f), new Vector3(0.012f, 0.5f, 0.3f), plate);
            Part(PrimitiveType.Cube, "Shooter Plate R", root, new Vector3(0.21f, 0.54f, -0.18f), new Vector3(0.012f, 0.5f, 0.3f), plate);
            flywheel = Empty("Flywheel", root, new Vector3(0f, 0.62f, -0.2f));
            Part(PrimitiveType.Cylinder, "Flywheel Wheel", flywheel, Vector3.zero, new Vector3(0.17f, 0.19f, 0.17f), dark, Quaternion.Euler(0f, 0f, 90f));
            Part(PrimitiveType.Cube, "Flywheel Stripe", flywheel, new Vector3(0f, 0.086f, 0f), new Vector3(0.39f, 0.012f, 0.03f), white);
            Part(PrimitiveType.Cube, "Hood Top", root, new Vector3(0f, 0.75f, -0.21f), new Vector3(0.4f, 0.012f, 0.14f), dark);
            Part(PrimitiveType.Cube, "Hood Lip", root, new Vector3(0f, 0.8f, -0.1f), new Vector3(0.4f, 0.012f, 0.13f), dark, Quaternion.Euler(-35f, 0f, 0f));
            Muzzle = Empty("Muzzle", root, Shooter.MuzzleLocal);
            GameObject flash = Part(PrimitiveType.Sphere, "Muzzle Flash", Muzzle, Vector3.zero, Vector3.one * 0.1f,
                MaterialFactory.Emissive(new Color(1f, 0.85f, 0.4f), 2.5f), null, false);
            muzzleFlash = flash.transform;
            flash.SetActive(false);

            // ---- LED strips (one animated material per robot)
            ledMaterial = MaterialFactory.NewLitInstance(spec.AccentColor, 0.5f);
            ledMaterial.name = $"LED {label}";
            Part(PrimitiveType.Cube, "LED Left", root, new Vector3(-railOffsetX, railY + rail * 0.5f + 0.006f, 0f), new Vector3(0.03f, 0.012f, 0.6f), ledMaterial, null, false);
            Part(PrimitiveType.Cube, "LED Right", root, new Vector3(railOffsetX, railY + rail * 0.5f + 0.006f, 0f), new Vector3(0.03f, 0.012f, 0.6f), ledMaterial, null, false);

            // ---- Climber (telescoping mast at the rear)
            Part(PrimitiveType.Cube, "Climber Tube", root, new Vector3(0f, 0.56f, ClimberZ), new Vector3(0.05f, 0.56f, 0.05f), alu);
            climberStage = Empty("Climber Stage", root, new Vector3(0f, 0f, ClimberZ));
            Part(PrimitiveType.Cube, "Climber Inner", climberStage, new Vector3(0f, 0.55f, 0f), new Vector3(0.034f, 0.56f, 0.034f), dark);
            Part(PrimitiveType.Cube, "Climber Hook Bar", climberStage, new Vector3(0f, 0.85f, 0f), new Vector3(0.22f, 0.03f, 0.03f), orange);
            Part(PrimitiveType.Cube, "Climber Hook Tip", climberStage, new Vector3(0f, 0.815f, -0.03f), new Vector3(0.22f, 0.06f, 0.012f), orange);

            // ---- Driver marker (human robot only)
            if (spec.Driver == DriverType.Human)
            {
                var markerGo = new GameObject("Driver Marker");
                marker = markerGo.transform;
                marker.SetParent(root, false);
                marker.localPosition = new Vector3(0f, MarkerHeight, 0f);
                markerMesh = BuildDownPyramid(0.26f, 0.32f);
                markerGo.AddComponent<MeshFilter>().sharedMesh = markerMesh;
                var markerRenderer = markerGo.AddComponent<MeshRenderer>();
                markerRenderer.sharedMaterial = MaterialFactory.Emissive(spec.AccentColor, 1.4f);
                markerRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                markerRenderer.receiveShadows = false;
                RendererCount++;
            }

            CameraTarget = Empty("Camera Target", root, new Vector3(0f, 0.45f, 0f));
            ApplyLed(spec.AccentColor, 1.2f);
        }

        /// <summary>Back to the match-start look (empty hopper display, intake stowed, climber down).</summary>
        public void ResetVisuals()
        {
            if (robot == null) return;
            intakeAngle = IntakeStowedAngle;
            rollerSpeed = 0f;
            flashTimer = 0f;
            if (muzzleFlash != null) muzzleFlash.gameObject.SetActive(false);
            lastShotCount = robot.Shooter != null ? robot.Shooter.ShotCount : 0;
            if (intakePivot != null) intakePivot.localRotation = Quaternion.Euler(intakeAngle, 0f, 0f);
            if (climberStage != null) climberStage.localPosition = new Vector3(0f, 0f, ClimberZ);
            if (hopperBalls != null)
            {
                for (int i = 0; i < hopperBalls.Length; i++)
                {
                    bool on = i < robot.HeldFuel;
                    hopperBalls[i].SetActive(on);
                    hopperBalls[i].transform.localScale = Vector3.one * HopperBallDiameter;
                    hopperPop[i] = 1f;
                }
            }
        }

        void Update()
        {
            if (robot == null || robot.Body == null) return;
            float dt = Time.deltaTime;
            float t = Time.time;
            AnimateModules(dt);
            AnimateIntake(dt);
            AnimateShooter(dt);
            AnimateHopper(dt);
            if (climberStage != null) climberStage.localPosition = new Vector3(0f, robot.Climber.ArmExtension, ClimberZ);
            AnimateLeds(t);
            AnimateMarker(t);
        }

        void OnDestroy()
        {
            if (ledMaterial != null) Destroy(ledMaterial);
            if (markerMesh != null) Destroy(markerMesh);
        }

        // ---- Animation --------------------------------------------------------------------------

        void AnimateModules(float dt)
        {
            Vector3 localVelocity = Quaternion.Inverse(transform.rotation) * robot.Velocity;
            float yawRate = robot.Body.isKinematic ? 0f : robot.Body.angularVelocity.y; // rad/s
            for (int i = 0; i < modules.Length; i++)
            {
                SwerveModuleVisual m = modules[i];
                if (m == null) continue;
                Vector3 p = m.LocalPosition;
                // Module ground velocity = body velocity + ω × r.
                Vector3 v = localVelocity + new Vector3(yawRate * p.z, 0f, -yawRate * p.x);
                if (v.magnitude > 0.06f)
                {
                    float target = Mathf.Atan2(v.x, v.z) * Mathf.Rad2Deg;
                    // Real swerve modules reverse the wheel instead of turning more than 90°.
                    if (Mathf.Abs(Mathf.DeltaAngle(m.SteerAngle, target)) > 90f) target += 180f;
                    m.SteerAngle = Mathf.MoveTowardsAngle(m.SteerAngle, target, 1080f * dt);
                }
                float steerRad = m.SteerAngle * Mathf.Deg2Rad;
                float rolling = Vector3.Dot(v, new Vector3(Mathf.Sin(steerRad), 0f, Mathf.Cos(steerRad)));
                float spinRate = Mathf.Clamp(rolling / WheelRadius * Mathf.Rad2Deg, -1400f, 1400f);
                m.SpinAngle = Mathf.Repeat(m.SpinAngle + spinRate * dt, 360f);
                m.Steer.localRotation = Quaternion.Euler(0f, m.SteerAngle, 0f);
                m.Wheel.localRotation = Quaternion.Euler(m.SpinAngle, 0f, 0f);
            }
        }

        void AnimateIntake(float dt)
        {
            bool deployed = robot.Intake.Requested && !robot.IsOnTower;
            intakeAngle = Mathf.MoveTowards(intakeAngle, deployed ? IntakeDeployedAngle : IntakeStowedAngle, 300f * dt);
            intakePivot.localRotation = Quaternion.Euler(intakeAngle, 0f, 0f);
            rollerSpeed = Mathf.MoveTowards(rollerSpeed, robot.Intake.Running ? 1f : 0f, 4f * dt);
            rollerAngle = Mathf.Repeat(rollerAngle + rollerSpeed * 1300f * dt, 360f);
            intakeRoller.localRotation = Quaternion.Euler(rollerAngle, 0f, 0f);
        }

        void AnimateShooter(float dt)
        {
            flywheelAngle = Mathf.Repeat(flywheelAngle + robot.Shooter.Flywheel01 * 1500f * dt, 360f);
            flywheel.localRotation = Quaternion.Euler(flywheelAngle, 0f, 0f);

            if (robot.Shooter.ShotCount != lastShotCount)
            {
                lastShotCount = robot.Shooter.ShotCount;
                flashTimer = FlashDuration;
                muzzleFlash.gameObject.SetActive(true);
            }
            if (flashTimer > 0f)
            {
                flashTimer -= dt;
                float k = Mathf.Clamp01(flashTimer / FlashDuration);
                muzzleFlash.localScale = Vector3.one * Mathf.Lerp(0.05f, 0.2f, k);
                if (flashTimer <= 0f) muzzleFlash.gameObject.SetActive(false);
            }
        }

        void AnimateHopper(float dt)
        {
            int held = robot.HeldFuel;
            for (int i = 0; i < hopperBalls.Length; i++)
            {
                GameObject ball = hopperBalls[i];
                bool on = i < held;
                if (ball.activeSelf != on)
                {
                    ball.SetActive(on);
                    hopperPop[i] = on ? 0f : 1f;
                }
                if (on && hopperPop[i] < 1f)
                {
                    hopperPop[i] = Mathf.Min(1f, hopperPop[i] + dt * 7f);
                    float x = hopperPop[i];
                    float overshoot = 1f + 0.25f * Mathf.Sin(x * Mathf.PI); // little "pop"
                    ball.transform.localScale = Vector3.one * (HopperBallDiameter * Mathf.Lerp(0.5f, 1f, x) * overshoot);
                }
            }
        }

        void AnimateLeds(float t)
        {
            Color accent = robot.Spec.AccentColor;
            Color color;
            float intensity;
            if (robot.HasClimbed)
            {
                color = Color.HSVToRGB(Mathf.Repeat(t * 0.35f, 1f), 0.85f, 1f);
                intensity = 2f;
            }
            else if (robot.IsClimbing)
            {
                color = Gold;
                intensity = 1.2f + 0.8f * Mathf.Abs(Mathf.Sin(t * 8f));
            }
            else if (!MatchState.RobotsEnabled)
            {
                color = accent;
                intensity = 0.35f;
            }
            else if ((robot.Shooter.Requested && robot.HeldFuel > 0) || robot.Shooter.IsFiring)
            {
                color = accent;
                intensity = 1f + 1.6f * (0.5f + 0.5f * Mathf.Sin(t * 30f));
            }
            else if (robot.HeldFuel >= robot.Capacity)
            {
                bool blink = t - robot.FullSince < 1.2f && Mathf.Repeat(t * 6f, 1f) < 0.5f;
                color = FullGreen;
                intensity = blink ? 0.3f : 1.8f;
            }
            else
            {
                color = accent;
                intensity = robot.Intake.Running ? 1.6f : 1.2f;
            }
            ApplyLed(color, intensity);
        }

        void ApplyLed(Color color, float intensity)
        {
            if (ledMaterial == null) return;
            if (color == lastLedColor && Mathf.Abs(intensity - lastLedIntensity) < 0.01f) return;
            lastLedColor = color;
            lastLedIntensity = intensity;
            ledMaterial.color = color * 0.6f;
            MaterialFactory.SetEmission(ledMaterial, color * intensity);
        }

        void AnimateMarker(float t)
        {
            if (marker == null) return;
            bool show = robot.IsHumanDriven;
            if (marker.gameObject.activeSelf != show) marker.gameObject.SetActive(show);
            if (!show) return;
            // Stay above the climber arm when it is extended.
            float baseHeight = MarkerHeight + Mathf.Max(0f, robot.Climber.ArmExtension - 0.45f);
            marker.localPosition = new Vector3(0f, baseHeight + 0.07f * Mathf.Sin(t * 2.6f), 0f);
            marker.rotation = Quaternion.Euler(0f, t * 90f, 0f);
        }

        // ---- Construction helpers -------------------------------------------------------------------

        GameObject Part(PrimitiveType type, string name, Transform parent, Vector3 localPosition, Vector3 localScale,
            Material material, Quaternion? localRotation = null, bool castShadows = true)
        {
            GameObject go = MaterialFactory.Primitive(type, name, parent, localPosition, localScale, material, keepCollider: false);
            // MaterialFactory destroys the collider at the end of the frame; disable it right away so it never
            // joins the robot's compound collider in the meantime.
            Collider leftover = go.GetComponent<Collider>();
            if (leftover != null) leftover.enabled = false;
            if (localRotation.HasValue) go.transform.localRotation = localRotation.Value;
            if (!castShadows)
            {
                Renderer r = go.GetComponent<Renderer>();
                if (r != null)
                {
                    r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                    r.receiveShadows = false;
                }
            }
            RendererCount++;
            return go;
        }

        void Label(string text, Transform parent, Vector3 localPosition, Quaternion localRotation, float height)
        {
            TextFactory.Create(text, parent, localPosition, localRotation, height, Color.white);
            RendererCount++;
        }

        static Transform Empty(string name, Transform parent, Vector3 localPosition)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPosition;
            return go.transform;
        }

        /// <summary>Square pyramid with its apex at the local origin pointing down (flat shaded).</summary>
        static Mesh BuildDownPyramid(float baseSize, float height)
        {
            float h = baseSize * 0.5f;
            Vector3 apex = Vector3.zero;
            var a = new Vector3(-h, height, -h);
            var b = new Vector3(h, height, -h);
            var c = new Vector3(h, height, h);
            var d = new Vector3(-h, height, h);
            var interior = new Vector3(0f, height * 0.7f, 0f);

            var vertices = new List<Vector3>(18);
            var triangles = new List<int>(18);
            AddTriangle(vertices, triangles, apex, a, b, interior);
            AddTriangle(vertices, triangles, apex, b, c, interior);
            AddTriangle(vertices, triangles, apex, c, d, interior);
            AddTriangle(vertices, triangles, apex, d, a, interior);
            AddTriangle(vertices, triangles, a, b, c, interior);
            AddTriangle(vertices, triangles, a, c, d, interior);

            var mesh = new Mesh { name = "Driver Marker" };
            mesh.SetVertices(vertices);
            mesh.SetTriangles(triangles, 0);
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return mesh;
        }

        static void AddTriangle(List<Vector3> vertices, List<int> triangles, Vector3 p0, Vector3 p1, Vector3 p2, Vector3 interior)
        {
            // Unity front faces wind clockwise as seen from outside: Cross(p1 - p0, p2 - p0) points outward.
            Vector3 normal = Vector3.Cross(p1 - p0, p2 - p0);
            Vector3 outward = (p0 + p1 + p2) / 3f - interior;
            if (Vector3.Dot(normal, outward) < 0f)
            {
                Vector3 swap = p1;
                p1 = p2;
                p2 = swap;
            }
            int start = vertices.Count;
            vertices.Add(p0);
            vertices.Add(p1);
            vertices.Add(p2);
            triangles.Add(start);
            triangles.Add(start + 1);
            triangles.Add(start + 2);
        }
    }
}
