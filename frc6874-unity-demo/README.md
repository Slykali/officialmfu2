# Team 6874: Robot Arena (Unity demo)

An FRC-style 3v3 robot game made as a demo for **FRC Team 6874**. You drive robot **6874**: collect yellow fuel balls, shoot them into the center hub, score in autonomous, and climb your alliance's tower in the endgame. Two AI partners play with you against three AI opponents.

> Unofficial fan demo. Not affiliated with or endorsed by *FIRST*®. The game rules are a simplified, FRC-inspired design, not an official game.

## Quick start

1. Install **Unity 2022.3 LTS** or **Unity 6** (any 6000.x) through Unity Hub. Built-in render pipeline and URP both work.
2. Unity Hub → **Add** → **Add project from disk** → choose this `frc6874-unity-demo` folder. If Hub says the editor version (2022.3.45f1) isn't installed, pick any 2022.3+ version you have.
3. On the first open, Unity may ask to **enable the new Input System backends** and restart. Click **Yes**. (Clicking No also works: the game then falls back to the old Input Manager, with full keyboard/mouse support and basic gamepad buttons.)
4. The project creates and opens `Assets/FRC6874/Scenes/FRC6874_Demo.unity` by itself. Press **Play**.
   - If the scene isn't open, use the menu **FRC 6874 → Play Demo**.
   - Pressing Play in any empty scene also starts the game.

The whole game is generated from C# at runtime: field, robots, materials, UI and sound effects. The repo has no art assets or prefabs to break.

## How to play

| Period | Time | What happens |
| --- | --- | --- |
| Autonomous | 0:15 | Every robot runs its auto routine: it shoots its 3 preloaded balls, then drives out of the starting zone. |
| Teleop | 2:15 | You drive. Collect fuel, shoot it into the hub, and play with or around the AI. |
| Endgame | last 0:20 | Drive into your alliance's climb zone (outlined in front of your tower) and climb. |

**Scoring**

| Action | Points |
| --- | --- |
| Fuel in hub (auto) | 2 |
| Fuel in hub (teleop) | 1 |
| Leave starting zone in auto | 3 |
| Climb in endgame | 10 |

The hub rolls scored balls back onto the field, so balls keep circulating.

## Controls

| Action | Keyboard / mouse | Gamepad |
| --- | --- | --- |
| Drive (swerve) | W A S D | Left stick |
| Turn | Q / E or ← / → | Right stick |
| Intake (hold) | Space or left mouse | LT or RB |
| Shoot (hold) | F or right mouse | RT |
| Climb (endgame) | C | Y |
| Auto-aim on/off | T | X |
| Field-centric / robot-centric | G | L3 |
| Cycle camera | V | View / Back |
| Precision (slow) mode | Left Shift | LB |
| Pause | Esc or P | Menu / Start |
| Confirm | Enter | A |
| Help overlay | H or F1 | – |

With auto-aim on, holding Shoot turns the robot toward the hub and fires once it's lined up. With auto-aim off, you aim yourself; the shooter still sets its power from the distance.

Camera modes: **Driver Station** (default: the view from behind your alliance wall, with true field-centric driving), **Chase**, and **Broadcast**.

## Customizing for the team

Every tunable value is in [`Assets/FRC6874/Scripts/Core/GameConfig.cs`](Assets/FRC6874/Scripts/Core/GameConfig.cs):

- `TeamAccentColor` / `TeamSecondaryColor`: set these to the team's colors.
- `TeamName`, `GameTitle`, `GameSubtitle`: the menu text.
- Robot speed, acceleration, shooter range, fuel capacity and fire rate.
- Match period lengths and point values.
- Field and hub dimensions. The field builder and the AI both read these, so they stay in sync.

## Project layout

```
Assets/FRC6874/
  Editor/FRC6874DemoSetup.cs   Creates/opens the demo scene, "FRC 6874" menu
  Scripts/Core/                Shared contract: config, field layout (Arena), match state & scoring,
                               input, fuel balls (GamePiece), material/text/physics helpers
  Scripts/Field/               Field, hub, climb towers, markings, venue, scoreboard, ball layout
  Scripts/Robot/               Robot build, swerve drive, intake, shooter, climber, player/auto/AI drivers
  Scripts/Match/               Bootstrap, match flow, camera rig, HUD & menus, synthesized audio
```

## Building

**File → Build Settings**: the demo scene is already in the list. Pick **Windows/Mac/Linux** for a desktop build. Pick **WebGL** to put the game on the team website; the build output can be served as static files, for example from this repo's Next.js `public/` folder.

## Notes

This project was written and compile-checked without a Unity editor. The scripts compile against the UnityEngine 2021.3 reference assemblies, but nobody has playtested it yet. Expect some tuning on first run (robot speed, shooter spread, AI behavior); all of it lives in `GameConfig.cs` or at the top of the relevant script.
