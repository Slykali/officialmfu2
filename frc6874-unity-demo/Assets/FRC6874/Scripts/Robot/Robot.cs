using System.Collections.Generic;
using UnityEngine;

namespace FRC6874
{
    public enum DriverType
    {
        Human,
        AI,
    }

    public enum DriveMode
    {
        /// <summary>Stick "up" always drives along <see cref="ControlFrame.YawDegrees"/> (swerve-style).</summary>
        FieldCentric,
        /// <summary>Stick "up" drives the way the robot is facing.</summary>
        RobotCentric,
    }

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

    /// <summary>
    /// CONTRACT STUB — owned by the Robot agent. Keep these public members (you may add more);
    /// replace the bodies with the real drivetrain / intake / shooter / climber / AI.
    /// </summary>
    public class Robot : MonoBehaviour
    {
        /// <summary>Every robot in the scene.</summary>
        public static readonly List<Robot> All = new List<Robot>();

        /// <summary>The robot whose spec says Driver == Human (null if none).</summary>
        public static Robot Human
        {
            get
            {
                foreach (Robot r in All)
                    if (r != null && r.Spec != null && r.Spec.Driver == DriverType.Human) return r;
                return null;
            }
        }

        public RobotSpec Spec { get; private set; }
        public Alliance Alliance => Spec.Alliance;
        /// <summary>True when the controller currently drives this robot (human robot, not in attract mode).</summary>
        public bool IsHumanDriven => Spec.Driver == DriverType.Human && !MatchState.AttractMode;

        public int HeldFuel { get; protected set; }
        public int Capacity => GameConfig.FuelCapacity;
        public bool AutoAim { get; set; } = true;
        public DriveMode DriveMode { get; set; } = DriveMode.FieldCentric;
        public bool IsClimbing { get; protected set; }
        public bool HasClimbed { get; protected set; }
        public bool HasLeftStartingZone { get; protected set; }
        /// <summary>Short state text for the HUD, e.g. "INTAKING", "SHOOTING", "AUTO: SCORING PRELOAD".</summary>
        public string Status { get; protected set; } = "";
        /// <summary>Point the chase camera should follow/look at.</summary>
        public Transform CameraTarget { get; protected set; }
        public Vector3 Velocity => Vector3.zero;

        /// <summary>Called once by <see cref="RobotFactory.Create"/>.</summary>
        public void Initialize(RobotSpec spec)
        {
            Spec = spec;
            CameraTarget = transform;
        }

        /// <summary>Teleports to a start pose, stops, clears climb/leave state and sets held fuel.</summary>
        public void ResetForMatch(Vector3 position, float yawDegrees, int preloadFuel)
        {
            transform.SetPositionAndRotation(position, Quaternion.Euler(0f, yawDegrees, 0f));
            HeldFuel = preloadFuel;
            IsClimbing = HasClimbed = HasLeftStartingZone = false;
        }

        protected virtual void OnEnable() => All.Add(this);
        protected virtual void OnDisable() => All.Remove(this);
    }

    /// <summary>CONTRACT STUB — owned by the Robot agent.</summary>
    public static class RobotFactory
    {
        /// <summary>Builds a complete robot (visuals, physics, mechanisms, driver) at a pose (y = 0 is the carpet).</summary>
        public static Robot Create(RobotSpec spec, Vector3 position, float yawDegrees, Transform parent)
        {
            var go = new GameObject($"Robot {spec.BumperLabel}");
            go.transform.SetParent(parent, false);
            var robot = go.AddComponent<Robot>();
            robot.Initialize(spec);
            robot.ResetForMatch(position, yawDegrees, GameConfig.PreloadFuel);
            return robot;
        }
    }
}
