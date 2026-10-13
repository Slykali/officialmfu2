using UnityEngine;

namespace FRC6874
{
    /// <summary>
    /// The reference direction for the driver's stick. The camera rig sets <see cref="YawDegrees"/>
    /// every frame to the yaw that "stick up" should mean (e.g. straight downfield in the driver
    /// station view, or the camera heading in chase view). Robots convert stick input with
    /// <see cref="StickToWorld"/> for field-centric driving.
    /// </summary>
    public static class ControlFrame
    {
        public static float YawDegrees = 90f;

        /// <summary>Converts a stick vector (x = right, y = up) to a world-space XZ direction.</summary>
        public static Vector3 StickToWorld(Vector2 stick) =>
            Quaternion.Euler(0f, YawDegrees, 0f) * new Vector3(stick.x, 0f, stick.y);
    }

    public enum GameSound
    {
        MatchStart,
        AutoEnd,
        TeleopStart,
        EndgameWarning,
        MatchEnd,
        Score,
        Shoot,
        Intake,
        Climb,
        UIClick,
        Countdown,
    }

    /// <summary>
    /// Fire-and-forget sound requests. Gameplay code calls <see cref="Play"/>; the audio system
    /// (created by the bootstrap) installs <see cref="Handler"/>. Safe to call when no handler exists.
    /// </summary>
    public static class GameAudio
    {
        /// <summary>(sound, world position or null for 2D/UI sounds)</summary>
        public static System.Action<GameSound, Vector3?> Handler;

        public static void Play(GameSound sound) => Handler?.Invoke(sound, null);
        public static void Play(GameSound sound, Vector3 position) => Handler?.Invoke(sound, position);
    }
}
