using UnityEngine;

namespace FRC6874
{
    /// <summary>Builds complete robots (physics body, mechanisms, drivers and visuals) from a <see cref="RobotSpec"/>.</summary>
    public static class RobotFactory
    {
        /// <summary>Builds a complete robot (visuals, physics, mechanisms, driver) at a pose (y = 0 is the carpet).</summary>
        public static Robot Create(RobotSpec spec, Vector3 position, float yawDegrees, Transform parent)
        {
            if (spec == null) spec = new RobotSpec { BumperLabel = "BOT", DisplayName = "Robot", Driver = DriverType.AI };

            var go = new GameObject($"Robot {spec.BumperLabel}");
            // Build while inactive so Robot.All never sees a half-built robot.
            go.SetActive(false);
            go.transform.SetParent(parent, false);
            go.transform.SetPositionAndRotation(new Vector3(position.x, 0f, position.z), Quaternion.Euler(0f, yawDegrees, 0f));

            var robot = go.AddComponent<Robot>();
            robot.Initialize(spec);
            go.SetActive(true);
            robot.ResetForMatch(position, yawDegrees, GameConfig.PreloadFuel);
            return robot;
        }
    }
}
