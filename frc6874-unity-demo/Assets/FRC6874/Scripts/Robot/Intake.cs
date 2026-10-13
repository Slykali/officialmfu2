using System.Collections.Generic;
using UnityEngine;

namespace FRC6874
{
    /// <summary>
    /// Front intake. While running and not full, any collectable ball whose center is inside a box
    /// at the front bumper (robot width × ~0.35 m deep × 0.4 m tall, tested in robot-local space)
    /// is collected. Loose balls just ahead of the box are gently pulled in.
    /// </summary>
    public sealed class Intake
    {
        readonly Robot robot;

        public Intake(Robot robot)
        {
            this.robot = robot;
        }

        /// <summary>The driver asked for the intake this step.</summary>
        public bool Requested { get; private set; }
        /// <summary>The intake is actually running (requested and the hopper is not full).</summary>
        public bool Running { get; private set; }
        public float LastCollectTime { get; private set; } = -10f;
        /// <summary>Total balls collected since the last reset.</summary>
        public int CollectCount { get; private set; }

        public void Reset()
        {
            Requested = Running = false;
            LastCollectTime = -10f;
        }

        public void Tick(bool want, float dt)
        {
            Requested = want;
            Running = want && robot.HeldFuel < robot.Capacity;
            if (!Running) return;

            Rigidbody body = robot.Body;
            Vector3 origin = body.position;
            Quaternion rotation = body.rotation;
            Quaternion inverse = Quaternion.Inverse(rotation);

            float front = GameConfig.RobotLength * 0.5f;
            float zMin = front - RobotTuning.IntakeOverlap;
            float zMax = front + RobotTuning.IntakeReach;
            float zPull = zMax + RobotTuning.SuckInReach;
            float halfWidth = GameConfig.RobotWidth * 0.5f - 0.02f;
            float pullHalfWidth = halfWidth + 0.15f;
            Vector3 mouth = origin + rotation * new Vector3(0f, GameConfig.FuelRadius, front + 0.12f);

            bool collected = false;
            List<GamePiece> pieces = GamePiece.Active;
            // Backwards: Despawn() removes the piece from the list.
            for (int i = pieces.Count - 1; i >= 0; i--)
            {
                if (i >= pieces.Count) continue;
                GamePiece piece = pieces[i];
                if (piece == null || piece.Body == null) continue;

                Vector3 world = piece.Body.position;
                Vector3 local = inverse * (world - origin);
                if (local.z < zMin || local.z > zPull) continue;
                if (local.y < -0.1f || local.y > RobotTuning.IntakeHeight) continue;
                if (Mathf.Abs(local.x) > pullHalfWidth) continue;
                if (!piece.IsCollectable) continue;

                if (!collected && local.z <= zMax && Mathf.Abs(local.x) <= halfWidth && robot.HeldFuel < robot.Capacity)
                {
                    piece.Despawn();
                    robot.AddFuel(1);
                    collected = true;
                    LastCollectTime = Time.time;
                    CollectCount++;
                    GameAudio.Play(GameSound.Intake, world);
                    continue;
                }

                // Gentle "suck-in" toward the middle of the intake for balls just ahead of it.
                if (piece.State == PieceState.Free && local.z > zMax - 0.08f)
                {
                    Vector3 pull = mouth - world;
                    pull.y = 0f;
                    float distance = pull.magnitude;
                    if (distance > 1e-3f)
                    {
                        float strength = RobotTuning.SuckInAcceleration * Mathf.Clamp01(1.2f - distance / (RobotTuning.SuckInReach + 0.4f));
                        piece.Body.AddForce(pull / distance * strength, ForceMode.Acceleration);
                    }
                }
            }
        }
    }
}
