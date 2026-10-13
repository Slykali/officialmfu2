using UnityEngine;

namespace FRC6874
{
    /// <summary>
    /// Full-screen IMGUI menus drawn on top of the HUD: main menu (over the attract-mode match),
    /// pause menu, controls overlay and the post-match results. Buttons work with the mouse and
    /// with keyboard/gamepad: move up/down (W/S or the stick) to change the highlighted button and
    /// press Confirm (Enter / A) to activate it. Owned by <see cref="HUD"/>.
    /// </summary>
    public sealed class MenuScreens
    {
        enum View
        {
            None,
            MainMenu,
            Pause,
            Controls,
            Results,
        }

        /// <summary>Seconds after the final buzzer before the results appear (the "MATCH OVER" banner plays first).</summary>
        public const float ResultsDelay = 1.6f;
        const float ResultsInputDelay = 2.3f;

        static readonly Color RedButton = new Color(0.58f, 0.09f, 0.11f, 0.95f);
        static readonly Color BlueButton = new Color(0.09f, 0.24f, 0.62f, 0.95f);
        static readonly Color GoButton = new Color(0.12f, 0.42f, 0.26f, 0.95f);
        static readonly Color KeyboardText = new Color(1f, 0.87f, 0.52f, 1f);
        static readonly Color GamepadText = new Color(0.62f, 0.80f, 1f, 1f);

        /// <summary>Control bindings as documented in <see cref="GameInput"/>: action, keyboard/mouse, gamepad.</summary>
        static readonly string[,] Bindings =
        {
            { "Drive (swerve)", "W A S D", "Left stick" },
            { "Turn", "Q / E  or  ← / →", "Right stick" },
            { "Intake (hold)", "Space  or  left mouse", "LT  or  RB" },
            { "Shoot (hold)", "F  or  right mouse", "RT" },
            { "Climb (endgame)", "C", "Y" },
            { "Auto-aim on / off", "T", "X" },
            { "Field / robot-centric", "G", "L3 (press left stick)" },
            { "Cycle camera", "V", "View / Back" },
            { "Precision (slow) mode", "Left Shift (hold)", "LB (hold)" },
            { "Pause", "Esc  or  P", "Menu / Start" },
            { "Confirm", "Enter", "A" },
            { "Help (this screen)", "H  or  F1", "–" },
        };

        readonly HUD hud;
        View navView = View.None;
        int navSelected;
        int navCount;
        int navCountLast;
        bool navConfirm;
        bool navArmed;
        float viewShownTime;
        bool resumeOnClose;

        string fittedTitle;
        int fittedTitleSize;

        /// <summary>True while the controls overlay is open.</summary>
        public bool ControlsOpen { get; private set; }

        /// <summary>True once the results screen is up after a match.</summary>
        public bool ResultsVisible => MatchState.Phase == MatchPhase.PostMatch && MatchState.TimeInPhase >= ResultsDelay;

        bool ResultsInteractive => MatchState.Phase == MatchPhase.PostMatch && MatchState.TimeInPhase >= ResultsInputDelay;

        public MenuScreens(HUD hud)
        {
            this.hud = hud;
        }

        View Current
        {
            get
            {
                if (ControlsOpen) return View.Controls;
                if (hud.Match != null && hud.Match.IsPaused) return View.Pause;
                if (MatchState.Phase == MatchPhase.Menu) return View.MainMenu;
                if (ResultsVisible) return View.Results;
                return View.None;
            }
        }

        /// <summary>Opens the controls overlay. During a running match it also pauses (and resumes on close).</summary>
        public void OpenControls()
        {
            if (ControlsOpen) return;
            ControlsOpen = true;
            resumeOnClose = false;
            MatchManager match = hud.Match;
            if (match != null && match.CanPause && !match.IsPaused)
            {
                match.SetPaused(true);
                resumeOnClose = match.IsPaused;
            }
        }

        public void CloseControls()
        {
            if (!ControlsOpen) return;
            ControlsOpen = false;
            if (resumeOnClose && hud.Match != null) hud.Match.SetPaused(false);
            resumeOnClose = false;
        }

        /// <summary>Back/pause key handler for <see cref="MatchManager.BackHandler"/>. Returns true when consumed.</summary>
        public bool HandleBack()
        {
            if (ControlsOpen)
            {
                CloseControls();
                return true;
            }
            if (MatchState.Phase == MatchPhase.PostMatch)
            {
                if (ResultsInteractive && hud.Match != null) hud.Match.ReturnToMenu();
                return true;
            }
            return false;
        }

        /// <summary>Called by the HUD on every phase change: overlays never survive one.</summary>
        public void OnPhaseChanged()
        {
            ControlsOpen = false;
            resumeOnClose = false;
        }

        /// <summary>Keyboard/gamepad navigation. Called from the HUD's Update.</summary>
        public void Update()
        {
            if (GameInput.HelpTogglePressed)
            {
                if (ControlsOpen) CloseControls();
                else OpenControls();
            }

            View view = TrackView();
            if (view == View.None)
            {
                navConfirm = false;
                return;
            }

            Vector2 move = GameInput.Move;
            if (Mathf.Abs(move.x) < 0.35f && Mathf.Abs(move.y) < 0.35f)
            {
                navArmed = true;
            }
            else if (navArmed)
            {
                float v = Mathf.Abs(move.y) >= Mathf.Abs(move.x) ? -move.y : move.x;
                if (Mathf.Abs(v) > 0.6f)
                {
                    navArmed = false;
                    Step(v > 0f ? 1 : -1);
                }
            }

            if (GameInput.ConfirmPressed && (view != View.Results || ResultsInteractive)) navConfirm = true;
        }

        /// <summary>Draws the active menu screen (if any). Called from the HUD's OnGUI with the GUI matrix set.</summary>
        public void Draw(float W, float H)
        {
            View view = TrackView();
            navCount = 0;
            HudSkin s = hud.Skin;
            s.Opacity = Mathf.Clamp01((Time.unscaledTime - viewShownTime) / 0.25f);

            switch (view)
            {
                case View.MainMenu: DrawMainMenu(W, H); break;
                case View.Pause: DrawPause(W, H); break;
                case View.Controls: DrawControls(W, H); break;
                case View.Results: DrawResults(W, H); break;
            }

            s.Opacity = 1f;
            if (Event.current.type == EventType.Repaint)
            {
                navCountLast = navCount;
                if (navCountLast > 0 && navSelected >= navCountLast) navSelected = navCountLast - 1;
                navConfirm = false;
            }
        }

        // ---- Navigation -----------------------------------------------------------------------------

        View TrackView()
        {
            View view = Current;
            if (view != navView)
            {
                navView = view;
                navSelected = 0;
                navConfirm = false;
                navArmed = false;
                navCountLast = 0;
                viewShownTime = Time.unscaledTime;
            }
            return view;
        }

        void Step(int direction)
        {
            if (navCountLast <= 0) return;
            navSelected = (navSelected + direction + navCountLast) % navCountLast;
            GameAudio.Play(GameSound.UIClick);
        }

        /// <summary>A menu button that also responds to keyboard/gamepad focus and Confirm.</summary>
        bool NavButton(Rect r, string label, Color color, int fontSize = 26)
        {
            int index = navCount++;
            bool focused = index == navSelected;
            bool clicked = hud.Skin.Button(r, label, color, focused, fontSize);
            if (!clicked && focused && navConfirm)
            {
                navConfirm = false;
                clicked = true;
                GameAudio.Play(GameSound.UIClick);
            }
            return clicked;
        }

        // ---- Main menu ------------------------------------------------------------------------------

        void DrawMainMenu(float W, float H)
        {
            HudSkin s = hud.Skin;
            MatchManager match = hud.Match;
            float cx = W * 0.5f;

            // Let the attract-mode match show through: light dim, darker edges, translucent column.
            s.Fill(new Rect(0f, 0f, W, H), new Color(0.02f, 0.03f, 0.05f, 0.28f));
            s.Image(new Rect(0f, 0f, W, H), s.VignetteTexture, new Color(0.01f, 0.015f, 0.03f, 0.85f));
            Rect column = new Rect(cx - 390f, 56f, 780f, H - 128f);
            s.Panel(column, new Color(0.035f, 0.045f, 0.075f, 0.66f));
            s.Fill(new Rect(column.x + 40f, column.y, column.width - 80f, 3f), HudSkin.Accent);

            // Title: "TEAM 6874: ROBOT ARENA" becomes an accent kicker line over a big main title.
            string title = GameConfig.GameTitle ?? "";
            string kicker = null;
            int colon = title.IndexOf(':');
            if (colon > 0 && colon < title.Length - 1)
            {
                kicker = title.Substring(0, colon).Trim();
                title = title.Substring(colon + 1).Trim();
            }
            if (fittedTitle != title)
            {
                fittedTitle = title;
                fittedTitleSize = s.FitFontSize(title, kicker != null ? 104 : 84, column.width - 60f, 40);
            }

            float y = column.y + 44f;
            if (kicker != null)
            {
                s.Text(new Rect(column.x, y, column.width, 44f), kicker, s.Font(34), HudSkin.Accent, 2f);
                y += 46f;
            }
            float titleHeight = fittedTitleSize * 1.15f;
            s.Text(new Rect(column.x, y, column.width, titleHeight), title, s.Font(fittedTitleSize), HudSkin.TextColor, 4f);
            y += titleHeight + 2f;
            s.Text(new Rect(column.x, y, column.width, 34f), GameConfig.GameSubtitle, s.Font(26, false), HudSkin.MutedText, 1f);
            y += 46f;
            s.Fill(new Rect(cx - 70f, y, 140f, 3f), HudSkin.Accent);
            y += 14f;
            s.Text(new Rect(column.x, y, column.width, 30f), GameConfig.TeamName.ToUpperInvariant(), s.Font(21), HudSkin.TextColor, 1f);
            y += 56f;

            const float bw = 520f, bh = 62f, gap = 14f;
            float bx = cx - bw * 0.5f;
            if (match != null)
            {
                if (NavButton(new Rect(bx, y, bw, bh), "PLAY MATCH: RED ALLIANCE", RedButton)) { match.StartMatch(Alliance.Red); return; }
                y += bh + gap;
                if (NavButton(new Rect(bx, y, bw, bh), "PLAY MATCH: BLUE ALLIANCE", BlueButton)) { match.StartMatch(Alliance.Blue); return; }
                y += bh + gap;
                if (NavButton(new Rect(bx, y, bw, bh), "PRACTICE MODE", HudSkin.ButtonColor)) { match.StartPractice(MatchState.PlayerAlliance); return; }
                y += bh + gap;
            }
            if (NavButton(new Rect(bx, y, bw, bh), "CONTROLS", HudSkin.ButtonColor)) { OpenControls(); return; }
            y += bh + gap;

            GameAudioSynth audio = hud.AudioSynth;
            bool muted = audio != null && audio.Muted;
            if (NavButton(new Rect(bx, y, bw, bh), muted ? "SOUND: OFF" : "SOUND: ON", HudSkin.ButtonColor) && audio != null)
            {
                audio.Muted = !audio.Muted;
                if (!audio.Muted) GameAudio.Play(GameSound.UIClick);
            }
            y += bh + gap;

            if (Application.platform != RuntimePlatform.WebGLPlayer)
            {
                if (NavButton(new Rect(bx, y, bw, bh), "QUIT", HudSkin.ButtonColor)) Application.Quit();
                y += bh + gap;
            }

            s.Text(new Rect(column.x, y + 2f, column.width, 24f), "Enter: play red  ·  W/S or stick: select  ·  H: controls",
                s.Font(17, false), HudSkin.MutedText, 0f);

            s.Text(new Rect(0f, H - 48f, W, 28f),
                "Unofficial FRC-style demo by " + GameConfig.TeamName + " · Not affiliated with FIRST®",
                s.Font(17, false), HudSkin.MutedText, 1f);
        }

        // ---- Pause ----------------------------------------------------------------------------------

        void DrawPause(float W, float H)
        {
            HudSkin s = hud.Skin;
            MatchManager match = hud.Match;
            if (match == null) return;
            bool practice = MatchState.Phase == MatchPhase.Practice;
            float cx = W * 0.5f;

            s.Fill(new Rect(0f, 0f, W, H), new Color(0f, 0f, 0f, 0.55f));

            const float bw = 440f, bh = 58f, gap = 12f;
            int buttons = practice ? 6 : 4;
            float height = 156f + buttons * (bh + gap) + 44f;
            Rect r = new Rect(cx - 280f, (H - height) * 0.5f, 560f, height);
            s.Panel(r, new Color(0.05f, 0.06f, 0.09f, 0.97f));
            s.Fill(new Rect(r.x + 40f, r.y, r.width - 80f, 3f), HudSkin.Accent);

            s.Text(new Rect(r.x, r.y + 26f, r.width, 64f), "PAUSED", s.Font(58), HudSkin.TextColor, 3f);
            string subtitle = practice
                ? "PRACTICE MODE"
                : MatchState.Phase == MatchPhase.Countdown
                    ? "MATCH STARTING"
                    : MatchState.Phase.ToString().ToUpperInvariant() + "  ·  " + HUD.FormatClock(MatchState.TimeRemaining) + " LEFT";
            s.Text(new Rect(r.x, r.y + 92f, r.width, 26f), subtitle, s.Font(19, false), HudSkin.MutedText, 0f);

            float y = r.y + 140f;
            float bx = cx - bw * 0.5f;
            if (NavButton(new Rect(bx, y, bw, bh), "RESUME", GoButton)) { match.SetPaused(false); return; }
            y += bh + gap;
            if (practice)
            {
                if (NavButton(new Rect(bx, y, bw, bh), "RESET FIELD", HudSkin.ButtonColor))
                {
                    match.SetPaused(false);
                    hud.ResetPracticeField();
                    return;
                }
                y += bh + gap;
                if (NavButton(new Rect(bx, y, bw, bh), match.AIRobotsActive ? "AI ROBOTS: ON" : "AI ROBOTS: OFF", HudSkin.ButtonColor))
                    match.SetAIRobotsActive(!match.AIRobotsActive);
                y += bh + gap;
            }
            if (NavButton(new Rect(bx, y, bw, bh), practice ? "RESTART PRACTICE" : "RESTART MATCH", HudSkin.ButtonColor)) { match.RestartCurrent(); return; }
            y += bh + gap;
            if (NavButton(new Rect(bx, y, bw, bh), "CONTROLS", HudSkin.ButtonColor)) { OpenControls(); return; }
            y += bh + gap;
            if (NavButton(new Rect(bx, y, bw, bh), "MAIN MENU", HudSkin.ButtonColor)) { match.ReturnToMenu(); return; }

            s.Text(new Rect(r.x, r.yMax - 42f, r.width, 24f), "Esc to resume", s.Font(17, false), HudSkin.MutedText, 0f);
        }

        // ---- Controls -------------------------------------------------------------------------------

        void DrawControls(float W, float H)
        {
            HudSkin s = hud.Skin;
            s.Fill(new Rect(0f, 0f, W, H), new Color(0f, 0f, 0f, 0.66f));

            float width = Mathf.Min(1240f, W - 32f);
            const float height = 800f;
            Rect r = new Rect((W - width) * 0.5f, Mathf.Max(16f, (H - height) * 0.5f), width, height);
            s.Panel(r, new Color(0.05f, 0.06f, 0.09f, 0.97f));

            float x = r.x + 40f;
            float y = r.y + 28f;
            float inner = r.width - 80f;
            s.Text(new Rect(x, y, 500f, 56f), "CONTROLS", s.Font(48, true, TextAnchor.MiddleLeft), HudSkin.TextColor, 3f);
            s.Text(new Rect(r.xMax - 40f - 400f, y, 400f, 56f), "Esc / H to close", s.Font(18, false, TextAnchor.MiddleRight), HudSkin.MutedText, 0f);
            y += 64f;
            s.Fill(new Rect(x, y, inner, 2f), HudSkin.WithAlpha(HudSkin.Accent, 0.8f));
            y += 22f;
            float sectionTop = y;

            // Bindings table
            const float scoreWidth = 380f, columnGap = 44f, rowHeight = 38f;
            float tableWidth = inner - scoreWidth - columnGap;
            float col1 = x + tableWidth * 0.35f;
            float col2 = x + tableWidth * 0.69f;
            GUIStyle header = s.Font(14, true, TextAnchor.MiddleLeft);
            s.Text(new Rect(x, y, 200f, 26f), "ACTION", header, HudSkin.MutedText, 0f);
            s.Text(new Rect(col1, y, 240f, 26f), "KEYBOARD & MOUSE", header, HudSkin.MutedText, 0f);
            s.Text(new Rect(col2, y, 240f, 26f), "GAMEPAD", header, HudSkin.MutedText, 0f);
            y += 32f;

            GUIStyle actionStyle = s.Font(19, true, TextAnchor.MiddleLeft);
            GUIStyle bindStyle = s.Font(19, true, TextAnchor.MiddleLeft);
            int rows = Bindings.GetLength(0);
            for (int i = 0; i < rows; i++)
            {
                if (i % 2 == 0) s.Fill(new Rect(x - 12f, y, tableWidth + 24f, rowHeight), new Color(1f, 1f, 1f, 0.035f));
                s.Text(new Rect(x, y, col1 - x - 8f, rowHeight), Bindings[i, 0], actionStyle, HudSkin.TextColor, 0f);
                s.Text(new Rect(col1, y, col2 - col1 - 8f, rowHeight), Bindings[i, 1], bindStyle, KeyboardText, 0f);
                s.Text(new Rect(col2, y, x + tableWidth - col2, rowHeight), Bindings[i, 2], bindStyle, GamepadText, 0f);
                y += rowHeight;
            }

            // How to score
            float sx = x + tableWidth + columnGap;
            float sy = sectionTop;
            s.Panel(new Rect(sx - 20f, sy - 8f, scoreWidth + 20f, r.yMax - 110f - (sy - 8f)), new Color(1f, 1f, 1f, 0.04f));
            s.Text(new Rect(sx, sy, scoreWidth, 30f), "HOW TO SCORE", s.Font(22, true, TextAnchor.MiddleLeft), HudSkin.Accent, 0f);
            sy += 40f;
            GUIStyle headingStyle = s.Font(16, true, TextAnchor.MiddleLeft);
            GUIStyle bodyStyle = s.Font(18, false, TextAnchor.UpperLeft, true);
            float textWidth = scoreWidth - 20f;
            sy = ScoreTip(s, sx, sy, textWidth, headingStyle, bodyStyle, "THE GOAL",
                "Collect yellow fuel (up to " + GameConfig.FuelCapacity + " at a time) and shoot it into the hub in the middle of the field.");
            sy = ScoreTip(s, sx, sy, textWidth, headingStyle, bodyStyle, "AUTO  ·  FIRST " + HUD.FormatClock(GameConfig.AutoDuration),
                "Robots drive themselves. Leaving the starting zone +" + GameConfig.PointsLeave +
                ", fuel +" + GameConfig.PointsFuelAuto + " each.");
            sy = ScoreTip(s, sx, sy, textWidth, headingStyle, bodyStyle, "TELEOP  ·  " + HUD.FormatClock(GameConfig.TeleopDuration),
                "You drive " + GameConfig.TeamNumber + ". Fuel +" + GameConfig.PointsFuelTeleop + " each.");
            sy = ScoreTip(s, sx, sy, textWidth, headingStyle, bodyStyle, "ENDGAME  ·  LAST " + HUD.FormatClock(GameConfig.EndgameDuration),
                "Drive into your alliance's climb zone and climb (C / Y) for +" + GameConfig.PointsClimb + ".");
            ScoreTip(s, sx, sy, textWidth, headingStyle, bodyStyle, "TIP",
                "Auto-aim (T / X) turns you toward the hub. Shoot from " + GameConfig.ShooterMinRange.ToString("0.#") +
                " to " + GameConfig.ShooterMaxRange.ToString("0.#") + " m.");

            if (NavButton(new Rect(r.center.x - 130f, r.yMax - 84f, 260f, 56f), "CLOSE", HudSkin.ButtonColor)) CloseControls();
        }

        static float ScoreTip(HudSkin s, float x, float y, float width, GUIStyle headingStyle, GUIStyle bodyStyle, string heading, string body)
        {
            s.Text(new Rect(x, y, width, 22f), heading, headingStyle, HudSkin.TextColor, 0f);
            y += 24f;
            float h = s.MeasureHeight(body, bodyStyle, width);
            s.Text(new Rect(x, y, width, h), body, bodyStyle, HudSkin.MutedText, 0f);
            return y + h + 14f;
        }

        // ---- Results --------------------------------------------------------------------------------

        void DrawResults(float W, float H)
        {
            HudSkin s = hud.Skin;
            MatchManager match = hud.Match;
            float t = MatchState.TimeInPhase - ResultsDelay;
            s.Opacity = Mathf.Clamp01(t / 0.4f);
            float cx = W * 0.5f;

            s.Fill(new Rect(0f, 0f, W, H), new Color(0f, 0f, 0f, 0.45f));

            AllianceScore red = MatchState.Red;
            AllianceScore blue = MatchState.Blue;
            Alliance player = MatchState.PlayerAlliance;
            int diff = red.Total - blue.Total;
            bool tie = diff == 0;
            bool won = !tie && (diff > 0) == (player == Alliance.Red);
            string header = tie ? "TIE" : diff > 0 ? "RED WINS" : "BLUE WINS";
            Color band = tie ? HudSkin.Neutral : HudSkin.AlliancePanel(diff > 0 ? Alliance.Red : Alliance.Blue);

            const float width = 880f, height = 680f;
            float top = Mathf.Max(170f, 170f + (H - 200f - height) * 0.5f) + (1f - s.Opacity) * 30f;
            Rect r = new Rect(cx - width * 0.5f, top, width, height);
            s.Panel(r, new Color(0.05f, 0.06f, 0.09f, 0.96f));

            Rect bandRect = new Rect(r.x, r.y, r.width, 116f);
            s.Panel(bandRect, band);
            s.Fill(new Rect(r.x, bandRect.yMax - 14f, r.width, 14f), band);   // square off the band's lower corners
            s.Text(bandRect, header, s.Font(72), HudSkin.TextColor, 3f);

            float y = bandRect.yMax + 16f;
            string verdict = tie ? "DRAW" : won ? "VICTORY!" : "DEFEAT";
            Color verdictColor = tie ? HudSkin.TextColor : won ? HudSkin.Accent : HudSkin.MutedText;
            s.Text(new Rect(r.x, y, r.width, 50f), verdict, s.Font(44), verdictColor, 2f);
            y += 52f;
            s.Text(new Rect(r.x, y, r.width, 24f), "TEAM " + GameConfig.TeamNumber + "  ·  " + player.DisplayName() + " ALLIANCE",
                s.Font(18), HudSkin.AllianceText(player), 1f);
            y += 42f;

            // Breakdown table
            const float headerHeight = 36f, rowHeight = 38f, columnWidth = 170f;
            float labelX = r.x + 80f;
            float redX = r.x + 420f;
            float blueX = r.x + 620f;
            float tableTop = y;
            float tableHeight = headerHeight + rowHeight * 5f + 12f + 50f;
            float playerX = player == Alliance.Red ? redX : blueX;
            s.Panel(new Rect(playerX - 8f, tableTop - 6f, columnWidth + 16f, tableHeight + 10f),
                HudSkin.WithAlpha(player.ToColor(), 0.16f));

            GUIStyle headStyle = s.Font(22);
            s.Text(new Rect(redX, y, columnWidth, headerHeight), player == Alliance.Red ? "RED · YOU" : "RED", headStyle, HudSkin.AllianceText(Alliance.Red), 1f);
            s.Text(new Rect(blueX, y, columnWidth, headerHeight), player == Alliance.Blue ? "BLUE · YOU" : "BLUE", headStyle, HudSkin.AllianceText(Alliance.Blue), 1f);
            y += headerHeight;

            y = ResultRow(s, y, labelX, redX, blueX, columnWidth, rowHeight, "AUTO POINTS", red.AutoPoints, blue.AutoPoints);
            y = ResultRow(s, y, labelX, redX, blueX, columnWidth, rowHeight, "FUEL SCORED", red.FuelCount, blue.FuelCount);
            y = ResultRow(s, y, labelX, redX, blueX, columnWidth, rowHeight, "FUEL POINTS", red.FuelPoints, blue.FuelPoints);
            y = ResultRow(s, y, labelX, redX, blueX, columnWidth, rowHeight, "LEAVE POINTS", red.LeavePoints, blue.LeavePoints);
            y = ResultRow(s, y, labelX, redX, blueX, columnWidth, rowHeight, "CLIMB POINTS", red.ClimbPoints, blue.ClimbPoints);
            y += 4f;
            s.Fill(new Rect(labelX, y, blueX + columnWidth - labelX, 2f), new Color(1f, 1f, 1f, 0.18f));
            y += 8f;

            GUIStyle totalStyle = s.Font(36);
            s.Text(new Rect(labelX, y, 300f, 50f), "TOTAL", s.Font(24, true, TextAnchor.MiddleLeft), HudSkin.TextColor, 1f);
            s.Text(new Rect(redX, y, columnWidth, 50f), red.Total.ToString(), totalStyle, diff > 0 ? HudSkin.Accent : HudSkin.TextColor, 2f);
            s.Text(new Rect(blueX, y, columnWidth, 50f), blue.Total.ToString(), totalStyle, diff < 0 ? HudSkin.Accent : HudSkin.TextColor, 2f);
            y += 62f;

            string contribution = "TEAM " + GameConfig.TeamNumber + " THIS MATCH:  " + hud.PlayerPoints + " PTS  ·  " + hud.PlayerFuel + " FUEL";
            if (hud.PlayerLeftZone) contribution += "  ·  LEFT ZONE";
            if (hud.PlayerClimbed) contribution += "  ·  CLIMBED";
            s.Text(new Rect(r.x, y, r.width, 26f), contribution, s.Font(19), HudSkin.Accent, 1f);

            const float bw = 250f, bh = 60f;
            float by = r.yMax - 96f;
            bool interactive = ResultsInteractive && match != null;
            if (NavButton(new Rect(cx - bw - 10f, by, bw, bh), "REMATCH", player == Alliance.Red ? RedButton : BlueButton) && interactive)
            {
                match.Rematch();
                return;
            }
            if (NavButton(new Rect(cx + 10f, by, bw, bh), "MAIN MENU", HudSkin.ButtonColor) && interactive)
            {
                match.ReturnToMenu();
                return;
            }
            s.Text(new Rect(r.x, r.yMax - 30f, r.width, 22f), "Enter: rematch  ·  Esc: main menu", s.Font(15, false), HudSkin.MutedText, 0f);
        }

        static float ResultRow(HudSkin s, float y, float labelX, float redX, float blueX, float columnWidth, float rowHeight,
            string label, int redValue, int blueValue)
        {
            s.Text(new Rect(labelX, y, 300f, rowHeight), label, s.Font(18, true, TextAnchor.MiddleLeft), HudSkin.MutedText, 0f);
            s.Text(new Rect(redX, y, columnWidth, rowHeight), redValue.ToString(), s.Font(24), HudSkin.TextColor, 1f);
            s.Text(new Rect(blueX, y, columnWidth, rowHeight), blueValue.ToString(), s.Font(24), HudSkin.TextColor, 1f);
            return y + rowHeight;
        }
    }
}
