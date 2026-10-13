using UnityEngine;

namespace FRC6874
{
    public enum Alliance
    {
        Red,
        Blue,
    }

    public static class AllianceExtensions
    {
        public static Color ToColor(this Alliance alliance) =>
            alliance == Alliance.Red ? GameConfig.RedAllianceColor : GameConfig.BlueAllianceColor;

        public static Alliance Opponent(this Alliance alliance) =>
            alliance == Alliance.Red ? Alliance.Blue : Alliance.Red;

        public static string DisplayName(this Alliance alliance) =>
            alliance == Alliance.Red ? "RED" : "BLUE";
    }
}
