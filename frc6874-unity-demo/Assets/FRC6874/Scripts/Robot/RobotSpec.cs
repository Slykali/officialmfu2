using UnityEngine;

namespace FRC6874
{
    /// <summary>Who a robot is and how it is driven.</summary>
    public sealed class RobotSpec
    {
        public int TeamNumber;
        /// <summary>Text printed on the bumpers, e.g. "6874".</summary>
        public string BumperLabel;
        /// <summary>Name for UI, e.g. "Team 6874" or "AI Partner".</summary>
        public string DisplayName;
        public Alliance Alliance;
        public DriverType Driver;
        public Color AccentColor = Color.gray;
    }
}
