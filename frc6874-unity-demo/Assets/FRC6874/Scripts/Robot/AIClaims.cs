using System.Collections.Generic;
using UnityEngine;

namespace FRC6874
{
    /// <summary>
    /// Shared "whiteboard" for robots on the same alliance: which ball each robot is going for and
    /// which climb slot it has taken, so teammates don't chase the same ball or park on each other.
    /// Opponents ignore each other's claims (they contest balls).
    /// </summary>
    public static class AIClaims
    {
        /// <summary>Climb slots per alliance, spread along Z inside the climb zone.</summary>
        public const int ClimbSlotCount = 3;

        struct BallClaim
        {
            public GamePiece Piece;
            public Alliance Alliance;
        }

        static readonly Dictionary<GamePiece, Robot> redBalls = new Dictionary<GamePiece, Robot>();
        static readonly Dictionary<GamePiece, Robot> blueBalls = new Dictionary<GamePiece, Robot>();
        static readonly Dictionary<Robot, BallClaim> claimByRobot = new Dictionary<Robot, BallClaim>();
        static readonly Robot[] redSlots = new Robot[ClimbSlotCount];
        static readonly Robot[] blueSlots = new Robot[ClimbSlotCount];

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            // Supports "Enter Play Mode Options" with domain reload disabled.
            Clear();
        }

        /// <summary>Forgets every claim.</summary>
        public static void Clear()
        {
            redBalls.Clear();
            blueBalls.Clear();
            claimByRobot.Clear();
            System.Array.Clear(redSlots, 0, redSlots.Length);
            System.Array.Clear(blueSlots, 0, blueSlots.Length);
        }

        static Dictionary<GamePiece, Robot> BallsFor(Alliance alliance) => alliance == Alliance.Red ? redBalls : blueBalls;
        static Robot[] SlotsFor(Alliance alliance) => alliance == Alliance.Red ? redSlots : blueSlots;

        static bool IsAlive(Robot robot) => robot != null && robot.isActiveAndEnabled;

        // ---- Balls ------------------------------------------------------------------------------

        /// <summary>Marks <paramref name="piece"/> as <paramref name="robot"/>'s target (replacing its previous claim).</summary>
        public static void ClaimBall(Robot robot, GamePiece piece)
        {
            if (ReferenceEquals(robot, null) || robot.Spec == null) return;
            ReleaseBall(robot);
            if (piece == null) return;
            BallsFor(robot.Alliance)[piece] = robot;
            claimByRobot[robot] = new BallClaim { Piece = piece, Alliance = robot.Alliance };
        }

        public static void ReleaseBall(Robot robot)
        {
            if (ReferenceEquals(robot, null)) return;
            if (!claimByRobot.TryGetValue(robot, out BallClaim claim)) return;
            claimByRobot.Remove(robot);
            Dictionary<GamePiece, Robot> balls = BallsFor(claim.Alliance);
            if (!ReferenceEquals(claim.Piece, null) && balls.TryGetValue(claim.Piece, out Robot owner) && ReferenceEquals(owner, robot))
                balls.Remove(claim.Piece);
        }

        /// <summary>True if a living teammate of <paramref name="robot"/> (not itself) is going for this ball.</summary>
        public static bool IsClaimedByTeammate(GamePiece piece, Robot robot)
        {
            if (piece == null || robot == null || robot.Spec == null) return false;
            return BallsFor(robot.Alliance).TryGetValue(piece, out Robot owner) &&
                   !ReferenceEquals(owner, robot) && IsAlive(owner);
        }

        // ---- Climb slots --------------------------------------------------------------------------

        /// <summary>Ground position of a climb slot (robot center).</summary>
        public static Vector3 ClimbSlotPosition(Alliance alliance, int slot)
        {
            Bounds zone = Arena.ClimbZone(alliance);
            slot = Mathf.Clamp(slot, 0, ClimbSlotCount - 1);
            float spacing = zone.size.z / ClimbSlotCount;
            float z = zone.center.z + (slot - (ClimbSlotCount - 1) * 0.5f) * spacing;
            return new Vector3(zone.center.x, 0f, z);
        }

        /// <summary>True if some other robot is parked on (or very near) the slot.</summary>
        public static bool IsSlotOccupied(Robot robot, int slot)
        {
            if (robot == null || robot.Spec == null) return false;
            Vector3 p = ClimbSlotPosition(robot.Alliance, slot);
            List<Robot> all = Robot.All;
            for (int i = 0; i < all.Count; i++)
            {
                Robot other = all[i];
                if (other == null || other == robot) continue;
                Vector3 d = other.FlatPosition - p;
                d.y = 0f;
                if (d.sqrMagnitude < 0.8f * 0.8f) return true;
            }
            return false;
        }

        /// <summary>The slot <paramref name="robot"/> would take right now (without claiming it).</summary>
        public static int PreviewClimbSlot(Robot robot) => ChooseSlot(robot);

        /// <summary>Claims the best free climb slot for <paramref name="robot"/> and returns its index.</summary>
        public static int ClaimClimbSlot(Robot robot)
        {
            if (robot == null || robot.Spec == null) return 0;
            ReleaseClimbSlot(robot);
            int slot = ChooseSlot(robot);
            SlotsFor(robot.Alliance)[slot] = robot;
            return slot;
        }

        public static void ReleaseClimbSlot(Robot robot)
        {
            if (ReferenceEquals(robot, null)) return;
            for (int i = 0; i < ClimbSlotCount; i++)
            {
                if (ReferenceEquals(redSlots[i], robot)) redSlots[i] = null;
                if (ReferenceEquals(blueSlots[i], robot)) blueSlots[i] = null;
            }
        }

        /// <summary>Releases everything a robot registered (called when it resets, is disabled or destroyed).</summary>
        public static void ReleaseAll(Robot robot)
        {
            ReleaseBall(robot);
            ReleaseClimbSlot(robot);
        }

        static int ChooseSlot(Robot robot)
        {
            if (robot == null || robot.Spec == null) return 0;
            Robot[] slots = SlotsFor(robot.Alliance);
            Vector3 position = robot.FlatPosition;
            int best = -1;
            float bestScore = float.MaxValue;
            for (int i = 0; i < ClimbSlotCount; i++)
            {
                Robot owner = slots[i];
                bool takenByOther = owner != null && !ReferenceEquals(owner, robot) && IsAlive(owner);
                float score = Vector3.Distance(position, ClimbSlotPosition(robot.Alliance, i));
                if (takenByOther) score += 20f;
                if (IsSlotOccupied(robot, i)) score += 10f;
                if (score < bestScore)
                {
                    bestScore = score;
                    best = i;
                }
            }
            return Mathf.Max(0, best);
        }
    }
}
