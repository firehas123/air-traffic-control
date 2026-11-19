# Working rules for this repository

Read `docs/BLUEPRINT.md` before proposing or building anything. It is a copy of the planning Google Doc, which stays the source of truth.

## Working with the other assistants

- The planning Google Doc is shared by the owner, Claude (chat) and ChatGPT. They update it from this repository's commit history, and Claude (chat) regenerates `docs/BLUEPRINT.md` from it. Do not edit `docs/BLUEPRINT.md` by hand.
- Write every commit message so someone who has not seen the session can update the plan from it: what was built, what was verified on which device, and any decision the owner made.
- Run `git pull` before committing, because other sessions also push to `main`.

## Developer name: MHC Studio

The owner decided on 15 Nov 2026 that this game is developed under the name **MHC Studio**. Stamp it wherever a developer or company name appears:

- Unity Player Settings: Company Name `MHC Studio` (Product Name stays `Air Traffic Control` until the title is decided).
- In-game credits or about screen, when one is built: "Developed by MHC Studio".
- Store listings later: developer name MHC Studio.
- Keep the bundle ID `com.firehas123.atcprototype` unless the owner asks to change it. A new bundle ID installs as a separate app.

## Decisions

- Items marked AGREED are requirements. Do not change them without asking the owner.
- Items marked PROPOSED or OPEN are not settled. Ask before building on them.
- Section 11 (items C1 to C21) was accepted on 14 November 2026. Where it differs from sections 1 to 7, section 11 applies.
- Engine is AGREED: Unity 6.3 LTS (6000.3.28f1) with C#, Universal 2D template. The Unity project is already at the repository root (Assets, Packages, ProjectSettings). Do not upgrade the Unity version without asking the owner.

## Current priority

M0 and M1 are done (M1 passed on 15 Nov 2026). **M2 is done: passed by the owner on 19 Nov 2026 after checking it on the iPad.** The plan was in `docs/BLUEPRINT.md` section 13. Its steps were:

1. Aircraft kinds as data (an AircraftType asset per kind: name, speed, size, colour, silhouette, landing zone kind). Status moves from colouring the whole aircraft to a ring and icon (green ring with a tick when cleared, yellow ring when it needs a new line, red pulsing ring for danger). Airliner, jumbo jet and fighter jet on the runway. Built 19 Nov 2026 with the collision radius per kind (14.1) and the wind grace period (14.3).
2. Helipad with helicopter and drone; balloon field with hot air balloon. A line lands only on a zone that matches the aircraft kind; matching zones glow while drawing. Built 19 Nov 2026.
3. Carrier ship level feature (AGREED 19 Nov 2026, section 13.2). A carrier crosses the sea; a fighter jet lands on its moving deck while it passes, with a countdown. The drawn line stays exact; only a dashed tether from its end to the deck moves with the ship (section 14.2), the one exception to the exact-line rule. A fighter can connect only once the ship is fully on screen. If the ship leaves the screen before touchdown, even at the last moment, the connection is lost and the fighter keeps flying, turning back at the screen edges (like C5) until the next ship. The owner wants that chaos. Starting values as level data: a ship every 40 to 60 s, about 20 s to cross, fighters only. Built 19 Nov 2026.
4. Sound and vibration for landing, warning and collision; the level result screen (C10). Built 19 Nov 2026 (iPads have no haptic engine, so vibration is felt only on iPhones; star thresholds are starting values, still PROPOSED).
5. First-time help that teaches by doing; check that a cleared level takes 60 to 120 seconds (C3). Built 19 Nov 2026 (the test level is estimated at 65 s; the owner times real play).
6. M2 pass test: a player who has never seen the game understands it without coaching.

The owner asked on 19 Nov 2026 for all M2 steps to be built together, checked steps 1 to 5 on the iPad and passed M2 the same day. **M3 started on 20 Nov 2026**: the owner approved the plan in `docs/BLUEPRINT.md` section 15.1, in that order, Island first (C16). Art (changed by the owner on 20 Nov 2026): maps and aircraft are painted images made with ChatGPT's image generator and added by the owner under `Assets/Art/`. Code-drawn shapes are no longer the art; keep code only for animated overlays (lights, rotors, shadows, clouds, birds, boats, effects, status rings, zone highlights). Never copy another game's art. Steps 1 (saves and level select), 2 (Island map art and aircraft animations) and 3 (hybrid controls) were built together on 20 Nov 2026 and installed on the iPad, waiting for the owner's check. Step 4 (Island levels 1 to 10 as data) is next; until then levels 2 to 10 reuse the test level. The test level asks for land 6 and launch 3, with 2 arrivals in the air, two gates and one runway crossing. Already AGREED for M3 (19 Nov 2026): richer original map art inspired by Airport Madness Challenge and aircraft animations, section 15.3; aircraft lights are white, never red or green. Also AGREED for M3 (20 Nov 2026): hybrid controls, section 15.4: drawing stays the way to fly; a tap on an aircraft opens Slow/Fast in the air and Taxi, Cross and Take off on the ground, with departures from gates and a busy-runway rule. Blimp, rocket and space theme (15.5) are only PROPOSED: do not build them.

Collision size per kind (section 14.1, AGREED 19 Nov 2026): each AircraftType has a collision radius. Two aircraft collide when their centres are closer than the two radii added together, and warning distances scale the same way. Airliner 0.25 (so two airliners still collide at 0.5), fighter jet and drone smaller, jumbo jet 1.4x the airliner. Tunable in the Inspector.

Aircraft kinds (section 13.1, AGREED). Speed is relative to the current plane at 1x (0.6 world units per second):

| Kind | Speed | Size | Colour | Lands on |
|---|---|---|---|---|
| Hot air balloon | 0.35x | normal, round | pink and purple stripes | balloon field (grass circle) |
| Helicopter | 0.6x | normal | orange | helipad (circle marked H) |
| Drone | 0.8x | small | purple | helipad |
| Airliner (current plane) | 1.0x | normal | white | runway |
| Jumbo jet | 0.85x | 1.4x, so its collision radius grows too | sky blue | runway |
| Fighter jet | 1.6x | small | dark grey | runway, or the carrier ship when a level has one |

No kind may be green, yellow or red: those colours mean cleared, needs a new line and danger. Kinds should be tunable in the Inspector so the owner can adjust speeds after playing.

M1 gameplay rules still apply (details in the commit messages):

- Aircraft follow the drawn line exactly, with instant turns at corners. The line is never smoothed or cut; any change to it throws `PathAlteredException`.
- A line lands when it ends in a matching touchdown zone, from any direction, overlapping it by at least 0.12 world units (`LandingRules.ConnectInset`). It connects by itself on reaching it.
- Game speed button 1x / 2x; collision freezes the game with one large Retry; the prototype level ends after 8 landings.
- Slow draw (C4) and Wind (C1) each have an on/off button. Whether players see these switches in the release is still OPEN.

Rules that apply from the M1 prototype onwards:

- C1: each map has one rule of its own. Island uses changing wind that reverses the runway landing direction. Build it with an on/off switch. Wind moves the runway's touchdown zone to the other end; a line may still arrive from any direction. An aircraft within about 1.5 s of touchdown when the wind changes still lands (section 14.3). Wind affects runways only.
- C3: levels last 60 to 120 seconds, and restart after a collision takes under two seconds.
- C4: slow time while the player is drawing, with an on/off switch so both versions can be compared. Whether players see the wind and slow-time switches in the release is still OPEN.
- C5: an aircraft with no path flies straight and turns back at the map edge. When a path ends away from a runway, the aircraft continues on its last heading.
- C18 (changed by the owner on 15 Nov 2026): no Android work in M1 to M3; build and test only for iOS. Android starts in M4: first on the Android Emulator (Android Studio, ARM64 system image) on the MacBook, then a real phone before release. The Android build must include ARM64, because the emulator on Apple Silicon runs ARM64 images.

## Code

- Keep touch input separate from game logic so mouse and gamepad can be added later.
- Use the module names from blueprint section 5: InputAdapter, PathPlanner, AircraftController, AirspaceSafety, LandingSystem, LevelDirector, ProgressionSave, PlatformServices.
- Levels are data, not code.
- All gameplay and saves work without a network.
- Write unit tests for path and collision maths.
- For each change, state the files touched and how to verify it on a device.

## Out of scope for the first release

Multiplayer, accounts, online leaderboards, subscriptions, advertising, realistic flight simulation and console builds.
