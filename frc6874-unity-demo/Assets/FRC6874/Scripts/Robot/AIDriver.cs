using System.Collections.Generic;
using UnityEngine;

namespace FRC6874
{
    /// <summary>
    /// Computer driver for AI robots (and every robot in attract mode). A small state machine:
    /// COLLECT the best loose ball (near, away from opponents, not claimed by a teammate), SHOOT from
    /// a spot 3–5.5 m from the hub when full (or holding a few with nothing good nearby), and CLIMB
    /// in the endgame when the remaining time calls for it. Decisions are re-evaluated after a short,
    /// randomized reaction delay and each robot has a slightly different "skill", so it feels human.
    /// </summary>
    public sealed class AIDriver
    {
        public enum Mode
        {
            Collect,
            Shoot,
            Climb,
        }

        readonly Robot robot;
        readonly float skill;
        readonly float climbMarginJitter;
        readonly Dictionary<GamePiece, float> ignored = new Dictionary<GamePiece, float>();
        readonly List<GamePiece> expired = new List<GamePiece>();

        Mode mode = Mode.Collect;
        float modeSince;
        float nextDecision;
        bool autoPeriod;

        GamePiece target;
        float targetDeadline;
        float currentTargetCost;

        Vector3 shootSpot;
        bool hasShootSpot;
        int climbSlot = -1;
        Vector3 patrolSpot;
        float patrolUntil;
        bool nothingToCollect;

        public AIDriver(Robot robot)
        {
            this.robot = robot;
            skill = Random.Range(0.85f, 1f);
            climbMarginJitter = Random.Range(0f, 2f);
        }

        public Mode State => mode;
        /// <summary>Short label for the HUD, e.g. "COLLECTING", "SHOOTING", "TO CLIMB".</summary>
        public string Label { get; private set; } = "COLLECTING";
        /// <summary>The ball this robot is going for (null if none).</summary>
        public GamePiece Target => target;
        public bool HasShootSpot => mode == Mode.Shoot && hasShootSpot;
        public Vector3 ShootSpot => shootSpot;
        /// <summary>0.85–1: how quick and sharp this AI is.</summary>
        public float Skill => skill;

        float DriveSpeed => robot.MaxSpeed * (0.92f + 0.08f * skill);

        public void Reset()
        {
            SetTarget(null, Time.time);
            AIClaims.ReleaseClimbSlot(robot);
            climbSlot = -1;
            hasShootSpot = false;
            mode = Mode.Collect;
            modeSince = Time.time;
            ignored.Clear();
            nextDecision = Time.time + Random.Range(0.05f, 0.3f);
            patrolUntil = 0f;
            nothingToCollect = false;
            Label = "COLLECTING";
        }

        /// <summary>Fills a command for this physics step. <paramref name="inAuto"/> makes it shoot sooner (auto cycles).</summary>
        public void Tick(ref RobotCommand cmd, bool inAuto)
        {
            autoPeriod = inAuto;
            float now = Time.time;

            if (target != null && !IsCandidate(target, now))
            {
                SetTarget(null, now);
                nextDecision = Mathf.Min(nextDecision, now + Reaction() * 0.6f);
            }
            if (now >= nextDecision)
            {
                Decide(now);
                nextDecision = now + Reaction();
            }

            switch (mode)
            {
                case Mode.Collect: TickCollect(ref cmd, now); break;
                case Mode.Shoot: TickShoot(ref cmd, now); break;
                case Mode.Climb: TickClimb(ref cmd, now); break;
            }

            if (robot.Navigator.ConsumeRecovered())
            {
                // Got stuck: forget this target/spot for a while and re-plan right away.
                if (target != null) Ignore(target, now, 4f);
                SetTarget(null, now);
                hasShootSpot = false;
                if (mode == Mode.Climb) climbSlot = AIClaims.ClaimClimbSlot(robot);
                nextDecision = now;
            }
            if (robot.Navigator.IsRecovering) Label = "UNSTUCK";
        }

        // ---- Decisions ---------------------------------------------------------------------------

        void Decide(float now)
        {
            if (mode == Mode.Climb)
            {
                // Switch slots if someone else parked on ours.
                if (climbSlot >= 0 && AIClaims.IsSlotOccupied(robot, climbSlot) && !robot.IsInClimbZone())
                    climbSlot = AIClaims.ClaimClimbSlot(robot);
                return;
            }
            if (ShouldGoClimb())
            {
                EnterMode(Mode.Climb, now);
                SetTarget(null, now);
                climbSlot = AIClaims.ClaimClimbSlot(robot);
                return;
            }

            if (ignored.Count > 24) PruneIgnored(now);

            int held = robot.HeldFuel;
            GamePiece best = FindBestBall(now, out float bestCost);

            if (mode == Mode.Collect)
            {
                nothingToCollect = best == null;
                bool full = held >= robot.Capacity;
                bool nothingGood = best == null || bestCost > RobotTuning.AIGoodBallCost;
                bool hurry = autoPeriod && held > 0 && MatchState.TimeRemaining < 4.5f;
                bool autoBatch = autoPeriod && held >= 3;
                if (full || (held >= 2 && nothingGood) || (held >= 1 && best == null) || hurry || autoBatch)
                {
                    SetTarget(null, now);
                    EnterMode(Mode.Shoot, now);
                    return;
                }
                if (best != null && (target == null || bestCost < currentTargetCost - RobotTuning.AITargetHysteresis))
                    SetTarget(best, now);
            }
            else if (mode == Mode.Shoot)
            {
                if (held <= 0 || now - modeSince > RobotTuning.AIShootTimeout)
                {
                    EnterMode(Mode.Collect, now);
                    if (best != null) SetTarget(best, now);
                }
            }
        }

        bool ShouldGoClimb()
        {
            if (!MatchState.IsEndgame || robot.HasClimbed || robot.IsClimbing) return false;
            Vector3 slot = AIClaims.ClimbSlotPosition(robot.Alliance, AIClaims.PreviewClimbSlot(robot));
            float path = RobotNavigator.EstimatePathLength(robot.FlatPosition, slot);
            if (path > 8f) return true; // far away: head over as soon as the endgame starts
            float travel = path / Mathf.Max(0.5f, DriveSpeed * 0.6f) + 1f;
            return MatchState.TimeRemaining <= travel + GameConfig.ClimbDuration + RobotTuning.AIClimbMargin + climbMarginJitter;
        }

        void EnterMode(Mode next, float now)
        {
            mode = next;
            modeSince = now;
            hasShootSpot = false;
            robot.Navigator.Reset();
        }

        /// <summary>Faces the direction of travel, but only when actually moving (no heading jitter at low speed).</summary>
        static void FaceTravel(ref RobotCommand cmd, Vector3 velocity)
        {
            velocity.y = 0f;
            if (velocity.sqrMagnitude > 0.8f * 0.8f) cmd.FaceDirection(velocity);
        }

        float Reaction() => Random.Range(RobotTuning.AIReactionMin, RobotTuning.AIReactionMax) * Mathf.Lerp(1.3f, 1f, (skill - 0.85f) / 0.15f);

        // ---- Collect --------------------------------------------------------------------------------

        void TickCollect(ref RobotCommand cmd, float now)
        {
            cmd.Intake = true;
            cmd.SpinUp = robot.HeldFuel >= robot.Capacity - 1;

            if (target == null)
            {
                if (!nothingToCollect)
                {
                    // Between balls (reaction delay): ease off and keep the heading while picking the next one.
                    cmd.Velocity = robot.Velocity * Mathf.Clamp01(1f - 2.5f * Time.fixedDeltaTime);
                    Label = "COLLECTING";
                    return;
                }
                if (robot.HeldFuel > 0 && now - modeSince > 1.5f)
                {
                    EnterMode(Mode.Shoot, now);
                    return;
                }
                Patrol(ref cmd, now);
                return;
            }

            Vector3 position = robot.FlatPosition;
            Vector3 ball = target.Body.position;
            ball.y = 0f;
            Vector3 toBall = ball - position;
            float distance = toBall.magnitude;

            Vector3 velocity = robot.Navigator.Steer(ball, DriveSpeed, 0f, 1.2f, out _);
            if (distance > 0.55f)
            {
                cmd.FaceDirection(toBall);
                // Close to the ball but not facing it yet: slow down so it goes into the intake, not the side bumper.
                if (distance < 1.8f)
                {
                    float error = Mathf.Abs(Mathf.DeltaAngle(robot.Yaw, Mathf.Atan2(toBall.x, toBall.z) * Mathf.Rad2Deg));
                    float align = Mathf.Clamp01(1f - (error - 25f) / 55f);
                    velocity *= Mathf.Lerp(0.2f, 1f, align);
                }
            }
            cmd.Velocity = velocity;
            Label = "COLLECTING";

            if (now > targetDeadline)
            {
                Ignore(target, now, 4f);
                SetTarget(null, now);
                nextDecision = now;
            }
        }

        void Patrol(ref RobotCommand cmd, float now)
        {
            if (now >= patrolUntil)
            {
                // Wait near the hub on our side of the field, where ejected balls roll out.
                Vector3 position = robot.FlatPosition;
                float side = Mathf.Abs(position.x) > 0.5f ? Mathf.Sign(position.x) : Arena.WallSign(robot.Alliance);
                float angle = Random.Range(-50f, 50f) * Mathf.Deg2Rad;
                float radius = Random.Range(2.6f, 3.4f);
                patrolSpot = new Vector3(side * radius * Mathf.Cos(angle), 0f, radius * Mathf.Sin(angle));
                patrolUntil = now + Random.Range(2.5f, 4f);
            }
            cmd.Velocity = robot.Navigator.Steer(patrolSpot, DriveSpeed * 0.6f, 0.4f, 0f, out _);
            cmd.FaceDirection(Arena.HubCenter - robot.FlatPosition);
            Label = "SEARCHING";
        }

        GamePiece FindBestBall(float now, out float bestCost)
        {
            GamePiece best = null;
            bestCost = float.MaxValue;
            currentTargetCost = float.MaxValue;

            Vector3 position = robot.FlatPosition;
            Vector3 forward = robot.Forward;
            List<GamePiece> pieces = GamePiece.Active;
            for (int i = 0; i < pieces.Count; i++)
            {
                GamePiece piece = pieces[i];
                if (!IsCandidate(piece, now)) continue;
                if (AIClaims.IsClaimedByTeammate(piece, robot)) continue;

                Vector3 ball = piece.Body.position;
                ball.y = 0f;
                Vector3 toBall = ball - position;
                float cost = RobotNavigator.EstimatePathLength(position, ball);
                if (toBall.sqrMagnitude > 1e-4f) cost += Vector3.Angle(forward, toBall) / 180f * 0.9f;
                cost += CrowdPenalty(ball) + WallPenalty(ball);

                if (piece == target) currentTargetCost = cost;
                else cost += Random.Range(0f, 0.6f) * (1.4f - skill); // imperfect judgment

                if (cost < bestCost)
                {
                    bestCost = cost;
                    best = piece;
                }
            }
            return best;
        }

        bool IsCandidate(GamePiece piece, float now)
        {
            if (piece == null || piece.State == PieceState.Pooled || piece.Body == null || !piece.IsCollectable) return false;
            if (ignored.TryGetValue(piece, out float until) && now < until) return false;

            Vector3 p = piece.Body.position;
            if (p.y > 0.45f) return false;
            if (!Arena.IsInsideField(p, 0.05f)) return false;
            Vector3 fromHub = p - Arena.HubCenter;
            fromHub.y = 0f;
            if (fromHub.magnitude < GameConfig.HubBaseRadius + 0.02f) return false;

            // Balls under or right next to a robot on its tower are out of reach.
            List<Robot> all = Robot.All;
            for (int i = 0; i < all.Count; i++)
            {
                Robot other = all[i];
                if (other == null || !other.IsOnTower) continue;
                Vector3 d = other.FlatPosition - p;
                d.y = 0f;
                if (d.sqrMagnitude < 0.9f * 0.9f) return false;
            }
            return true;
        }

        float CrowdPenalty(Vector3 ball)
        {
            float penalty = 0f;
            List<Robot> all = Robot.All;
            for (int i = 0; i < all.Count; i++)
            {
                Robot other = all[i];
                if (other == null || other == robot || other.Spec == null) continue;
                Vector3 d = other.FlatPosition - ball;
                d.y = 0f;
                float distance = d.magnitude;
                if (other.Alliance != robot.Alliance)
                {
                    if (distance < 2f) penalty += (2f - distance) * 1.2f;
                }
                else if (distance < 1.2f) penalty += (1.2f - distance) * 0.6f;
            }
            return penalty;
        }

        static float WallPenalty(Vector3 ball)
        {
            float wx = Arena.HalfLength - Mathf.Abs(ball.x);
            float wz = Arena.HalfWidth - Mathf.Abs(ball.z);
            if (wx < 0.45f && wz < 0.45f) return 3f;      // corner
            if (Mathf.Min(wx, wz) < 0.3f) return 0.6f;     // against a wall
            return 0f;
        }

        void SetTarget(GamePiece piece, float now)
        {
            if (piece == target && piece != null) return;
            target = piece;
            if (piece != null)
            {
                AIClaims.ClaimBall(robot, piece);
                float distance = Vector3.Distance(robot.FlatPosition, piece.Body.position);
                targetDeadline = now + 2.5f + distance / Mathf.Max(1f, DriveSpeed * 0.5f);
                currentTargetCost = 0f;
            }
            else
            {
                AIClaims.ReleaseBall(robot);
                currentTargetCost = float.MaxValue;
            }
        }

        void Ignore(GamePiece piece, float now, float seconds)
        {
            if (piece != null) ignored[piece] = now + seconds;
        }

        void PruneIgnored(float now)
        {
            expired.Clear();
            foreach (KeyValuePair<GamePiece, float> pair in ignored)
                if (pair.Key == null || now >= pair.Value) expired.Add(pair.Key);
            for (int i = 0; i < expired.Count; i++) ignored.Remove(expired[i]);
            expired.Clear();
        }

        // ---- Shoot ------------------------------------------------------------------------------------

        void TickShoot(ref RobotCommand cmd, float now)
        {
            if (!hasShootSpot) PickShootSpot();

            Vector3 position = robot.FlatPosition;
            Vector3 toSpot = shootSpot - position;
            toSpot.y = 0f;
            float spotDistance = toSpot.magnitude;
            float hubDistance = robot.HubDistance;
            bool inRange = hubDistance >= RobotTuning.AIShootDistanceMin - 0.4f && hubDistance <= RobotTuning.AIShootDistanceMax + 0.5f;

            Vector3 velocity = robot.Navigator.Steer(shootSpot, DriveSpeed, 0.3f, 0f, out bool arrived);
            bool settle = arrived || (inRange && spotDistance < 1f && robot.Velocity.magnitude < 1.5f);

            cmd.Intake = robot.HeldFuel < robot.Capacity;
            cmd.SpinUp = spotDistance < 3.5f;
            cmd.ForceAutoAim = true;
            if (settle)
            {
                cmd.Velocity = arrived ? velocity : velocity * 0.5f;
                cmd.Shoot = true;
                Label = "SHOOTING";
            }
            else
            {
                cmd.Velocity = velocity;
                if (spotDistance < 2.5f) cmd.FaceDirection(Arena.HubCenter - position);
                else FaceTravel(ref cmd, velocity);
                Label = "SCORING";
            }
        }

        void PickShootSpot()
        {
            Vector3 position = robot.FlatPosition;
            float side = Mathf.Abs(position.x) > 0.5f ? Mathf.Sign(position.x) : Arena.WallSign(robot.Alliance);
            float min = RobotTuning.AIShootDistanceMin, max = RobotTuning.AIShootDistanceMax;
            float radius = Mathf.Clamp(robot.HubDistance, min + 0.3f, max - 0.3f) + Random.Range(-0.3f, 0.3f);
            radius = Mathf.Clamp(radius, min, max);
            float baseAngle = Mathf.Clamp(Mathf.Atan2(position.z, position.x * side) * Mathf.Rad2Deg, -55f, 55f);

            Vector3 best = new Vector3(side * 4f, 0f, 0f);
            float bestScore = float.MaxValue;
            for (int i = -3; i <= 3; i++)
            {
                float degrees = baseAngle + i * 18f;
                if (Mathf.Abs(degrees) > 75f) continue; // stay on this side of the hub
                float angle = degrees * Mathf.Deg2Rad;
                Vector3 p = new Vector3(side * radius * Mathf.Cos(angle), 0f, radius * Mathf.Sin(angle));
                if (!Arena.IsInsideField(p, 0.9f)) continue;

                float score = Vector3.Distance(position, p) + Mathf.Abs(i) * 0.15f + Random.Range(0f, 0.4f);
                List<Robot> all = Robot.All;
                for (int j = 0; j < all.Count; j++)
                {
                    Robot other = all[j];
                    if (other == null || other == robot) continue;
                    float d = Vector3.Distance(p, other.FlatPosition);
                    if (d < 1.8f) score += (1.8f - d) * 3f;
                    if (other.AI != null && other.AI.HasShootSpot)
                    {
                        float ds = Vector3.Distance(p, other.AI.ShootSpot);
                        if (ds < 1.8f) score += (1.8f - ds) * 3f;
                    }
                }
                if (score < bestScore)
                {
                    bestScore = score;
                    best = p;
                }
            }
            shootSpot = best;
            hasShootSpot = true;
        }

        // ---- Climb ------------------------------------------------------------------------------------

        void TickClimb(ref RobotCommand cmd, float now)
        {
            if (robot.IsClimbing || robot.HasClimbed)
            {
                Label = "CLIMBING";
                return;
            }
            if (climbSlot < 0) climbSlot = AIClaims.ClaimClimbSlot(robot);

            Vector3 slot = AIClaims.ClimbSlotPosition(robot.Alliance, climbSlot);
            Vector3 position = robot.FlatPosition;
            Vector3 toSlot = slot - position;
            toSlot.y = 0f;
            float distance = toSlot.magnitude;

            Vector3 velocity = robot.Navigator.Steer(slot, DriveSpeed, 0.2f, 0f, out _);
            cmd.Velocity = velocity;
            // Back in toward the tower so the climber (at the rear) faces it.
            if (distance < 3f) cmd.FaceYaw(Arena.AllianceForwardYaw(robot.Alliance));
            else FaceTravel(ref cmd, velocity);

            bool inZone = robot.IsInClimbZone();
            bool parked = distance < 0.35f && robot.Velocity.magnitude < 0.6f;
            bool lowOnTime = MatchState.TimeRemaining < GameConfig.ClimbDuration + 1.5f;
            bool blocked = distance < 1.2f && AIClaims.IsSlotOccupied(robot, climbSlot);
            bool waitedLong = now - modeSince > 12f;
            if (inZone && (parked || lowOnTime || blocked || waitedLong)) cmd.Climb = true;
            Label = "TO CLIMB";
        }
    }
}
