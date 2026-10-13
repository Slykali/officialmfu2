using System;
using System.Collections.Generic;
using UnityEngine;

namespace FRC6874
{
    /// <summary>
    /// Owns the match flow: Menu (attract mode) → Countdown → Auto → Transition → Teleop → PostMatch,
    /// plus free-play Practice. It is the only writer of <see cref="MatchState.Phase"/> and
    /// <see cref="MatchState.TimeRemaining"/>, spawns the six robots, resets the field before every
    /// (re)start and handles pausing. Timers run on scaled time, so pausing freezes the clock.
    /// </summary>
    public class MatchManager : MonoBehaviour
    {
        /// <summary>The attract-mode demo match behind the main menu restarts this often (seconds).</summary>
        public const float AttractCycleDuration = 75f;
        /// <summary>An unattended results screen returns to the main menu after this many seconds.</summary>
        public const float PostMatchIdleTimeout = 150f;

        sealed class RobotSlot
        {
            public Robot Robot;
            public Alliance Alliance;
            public int Slot;
            public bool IsPlayer;
        }

        static readonly Color[] PartnerAccents =
        {
            new Color(0.62f, 0.64f, 0.68f),
            new Color(0.48f, 0.50f, 0.54f),
        };

        static readonly Color[] OpponentAccents =
        {
            new Color(0.56f, 0.56f, 0.59f),
            new Color(0.70f, 0.70f, 0.72f),
            new Color(0.44f, 0.45f, 0.49f),
        };

        readonly List<RobotSlot> slots = new List<RobotSlot>();
        readonly List<Robot> robots = new List<Robot>();
        Transform robotRoot;
        bool initialized;

        float phaseTimer;
        int lastCountdownSecond;
        bool endgameAnnounced;
        float attractTimer;
        float postMatchTimer;

        /// <summary>Every robot this manager spawned, including AI robots hidden in practice. Do not modify.</summary>
        public IReadOnlyList<Robot> Robots => robots;

        /// <summary>The human-driven robot (team 6874), or null if it failed to spawn.</summary>
        public Robot PlayerRobot { get; private set; }

        /// <summary>True while the pause menu is up (<see cref="Time.timeScale"/> is 0).</summary>
        public bool IsPaused { get; private set; }

        /// <summary>False when the player turned the AI robots off in practice.</summary>
        public bool AIRobotsActive { get; private set; } = true;

        /// <summary>Pausing is possible during a running match and in practice.</summary>
        public bool CanPause
        {
            get
            {
                MatchPhase phase = MatchState.Phase;
                return MatchState.IsMatchInProgress || phase == MatchPhase.Practice;
            }
        }

        /// <summary>Seconds of PostMatch so far (scaled time).</summary>
        public float TimeInPostMatch => MatchState.Phase == MatchPhase.PostMatch ? postMatchTimer : 0f;

        /// <summary>
        /// Called first when the pause key is pressed. Return true to consume it (the UI uses this to
        /// close an overlay instead of toggling pause).
        /// </summary>
        public Func<bool> BackHandler { get; set; }

        /// <summary>Raised once per match when teleop enters the endgame.</summary>
        public event Action EndgameStarted;

        /// <summary>Raised when the match is paused (true) or resumed (false).</summary>
        public event Action<bool> PauseChanged;

        /// <summary>Spawns the robots under <paramref name="parent"/>. Called once by the bootstrap.</summary>
        public void Initialize(Transform parent)
        {
            if (initialized) return;
            robotRoot = new GameObject("Robots").transform;
            robotRoot.SetParent(parent, false);
            SpawnRobots(MatchState.PlayerAlliance);
            initialized = true;
        }

        // ---- Public flow controls -------------------------------------------------------------

        /// <summary>Goes to the main menu with every robot AI-driven in the background.</summary>
        public void ReturnToMenu()
        {
            ClearPause();
            AIRobotsActive = true;
            MatchState.AttractMode = true;
            ResetFieldAndRobots();
            attractTimer = AttractCycleDuration;
            MatchState.TimeRemaining = attractTimer;
            MatchState.SetPhase(MatchPhase.Menu);
        }

        /// <summary>Starts a full match (countdown first) with the player on <paramref name="alliance"/>.</summary>
        public void StartMatch(Alliance alliance)
        {
            ClearPause();
            EnsureAlliance(alliance);
            AIRobotsActive = true;
            MatchState.AttractMode = false;
            ResetFieldAndRobots();

            phaseTimer = GameConfig.CountdownDuration;
            lastCountdownSecond = Mathf.CeilToInt(phaseTimer);
            endgameAnnounced = false;
            MatchState.TimeRemaining = Mathf.Max(0f, phaseTimer);
            MatchState.SetPhase(MatchPhase.Countdown);
            if (phaseTimer > 0f) GameAudio.Play(GameSound.Countdown);
        }

        /// <summary>Starts free play with no clock. AI robots are on until the player turns them off.</summary>
        public void StartPractice(Alliance alliance)
        {
            ClearPause();
            EnsureAlliance(alliance);
            AIRobotsActive = true;
            MatchState.AttractMode = false;
            ResetFieldAndRobots();

            phaseTimer = 0f;
            MatchState.TimeRemaining = 0f;
            MatchState.SetPhase(MatchPhase.Practice);
        }

        /// <summary>Starts a new match on the same alliance.</summary>
        public void Rematch() => StartMatch(MatchState.PlayerAlliance);

        /// <summary>Restarts whatever is running: practice restarts practice, anything else starts a new match.</summary>
        public void RestartCurrent()
        {
            if (MatchState.Phase == MatchPhase.Practice) StartPractice(MatchState.PlayerAlliance);
            else StartMatch(MatchState.PlayerAlliance);
        }

        /// <summary>Practice only: clears the scores and puts the balls and robots back at the start.</summary>
        public void ResetPracticeField()
        {
            if (MatchState.Phase != MatchPhase.Practice) return;
            ResetFieldAndRobots();
        }

        /// <summary>
        /// Shows or hides the AI robots. They can only be turned off in practice; turning them back on
        /// returns them to their starting spots.
        /// </summary>
        public void SetAIRobotsActive(bool active)
        {
            if (active == AIRobotsActive) return;
            if (!active && MatchState.Phase != MatchPhase.Practice) return;
            AIRobotsActive = active;

            // Robots reappear on their start spots; move the player home first if parked on one.
            if (active) ResetPlayerIfOnStartSpot();

            foreach (RobotSlot slot in slots)
            {
                if (slot.IsPlayer || slot.Robot == null) continue;
                GameObject go = slot.Robot.gameObject;
                if (active)
                {
                    go.SetActive(true);
                    ResetRobot(slot);
                }
                else
                {
                    go.SetActive(false);
                }
            }
            Physics.SyncTransforms();
        }

        /// <summary>Pauses (time scale 0) or resumes. Ignored when <see cref="CanPause"/> is false.</summary>
        public void SetPaused(bool paused)
        {
            if (paused && !CanPause) return;
            if (paused == IsPaused) return;
            IsPaused = paused;
            Time.timeScale = paused ? 0f : 1f;
            AudioListener.pause = paused;
            PauseChanged?.Invoke(paused);
        }

        // ---- Frame update -----------------------------------------------------------------------

        void Update()
        {
            if (!initialized) return;

            if (GameInput.PausePressed)
            {
                bool consumed = BackHandler != null && BackHandler();
                if (!consumed && CanPause) SetPaused(!IsPaused);
            }

            float dt = Time.deltaTime;   // scaled: 0 while paused
            switch (MatchState.Phase)
            {
                case MatchPhase.Menu:
                    attractTimer -= dt;
                    if (attractTimer <= 0f)
                    {
                        ResetFieldAndRobots();
                        attractTimer = AttractCycleDuration;
                    }
                    MatchState.TimeRemaining = Mathf.Max(0f, attractTimer);
                    break;

                case MatchPhase.Countdown:
                    phaseTimer -= dt;
                    MatchState.TimeRemaining = Mathf.Max(0f, phaseTimer);
                    int second = Mathf.CeilToInt(phaseTimer);
                    if (second < lastCountdownSecond && second > 0)
                    {
                        lastCountdownSecond = second;
                        GameAudio.Play(GameSound.Countdown);
                    }
                    if (phaseTimer <= 0f) BeginAuto();
                    break;

                case MatchPhase.Auto:
                    phaseTimer -= dt;
                    MatchState.TimeRemaining = Mathf.Max(0f, phaseTimer);
                    if (phaseTimer <= 0f) BeginTransition();
                    break;

                case MatchPhase.Transition:
                    phaseTimer -= dt;
                    MatchState.TimeRemaining = Mathf.Max(0f, phaseTimer);
                    if (phaseTimer <= 0f) BeginTeleop();
                    break;

                case MatchPhase.Teleop:
                    phaseTimer -= dt;
                    MatchState.TimeRemaining = Mathf.Max(0f, phaseTimer);
                    if (!endgameAnnounced && phaseTimer <= GameConfig.EndgameDuration)
                    {
                        endgameAnnounced = true;
                        GameAudio.Play(GameSound.EndgameWarning);
                        EndgameStarted?.Invoke();
                    }
                    if (phaseTimer <= 0f) EndMatch();
                    break;

                case MatchPhase.PostMatch:
                    MatchState.TimeRemaining = 0f;
                    postMatchTimer += dt;
                    if (postMatchTimer >= PostMatchIdleTimeout) ReturnToMenu();
                    break;

                case MatchPhase.Practice:
                    MatchState.TimeRemaining = 0f;
                    break;
            }
        }

        void BeginAuto()
        {
            phaseTimer = GameConfig.AutoDuration;
            MatchState.TimeRemaining = phaseTimer;
            MatchState.SetPhase(MatchPhase.Auto);
            GameAudio.Play(GameSound.MatchStart);
        }

        void BeginTransition()
        {
            phaseTimer = GameConfig.TransitionDuration;
            MatchState.TimeRemaining = phaseTimer;
            MatchState.SetPhase(MatchPhase.Transition);
            GameAudio.Play(GameSound.AutoEnd);
        }

        void BeginTeleop()
        {
            phaseTimer = GameConfig.TeleopDuration;
            endgameAnnounced = false;
            MatchState.TimeRemaining = phaseTimer;
            MatchState.SetPhase(MatchPhase.Teleop);
            GameAudio.Play(GameSound.TeleopStart);
        }

        void EndMatch()
        {
            phaseTimer = 0f;
            postMatchTimer = 0f;
            MatchState.TimeRemaining = 0f;
            MatchState.SetPhase(MatchPhase.PostMatch);
            GameAudio.Play(GameSound.MatchEnd);
        }

        void ClearPause()
        {
            bool was = IsPaused;
            IsPaused = false;
            Time.timeScale = 1f;
            AudioListener.pause = false;
            if (was) PauseChanged?.Invoke(false);
        }

        void OnDisable()
        {
            // Never leave the game frozen or muted when this manager goes away.
            Time.timeScale = 1f;
            AudioListener.pause = false;
            IsPaused = false;
        }

        // ---- Robots and field -----------------------------------------------------------------

        /// <summary>Reset order matters: scores, then balls, then robots onto their start spots.</summary>
        void ResetFieldAndRobots()
        {
            MatchState.ResetScores();
            try
            {
                FieldBuilder.ResetGamePieces();
            }
            catch (Exception e)
            {
                Debug.LogException(e);
            }

            foreach (RobotSlot slot in slots)
            {
                if (slot.Robot == null) continue;
                GameObject go = slot.Robot.gameObject;
                bool active = slot.IsPlayer || AIRobotsActive;
                if (!active)
                {
                    if (go.activeSelf) go.SetActive(false);
                    continue;
                }
                if (!go.activeSelf) go.SetActive(true);
                ResetRobot(slot);
            }
            Physics.SyncTransforms();
        }

        void ResetPlayerIfOnStartSpot()
        {
            RobotSlot player = slots.Find(s => s.IsPlayer && s.Robot != null);
            if (player == null) return;
            Vector3 p = player.Robot.transform.position;
            foreach (RobotSlot slot in slots)
            {
                if (slot.IsPlayer) continue;
                Vector3 d = Arena.StartPosition(slot.Alliance, slot.Slot) - p;
                d.y = 0f;
                if (d.magnitude < 1.3f)
                {
                    ResetRobot(player);
                    return;
                }
            }
        }

        static void ResetRobot(RobotSlot slot)
        {
            try
            {
                slot.Robot.ResetForMatch(Arena.StartPosition(slot.Alliance, slot.Slot),
                    Arena.AllianceForwardYaw(slot.Alliance), GameConfig.PreloadFuel);
            }
            catch (Exception e)
            {
                Debug.LogException(e);
            }
        }

        void EnsureAlliance(Alliance alliance)
        {
            if (alliance != MatchState.PlayerAlliance || slots.Count == 0) SpawnRobots(alliance);
        }

        /// <summary>
        /// Player in the middle slot of their alliance with AI partners on both sides ("AI1", "AI2"),
        /// three AI opponents ("AI3".."AI5") on the other alliance.
        /// </summary>
        void SpawnRobots(Alliance playerAlliance)
        {
            DestroyRobots();
            MatchState.PlayerAlliance = playerAlliance;

            int perAlliance = Mathf.Max(1, GameConfig.RobotsPerAlliance);
            int playerSlot = perAlliance / 2;
            int aiNumber = 1;

            int partner = 0;
            for (int slot = 0; slot < perAlliance; slot++)
            {
                if (slot == playerSlot)
                {
                    AddRobot(new RobotSpec
                    {
                        TeamNumber = GameConfig.TeamNumber,
                        BumperLabel = GameConfig.TeamNumber.ToString(),
                        DisplayName = GameConfig.TeamName,
                        Alliance = playerAlliance,
                        Driver = DriverType.Human,
                        AccentColor = GameConfig.TeamAccentColor,
                    }, playerAlliance, slot, true);
                    continue;
                }

                AddRobot(new RobotSpec
                {
                    TeamNumber = 0,
                    BumperLabel = "AI" + aiNumber,
                    DisplayName = "AI Partner " + (partner + 1),
                    Alliance = playerAlliance,
                    Driver = DriverType.AI,
                    AccentColor = PartnerAccents[partner % PartnerAccents.Length],
                }, playerAlliance, slot, false);
                aiNumber++;
                partner++;
            }

            Alliance opponents = playerAlliance.Opponent();
            for (int slot = 0; slot < perAlliance; slot++)
            {
                AddRobot(new RobotSpec
                {
                    TeamNumber = 0,
                    BumperLabel = "AI" + aiNumber,
                    DisplayName = "AI Opponent " + (slot + 1),
                    Alliance = opponents,
                    Driver = DriverType.AI,
                    AccentColor = OpponentAccents[slot % OpponentAccents.Length],
                }, opponents, slot, false);
                aiNumber++;
            }
            Physics.SyncTransforms();
        }

        void AddRobot(RobotSpec spec, Alliance alliance, int slot, bool isPlayer)
        {
            Robot robot = null;
            try
            {
                robot = RobotFactory.Create(spec, Arena.StartPosition(alliance, slot),
                    Arena.AllianceForwardYaw(alliance), robotRoot);
            }
            catch (Exception e)
            {
                Debug.LogException(e);
            }
            if (robot == null) return;

            slots.Add(new RobotSlot { Robot = robot, Alliance = alliance, Slot = slot, IsPlayer = isPlayer });
            robots.Add(robot);
            if (isPlayer) PlayerRobot = robot;
        }

        void DestroyRobots()
        {
            foreach (RobotSlot slot in slots)
            {
                if (slot.Robot == null) continue;
                // Deactivate first so the robot leaves Robot.All right now (Destroy is deferred).
                slot.Robot.gameObject.SetActive(false);
                Destroy(slot.Robot.gameObject);
            }
            slots.Clear();
            robots.Clear();
            PlayerRobot = null;
        }
    }
}
