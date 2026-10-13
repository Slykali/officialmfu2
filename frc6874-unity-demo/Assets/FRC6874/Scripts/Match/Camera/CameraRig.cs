using UnityEngine;

namespace FRC6874
{
    /// <summary>
    /// Drives the main camera. During a match the player cycles Driver Station / Chase / Broadcast
    /// with <see cref="GameInput.CameraCyclePressed"/>; in the menu (attract mode) and on the results
    /// screen it flies a slow cinematic orbit around the field. While a match or practice runs it
    /// sets <see cref="ControlFrame.YawDegrees"/> every frame so "stick up" matches the view.
    /// Every move is smoothed, so mode and phase changes glide instead of cutting.
    /// </summary>
    [RequireComponent(typeof(Camera))]
    public class CameraRig : MonoBehaviour
    {
        const float BlendDuration = 1.4f;      // extra smoothing after a mode or phase change
        const float BlendSmoothTime = 0.6f;
        const float OrbitDegreesPerSecond = 5f;

        /// <summary>Selected match view. Kept between matches.</summary>
        public CameraMode Mode { get; private set; } = CameraMode.DriverStation;

        public Camera Camera { get; private set; }

        /// <summary>True in the menu and after the match, when the camera orbits the field on its own.</summary>
        public bool IsCinematic => MatchState.Phase == MatchPhase.Menu || MatchState.Phase == MatchPhase.PostMatch;

        /// <summary>Upper-case name of the current view for the HUD.</summary>
        public string ModeName => DisplayName(Mode);

        public static string DisplayName(CameraMode mode)
        {
            switch (mode)
            {
                case CameraMode.Chase: return "CHASE";
                case CameraMode.Broadcast: return "BROADCAST";
                default: return "DRIVER STATION";
            }
        }

        Vector3 position;
        Vector3 look;
        Vector3 positionVelocity;
        Vector3 lookVelocity;
        float fov = 55f;
        float chaseYaw;
        float orbitAngle = 205f;
        float blendTimer;
        bool snap = true;
        bool wasCinematic = true;

        void Awake()
        {
            Camera = GetComponent<Camera>();
            position = transform.position;
            look = position + transform.forward * 10f;
            if (Camera != null) fov = Camera.fieldOfView;
        }

        /// <summary>Switches to the next view (Driver Station → Chase → Broadcast → ...).</summary>
        public void CycleMode()
        {
            int count = System.Enum.GetValues(typeof(CameraMode)).Length;
            SetMode((CameraMode)(((int)Mode + 1) % count));
        }

        public void SetMode(CameraMode mode)
        {
            if (mode == Mode) return;
            Mode = mode;
            blendTimer = BlendDuration;
            if (mode == CameraMode.Chase) chaseYaw = Heading(Robot.Human, transform.eulerAngles.y);
        }

        /// <summary>Jumps straight to the current target on the next frame (no glide).</summary>
        public void SnapToTarget() => snap = true;

        void Update()
        {
            if (!IsCinematic && Time.timeScale > 0f && GameInput.CameraCyclePressed) CycleMode();
        }

        void LateUpdate()
        {
            bool cinematic = IsCinematic;
            if (cinematic != wasCinematic)
            {
                wasCinematic = cinematic;
                blendTimer = BlendDuration;
                if (!cinematic) chaseYaw = Heading(Robot.Human, transform.eulerAngles.y);
            }

            // Only the cinematic orbit runs on unscaled time; match views freeze with the pause.
            float dt = cinematic ? Mathf.Min(Time.unscaledDeltaTime, 0.1f) : Time.deltaTime;
            Robot robot = Robot.Human;

            Vector3 targetPosition, targetLook;
            float targetFov, smooth, lookSmooth;
            if (cinematic)
                Orbit(dt, out targetPosition, out targetLook, out targetFov, out smooth, out lookSmooth);
            else if (Mode == CameraMode.Chase && robot != null)
                Chase(robot, dt, out targetPosition, out targetLook, out targetFov, out smooth, out lookSmooth);
            else if (Mode == CameraMode.Broadcast)
                Broadcast(robot, out targetPosition, out targetLook, out targetFov, out smooth, out lookSmooth);
            else
                DriverStation(robot, out targetPosition, out targetLook, out targetFov, out smooth, out lookSmooth);

            if (snap)
            {
                snap = false;
                blendTimer = 0f;
                position = targetPosition;
                look = targetLook;
                fov = targetFov;
                positionVelocity = Vector3.zero;
                lookVelocity = Vector3.zero;
            }
            else if (dt > 0f)
            {
                if (blendTimer > 0f)
                {
                    float blend = Mathf.Clamp01(blendTimer / BlendDuration);
                    smooth = Mathf.Lerp(smooth, Mathf.Max(smooth, BlendSmoothTime), blend);
                    lookSmooth = Mathf.Lerp(lookSmooth, Mathf.Max(lookSmooth, BlendSmoothTime * 0.8f), blend);
                    blendTimer -= dt;
                }
                position = Vector3.SmoothDamp(position, targetPosition, ref positionVelocity, smooth, Mathf.Infinity, dt);
                look = Vector3.SmoothDamp(look, targetLook, ref lookVelocity, lookSmooth, Mathf.Infinity, dt);
                fov = Mathf.Lerp(fov, targetFov, 1f - Mathf.Exp(-5f * dt));
            }

            transform.position = position;
            Vector3 direction = look - position;
            if (direction.sqrMagnitude > 1e-4f) transform.rotation = Quaternion.LookRotation(direction, Vector3.up);
            if (Camera != null) Camera.fieldOfView = fov;

            if (!cinematic)
            {
                ControlFrame.YawDegrees = Mode == CameraMode.DriverStation || (Mode == CameraMode.Chase && robot == null)
                    ? Arena.AllianceForwardYaw(MatchState.PlayerAlliance)
                    : transform.eulerAngles.y;
            }
        }

        // ---- Views --------------------------------------------------------------------------------

        /// <summary>Behind the alliance wall, panning/tilting gently toward a point between the hub and the robot.</summary>
        void DriverStation(Robot robot, out Vector3 pos, out Vector3 target, out float fieldOfView,
            out float smooth, out float lookSmooth)
        {
            Alliance alliance = MatchState.PlayerAlliance;
            // A little forward and up from the nominal spot so the wall does not hide robots at the start line.
            pos = Arena.DriverStationCameraPosition(alliance) + Arena.AllianceForward(alliance) * 0.6f + Vector3.up * 0.4f;
            target = new Vector3(0f, 0.6f, 0f);
            if (robot != null)
            {
                Vector3 robotPosition = Arena.ClampToField(robot.transform.position);
                target = Vector3.Lerp(target, robotPosition + Vector3.up * 0.3f, 0.45f);
                pos.z += robotPosition.z * 0.12f;
            }
            fieldOfView = 58f;
            smooth = 0.6f;
            lookSmooth = 0.35f;
        }

        /// <summary>About 4.5 m behind and 3 m above the robot, swinging around to follow its heading.</summary>
        void Chase(Robot robot, float dt, out Vector3 pos, out Vector3 target, out float fieldOfView,
            out float smooth, out float lookSmooth)
        {
            Transform follow = robot.CameraTarget != null ? robot.CameraTarget : robot.transform;
            Vector3 anchor = follow.position;
            if (dt > 0f) chaseYaw = Mathf.LerpAngle(chaseYaw, Heading(robot, chaseYaw), 1f - Mathf.Exp(-3.5f * dt));

            Quaternion yaw = Quaternion.Euler(0f, chaseYaw, 0f);
            pos = ClampToVenue(anchor + yaw * new Vector3(0f, 3f, -4.5f));
            target = anchor + yaw * new Vector3(0f, 0.4f, 2.5f);
            fieldOfView = 60f;
            smooth = 0.12f;
            lookSmooth = 0.08f;
        }

        /// <summary>High side-on TV shot, the player's alliance on the left, drifting with the robot.</summary>
        void Broadcast(Robot robot, out Vector3 pos, out Vector3 target, out float fieldOfView,
            out float smooth, out float lookSmooth)
        {
            float side = MatchState.PlayerAlliance == Alliance.Red ? -1f : 1f;
            Vector3 robotPosition = robot != null ? Arena.ClampToField(robot.transform.position) : Vector3.zero;
            pos = new Vector3(robotPosition.x * 0.3f, 11.5f, side * (Arena.HalfWidth + 8.5f));
            target = new Vector3(robotPosition.x * 0.45f, 0f, robotPosition.z * 0.2f);
            fieldOfView = 46f;
            smooth = 0.7f;
            lookSmooth = 0.5f;
        }

        /// <summary>Slow elliptical orbit with a gentle rise and fall, always looking at the hub.</summary>
        void Orbit(float dt, out Vector3 pos, out Vector3 target, out float fieldOfView,
            out float smooth, out float lookSmooth)
        {
            orbitAngle = Mathf.Repeat(orbitAngle + dt * OrbitDegreesPerSecond, 360f);
            float a = orbitAngle * Mathf.Deg2Rad;
            // Radii keep the orbit inside the venue's end boards, backdrops and video board.
            pos = new Vector3(Mathf.Cos(a) * 12.5f, 7f + Mathf.Sin(a * 2f) * 1.1f, Mathf.Sin(a) * 10.5f);
            target = new Vector3(0f, 0.6f, 0f);
            fieldOfView = 50f;
            smooth = 0.25f;
            lookSmooth = 0.25f;
        }

        // ---- Helpers ------------------------------------------------------------------------------

        static float Heading(Robot robot, float fallback)
        {
            if (robot == null) return fallback;
            Vector3 forward = robot.transform.forward;
            forward.y = 0f;
            if (forward.sqrMagnitude < 1e-4f) return fallback;
            return Mathf.Atan2(forward.x, forward.z) * Mathf.Rad2Deg;
        }

        static Vector3 ClampToVenue(Vector3 p)
        {
            p.x = Mathf.Clamp(p.x, -Arena.HalfLength - 4f, Arena.HalfLength + 4f);
            p.z = Mathf.Clamp(p.z, -Arena.HalfWidth - 4f, Arena.HalfWidth + 4f);
            p.y = Mathf.Max(p.y, 0.8f);
            return p;
        }
    }
}
