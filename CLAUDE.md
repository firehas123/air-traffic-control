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

M0, M1 (15 Nov 2026) and M2 (19 Nov 2026, checked by the owner on the iPad) are done. The M2 pass test with a player who has never seen the game is still recommended.

**M3 now: two game modes and seven maps (blueprint section 16, owner 25 Nov 2026).** Built on the GEEKOM on 25-26 Nov 2026 in four phases (commits "Phase 1 of 4" to "Phase 4 of 4"); waiting for the owner's check on the iPad:

- Draw mode (draw lines, zoomed-out sky view, everything from M1 and M2) and Tower mode (airport close-up, fixed approach and taxi routes, tap buttons: Land on runway A/B/C, Land on helipad / balloon field, Go around, Hold, Slow, Fast, Taxi to stand, Taxi, Cross, Take off). The ground code from M3 step 3 lives in Tower mode; Draw mode has no departures.
- Maps 1 to 5 are playable in both modes: Island, City, Desert, Mountain Valley, Arctic, 5 levels per mode (50 levels, data in `Assets/Levels/Draw` and `Assets/Levels/Tower`, listed in `Assets/Maps/MapCatalog.asset`, seeded by Tools > Air Traffic Control > Build Draw Levels / Build Tower Levels). Each map's rule is on from level 2: Island wind, City runway closures, Desert low fuel (from 2) and sandstorm (from 3), Mountain Valley mountains (Draw: no-fly areas; Tower: landings along the valley only), Arctic icy runways. Island Draw 5 has the carrier.
- Maps are the owner's painted pictures (`Assets/Art/Maps/01_island_tower.png` ... `10_arctic_draw.png`, `15_carrier.png`) with measured layouts (`Assets/Maps/Layouts`). Code draws only overlays that give information (status rings, lines, zone glow, touchdown zones, runway letters, busy-runway lights, windsock, closed X, sandstorm, fuel timer, slow rotors, shadows, a short smoke puff). Clouds, birds, boats, palms, waves and the other removed animations are gone; no code-drawn scenery is drawn over a picture. `Assets/Art/Maps/Variants/` is unused.
- Home screen with the seven map cards, controller rank and XP bar, coins and "Developed by MHC Studio". XP: 10 per landing and departure, 25 per first-time star, 50 for no danger warning. Unlocks: next level after clearing the one before in the same mode; both Island modes open at the start; the next map needs level 5 of the previous map (either mode) and its XP (City 1,000, Desert 2,500, Mountain Valley 4,000, Arctic 5,500). Save format version 2 upgrades the old M3 save.
- Military Base and Spaceport show "Coming soon" on the home screen with their pictures; their layouts are not measured yet and they have no levels.

Still PROPOSED (ask before changing): map themes 4 to 7, level counts, XP values, rank thresholds, unlock XP, star times, and the tuning values chosen by Claude (listed in the phase commit messages). A first play of one map in one mode gives about 700 to 1,250 XP, so a player who never tries the other mode reaches City only with near-perfect Island play, and Draw only gives 3,250 to 4,750 XP after maps 1 to 4 while Arctic needs 5,500 (Tower only: 2,900 to 4,400); both modes give about 1,500 per map, as section 16.3 intends.

**Next (wait for the owner's go):** owner's iPad check of both modes on maps 1 to 5, then Military Base and Spaceport gameplay (their rules: fighter scrambles, rocket launch no-fly column), Tower Shift (endless, C6), Daily Flight, cosmetics. Blimp and rocket aircraft (15.5) are only PROPOSED: do not build them. Development happens on the GEEKOM; iOS builds and iPad installs stay on the owner's Mac.

History: M3 steps 1 to 3 (saves and level select, Island art and aircraft images, hybrid controls) were built on 20 Nov 2026; on 25 Nov 2026 the owner replaced the single hybrid map with the two modes above. Art rule (owner, 20 Nov 2026): maps and aircraft are painted images made with ChatGPT's image generator and added by the owner under `Assets/Art/`; never copy another game's art; aircraft lights are white, never red or green.

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
