using UnityEngine;

namespace FRC6874
{
    /// <summary>
    /// Ballistics for the shooter. Shots are solved by apex height: the ball rises to an apex
    /// <c>apexAboveTarget</c> above the target and then comes DOWN onto it, so it drops into the
    /// hub opening instead of hitting the rim. The balls' small linear damping is compensated.
    /// </summary>
    public static class BallisticSolver
    {
        /// <summary>Gravity magnitude from <see cref="Physics.gravity"/> (falls back to 9.81).</summary>
        public static float Gravity
        {
            get
            {
                float g = -Physics.gravity.y;
                return g > 0.1f ? g : 9.81f;
            }
        }

        /// <summary>Apex height above the aim point for a shot over a horizontal distance (grows with range).</summary>
        public static float ApexHeightFor(float horizontalDistance) =>
            Mathf.Lerp(RobotTuning.ApexAboveAimNear, RobotTuning.ApexAboveAimFar,
                Mathf.InverseLerp(2f, GameConfig.ShooterMaxRange, horizontalDistance));

        /// <summary>
        /// Launch velocity from <paramref name="start"/> that passes an apex <paramref name="apexAboveTarget"/>
        /// above the target and descends through <paramref name="target"/>.
        /// </summary>
        public static Vector3 Solve(Vector3 start, Vector3 target, float apexAboveTarget, out float flightTime)
        {
            float g = Gravity;
            float apexY = Mathf.Max(target.y + Mathf.Max(0.05f, apexAboveTarget), start.y + 0.05f);
            float rise = apexY - start.y;
            float fall = apexY - target.y;
            float vy = Mathf.Sqrt(2f * g * rise);
            float t = vy / g + Mathf.Sqrt(2f * fall / g);

            Vector3 flat = target - start;
            flat.y = 0f;
            Vector3 vh = flat / t;

            // Linear damping k: x(t) ≈ v·t·(1 - k·t/2) and y(t) ≈ vy·t·(1 - k·t/2) - g·t²/2·(1 - k·t/3).
            float k = RobotTuning.BallLinearDamping;
            float compensation = 1f / Mathf.Max(0.5f, 1f - k * t * 0.5f);
            vh *= compensation;
            vy = compensation * (vy - g * k * t * t / 6f);

            flightTime = t;
            return new Vector3(vh.x, vy, vh.z);
        }

        /// <summary>
        /// Like <see cref="Solve"/>, but if the target is inside the hub opening it raises the apex
        /// until the ball clears the near rim.
        /// </summary>
        public static Vector3 SolveIntoHub(Vector3 start, Vector3 target, float apexAboveTarget, out float flightTime)
        {
            Vector3 velocity = Solve(start, target, apexAboveTarget, out flightTime);
            Vector3 fromHub = target - Arena.HubCenter;
            fromHub.y = 0f;
            if (fromHub.magnitude > GameConfig.HubOpeningRadius) return velocity;

            for (int i = 0; i < 4 && !ClearsRim(start, velocity, Arena.HubCenter); i++)
            {
                apexAboveTarget += 0.25f;
                velocity = Solve(start, target, apexAboveTarget, out flightTime);
            }
            return velocity;
        }

        /// <summary>True if the ball is above the rim (plus its radius) where its path crosses the near edge of the opening.</summary>
        public static bool ClearsRim(Vector3 start, Vector3 velocity, Vector3 hubCenter)
        {
            Vector3 flatVelocity = velocity;
            flatVelocity.y = 0f;
            float speed = flatVelocity.magnitude;
            if (speed < 1e-3f) return true;

            Vector3 direction = flatVelocity / speed;
            Vector3 toHub = hubCenter - start;
            toHub.y = 0f;
            float along = Vector3.Dot(toHub, direction);
            float lateralSq = toHub.sqrMagnitude - along * along;
            float radius = GameConfig.HubOpeningRadius;
            if (lateralSq >= radius * radius) return true;

            float s = along - Mathf.Sqrt(radius * radius - lateralSq);
            if (s <= 0f) return true;
            float t = s / speed;
            float y = start.y + velocity.y * t - 0.5f * Gravity * t * t;
            return y >= GameConfig.HubOpeningHeight + GameConfig.FuelRadius + 0.02f;
        }

        /// <summary>Drag-free position along a trajectory (for previews and checks).</summary>
        public static Vector3 PositionAt(Vector3 start, Vector3 velocity, float time) =>
            start + velocity * time + 0.5f * Physics.gravity * time * time;
    }
}
