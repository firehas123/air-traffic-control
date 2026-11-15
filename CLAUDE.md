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

M0 is done. M1 is in progress, one step at a time, each built, installed and checked on the iPad before the next starts:

1. Plane follows a finger-drawn path (done).
2. Runway and landing detection (LandingSystem).
3. A second aircraft with danger warnings and collisions (AirspaceSafety).
4. Landing counter and restart in under two seconds (C3).
5. Island wind rule (C1), with an on/off switch.
6. Slow time while drawing (C4), with an on/off switch.

Rules that apply from the M1 prototype:

- C1: each map has one rule of its own. Island uses changing wind that reverses the runway landing direction. Build it with an on/off switch.
- C3: levels last 60 to 120 seconds, and restart after a collision takes under two seconds.
- C4: slow time while the player is drawing, with an on/off switch so both versions can be compared. Whether players see the wind and slow-time switches in the release is still OPEN.
- C5: an aircraft with no path flies straight and turns back at the map edge. When a path ends away from a runway, the aircraft continues on its last heading.
- C18 (changed by the owner on 15 Nov 2026): test on a real Android phone once the iOS version is ready, not during M1.

## Code

- Keep touch input separate from game logic so mouse and gamepad can be added later.
- Use the module names from blueprint section 5: InputAdapter, PathPlanner, AircraftController, AirspaceSafety, LandingSystem, LevelDirector, ProgressionSave, PlatformServices.
- Levels are data, not code.
- All gameplay and saves work without a network.
- Write unit tests for path and collision maths.
- For each change, state the files touched and how to verify it on a device.

## Out of scope for the first release

Multiplayer, accounts, online leaderboards, subscriptions, advertising, realistic flight simulation and console builds.
