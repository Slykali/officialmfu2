using System.Collections.Generic;
using UnityEngine;

namespace FRC6874
{
    /// <summary>
    /// Steering for computer-driven robots (auto routines and the AI). Turns a goal point into a
    /// desired velocity that goes around the hub via a tangent waypoint, keeps clear of other robots
    /// (with a keep-right rule so head-on meetings resolve), stays inside the field, slows down on
    /// arrival, and backs up + sidesteps when it stops making progress.
    /// </summary>
    public sealed class RobotNavigator
    {
        readonly Robot robot;

        Vector3 progressGoal;
        float bestDistance;
        float lastProgressTime;
        bool tracking;

        float recoverUntil;
        Vector3 recoverDirection;
        bool recoveryPending;
        bool recoveredFlag;
        float detourSign = 1f;

        public RobotNavigator(Robot robot)
        {
            this.robot = robot;
        }

        /// <summary>True while a stuck-recovery maneuver is running.</summary>
        public bool IsRecovering => Time.time < recoverUntil;
        /// <summary>Number of stuck recoveries since the robot was built (handy for debugging).</summary>
        public int RecoveryCount { get; private set; }

        public void Reset()
        {
            tracking = false;
            recoverUntil = 0f;
            recoveryPending = false;
            recoveredFlag = false;
        }

        /// <summary>Returns true once after a recovery maneuver finished, so the caller can re-plan.</summary>
        public bool ConsumeRecovered()
        {
            bool value = recoveredFlag;
            recoveredFlag = false;
            return value;
        }

        /// <summary>The goal moved inside the field margin and out of the hub's keep-out circle (y = 0).</summary>
        public static Vector3 SafeGoal(Vector3 goal)
        {
            goal.y = 0f;
            goal = Arena.ClampToField(goal, RobotTuning.FieldMargin);
            Vector3 hub = Arena.HubCenter;
            hub.y = 0f;
            Vector3 fromHub = goal - hub;
            float keepOut = Arena.HubKeepOutRadius;
            float distance = fromHub.magnitude;
            if (distance < keepOut)
            {
                Vector3 dir = distance > 1e-3f ? fromHub / distance : Vector3.right;
                goal = hub + dir * keepOut;
            }
            return goal;
        }

        /// <summary>
        /// Desired field velocity toward <paramref name="goal"/>. <paramref name="speedAtGoal"/> &gt; 0 drives
        /// through the goal (for balls); 0 stops within <paramref name="arriveRadius"/>.
        /// </summary>
        public Vector3 Steer(Vector3 goal, float maxSpeed, float arriveRadius, float speedAtGoal, out bool arrived)
        {
            float now = Time.time;
            Vector3 position = robot.FlatPosition;
            Vector3 rawGoal = goal;
            rawGoal.y = 0f;
            goal = SafeGoal(goal);
            Vector3 toGoal = goal - position;
            float distance = toGoal.magnitude;
            arrived = distance <= arriveRadius;

            if (now < recoverUntil)
            {
                Vector3 recover = recoverDirection * RobotTuning.RecoverSpeed + WallPush(position) + HubPush(position);
                return Vector3.ClampMagnitude(recover, maxSpeed);
            }
            if (recoveryPending)
            {
                recoveryPending = false;
                recoveredFlag = true;
                tracking = false;
            }

            Vector3 waypoint = DetourWaypoint(position, goal);
            Vector3 toWaypoint = waypoint - position;
            toWaypoint.y = 0f;
            float waypointDistance = toWaypoint.magnitude;
            Vector3 direction = waypointDistance > 1e-3f ? toWaypoint / waypointDistance : Vector3.zero;
            if (speedAtGoal > 0f && distance < 0.4f)
            {
                // Driving "through" a goal (a ball): near it, press toward the real target, even when it sits
                // beyond the reachable area (against a wall or the hub), instead of circling the clamped point.
                Vector3 press = rawGoal - position;
                press.y = 0f;
                direction = press.sqrMagnitude > 0.01f ? press.normalized : robot.Forward;
            }

            float speed;
            if (arrived && speedAtGoal <= 0f) speed = 0f;
            else
            {
                float decel = GameConfig.RobotAcceleration * 0.55f;
                float d = Mathf.Max(0f, distance - arriveRadius * 0.5f);
                speed = speedAtGoal + Mathf.Min(Mathf.Sqrt(2f * decel * d), 2.5f * d);
                speed = Mathf.Min(maxSpeed, speed);
            }

            Vector3 moveDirection = speed > 0.05f ? direction : Vector3.zero;
            Vector3 desired = direction * speed + Separation(position, moveDirection) + WallPush(position) + HubPush(position);
            desired.y = 0f;
            desired = Vector3.ClampMagnitude(desired, maxSpeed);

            TrackProgress(goal, distance, speed > 0.4f && !arrived, now, direction);
            return desired;
        }

        /// <summary>Rough driving distance between two points, including a detour around the hub.</summary>
        public static float EstimatePathLength(Vector3 from, Vector3 to)
        {
            from.y = 0f;
            to.y = 0f;
            float distance = Vector3.Distance(from, to);
            if (IsBlockedByHub(from, to)) distance += (Arena.HubKeepOutRadius + RobotTuning.HubClearance) * 0.9f;
            return distance;
        }

        /// <summary>True if the straight line from <paramref name="from"/> to <paramref name="to"/> cuts through the hub's keep-out ring.</summary>
        public static bool IsBlockedByHub(Vector3 from, Vector3 to)
        {
            Vector3 hub = Arena.HubCenter;
            hub.y = 0f;
            from.y = 0f;
            to.y = 0f;
            float ring = Arena.HubKeepOutRadius + RobotTuning.HubClearance;
            Vector3 segment = to - from;
            float lengthSq = segment.sqrMagnitude;
            if (lengthSq < 1e-4f) return false;
            float t = Mathf.Clamp01(Vector3.Dot(hub - from, segment) / lengthSq);
            Vector3 closest = from + segment * t;
            // Endpoints inside the ring (e.g. a ball next to the hub) only count if the path passes *behind* them.
            float limit = Mathf.Min(ring, Mathf.Min((to - hub).magnitude, (from - hub).magnitude) - 0.1f);
            return (closest - hub).magnitude < limit;
        }

        Vector3 DetourWaypoint(Vector3 position, Vector3 goal)
        {
            if (!IsBlockedByHub(position, goal)) return goal;

            Vector3 hub = Arena.HubCenter;
            hub.y = 0f;
            float ring = Arena.HubKeepOutRadius + RobotTuning.HubClearance;
            Vector3 radial = position - hub;
            float r = radial.magnitude;
            if (r < 1e-3f) return goal;
            radial /= r;

            // Go around the short way (keep the previous choice when it's a coin flip, so we don't dither).
            float cross = Vector3.Cross(position - hub, goal - hub).y;
            float scale = r * Mathf.Max(0.1f, (goal - hub).magnitude);
            if (Mathf.Abs(cross) > 0.15f * scale) detourSign = cross >= 0f ? 1f : -1f;

            if (r <= ring + 0.05f)
            {
                // Already hugging the hub: follow the circle, drifting slightly outward.
                Vector3 tangent = Quaternion.AngleAxis(90f * detourSign, Vector3.up) * radial;
                return position + (tangent * 0.9f + radial * 0.35f) * 1.5f;
            }

            float alpha = Mathf.Acos(Mathf.Clamp(ring / r, -1f, 1f)) * Mathf.Rad2Deg;
            return hub + Quaternion.AngleAxis(alpha * detourSign, Vector3.up) * radial * (ring + 0.15f);
        }

        Vector3 Separation(Vector3 position, Vector3 moveDirection)
        {
            Vector3 sum = Vector3.zero;
            float range = RobotTuning.SeparationRadius;
            List<Robot> all = Robot.All;
            for (int i = 0; i < all.Count; i++)
            {
                Robot other = all[i];
                if (other == null || other == robot || other.Body == null) continue;
                Vector3 d = position - other.FlatPosition;
                d.y = 0f;
                float distance = d.magnitude;
                if (distance >= range || distance < 1e-3f) continue;

                Vector3 away = d / distance;
                float weight = (range - distance) / range;
                sum += away * (weight * RobotTuning.SeparationStrength);
                // Keep-right rule: if the other robot is ahead of us, veer right so head-on meetings pass cleanly.
                if (moveDirection.sqrMagnitude > 0.01f && Vector3.Dot(moveDirection, -away) > 0.3f)
                    sum += Vector3.Cross(Vector3.up, moveDirection) * (weight * RobotTuning.SeparationStrength * 0.6f);
            }
            return sum;
        }

        static Vector3 WallPush(Vector3 position)
        {
            const float margin = 0.55f;
            const float strength = 2f;
            Vector3 push = Vector3.zero;
            float left = position.x + Arena.HalfLength, right = Arena.HalfLength - position.x;
            float near = position.z + Arena.HalfWidth, far = Arena.HalfWidth - position.z;
            if (left < margin) push.x += (margin - left) / margin * strength;
            if (right < margin) push.x -= (margin - right) / margin * strength;
            if (near < margin) push.z += (margin - near) / margin * strength;
            if (far < margin) push.z -= (margin - far) / margin * strength;
            return push;
        }

        static Vector3 HubPush(Vector3 position)
        {
            Vector3 d = position - Arena.HubCenter;
            d.y = 0f;
            float distance = d.magnitude;
            float radius = Arena.HubKeepOutRadius + 0.1f;
            if (distance >= radius || distance < 1e-3f) return Vector3.zero;
            return d / distance * ((radius - distance) / 0.5f * 2f);
        }

        void TrackProgress(Vector3 goal, float distance, bool wantsToMove, float now, Vector3 direction)
        {
            if (!tracking || (goal - progressGoal).sqrMagnitude > 0.75f * 0.75f)
            {
                tracking = true;
                progressGoal = goal;
                bestDistance = distance;
                lastProgressTime = now;
                return;
            }
            if (!wantsToMove)
            {
                bestDistance = distance;
                lastProgressTime = now;
                return;
            }
            if (distance < bestDistance - 0.2f)
            {
                bestDistance = distance;
                lastProgressTime = now;
            }
            else if (now - lastProgressTime > RobotTuning.StuckTime)
            {
                BeginRecovery(direction, now);
            }
        }

        void BeginRecovery(Vector3 blockedDirection, float now)
        {
            if (blockedDirection.sqrMagnitude < 1e-4f) blockedDirection = robot.Forward;
            blockedDirection.y = 0f;
            blockedDirection.Normalize();
            Vector3 side = Vector3.Cross(Vector3.up, blockedDirection);
            float sign = ChooseSide(robot.FlatPosition, side);
            recoverDirection = (-blockedDirection * 0.75f + side * (sign * 0.65f)).normalized;
            recoverUntil = now + RobotTuning.RecoverTime;
            recoveryPending = true;
            tracking = false;
            RecoveryCount++;
        }

        float ChooseSide(Vector3 position, Vector3 side)
        {
            float right = Clearance(position + side * 0.9f);
            float left = Clearance(position - side * 0.9f);
            if (Mathf.Abs(right - left) < 0.05f) return Random.value < 0.5f ? 1f : -1f;
            return right > left ? 1f : -1f;
        }

        float Clearance(Vector3 p)
        {
            float clearance = Mathf.Min(Arena.HalfLength - Mathf.Abs(p.x), Arena.HalfWidth - Mathf.Abs(p.z));
            Vector3 fromHub = p - Arena.HubCenter;
            fromHub.y = 0f;
            clearance = Mathf.Min(clearance, fromHub.magnitude - GameConfig.HubBaseRadius);
            List<Robot> all = Robot.All;
            for (int i = 0; i < all.Count; i++)
            {
                Robot other = all[i];
                if (other == null || other == robot) continue;
                Vector3 d = p - other.FlatPosition;
                d.y = 0f;
                clearance = Mathf.Min(clearance, d.magnitude - 0.6f);
            }
            return clearance;
        }
    }
}
