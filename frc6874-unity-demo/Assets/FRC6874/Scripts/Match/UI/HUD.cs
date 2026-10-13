using System;
using System.Collections.Generic;
using UnityEngine;

namespace FRC6874
{
    /// <summary>
    /// The in-game IMGUI overlay: broadcast-style scoreboard, driver panel, floating score popups,
    /// scoring feed, phase banners, the 3-2-1 countdown, hints and the practice bar. Full-screen
    /// menus (main menu, pause, controls, results) are drawn by <see cref="MenuScreens"/>.
    /// Everything is laid out on a virtual canvas 1080 units tall (1920 wide at 16:9, wider or
    /// narrower with the aspect ratio) and scaled with GUI.matrix, so it reads the same at 720p,
    /// 1080p and 4K.
    /// </summary>
    public class HUD : MonoBehaviour
    {
        public const float VirtualHeight = 1080f;
        /// <summary>Narrow screens scale down further so the canvas is never narrower than this.</summary>
        public const float MinVirtualWidth = 1280f;

        const float PopupLifetime = 1.2f;
        const int MaxPopups = 32;
        const float FeedLifetime = 6f;
        const int MaxFeed = 7;
        const float BannerDuration = 1.6f;
        const string FirstMatchHint = "WASD drive  ·  Space intake  ·  F shoot  ·  H help";

        struct Popup
        {
            public Vector3 World;
            public string Text;
            public Color Color;
            public float Start;
            public float Jitter;
            public bool Big;
        }

        struct FeedEntry
        {
            public string Text;
            public Color Stripe;
            public bool Highlight;
            public float Start;
        }

        public HudSkin Skin { get; private set; }
        public MatchManager Match { get; private set; }
        public CameraRig CameraRig { get; private set; }
        public GameAudioSynth AudioSynth { get; private set; }
        public MenuScreens Menus { get; private set; }

        /// <summary>Points the player's robot scored this match (from <see cref="MatchState.Scored"/> sources).</summary>
        public int PlayerPoints { get; private set; }
        /// <summary>Fuel the player's robot scored this match.</summary>
        public int PlayerFuel { get; private set; }
        public bool PlayerLeftZone { get; private set; }
        public bool PlayerClimbed { get; private set; }

        /// <summary>Size of the virtual canvas during the last OnGUI.</summary>
        public float CanvasWidth { get; private set; } = 1920f;
        public float CanvasHeight { get; private set; } = 1080f;
        /// <summary>Screen pixels per virtual unit during the last OnGUI.</summary>
        public float CanvasScale { get; private set; } = 1f;

        readonly List<Popup> popups = new List<Popup>();
        readonly List<FeedEntry> feed = new List<FeedEntry>();

        string bannerTitle;
        string bannerSubtitle;
        Color bannerColor;
        float bannerStart = -100f;
        float bannerLength = BannerDuration;

        string toastText;
        float toastStart = -100f;
        float toastLength;
        bool hintShown;

        float redFlash, blueFlash;
        int lastRed, lastBlue;
        float lastPracticeReset = -100f;

        bool subscribed;
        Func<bool> backHandler;

        /// <summary>Connects the HUD to the game systems. Called once by the bootstrap.</summary>
        public void Initialize(MatchManager match, CameraRig rig, GameAudioSynth audioSynth)
        {
            Unsubscribe();
            Match = match;
            CameraRig = rig;
            AudioSynth = audioSynth;
            if (Skin == null) Skin = new HudSkin();
            Menus = new MenuScreens(this);
            Subscribe();
        }

        /// <summary>Shows a short message near the bottom of the screen.</summary>
        public void ShowToast(string text, float seconds = 5f)
        {
            toastText = text;
            toastStart = Time.unscaledTime;
            toastLength = seconds;
        }

        /// <summary>Shows a big center banner (phase changes, endgame...).</summary>
        public void ShowBanner(string title, string subtitle, Color accent, float seconds = BannerDuration)
        {
            bannerTitle = title;
            bannerSubtitle = subtitle;
            bannerColor = accent;
            bannerStart = Time.unscaledTime;
            bannerLength = seconds;
        }

        /// <summary>
        /// Scales everything drawn next around <paramref name="pivot"/>, given in virtual-canvas units.
        /// (GUIUtility.ScaleAroundPivot expects screen pixels and pre-multiplies, which drifts under
        /// the canvas scale whenever the screen is not exactly 1080 px tall.)
        /// </summary>
        static void ScaleAround(Vector2 scale, Vector2 pivot)
        {
            GUI.matrix *= Matrix4x4.TRS(pivot, Quaternion.identity, new Vector3(scale.x, scale.y, 1f)) *
                          Matrix4x4.TRS(-pivot, Quaternion.identity, Vector3.one);
        }

        /// <summary>m:ss, rounding up so a 15 s period reads 0:15 → 0:01 → 0:00.</summary>
        public static string FormatClock(float seconds)
        {
            int s = Mathf.CeilToInt(Mathf.Max(0f, seconds));
            return (s / 60) + ":" + (s % 60).ToString("00");
        }

        void Subscribe()
        {
            if (subscribed) return;
            MatchState.Scored += OnScored;
            MatchState.PhaseChanged += OnPhaseChanged;
            if (Match != null)
            {
                Match.EndgameStarted += OnEndgameStarted;
                backHandler = Menus.HandleBack;
                Match.BackHandler = backHandler;
            }
            subscribed = true;
        }

        void Unsubscribe()
        {
            if (!subscribed) return;
            MatchState.Scored -= OnScored;
            MatchState.PhaseChanged -= OnPhaseChanged;
            if (Match != null)
            {
                Match.EndgameStarted -= OnEndgameStarted;
                if (Match.BackHandler == backHandler) Match.BackHandler = null;
            }
            backHandler = null;
            subscribed = false;
        }

        void OnDestroy()
        {
            Unsubscribe();
            if (Skin != null) Skin.Dispose();
        }

        // ---- Events -------------------------------------------------------------------------------

        void OnPhaseChanged(MatchPhase previous, MatchPhase next)
        {
            if (Menus != null) Menus.OnPhaseChanged();
            switch (next)
            {
                case MatchPhase.Menu:
                    ClearTransient();
                    toastStart = -100f;
                    break;

                case MatchPhase.Countdown:
                    ClearTransient();
                    ResetPlayerStats();
                    if (!hintShown)
                    {
                        hintShown = true;
                        ShowToast(FirstMatchHint, 9f);
                    }
                    break;

                case MatchPhase.Auto:
                    ShowBanner("AUTONOMOUS", "Robots run their auto routines", HudSkin.Accent);
                    break;

                case MatchPhase.Teleop:
                    ShowBanner("TELEOP", "You're driving " + GameConfig.TeamNumber + "!", HudSkin.Good);
                    break;

                case MatchPhase.PostMatch:
                    ShowBanner("MATCH OVER", "", HudSkin.TextColor, 1.5f);
                    break;

                case MatchPhase.Practice:
                    ClearTransient();
                    ResetPlayerStats();
                    ShowBanner("PRACTICE MODE", "Free play · no clock", HudSkin.Accent);
                    if (!hintShown)
                    {
                        hintShown = true;
                        ShowToast(FirstMatchHint, 9f);
                    }
                    break;
            }
        }

        void OnEndgameStarted()
        {
            ShowBanner("ENDGAME — CLIMB!", "Get into your climb zone", HudSkin.Amber, 1.8f);
        }

        void OnScored(ScoreEvent e)
        {
            string popupText;
            string source = string.IsNullOrEmpty(e.Source) ? e.Alliance.DisplayName() : e.Source;
            string feedText;
            switch (e.Kind)
            {
                case ScoreKind.Leave:
                    popupText = "+" + e.Points + " LEAVE";
                    feedText = source + " left the zone +" + e.Points;
                    break;
                case ScoreKind.Climb:
                    popupText = "+" + e.Points + " CLIMB";
                    feedText = source + " climbed +" + e.Points;
                    break;
                default:
                    popupText = "+" + e.Points;
                    feedText = source + " scored +" + e.Points;
                    break;
            }

            if (popups.Count >= MaxPopups) popups.RemoveAt(0);
            popups.Add(new Popup
            {
                World = e.Position + Vector3.up * 0.3f,
                Text = popupText,
                Color = HudSkin.AllianceText(e.Alliance),
                Start = Time.unscaledTime,
                Jitter = UnityEngine.Random.Range(-36f, 36f),
                Big = e.Kind != ScoreKind.Fuel,
            });

            bool mine = IsPlayerSource(e.Source);
            feed.Insert(0, new FeedEntry
            {
                Text = feedText,
                Stripe = e.Alliance.ToColor(),
                Highlight = mine,
                Start = Time.unscaledTime,
            });
            if (feed.Count > MaxFeed) feed.RemoveAt(feed.Count - 1);

            if (mine)
            {
                PlayerPoints += e.Points;
                if (e.Kind == ScoreKind.Fuel) PlayerFuel++;
                if (e.Kind == ScoreKind.Leave) PlayerLeftZone = true;
                if (e.Kind == ScoreKind.Climb) PlayerClimbed = true;
            }
        }

        bool IsPlayerSource(string source)
        {
            if (string.IsNullOrEmpty(source)) return false;
            Robot player = Match != null ? Match.PlayerRobot : Robot.Human;
            if (player != null && player.Spec != null &&
                (source == player.Spec.BumperLabel || source == player.Spec.DisplayName)) return true;
            return source == GameConfig.TeamNumber.ToString();
        }

        void ResetPlayerStats()
        {
            PlayerPoints = 0;
            PlayerFuel = 0;
            PlayerLeftZone = false;
            PlayerClimbed = false;
        }

        void ClearTransient()
        {
            popups.Clear();
            feed.Clear();
            bannerStart = -100f;
            lastRed = MatchState.Red.Total;
            lastBlue = MatchState.Blue.Total;
            redFlash = blueFlash = 0f;
        }

        // ---- Frame update ---------------------------------------------------------------------------

        void Update()
        {
            if (Menus != null) Menus.Update();

            float dt = Time.unscaledDeltaTime;
            redFlash = Mathf.Max(0f, redFlash - dt * 2.5f);
            blueFlash = Mathf.Max(0f, blueFlash - dt * 2.5f);
            if (MatchState.Red.Total != lastRed)
            {
                if (MatchState.Red.Total > lastRed) redFlash = 1f;
                lastRed = MatchState.Red.Total;
            }
            if (MatchState.Blue.Total != lastBlue)
            {
                if (MatchState.Blue.Total > lastBlue) blueFlash = 1f;
                lastBlue = MatchState.Blue.Total;
            }

            float now = Time.unscaledTime;
            for (int i = popups.Count - 1; i >= 0; i--)
                if (now - popups[i].Start > PopupLifetime) popups.RemoveAt(i);
            for (int i = feed.Count - 1; i >= 0; i--)
                if (now - feed[i].Start > FeedLifetime) feed.RemoveAt(i);
        }

        void OnGUI()
        {
            if (Skin == null) Skin = new HudSkin();
            Skin.EnsureBuilt();

            float scale = Mathf.Max(0.05f, Mathf.Min(Screen.height / VirtualHeight, Screen.width / MinVirtualWidth));
            CanvasScale = scale;
            CanvasWidth = Screen.width / scale;
            CanvasHeight = Screen.height / scale;
            Matrix4x4 previousMatrix = GUI.matrix;
            GUI.matrix = Matrix4x4.Scale(new Vector3(scale, scale, 1f));
            Skin.Opacity = 1f;

            HandleHotkeys();

            float W = CanvasWidth, H = CanvasHeight;
            MatchPhase phase = MatchState.Phase;
            bool live = MatchState.IsMatchInProgress || phase == MatchPhase.Practice;
            bool resultsUp = Menus != null && Menus.ResultsVisible;

            if (live) DrawPlayerMarker();
            if (live || phase == MatchPhase.PostMatch) DrawPopups();
            if (phase != MatchPhase.Menu) DrawScoreboard(W);
            if (live) DrawDriverPanel(H);
            if (phase != MatchPhase.Menu && !resultsUp) DrawFeed(W);
            if (phase == MatchPhase.Practice) DrawPracticeBar(W);
            if (phase == MatchPhase.Countdown) DrawCountdown(W, H);
            if (phase == MatchPhase.Transition) DrawTransition(W, H);
            if (live) DrawClimbHint(W, H);
            if (!resultsUp) DrawBanner(W, H);
            if (live) DrawToast(W, H);

            if (Menus != null) Menus.Draw(W, H);

            Skin.Opacity = 1f;
            GUI.matrix = previousMatrix;
        }

        /// <summary>Practice "R" reset, read from IMGUI key events (works with either input backend).</summary>
        void HandleHotkeys()
        {
            Event e = Event.current;
            if (e.type != EventType.KeyDown || e.keyCode != KeyCode.R) return;
            if (MatchState.Phase != MatchPhase.Practice || Match == null || Match.IsPaused) return;
            e.Use();
            if (Time.unscaledTime - lastPracticeReset < 0.4f) return;   // ignore key repeat
            ResetPracticeField();
        }

        /// <summary>Practice: resets balls, robots and scores, with a short confirmation toast.</summary>
        public void ResetPracticeField()
        {
            if (Match == null || MatchState.Phase != MatchPhase.Practice) return;
            lastPracticeReset = Time.unscaledTime;
            Match.ResetPracticeField();
            ShowToast("Field reset", 1.5f);
        }

        // ---- Scoreboard ----------------------------------------------------------------------------

        void DrawScoreboard(float W)
        {
            HudSkin s = Skin;
            MatchPhase phase = MatchState.Phase;
            const float top = 14f, centerW = 280f, centerH = 118f, sideW = 240f, sideH = 96f, gap = 6f;
            float cx = W * 0.5f;

            Rect center = new Rect(cx - centerW * 0.5f, top, centerW, centerH);
            Rect red = new Rect(center.x - gap - sideW, top + 11f, sideW, sideH);
            Rect blue = new Rect(center.xMax + gap, top + 11f, sideW, sideH);

            DrawAllianceBox(red, Alliance.Red, MatchState.Red.Total, redFlash, true);
            DrawAllianceBox(blue, Alliance.Blue, MatchState.Blue.Total, blueFlash, false);

            s.Panel(center, new Color(0.04f, 0.05f, 0.075f, 0.94f));
            s.Fill(new Rect(center.x + 30f, center.y, center.width - 60f, 3f), HudSkin.Accent);

            string label = PhaseLabel(phase);
            Color labelColor = phase == MatchPhase.Auto ? HudSkin.Accent : HudSkin.MutedText;
            s.Text(new Rect(center.x, center.y + 12f, centerW, 24f), label, s.Font(19), labelColor, 0f);

            string clock;
            int clockSize = 64;
            Color clockColor = HudSkin.TextColor;
            float pulse = 0f;
            float remaining = MatchState.TimeRemaining;
            switch (phase)
            {
                case MatchPhase.Countdown:
                    clock = FormatClock(GameConfig.AutoDuration);
                    break;
                case MatchPhase.Transition:
                    clock = FormatClock(GameConfig.TeleopDuration);
                    clockColor = HudSkin.MutedText;
                    break;
                case MatchPhase.Teleop:
                    clock = FormatClock(remaining);
                    if (MatchState.IsEndgame)
                    {
                        clockColor = HudSkin.Amber;
                        if (remaining <= 10f && remaining > 0f)
                        {
                            float frac = remaining - Mathf.Floor(remaining);
                            pulse = frac * frac;
                            clockColor = Color.Lerp(HudSkin.Amber, HudSkin.Warning, pulse);
                        }
                    }
                    break;
                case MatchPhase.PostMatch:
                    clock = "0:00";
                    clockColor = HudSkin.MutedText;
                    break;
                case MatchPhase.Practice:
                    clock = "FREE PLAY";
                    clockSize = 34;
                    break;
                default:
                    clock = FormatClock(remaining);
                    break;
            }

            Rect clockRect = new Rect(center.x, center.y + 36f, centerW, 74f);
            Matrix4x4 m = GUI.matrix;
            if (pulse > 0f) ScaleAround(Vector2.one * (1f + 0.12f * pulse), clockRect.center);
            s.Text(clockRect, clock, s.Font(clockSize), clockColor, 2.5f);
            GUI.matrix = m;

            if (MatchState.IsEndgame)
            {
                Rect pill = new Rect(cx - 82f, center.yMax + 6f, 164f, 30f);
                s.Panel(pill, HudSkin.Amber);
                s.Text(pill, "ENDGAME", s.Font(18), HudSkin.DarkText, 0f);
            }
        }

        void DrawAllianceBox(Rect r, Alliance alliance, int total, float flash, bool leftSide)
        {
            HudSkin s = Skin;
            bool isPlayer = MatchState.PlayerAlliance == alliance;
            Color panel = HudSkin.AlliancePanel(alliance);

            if (isPlayer)
            {
                // A tab tucked under the box marks the player's alliance.
                Rect tab = new Rect(r.x + 36f, r.yMax - 12f, r.width - 72f, 38f);
                Color tabColor = new Color(panel.r * 0.6f, panel.g * 0.6f, panel.b * 0.6f, 0.95f);
                s.Panel(tab, tabColor);
                s.Text(new Rect(tab.x, r.yMax, tab.width, 26f), "YOUR ALLIANCE", s.Font(14), HudSkin.TextColor, 0f);
            }

            s.Panel(r, Color.Lerp(panel, new Color(1f, 1f, 1f, panel.a), flash * 0.35f));
            s.Fill(new Rect(r.x + 16f, r.y + r.height - 4f, r.width - 32f, 1f), new Color(1f, 1f, 1f, 0.12f));

            TextAnchor nameAnchor = leftSide ? TextAnchor.MiddleLeft : TextAnchor.MiddleRight;
            TextAnchor scoreAnchor = leftSide ? TextAnchor.MiddleRight : TextAnchor.MiddleLeft;
            s.Text(new Rect(r.x + 20f, r.y, r.width - 40f, r.height), alliance.DisplayName(), s.Font(24, true, nameAnchor),
                new Color(1f, 1f, 1f, 0.85f), 1.5f);

            Rect scoreRect = new Rect(r.x + 20f, r.y + 2f, r.width - 40f, r.height);
            Matrix4x4 m = GUI.matrix;
            if (flash > 0f)
            {
                Vector2 pivot = new Vector2(leftSide ? scoreRect.xMax - 30f : scoreRect.x + 30f, scoreRect.center.y);
                ScaleAround(Vector2.one * (1f + 0.18f * flash), pivot);
            }
            s.Text(scoreRect, total.ToString(), s.Font(66, true, scoreAnchor), HudSkin.TextColor, 3f);
            GUI.matrix = m;
        }

        static string PhaseLabel(MatchPhase phase)
        {
            switch (phase)
            {
                case MatchPhase.Countdown: return "GET READY";
                case MatchPhase.Auto: return "AUTONOMOUS";
                case MatchPhase.Transition: return "TELEOP NEXT";
                case MatchPhase.Teleop: return "TELEOP";
                case MatchPhase.PostMatch: return "FINAL";
                case MatchPhase.Practice: return "PRACTICE";
                default: return "";
            }
        }

        // ---- Driver panel -------------------------------------------------------------------------------

        void DrawDriverPanel(float H)
        {
            Robot robot = Robot.Human;
            if (robot == null || robot.Spec == null) return;
            HudSkin s = Skin;

            const float w = 440f, h = 266f, labelW = 82f;
            Rect panel = new Rect(24f, H - 24f - h, w, h);
            s.Panel(panel, HudSkin.PanelColor);
            s.Fill(new Rect(panel.x + 12f, panel.y + 16f, 5f, h - 32f), robot.Alliance.ToColor());

            float x = panel.x + 32f;
            float right = panel.xMax - 22f;
            float y = panel.y + 14f;

            // Header
            s.Text(new Rect(x, y, 260f, 36f), "TEAM " + GameConfig.TeamNumber, s.Font(30, true, TextAnchor.MiddleLeft), HudSkin.Accent, 2f);
            s.Text(new Rect(right - 220f, y, 220f, 36f), robot.Alliance.DisplayName() + " ALLIANCE",
                s.Font(15, true, TextAnchor.MiddleRight), HudSkin.AllianceText(robot.Alliance), 1f);
            y += 46f;

            // Fuel pips
            int capacity = Mathf.Max(1, robot.Capacity);
            int held = Mathf.Clamp(robot.HeldFuel, 0, capacity);
            RowLabel(x, y, "FUEL");
            float pipX = x + labelW;
            float pipSpace = Mathf.Min(32f, (right - 70f - pipX) / capacity);
            float pipSize = Mathf.Min(24f, pipSpace - 4f);
            for (int i = 0; i < capacity; i++)
            {
                Rect pip = new Rect(pipX + i * pipSpace, y + 14f - pipSize * 0.5f, pipSize, pipSize);
                if (i < held) s.Image(pip, s.CircleTexture, GameConfig.FuelColor);
                else s.Image(pip, s.RingTexture, HudSkin.WithAlpha(HudSkin.MutedText, 0.55f));
            }
            s.Text(new Rect(right - 80f, y, 80f, 28f), held + " / " + capacity, s.Font(21, true, TextAnchor.MiddleRight),
                held >= capacity ? HudSkin.Accent : HudSkin.TextColor, 1.5f);
            y += 38f;

            // Status
            RowLabel(x, y, "STATUS");
            s.Text(new Rect(x + labelW, y, right - x - labelW, 28f), StatusText(robot), s.Font(20, true, TextAnchor.MiddleLeft),
                HudSkin.TextColor, 1.5f);
            y += 38f;

            // Toggles
            bool aim = robot.AutoAim;
            float px = x;
            px += s.Pill(px, y, aim ? "AUTO-AIM ON" : "AUTO-AIM OFF", 16,
                aim ? HudSkin.WithAlpha(HudSkin.Good, 0.92f) : HudSkin.Neutral, aim ? HudSkin.DarkText : HudSkin.MutedText) + 10f;
            bool fieldCentric = robot.DriveMode == DriveMode.FieldCentric;
            px += s.Pill(px, y, fieldCentric ? "FIELD-CENTRIC" : "ROBOT-CENTRIC", 16,
                fieldCentric ? new Color(0.10f, 0.55f, 0.66f, 0.95f) : new Color(0.50f, 0.34f, 0.80f, 0.95f), HudSkin.TextColor) + 10f;
            s.Text(new Rect(px, y, right - px, 30f), "T · G", s.Font(14, true, TextAnchor.MiddleRight), HudSkin.MutedText, 0f);
            y += 42f;

            // Camera
            RowLabel(x, y, "CAMERA");
            string cameraName = CameraRig != null ? CameraRig.ModeName : "DRIVER STATION";
            s.Text(new Rect(x + labelW, y, right - x - labelW, 28f), cameraName, s.Font(18, true, TextAnchor.MiddleLeft), HudSkin.TextColor, 1f);
            s.Text(new Rect(right - 60f, y, 60f, 28f), "V", s.Font(14, true, TextAnchor.MiddleRight), HudSkin.MutedText, 0f);
            y += 34f;

            // Hub distance and shooter range
            float distance = HubDistance(robot);
            bool tooClose = distance < GameConfig.ShooterMinRange;
            bool inRange = !tooClose && distance <= GameConfig.ShooterMaxRange;
            RowLabel(x, y, "HUB");
            s.Text(new Rect(x + labelW, y, 140f, 28f), distance.ToString("0.0") + " m", s.Font(20, true, TextAnchor.MiddleLeft), HudSkin.TextColor, 1.5f);
            s.Text(new Rect(right - 220f, y, 220f, 28f), inRange ? "IN RANGE" : tooClose ? "TOO CLOSE" : "OUT OF RANGE",
                s.Font(16, true, TextAnchor.MiddleRight), inRange ? HudSkin.Good : HudSkin.Amber, 1f);
            y += 34f;

            Rect bar = new Rect(x, y, right - x, 10f);
            float span = GameConfig.ShooterMaxRange + 2f;
            float bandStart = bar.x + bar.width * Mathf.Clamp01(GameConfig.ShooterMinRange / span);
            float bandEnd = bar.x + bar.width * Mathf.Clamp01(GameConfig.ShooterMaxRange / span);
            s.Fill(bar, new Color(1f, 1f, 1f, 0.10f));
            s.Fill(new Rect(bandStart, bar.y, bandEnd - bandStart, bar.height), HudSkin.WithAlpha(HudSkin.Good, inRange ? 0.7f : 0.4f));
            float marker = bar.x + bar.width * Mathf.Clamp01(distance / span);
            s.Fill(new Rect(marker - 2f, bar.y - 6f, 4f, bar.height + 12f), inRange ? Color.white : HudSkin.Amber);
        }

        void RowLabel(float x, float y, string text) =>
            Skin.Text(new Rect(x, y, 90f, 28f), text, Skin.Font(14, true, TextAnchor.MiddleLeft), HudSkin.MutedText, 0f);

        static string StatusText(Robot robot)
        {
            if (!string.IsNullOrEmpty(robot.Status)) return robot.Status.ToUpperInvariant();
            switch (MatchState.Phase)
            {
                case MatchPhase.Countdown:
                case MatchPhase.Transition:
                    return "DISABLED";
                case MatchPhase.Auto:
                    return "AUTONOMOUS";
                default:
                    return "READY";
            }
        }

        static float HubDistance(Robot robot)
        {
            Vector3 offset = robot.transform.position - Arena.HubCenter;
            offset.y = 0f;
            return offset.magnitude;
        }

        // ---- World-space overlays -------------------------------------------------------------------

        Camera ViewCamera => CameraRig != null && CameraRig.Camera != null ? CameraRig.Camera : Camera.main;

        bool WorldToCanvas(Camera cam, Vector3 world, out Vector2 point)
        {
            Vector3 screen = cam.WorldToScreenPoint(world);
            point = new Vector2(screen.x / CanvasScale, (Screen.height - screen.y) / CanvasScale);
            return screen.z > 0.1f;
        }

        /// <summary>A bobbing accent chevron over the player's robot (not in chase view, where it is obvious).</summary>
        void DrawPlayerMarker()
        {
            if (CameraRig != null && CameraRig.Mode == CameraMode.Chase) return;
            Robot robot = Robot.Human;
            Camera cam = ViewCamera;
            if (robot == null || robot.Spec == null || cam == null) return;
            if (!WorldToCanvas(cam, robot.transform.position + Vector3.up * 1.35f, out Vector2 p)) return;

            float bob = Mathf.Sin(Time.unscaledTime * 3.2f) * 4f;
            Skin.Image(new Rect(p.x - 12f, p.y - 18f + bob, 24f, 18f), Skin.TriangleTexture, HudSkin.Accent);
            Skin.Text(new Rect(p.x - 80f, p.y - 48f + bob, 160f, 28f), robot.Spec.BumperLabel, Skin.Font(20), HudSkin.Accent, 2f);
        }

        void DrawPopups()
        {
            Camera cam = ViewCamera;
            if (cam == null) return;
            float now = Time.unscaledTime;
            foreach (Popup popup in popups)
            {
                float t = (now - popup.Start) / PopupLifetime;
                if (t < 0f || t >= 1f) continue;
                if (!WorldToCanvas(cam, popup.World, out Vector2 p)) continue;

                float alpha = 1f - t * t;
                float pop = t < 0.15f ? Mathf.Lerp(0.6f, 1.15f, t / 0.15f) : Mathf.Lerp(1.15f, 1f, Mathf.Clamp01((t - 0.15f) / 0.15f));
                Vector2 center = new Vector2(p.x + popup.Jitter, p.y - Mathf.Sqrt(t) * 80f);
                Rect r = new Rect(center.x - 160f, center.y - 28f, 320f, 56f);

                Matrix4x4 m = GUI.matrix;
                ScaleAround(Vector2.one * pop, center);
                Skin.Text(r, popup.Text, Skin.Font(popup.Big ? 40 : 34), HudSkin.WithAlpha(popup.Color, alpha), 2.5f);
                GUI.matrix = m;
            }
        }

        // ---- Feed, banners, hints ---------------------------------------------------------------------

        void DrawFeed(float W)
        {
            if (feed.Count == 0) return;
            HudSkin s = Skin;
            const float width = 330f, height = 34f;
            float x = W - 24f - width;
            float y = 176f;
            float now = Time.unscaledTime;
            foreach (FeedEntry entry in feed)
            {
                float age = now - entry.Start;
                float alpha = Mathf.Clamp01((FeedLifetime - age) / 0.8f) * Mathf.Clamp01(age / 0.15f);
                if (alpha <= 0f) continue;
                float slide = (1f - Mathf.Clamp01(age / 0.2f)) * 40f;
                Rect r = new Rect(x + slide, y, width, height);
                s.Panel(r, HudSkin.WithAlpha(HudSkin.PanelColor, 0.8f * alpha));
                s.Fill(new Rect(r.x + 8f, r.y + 8f, 5f, height - 16f), HudSkin.WithAlpha(entry.Stripe, alpha));
                s.Text(new Rect(r.x + 24f, r.y, r.width - 32f, r.height), entry.Text, s.Font(18, true, TextAnchor.MiddleLeft),
                    HudSkin.WithAlpha(entry.Highlight ? HudSkin.Accent : HudSkin.TextColor, alpha), 1f);
                y += height + 6f;
            }
        }

        void DrawBanner(float W, float H)
        {
            float t = Time.unscaledTime - bannerStart;
            if (string.IsNullOrEmpty(bannerTitle) || t < 0f || t > bannerLength) return;
            HudSkin s = Skin;

            float alpha = Mathf.Min(Mathf.SmoothStep(0f, 1f, t / 0.18f), 1f - Mathf.SmoothStep(0f, 1f, (t - (bannerLength - 0.4f)) / 0.4f));
            float grow = 1f - Mathf.Clamp01(t / 0.25f);
            float scale = 1f + 0.25f * grow * grow;
            float cy = H * 0.34f;

            s.Fill(new Rect(0f, cy - 86f, W, 172f), new Color(0f, 0f, 0f, 0.5f * alpha));
            float lineWidth = Mathf.Lerp(0f, W * 0.5f, Mathf.SmoothStep(0f, 1f, t / 0.35f));
            s.Fill(new Rect(W * 0.5f - lineWidth, cy - 86f, lineWidth * 2f, 3f), HudSkin.WithAlpha(bannerColor, alpha));
            s.Fill(new Rect(W * 0.5f - lineWidth, cy + 83f, lineWidth * 2f, 3f), HudSkin.WithAlpha(bannerColor, alpha));

            Matrix4x4 m = GUI.matrix;
            ScaleAround(Vector2.one * scale, new Vector2(W * 0.5f, cy - 12f));
            int size = Skin.FitFontSize(bannerTitle, 100, W - 80f, 48);
            s.Text(new Rect(0f, cy - 76f, W, 120f), bannerTitle, s.Font(size), HudSkin.WithAlpha(HudSkin.TextColor, alpha), 4f);
            GUI.matrix = m;
            if (!string.IsNullOrEmpty(bannerSubtitle))
                s.Text(new Rect(0f, cy + 40f, W, 36f), bannerSubtitle, s.Font(26), HudSkin.WithAlpha(bannerColor, alpha), 2f);
        }

        void DrawCountdown(float W, float H)
        {
            HudSkin s = Skin;
            float remaining = MatchState.TimeRemaining;
            int number = Mathf.Max(1, Mathf.CeilToInt(remaining));
            float frac = Mathf.Clamp01(number - remaining);           // 0 → 1 across each second
            float pop = 1f - Mathf.Clamp01(frac / 0.3f);
            float scale = 1f + 0.6f * pop * pop;
            float alpha = 1f - Mathf.SmoothStep(0f, 1f, (frac - 0.7f) / 0.3f);
            float cy = H * 0.45f;

            s.Text(new Rect(0f, cy - 200f, W, 40f), "MATCH STARTS IN", s.Font(30), HudSkin.MutedText, 2f);
            Matrix4x4 m = GUI.matrix;
            ScaleAround(Vector2.one * scale, new Vector2(W * 0.5f, cy));
            s.Text(new Rect(0f, cy - 140f, W, 280f), number.ToString(), s.Font(240), HudSkin.WithAlpha(HudSkin.Accent, alpha), 5f);
            GUI.matrix = m;

            Alliance alliance = MatchState.PlayerAlliance;
            s.Text(new Rect(0f, cy + 150f, W, 40f), "TEAM " + GameConfig.TeamNumber + "  ·  " + alliance.DisplayName() + " ALLIANCE",
                s.Font(28), HudSkin.AllianceText(alliance), 2f);
        }

        void DrawTransition(float W, float H)
        {
            HudSkin s = Skin;
            int number = Mathf.Max(1, Mathf.CeilToInt(MatchState.TimeRemaining));
            float cy = H * 0.34f;
            s.Fill(new Rect(0f, cy - 70f, W, 140f), new Color(0f, 0f, 0f, 0.4f));
            s.Text(new Rect(0f, cy - 62f, W, 36f), "AUTO COMPLETE", s.Font(28), HudSkin.MutedText, 2f);
            s.Text(new Rect(0f, cy - 26f, W, 90f), "TELEOP IN " + number, s.Font(76), HudSkin.TextColor, 4f);
        }

        /// <summary>Endgame/practice climb guidance for the player.</summary>
        void DrawClimbHint(float W, float H)
        {
            Robot robot = Robot.Human;
            if (robot == null || robot.Spec == null || !robot.IsHumanDriven) return;
            bool practice = MatchState.Phase == MatchPhase.Practice;
            if (!MatchState.IsEndgame && !practice) return;

            string text = null;
            Color color = HudSkin.Accent;
            float pulse = 0.5f + 0.5f * Mathf.Sin(Time.unscaledTime * 6f);
            if (robot.HasClimbed)
            {
                if (!practice) { text = "CLIMBED!  +" + GameConfig.PointsClimb; color = HudSkin.Good; }
            }
            else if (robot.IsClimbing)
            {
                text = "CLIMBING...";
                color = HudSkin.Amber;
            }
            else if (InClimbZone(robot))
            {
                text = "PRESS C / Y TO CLIMB!";
                color = Color.Lerp(HudSkin.Accent, Color.white, pulse * 0.5f);
            }
            else if (!practice)
            {
                text = "ENDGAME: DRIVE TO YOUR CLIMB ZONE";
                color = HudSkin.Amber;
            }
            if (text == null) return;

            GUIStyle style = Skin.Font(26);
            float width = Skin.Measure(text, style).x + 60f;
            float cx = Mathf.Max(W * 0.5f, 24f + 440f + 24f + width * 0.5f);
            Rect r = new Rect(cx - width * 0.5f, H - 210f, width, 54f);
            Skin.Panel(r, HudSkin.WithAlpha(HudSkin.PanelColor, 0.92f));
            Skin.Fill(new Rect(r.x + 20f, r.yMax - 4f, r.width - 40f, 2f), color);
            Skin.Text(r, text, style, color, 2f);
        }

        static bool InClimbZone(Robot robot)
        {
            Bounds zone = Arena.ClimbZone(robot.Alliance);
            Vector3 p = robot.transform.position;
            p.y = zone.center.y;
            return zone.Contains(p);
        }

        void DrawToast(float W, float H)
        {
            float t = Time.unscaledTime - toastStart;
            if (string.IsNullOrEmpty(toastText) || t < 0f || t > toastLength) return;
            float alpha = Mathf.Clamp01(t / 0.3f) * Mathf.Clamp01((toastLength - t) / 0.6f);

            GUIStyle style = Skin.Font(24);
            float width = Skin.Measure(toastText, style).x + 64f;
            float cx = Mathf.Max(W * 0.5f, 24f + 440f + 24f + width * 0.5f);
            Rect r = new Rect(cx - width * 0.5f, H - 134f + (1f - alpha) * 12f, width, 52f);
            Skin.Panel(r, HudSkin.WithAlpha(HudSkin.PanelLight, 0.94f * alpha));
            Skin.Text(r, toastText, style, HudSkin.WithAlpha(HudSkin.TextColor, alpha), 1.5f);
        }

        // ---- Practice ---------------------------------------------------------------------------------

        void DrawPracticeBar(float W)
        {
            if (Match == null || Match.IsPaused) return;
            HudSkin s = Skin;
            const float width = 430f, height = 116f;
            // Below the scoreboard when the canvas is too narrow to fit beside it.
            float top = W * 0.5f - 390f < 24f + width + 16f ? 180f : 18f;
            Rect r = new Rect(24f, top, width, height);
            s.Panel(r, HudSkin.PanelColor);
            s.Text(new Rect(r.x + 22f, r.y + 10f, width - 44f, 30f), "PRACTICE MODE", s.Font(24, true, TextAnchor.MiddleLeft), HudSkin.Accent, 1.5f);
            s.Text(new Rect(r.x + 22f, r.y + 40f, width - 44f, 22f), "R reset field  ·  Esc menu", s.Font(17, false, TextAnchor.MiddleLeft), HudSkin.MutedText, 0f);

            if (s.Button(new Rect(r.x + 22f, r.y + 70f, 180f, 34f), "RESET FIELD", HudSkin.ButtonColor, false, 16))
                ResetPracticeField();
            string aiLabel = Match.AIRobotsActive ? "AI ROBOTS: ON" : "AI ROBOTS: OFF";
            if (s.Button(new Rect(r.x + 212f, r.y + 70f, 196f, 34f), aiLabel, Match.AIRobotsActive ? HudSkin.ButtonColor : HudSkin.Neutral, false, 16))
                Match.SetAIRobotsActive(!Match.AIRobotsActive);
        }
    }
}
