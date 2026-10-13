using System;
using UnityEngine;

namespace FRC6874
{
    public enum MatchPhase
    {
        /// <summary>Main menu. If <see cref="MatchState.AttractMode"/> is on, AI drives every robot in the background.</summary>
        Menu,
        /// <summary>3-2-1 before autonomous. Robots disabled.</summary>
        Countdown,
        /// <summary>Autonomous period. Robots run their scripted/AI routines; no human input.</summary>
        Auto,
        /// <summary>Short pause between auto and teleop. Robots disabled.</summary>
        Transition,
        /// <summary>Driver-controlled period. The last <see cref="GameConfig.EndgameDuration"/> seconds are endgame.</summary>
        Teleop,
        /// <summary>Match over, results on screen. Robots disabled.</summary>
        PostMatch,
        /// <summary>Free play with no timer. Everything scores, climbing is always allowed.</summary>
        Practice,
    }

    public enum ScoreKind
    {
        /// <summary>A ball shot into the hub.</summary>
        Fuel,
        /// <summary>A robot fully left its starting zone during auto.</summary>
        Leave,
        /// <summary>A robot completed a climb during endgame.</summary>
        Climb,
    }

    public struct ScoreEvent
    {
        public Alliance Alliance;
        public ScoreKind Kind;
        public int Points;
        public Vector3 Position;
        public string Source;
        public float Time;
    }

    public sealed class AllianceScore
    {
        public int Total;
        public int AutoPoints;
        public int FuelCount;
        public int FuelPoints;
        public int LeavePoints;
        public int ClimbPoints;

        public void Clear()
        {
            Total = AutoPoints = FuelCount = FuelPoints = LeavePoints = ClimbPoints = 0;
        }
    }

    /// <summary>
    /// Global match state shared by every system. The MatchManager is the only writer of the
    /// phase and clock; everything else reads it and reports points through <see cref="Award"/>,
    /// which owns the scoring rules so they stay consistent.
    /// </summary>
    public static class MatchState
    {
        public static MatchPhase Phase { get; private set; } = MatchPhase.Menu;
        public static float PhaseStartTime { get; private set; }
        /// <summary>Seconds left in the current timed phase (Countdown, Auto, Transition, Teleop). Set by the MatchManager.</summary>
        public static float TimeRemaining;
        /// <summary>When true during <see cref="MatchPhase.Menu"/>, every robot (including the player's) is AI-driven as a background demo.</summary>
        public static bool AttractMode;
        /// <summary>Which alliance the human driver is on. Set by the MatchManager.</summary>
        public static Alliance PlayerAlliance = Alliance.Red;

        public static readonly AllianceScore Red = new AllianceScore();
        public static readonly AllianceScore Blue = new AllianceScore();

        public static event Action<ScoreEvent> Scored;
        /// <summary>(previous, next)</summary>
        public static event Action<MatchPhase, MatchPhase> PhaseChanged;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            // Supports "Enter Play Mode Options" with domain reload disabled.
            Phase = MatchPhase.Menu;
            PhaseStartTime = 0f;
            TimeRemaining = 0f;
            AttractMode = false;
            PlayerAlliance = Alliance.Red;
            Scored = null;
            PhaseChanged = null;
            ResetScores();
        }

        public static AllianceScore Score(Alliance alliance) => alliance == Alliance.Red ? Red : Blue;

        public static bool IsAuto => Phase == MatchPhase.Auto;
        public static bool IsEndgame => Phase == MatchPhase.Teleop && TimeRemaining <= GameConfig.EndgameDuration;
        public static bool IsMatchInProgress =>
            Phase == MatchPhase.Countdown || Phase == MatchPhase.Auto ||
            Phase == MatchPhase.Transition || Phase == MatchPhase.Teleop;

        /// <summary>True when robots may move and use mechanisms.</summary>
        public static bool RobotsEnabled =>
            Phase == MatchPhase.Auto || Phase == MatchPhase.Teleop || Phase == MatchPhase.Practice ||
            (Phase == MatchPhase.Menu && AttractMode);

        /// <summary>True when a human-driven robot should listen to the controller.</summary>
        public static bool DriverControlEnabled =>
            (Phase == MatchPhase.Teleop || Phase == MatchPhase.Practice) && !AttractMode;

        /// <summary>True when climbing is allowed to start.</summary>
        public static bool ClimbAllowed => IsEndgame || Phase == MatchPhase.Practice || (Phase == MatchPhase.Menu && AttractMode);

        public static float TimeInPhase => Time.time - PhaseStartTime;

        public static void SetPhase(MatchPhase next)
        {
            MatchPhase previous = Phase;
            Phase = next;
            PhaseStartTime = Time.time;
            PhaseChanged?.Invoke(previous, next);
        }

        public static void ResetScores()
        {
            Red.Clear();
            Blue.Clear();
        }

        /// <summary>
        /// Reports a scoring action. Applies the rules for the current phase, adds the points
        /// and raises <see cref="Scored"/>. Returns the points awarded (0 if it did not count).
        /// </summary>
        public static int Award(Alliance alliance, ScoreKind kind, Vector3 position, string source)
        {
            int points = PointsFor(kind, out bool countsAsAuto);
            if (points <= 0) return 0;

            AllianceScore score = Score(alliance);
            score.Total += points;
            if (countsAsAuto) score.AutoPoints += points;
            switch (kind)
            {
                case ScoreKind.Fuel:
                    score.FuelCount++;
                    score.FuelPoints += points;
                    break;
                case ScoreKind.Leave:
                    score.LeavePoints += points;
                    break;
                case ScoreKind.Climb:
                    score.ClimbPoints += points;
                    break;
            }

            Scored?.Invoke(new ScoreEvent
            {
                Alliance = alliance,
                Kind = kind,
                Points = points,
                Position = position,
                Source = source,
                Time = Time.time,
            });
            return points;
        }

        static int PointsFor(ScoreKind kind, out bool countsAsAuto)
        {
            countsAsAuto = false;
            bool inGrace = TimeInPhase <= GameConfig.ScoringGracePeriod;
            switch (kind)
            {
                case ScoreKind.Fuel:
                    if (Phase == MatchPhase.Auto || (Phase == MatchPhase.Transition && inGrace))
                    {
                        countsAsAuto = true;
                        return GameConfig.PointsFuelAuto;
                    }
                    if (Phase == MatchPhase.Teleop || Phase == MatchPhase.Practice ||
                        (Phase == MatchPhase.PostMatch && inGrace))
                        return GameConfig.PointsFuelTeleop;
                    return 0;

                case ScoreKind.Leave:
                    countsAsAuto = true;
                    return Phase == MatchPhase.Auto ? GameConfig.PointsLeave : 0;

                case ScoreKind.Climb:
                    return IsEndgame || Phase == MatchPhase.Practice ? GameConfig.PointsClimb : 0;
            }
            return 0;
        }
    }
}
