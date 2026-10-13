namespace FRC6874
{
    /// <summary>
    /// Robot-specific tuning knobs (feel, accuracy, AI behavior) that are not part of the shared
    /// <see cref="GameConfig"/>. Units are meters, seconds and degrees.
    /// </summary>
    public static class RobotTuning
    {
        // ---- Geometry ---------------------------------------------------------------------
        /// <summary>Gap between the carpet and the bottom of the bumper collider.</summary>
        public const float GroundClearance = 0.02f;
        /// <summary>Height of the bumper collider band.</summary>
        public const float BumperBandHeight = 0.25f;
        /// <summary>Thickness of the visual bumper cushions.</summary>
        public const float BumperThickness = 0.085f;

        // ---- Drivetrain -------------------------------------------------------------------
        /// <summary>Angular acceleration limit of the drivetrain (deg/s²).</summary>
        public const float TurnAcceleration = 2400f;
        /// <summary>Proportional gain of the heading controller (1/s).</summary>
        public const float HeadingGain = 7f;
        /// <summary>Braking is this much stronger than accelerating.</summary>
        public const float BrakeScale = 1.35f;
        /// <summary>Speed multiplier while the precision button is held.</summary>
        public const float PrecisionScale = 0.4f;

        // ---- Intake -------------------------------------------------------------------------
        /// <summary>How far the collection box reaches in front of the front bumper face.</summary>
        public const float IntakeReach = 0.32f;
        /// <summary>How far the collection box starts behind the front bumper face.</summary>
        public const float IntakeOverlap = 0.05f;
        public const float IntakeHeight = 0.4f;
        /// <summary>Extra reach of the gentle "suck-in" pull beyond the collection box.</summary>
        public const float SuckInReach = 0.45f;
        /// <summary>Acceleration (m/s²) pulling loose balls into the intake while it runs.</summary>
        public const float SuckInAcceleration = 5f;

        // ---- Shooter ------------------------------------------------------------------------
        public const float FlywheelSpinUpTime = 0.3f;
        public const float FlywheelSpinDownTime = 1.5f;
        /// <summary>With auto-aim on, the shooter only fires once the heading is within this many degrees.</summary>
        public const float AimToleranceDegrees = 6f;
        /// <summary>Trajectory apex above the aim point for close shots...</summary>
        public const float ApexAboveAimNear = 0.6f;
        /// <summary>...and for shots at <see cref="GameConfig.ShooterMaxRange"/> (interpolated in between).</summary>
        public const float ApexAboveAimFar = 1.0f;
        /// <summary>The shooter's top exit speed is the speed a max-range shot needs, times this.</summary>
        public const float MaxExitSpeedScale = 1.12f;
        /// <summary>Std-dev of the landing point (m) at 2 m...</summary>
        public const float SpreadNear = 0.20f;
        /// <summary>...at 4 m...</summary>
        public const float SpreadMid = 0.24f;
        /// <summary>...at <see cref="GameConfig.ShooterMaxRange"/>.</summary>
        public const float SpreadAtMaxRange = 0.37f;
        /// <summary>Extra spread per meter beyond max range.</summary>
        public const float SpreadBeyondPerMeter = 0.25f;
        /// <summary>Spread when closer than <see cref="GameConfig.ShooterMinRange"/>.</summary>
        public const float SpreadTooClose = 0.55f;
        /// <summary>Extra spread per m/s of robot speed (shoot-on-the-move is a bit harder).</summary>
        public const float SpreadPerSpeed = 0.025f;
        /// <summary>Linear damping of the balls (matches GamePiece), compensated by the solver.</summary>
        public const float BallLinearDamping = 0.05f;

        // ---- Climber -------------------------------------------------------------------------
        /// <summary>Fraction of <see cref="GameConfig.ClimbDuration"/> spent extending the arm (the rest lifts).</summary>
        public const float ClimbExtendFraction = 0.35f;
        /// <summary>How far the climber arm reaches above its stowed height.</summary>
        public const float ClimbArmReach = 0.95f;
        public const float ClimbLowerDuration = 1.4f;

        // ---- AI -----------------------------------------------------------------------------
        public const float AIReactionMin = 0.25f;
        public const float AIReactionMax = 0.5f;
        /// <summary>A ball "costs" more than this (≈ meters of driving) → not worth collecting before shooting.</summary>
        public const float AIGoodBallCost = 4.5f;
        /// <summary>The AI only switches balls if the new one is this much cheaper.</summary>
        public const float AITargetHysteresis = 0.8f;
        public const float AIShootDistanceMin = 3f;
        public const float AIShootDistanceMax = 5.5f;
        /// <summary>Seconds of spare time the AI keeps when deciding to go climb.</summary>
        public const float AIClimbMargin = 4f;
        /// <summary>Gives up on a shooting attempt after this long.</summary>
        public const float AIShootTimeout = 9f;

        // ---- Navigation ---------------------------------------------------------------------
        public const float FieldMargin = 0.6f;
        /// <summary>Extra clearance the planner keeps around <see cref="Arena.HubKeepOutRadius"/>.</summary>
        public const float HubClearance = 0.35f;
        public const float SeparationRadius = 1.5f;
        public const float SeparationStrength = 2.6f;
        public const float StuckTime = 1.5f;
        public const float RecoverTime = 0.7f;
        public const float RecoverSpeed = 2.5f;
    }
}
