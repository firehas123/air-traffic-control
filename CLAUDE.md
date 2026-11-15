# Working rules for this repository

Read `docs/BLUEPRINT.md` before proposing or building anything. It is a copy of the planning Google Doc, which stays the source of truth.

## Decisions

- Items marked AGREED are requirements. Do not change them without asking the owner.
- Items marked PROPOSED or OPEN are not settled. Ask before building on them.
- Section 11 (items C1 to C21) was accepted on 14 November 2026. Where it differs from sections 1 to 7, section 11 applies.
- Engine is AGREED: Unity 6.3 LTS (6000.3.28f1) with C#, Universal 2D template. The Unity project is already at the repository root (Assets, Packages, ProjectSettings). Do not upgrade the Unity version without asking the owner.

## Current priority

Milestones M0 and M1: the blank SampleScene running on the iPad, then one map with path drawing, route following, landing, collision and restart.

Rules that apply from the M1 prototype:

- C1: each map has one rule of its own. Island uses changing wind that reverses the runway landing direction.
- C3: levels last 60 to 120 seconds, and restart after a collision takes under two seconds.
- C4: test slowing time while the player is drawing, and compare with it switched off.
- C5: an aircraft with no path flies straight and turns back at the map edge. When a path ends away from a runway, the aircraft continues on its last heading.
- C18: test on a real Android phone as well as the iPad.

## Code

- Keep touch input separate from game logic so mouse and gamepad can be added later.
- Use the module names from blueprint section 5: InputAdapter, PathPlanner, AircraftController, AirspaceSafety, LandingSystem, LevelDirector, ProgressionSave, PlatformServices.
- Levels are data, not code.
- All gameplay and saves work without a network.
- Write unit tests for path and collision maths.
- For each change, state the files touched and how to verify it on a device.

## Out of scope for the first release

Multiplayer, accounts, online leaderboards, subscriptions, advertising, realistic flight simulation and console builds.
