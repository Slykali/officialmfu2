using UnityEngine;

namespace FRC6874
{
    /// <summary>
    /// Scripted autonomous routine every robot runs during auto (the player's too): spin up while
    /// facing the hub, drive a little downfield out of the starting zone (leave points, and a closer
    /// shot), score the preload, then hand over to the <see cref="AIDriver"/> to collect and score
    /// more until the period ends. Start delay, distance and lateral offset vary by start lane so
    /// six robots don't collide.
    /// </summary>
    public sealed class AutoRoutine
    {
        enum Step
        {
            SpinUp,
            DriveOut,
            ScorePreload,
            Cycle,
        }

        readonly Robot robot;
        Step step;
        bool started;
        float stepSince;
        float startDelay;
        Vector3 driveGoal;

        public AutoRoutine(Robot robot)
        {
            this.robot = robot;
        }

        /// <summary>Short label for the HUD (shown as "AUTO: " + Label).</summary>
        public string Label { get; private set; } = "SPINNING UP";

        public void Reset()
        {
            started = false;
            step = Step.SpinUp;
            Label = "SPINNING UP";
        }

        public void Tick(ref RobotCommand cmd)
        {
            float now = Time.time;
            if (!started) Begin(now);

            Vector3 toHub = Arena.HubCenter - robot.FlatPosition;
            switch (step)
            {
                case Step.SpinUp:
                    cmd.SpinUp = true;
                    cmd.FaceDirection(toHub);
                    Label = "SPINNING UP";
                    if (now - stepSince >= startDelay) Next(Step.DriveOut, now);
                    break;

                case Step.DriveOut:
                {
                    cmd.Velocity = robot.Navigator.Steer(driveGoal, robot.MaxSpeed * 0.85f, 0.25f, 0f, out bool arrived);
                    cmd.FaceDirection(toHub);
                    cmd.SpinUp = true;
                    cmd.Intake = robot.HeldFuel < robot.Capacity;
                    Label = robot.HasLeftStartingZone ? "MOVING UP" : "LEAVING ZONE";
                    if (arrived || now - stepSince > 2.5f)
                        Next(robot.HeldFuel > 0 ? Step.ScorePreload : Step.Cycle, now);
                    break;
                }

                case Step.ScorePreload:
                    cmd.Velocity = robot.Navigator.Steer(driveGoal, robot.MaxSpeed * 0.5f, 0.3f, 0f, out _);
                    cmd.Shoot = true;
                    cmd.SpinUp = true;
                    cmd.ForceAutoAim = true;
                    Label = "SCORING PRELOAD";
                    if (robot.HeldFuel <= 0 || now - stepSince > 3.5f) Next(Step.Cycle, now);
                    break;

                case Step.Cycle:
                    robot.AI.Tick(ref cmd, true);
                    Label = robot.AI.Label;
                    break;
            }
        }

        void Begin(float now)
        {
            started = true;
            step = Step.SpinUp;
            stepSince = now;

            int lane = robot.StartLane;
            startDelay = 0.1f + Mathf.Abs(lane) * 0.2f + Random.Range(0f, 0.15f);

            Vector3 position = robot.FlatPosition;
            Vector3 forward = Arena.AllianceForward(robot.Alliance);
            float distance = (lane == 0 ? 2.1f : 1.8f) + Random.Range(-0.1f, 0.15f);
            float outward = lane == 0 ? 0f : Mathf.Sign(position.z) * 0.35f;
            driveGoal = position + forward * distance + new Vector3(0f, 0f, outward);
        }

        void Next(Step next, float now)
        {
            step = next;
            stepSince = now;
            robot.Navigator.Reset();
            if (next == Step.Cycle) robot.AI.Reset();
        }
    }
}
