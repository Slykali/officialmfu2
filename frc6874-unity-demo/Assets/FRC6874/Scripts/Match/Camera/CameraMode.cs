namespace FRC6874
{
    /// <summary>Player-selectable camera views during a match (cycled with the camera button).</summary>
    public enum CameraMode
    {
        /// <summary>Fixed view from behind the player's alliance wall, like a real driver. True field-centric controls.</summary>
        DriverStation,
        /// <summary>Third-person view behind and above the player's robot.</summary>
        Chase,
        /// <summary>High, side-on TV view of the whole field.</summary>
        Broadcast,
    }
}
