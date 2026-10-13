using UnityEngine;

namespace FRC6874
{
    /// <summary>
    /// Every tunable number for the demo lives here so the team can tweak the game
    /// without hunting through scripts. Units are meters, seconds and degrees.
    ///
    /// Field coordinate system: X runs the length of the field (red alliance wall at -X,
    /// blue alliance wall at +X), Z runs across the width, Y is up. The hub sits at the origin.
    /// </summary>
    public static class GameConfig
    {
        // ---- Team branding -------------------------------------------------------------
        public const int TeamNumber = 6874;
        public const string TeamName = "FRC Team 6874";
        public const string GameTitle = "TEAM 6874: ROBOT ARENA";
        public const string GameSubtitle = "An FRC-style demo game";
        /// <summary>Accent color used on the team's robot (LEDs, chassis trim). Change to your team colors.</summary>
        public static readonly Color TeamAccentColor = new Color(1.00f, 0.76f, 0.05f);
        public static readonly Color TeamSecondaryColor = new Color(0.10f, 0.10f, 0.12f);

        // ---- Alliance colors ----------------------------------------------------------
        public static readonly Color RedAllianceColor = new Color(0.86f, 0.12f, 0.14f);
        public static readonly Color BlueAllianceColor = new Color(0.10f, 0.35f, 0.90f);

        // ---- Field --------------------------------------------------------------------
        public const float FieldLength = 16.5f;          // X extent, inside the perimeter
        public const float FieldWidth = 8.2f;            // Z extent, inside the perimeter
        public const float PerimeterHeight = 0.5f;       // side rails (polycarbonate above to 1 m)
        public const float AllianceWallHeight = 2.0f;    // driver station walls at each end

        // Hub: one shared goal at the center of the field.
        public const float HubBaseRadius = 0.9f;         // solid lower body; robots cannot drive here
        public const float HubOpeningRadius = 0.75f;     // radius of the scoring opening at the top
        public const float HubOpeningHeight = 2.2f;      // height of the scoring opening's rim
        public const float HubEjectDelay = 0.8f;         // seconds a scored ball spends inside the hub
        public const float HubEjectSpeed = 2.5f;         // speed balls roll back out of the hub

        public const float StartingZoneDepth = 2.5f;     // depth of each alliance's starting zone from its wall
        public const float ClimbZoneDepth = 1.85f;       // depth of the climb zone from the alliance wall
        public const float ClimbZoneZMin = 0.9f;         // red climb zone spans Z in [0.9, 3.9]; blue is mirrored
        public const float ClimbZoneZMax = 3.9f;
        public const float StartLineInset = 1.65f;       // robot start X distance from its alliance wall

        // ---- Game pieces ("fuel" balls) ---------------------------------------------------
        public const float FuelRadius = 0.12f;
        public const float FuelMass = 0.2f;
        public static readonly Color FuelColor = new Color(1.0f, 0.86f, 0.10f);

        // ---- Robots -----------------------------------------------------------------------
        public const int RobotsPerAlliance = 3;
        public const float RobotLength = 0.9f;           // frame + bumpers
        public const float RobotWidth = 0.9f;
        public const float RobotFrameHeight = 0.35f;
        public const float RobotMass = 55f;
        public const float RobotMaxSpeed = 4.5f;         // m/s
        public const float RobotAcceleration = 12f;      // m/s^2
        public const float RobotTurnRate = 300f;         // deg/s
        public const float AIRobotSpeedScale = 0.8f;     // AI robots drive a little slower than the player
        public const int FuelCapacity = 5;
        public const int PreloadFuel = 3;
        public const float ShotsPerSecond = 5f;
        public const float ShooterMaxRange = 8.0f;       // horizontal distance to the hub center
        public const float ShooterMinRange = 1.4f;
        public const float ClimbDuration = 2.5f;
        public const float ClimbHeight = 0.6f;

        // ---- Match timing -------------------------------------------------------------------
        public const float CountdownDuration = 3f;
        public const float AutoDuration = 15f;
        public const float TransitionDuration = 3f;
        public const float TeleopDuration = 135f;
        public const float EndgameDuration = 20f;        // last N seconds of teleop
        public const float ScoringGracePeriod = 3f;      // balls already in the air still count briefly after a period ends

        // ---- Points -----------------------------------------------------------------------------
        public const int PointsFuelAuto = 2;
        public const int PointsFuelTeleop = 1;
        public const int PointsLeave = 3;
        public const int PointsClimb = 10;
    }
}
