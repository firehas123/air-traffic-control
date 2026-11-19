# AIR TRAFFIC CONTROL — MASTER GAME DEVELOPMENT BLUEPRINT

> Markdown copy of the planning document, exported 19 November 2026 (version 0.4). The Google Doc is the source of truth:
> https://docs.google.com/document/d/1grpGR4kCDI6xIENAf8Av_w_nv_V3wDn8k-94nV7S0ZY/edit

Version 0.4 | 19 November 2026 | Living game design document | Developed by MHC Studio

## STATUS KEY

AGREED = confirmed by project owner. PROPOSED = a suggestion requiring approval. OPEN = deliberately unresolved.

Project goal: Create an original, enjoyable top-down 2D aircraft-routing game, starting on mobile and designed for later PC and console ports. This is the shared source of truth for ChatGPT, Claude and the developer.

## 1. PROJECT SNAPSHOT

AGREED: Draw paths to guide aircraft safely to compatible landing zones while avoiding collisions. Include airplanes and potentially helicopters.

AGREED: Three original 2D maps and 30 progressive levels: Island Airport (1–10), City Airport (11–20), Desert Airport (21–30).

AGREED IN PRINCIPLE: Stars, earned coins, achievements, cosmetics, personal bests and progressive level/map unlocks.

AGREED: Develop on MacBook, test locally on iPad before publishing; Android first, Apple App Store second. PC (Steam/Epic) and consoles (Xbox/PlayStation, potentially Nintendo) are future targets.

AGREED: Unity 2D with C# (Unity 6.3 LTS), separating touch input from portable game logic.

AGREED: Offline-first, no custom backend required for initial release. Monetize lightly, chiefly optional rewarded ads on mobile; player enjoyment comes first.

OUT OF SCOPE FOR INITIAL RELEASE: Real-time multiplayer, accounts, online leaderboards, subscription systems, realistic flight simulation, console release builds.

## 2. CORE GAMEPLAY

Aircraft spawn around the edge of the map or at designated entry points. Players select an aircraft, trace a flight path, and may redraw it in flight. Aircraft follow the route with smooth movement; players coordinate overlapping traffic and land each aircraft in the correct runway or zone. Valid landings add score and progress. Meeting the level objective safely unlocks the next level.

Prototype scope: One Island Airport runway, basic airplane silhouette, touch selection, drawn route display, waypoint following, landing detection, two simultaneous aircraft, collision detection, landing counter, restart, and a working on-device iPad build.

Prototype success criterion: A first-time player can understand the interaction without written instructions; drawing feels precise and responsive.

PROPOSED RULES TO TEST: Landing zones require correct aircraft type; runway alignment and approach angle could be introduced later. Danger warnings precede aircraft collisions. Paths can be redrawn in flight. CHANGED 15 Nov 2026 by owner: aircraft follow the drawn line exactly, with instant turns at corners (previously: no impossible instantaneous turns). Initially, collisions may restart the level. Difficulty should rise gradually through spawn density, aircraft speed, crossing routes, runway conflicts and type diversity.

## 3. MAPS & 30 LEVELS

ISLAND AIRPORT (1–10): Blue sea, coast, one primary runway; tutorial, simple routing, light traffic and early near-misses.

CITY AIRPORT (11–20): City roads/buildings and crossing runways; busier airspace, crossing approaches and multiple aircraft classes.

DESERT AIRPORT (21–30): Sand, rocky terrain and multiple runways; higher traffic, faster aircraft, complex coordination.

PROPOSED: Each level is a data asset with map ID, aircraft types, spawn schedule, landing objective, scoring thresholds and rewards. Completing a level unlocks the next; finishing a world unlocks the next map. Completed levels remain replayable.

PROPOSED STAR DESIGN: 1 star for safe completion; 2 and 3 stars for skill/efficiency thresholds, adjusted through testing so they never reward reckless flying.

## 4. PLAYER PROGRESSION & EXPERIENCE

Stars (1–3), milestones (first landing, 100 successful landings, perfect world completion), earned coins, cosmetic aircraft skins, and personal best scores. No paid gameplay advantage, no purchased level completion, no required grinding, and no paywall on the 30-level campaign.

PROPOSED VISUAL DIRECTION: Clean, colorful original 2D art, readable aircraft silhouettes, distinct landing zones, easy-to-see flight paths, restrained success/warning animations. Do not copy another game's maps, art, logos or branding.

ACCESSIBILITY: Generous touch targets; status communicated by shape/icon as well as color; support multiple device ratios and safe areas. Landscape only for the prototype (15 Nov 2026); final orientation after phone-size testing.

## 5. TECHNICAL DESIGN & PLATFORM READINESS

AGREED ENGINE: Unity 2D + C#, Unity 6.3 LTS (6000.3.28f1), Universal 2D template. Review licensing and console developer eligibility at the time of expansion.

**Modules:**

InputAdapter — translate finger, mouse and gamepad cursor into common select/draw commands.

PathPlanner — record, simplify and validate user waypoint routes.

AircraftController — movement, turning, speed and flight state.

AirspaceSafety — separation checks, near-miss warnings and collision outcomes.

LandingSystem — matching destinations and landing validation.

LevelDirector — spawn scheduling, objectives, win/fail and difficulty from data.

ProgressionSave — versioned local saves for stars, unlocks, coins, cosmetics and best scores.

PlatformServices — optional ads, purchases, achievements, cloud features; gracefully absent offline or on unsupported platforms.

All core gameplay and saves operate without a network. Future console ports require manufacturer approval, SDKs, controller UX, certification and possible extra licensing/porting work; cross-platform readiness is not guaranteed one-click deployment.

Engineering baseline: Git repo, scene/prefab conventions, data-driven levels, unit tests for path and collision maths, real-device performance checks, repeatable builds and documented setup. Aim for 60 FPS where feasible, verify on hardware.

## 6. MONETIZATION & PUBLISHING

AGREED GOAL: Cover basic publishing/maintenance expenses, ideally a modest surplus, without frustrating users.

MOBILE PROPOSAL: Free download; no forced mid-level or completion ads. Offer optional rewarded ads for cosmetic coins or retry convenience with non-ad alternatives. Consider a voluntary supporter purchase with cosmetic benefit; never lock the core campaign behind payment.

PC/CONSOLES: Plan platform-appropriate pricing or supporter DLC; do not assume mobile advertising works on Steam or consoles.

Privacy: Introduce ad SDKs near release, use test ads while developing, disclose data processing, implement applicable consent/age-rating and platform policies.

INDICATIVE FEES (VERIFY BEFORE PAYMENT): Google Play registration around US$25 once, Apple Developer Program around US$99 annually. Taxes, commissions, policies and testing requirements change. Developing and personally testing on iPad can be done without paying publishing fees.

Initial custom server hosting: US$0; marketing, art, sound, device purchases, store fees and external software can create separate costs.

## 7. MILESTONES AND ACCEPTANCE

M0 — Setup: Unity project, version control and iPad build workflow. Pass: blank game scene launches on iPad. STATUS: DONE 15 Nov 2026, verified on iPad Pro 12.9 (6th gen).

M1 — Flight prototype: one map, path drawing, route following, landing, collision, restart. Pass: one complete playable session. STATUS: DONE 15 Nov 2026. All six steps built; the owner played a complete session on iPad Pro 12.9 (6th gen) (see section 12).

M2 — Fun/UX: controls, danger warnings, tutorial, balance and visual/audio feedback. Pass: a new tester understands play without coaching. STATUS: DONE 19 Nov 2026. Passed by the owner on iPad Pro 12.9 (6th gen) (see sections 12 and 13).

M3 — Content/progression: all maps, 30 levels, stars, coins, achievements, unlocks and local saves. Pass: full campaign playable offline; saved progress resumes correctly.

M4 — Android release: QA, device compatibility/performance, disclosures, artwork and optional test ads. Pass: Play testing and release checklist completed.

M5 — iOS release: iPhone/iPad QA, store compliance, ads/IAP as appropriate. Pass: App Store-ready build.

LATER — PC/consoles: mouse/controller UX, performance, SDK/porting feasibility and a separate go/no-go assessment.

**IMMEDIATE BACKLOG**

- [x] Create Unity 2D project and Git repository.
- [x] Build a blank Unity scene onto the iPad from the MacBook.

- [x] Implement finger path drawing and debug route line.
- [x] Move an aircraft along the drawn path at constant speed (follows the line exactly, per owner decision 15 Nov 2026).

- [x] Add Island Airport and valid landing detection.
- [x] Spawn multiple aircraft; warning and collision system.

- [x] Add score/restart; tested by the owner. M2, including its pass test, was passed by the owner on 19 Nov 2026.
- [ ] Record feedback before formalizing the 30-level data schema.

## 8. CHATGPT + CLAUDE COLLABORATION

One native Google Doc is the living planning workspace. The project owner grants editor access to both Google accounts. Each assistant must read the current document when making recommendations; edits in one AI conversation are NOT automatically visible in the other without document access and a new read.

Project owner: Approves priorities and major decisions; tests the iPad build.

ChatGPT: Development planning, Unity C# architecture/code, troubleshooting, technical documentation.

Claude: Independent proposals, gameplay critiques, code/design reviews, edge-case analysis. Responsibilities may be swapped.

Both: Distinguish AGREED/PROPOSED/OPEN, disclose assumptions, document suggested amendments and verification tests, avoid silently overwriting decisions.

Workflow: Idea → proposal → cross-review → owner approval → decision log → Git issue/branch → implementation → on-device test → completed. If both AIs edit code, use distinct branches and review diffs.

**PROPOSAL TEMPLATE:**

Title:

Why (player/engineering problem):

Exact proposed change:

Benefits/trade-offs:

Scope (Small/Medium/Large):

Validation method:

Status (Proposed/Accepted/Rejected/Deferred):

Owner decision/date/rationale:

**HANDOFF PROMPT:**

“We are making an original 2D air traffic control game. Read this latest master document and treat AGREED decisions as requirements. Suggest amendments separately instead of changing agreed scope. Our priority is an iPad-playable Unity 2D prototype, then Android, then iOS, with possible PC/consoles later. For each proposal explain player benefit, implementation impact, assumptions, and smallest useful test. For code, state filenames/dependencies and device verification steps. Finish with changes and approvals needed.”

## 9. OPEN DECISIONS

Game title and branding — OPEN; choose original title after prototype.

Portrait vs landscape — Landscape only for the prototype (15 Nov 2026, C19); confirm after phone testing.

Visual style — AGREED 19 Nov 2026: detailed, lively top-down 2D maps inspired by the scenery of Airport Madness Challenge, with original art, and animated aircraft (section 15.3). Art source PROPOSED in 15.2.

Helicopters — AGREED 19 Nov 2026: in M2 step 2, landing on a helipad (section 13.1).

Collision consequences — AGREED for the prototype (15 Nov 2026): a red warning ring before, then the game freezes with one large Retry button that restarts at once.

Cleared-to-land cue — AGREED 19 Nov 2026: status is shown as a ring and an icon around the aircraft, not only by colour (section 13.1).

Carrier ship — AGREED 19 Nov 2026 (section 13.2): a fighter that misses the ship bounces off the screen edges until the next one.

Star thresholds — PROPOSED starting values, built in M2 as level data: 1 star for clearing the level, 2 for clearing it within 100 s, 3 for that with no danger warning. Coins: 5 per landing plus 10 per star. To be tuned by playing.

Rewarded-ad choice — OPEN; cosmetics/retry, not mandatory.

Supporter purchase — OPEN; optional.

Android device testing strategy — No Android work until M4. Then: Android Emulator (Android Studio) on the MacBook first, a real phone before release (owner, 15 Nov 2026).

Wind and slow-time on/off switches — OPEN: a player setting in the release, or test switches only.

## 10. DECISION LOG

14 Nov 2026 — AGREED: Three maps and 30 sequentially unlocked levels.

14 Nov 2026 — AGREED: Local testing on iPad before publication.

14 Nov 2026 — AGREED: Android release before iOS release.

14 Nov 2026 — AGREED: Prepare for later PC/console portability.

14 Nov 2026 — AGREED: Offline-first, very light monetization, positive player experience.

14 Nov 2026 — PROPOSED: Unity 2D / C# as initial development stack.

14 Nov 2026 — AGREED: Owner accepted section 11 items C1 to C21. Where they differ from sections 1 to 7, section 11 applies: no advertising in the first release (C12), staged release starting with Island Airport (C16), one rule per map (C1).

15 Nov 2026 — AGREED: Unity 6.3 LTS (6000.3.28f1) with C# as the development stack. Project created in the GitHub repository firehas123/air-traffic-control.

15 Nov 2026 — DONE: M0 passed. Blank scene runs on iPad Pro 12.9 (6th gen). App name Air Traffic Control, bundle ID com.firehas123.atcprototype.

15 Nov 2026 — AGREED: Aircraft follow the drawn path exactly with instant turns at corners. Replaces the PROPOSED rule against instantaneous turns in section 2.

15 Nov 2026 — AGREED for the prototype: landscape orientation only (C19). Final choice after phone testing.

15 Nov 2026 — DONE: M1 step 1. One aircraft follows a finger-drawn path on the iPad, verified by the owner.

15 Nov 2026 — AGREED: M1 gains step 5, the Island wind rule (C1), and step 6, slowing time while drawing (C4). Both are built with an on/off switch.

15 Nov 2026 — AGREED: Android testing on a real phone starts once the iOS version is ready (changes C18).

15 Nov 2026 — AGREED: The game is developed under the name MHC Studio. The credit appears in the repository, the app's company name and the in-game credits. The game will be listed on the MHC website and resume site as in development (MHC inbox draft tasks 190 for the website and 191 for the resume site, which also adds Android and iOS development); the link goes to the GitHub repository until the App Store link exists.

15 Nov 2026 — AGREED: Landing rules for the prototype. A line lands when it ends in the runway's touchdown zone (the highlighted area with the yellow arrows), from any direction, overlapping the zone by at least 0.12 world units. On reaching it the line connects by itself and the aircraft turns green. (Replaces the PROPOSED alignment rule in section 2.)

15 Nov 2026 — AGREED: Game speed button 1x / 2x. 1x is 0.6 world units per second.

15 Nov 2026 — AGREED: The prototype level ends after 8 landings (C2 objective "land a set number of aircraft"); the number is level data.

15 Nov 2026 — DONE: M1 passed. The owner played a complete session on iPad Pro 12.9 (6th gen), including the wind and slow-draw switches both ways.

15 Nov 2026 — AGREED: No Android work in M1 to M3; it starts in M4 (Android release). Android is then checked on the Android Emulator (Android Studio) on the MacBook. The emulator shows whether the game works on Android, but it does not replace a real phone for touch feel and performance; a real phone is still needed before release, and Google Play's closed test needs real testers anyway.

19 Nov 2026 — AGREED: M2 started. Six aircraft kinds with their own speed, size, colour and landing zone (balloon, helicopter, drone, airliner, jumbo jet, fighter jet); details delegated to Claude (section 13.1).

19 Nov 2026 — AGREED: Carrier ship level feature (section 13.2). A fighter jet lands on the moving deck while the ship passes; if the ship leaves first, the fighter turns back at the screen edges and waits for the next ship, adding traffic. A line connected to the deck moves with the ship, the only exception to the exact-line rule.

19 Nov 2026 — AGREED: ChatGPT's review (section 14), with Claude's notes. Each aircraft kind has a collision radius; two aircraft collide when their centres are closer than the two radii added together (airliner 0.25, so two airliners still collide at 0.5). On the carrier, the drawn line stays exact and only a dashed tether to the deck moves with the ship. Wind moves the runway's touchdown zone to the other end; a line may still arrive from any direction, and an aircraft within about 1.5 seconds of touchdown when the wind changes still lands. New kinds arrive one at a time in the real levels.

19 Nov 2026 — AGREED: Build M2 steps 1 to 5 together and check them on the iPad at once, instead of one step at a time.

19 Nov 2026 — DONE: M2 passed. The owner checked the build from commit bffbc2c on iPad Pro 12.9 (6th gen) and passed M2 as a whole, including the step 6 pass test. Star thresholds and coin amounts stay PROPOSED starting values.

19 Nov 2026 — AGREED: M3 includes richer map art inspired by Airport Madness Challenge (original art, not a copy) and aircraft animations: fighter afterburner, balloon bobbing and burner, jet strobe lights, helicopter rotors and more; details delegated to Claude (section 15.3).

20 Nov 2026 — AGREED: Hybrid controls in M3, built for real: drawing stays the way to fly; tapping an aircraft opens buttons for Slow and Fast in the air and Taxi, Cross and Take off on the ground, with departures from gates and a busy-runway rule; details delegated to Claude (section 15.4). Blimp, rocket and space theme are PROPOSED (section 15.5).

20 Nov 2026 — AGREED: Art comes from ChatGPT's image generator, not from shapes drawn in code: a painted top-down Island map first, then aircraft images. Claude Code fits the runway, pads, gates and taxi routes to the picture and zooms the camera so the airport fills the screen.

## 11. ENGAGEMENT, GAMIFICATION AND BUSINESS MODEL

Added by Claude on 14 November 2026 after a full read of version 0.1. The owner accepted every item in this section on 14 November 2026. Each item has an ID so it can be referred to or changed by number. External figures were checked on 14 November 2026 and should be verified again before any payment or release.

### 11.1 WHAT MAKES THE GAME DIFFERENT

C1 AGREED: Give each map one rule of its own. Island: changing wind that reverses the landing direction of the runway. City: runways that close for short periods, plus helicopters with helipads. Desert: sandstorms that hide part of the map, plus low-fuel aircraft that must land first. Why: path-drawing aircraft games already exist, and without map rules the 30 levels differ only in traffic volume and speed. Test: build the wind rule in the M1 prototype and check whether testers describe it without being asked.

C2 AGREED: Vary the level objective. Use four types: land a set number of aircraft, keep the airspace safe for a set time, land a priority aircraft before a deadline, and finish with no danger warnings. Why: a new goal every few levels keeps the campaign from repeating itself. Test: play two objective types back to back in M2 and ask testers which they would replay.

C3 AGREED: Keep levels between 60 and 120 seconds, and make restart after a collision take under two seconds. Never add lives, energy timers or waiting. Why: a short level makes the proposed collision restart feel fair, and it suits phone play. Test: measure restart time on the iPad build.

C4 AGREED: Test slowing time while the player's finger is drawing. Why: it makes small phone screens playable and could set the game apart. Risk: it can make the game too easy. Test: play the same M1 level with and without it and compare.

C5 AGREED: Decide these missing rules in M1: what an aircraft does with no path (suggested: fly straight and turn back at the map edge), what it does when a path ends away from a runway (suggested: continue on its last heading), and whether two aircraft may use one runway close together (suggested: yes, if separation is kept).

### 11.2 REASONS TO COME BACK

C6 AGREED: Add an endless Tower Shift mode for each map, unlocked by finishing that map's ten levels. Traffic grows until the first collision, and the result is a personal best. Why: the campaign ends after 30 levels, and personal bests are agreed in principle but no mode uses them yet. It reuses the campaign systems, so the cost is small. Scope: Medium.

C7 AGREED: Add a Daily Flight: one level per day generated on the device from the date, so it needs no server and works offline. It pays a small coin reward. A streak counts consecutive days, and one missed day does not reset it. Why: a daily reason to open the game that does not punish players. Scope: Medium.

C8 AGREED: Add a controller rank shown on the home screen, based on total safe landings: Trainee, Controller, Senior Controller, Tower Chief. Why: it gives progress that continues after the campaign and costs little to build. Scope: Small.

C9 AGREED: Set the coin rules now. Coins come from landings, first-time stars, Daily Flight and achievements. They buy cosmetics only: aircraft skins, flight path colors and a night version of each map. Every cosmetic can be earned by play. Price them so that finishing the campaign pays for about one third of the catalog. Why: the remaining two thirds give Tower Shift and Daily Flight a purpose.

C10 AGREED: End every level with a short result screen: aircraft landed, closest separation, stars, coins earned and one button for the next level. Add sound and vibration for landing, warning and collision in M2. Why: clear feedback after each action is a large part of what makes a simple game enjoyable.

C11 AGREED: Stars never block the campaign. Use total stars only to unlock cosmetic tiers. This keeps the agreed rule that the 30 levels need no grinding.

### 11.3 BUSINESS MODEL

C12 AGREED: Launch with no advertising. Offer one optional Supporter Pack at about US$2.99 with a set of exclusive skins and path colors. Why: an ad SDK adds consent screens, privacy disclosures and store policy work, and a small offline game earns little from rewarded ads. One purchase is simpler and matches the agreed goal of light monetization. For the first release this would replace the rewarded-ad item in the mobile proposal in section 6.

C13 ESTIMATE: First-year store costs are about US$124 (Google Play about US$25 once, Apple about US$99 a year). Both stores charge small developers about 15% on in-app sales, so a US$2.99 pack returns about US$2.54 before tax. Covering costs needs about 50 purchases. If 1% to 2% of players buy, which is an assumption and not a measured figure, that is 2,500 to 5,000 installs.

C14 AGREED: Revisit rewarded ads only after the game has steady players, for example 1,000 a day. If added, offer one placement: watch to double the coins from a finished level. Never show an ad that the player did not ask for.

C15 AGREED: Release the PC version later as a paid game at a low fixed price, with all cosmetics earnable and no store inside the game. This agrees with the existing note that mobile advertising should not be assumed to work on Steam or consoles.

### 11.4 RELEASE AND MEASUREMENT

C16 AGREED, CHANGES THE RELEASE ORDER: Release in stages. First release: Island Airport with ten levels, Tower Shift, Daily Flight and the Supporter Pack. City and Desert follow as free updates. The agreed total of three maps and 30 levels stays the same; only the order of release changes. Why: a smaller first release reaches players sooner, real feedback shapes the later maps, and each update is a reason for players to return.

C17 NOTE: Google Play requires a new personal developer account to run a closed test with at least 12 testers opted in for 14 continuous days before it can apply for production release. Recruit 15 to 20 Android testers during M2 and plan about three weeks for this step in M4.

C18 CHANGED 15 Nov 2026 by owner: Android testing on a real phone starts once the iOS version is ready, instead of in M1. Android stays the first release platform.

C19 AGREED: Decide portrait or landscape during M1, because the choice changes map layout and how paths are drawn. Suggested starting point: landscape only.

C20 AGREED: Set retention targets. Published 2026 medians for mobile games are about 22% of players returning on day 1, 4% on day 7 and under 1% on day 30. Suggested targets: 30% on day 1 and 8% on day 7. How to measure this without accounts or a backend is OPEN: store console statistics may be enough, and an analytics SDK would need the same consent work as ads.

C21 NOTE: Unity Personal is free while revenue and funding stay under US$200,000 in the last 12 months. Building for consoles needs Unity Pro or a license key from the console maker. This supports keeping Unity as the proposed engine and the existing line about reviewing licensing at the time of expansion.

### 11.5 OWNER DECISION

All items C1 to C21 were accepted on 14 November 2026. C16 changes the release order agreed in section 1. C12 and C14 replace the rewarded-ad proposal in section 6 for the first release. C1, C4 and C5 apply from the M1 prototype.

## 12. PROGRESS AND CODE STATUS

Updated by Claude on 19 November 2026 from the GitHub repository (latest commit 7863fc9). Read this section before suggesting code, so suggestions build on what already exists.

Repository: https://github.com/firehas123/air-traffic-control (public, branch main). It holds the code and a markdown copy of this document (docs/BLUEPRINT.md). This Google Doc stays the source of truth for decisions. The repository is the source of truth for code.

Who works where: the owner tests on the iPad and approves decisions. Claude Code (CLI) writes, builds and installs the code, following CLAUDE.md in the repository. Claude (chat) and ChatGPT review, propose and update this document. Before suggesting code, check the latest commits in the repository.

M0, done 15 Nov 2026: app name Air Traffic Control, bundle ID com.firehas123.atcprototype. Assets/Editor/BuildScript.cs exports the Xcode project to Builds/iOS, so builds run from the command line. Verified on iPad Pro 12.9 (6th gen).

M1, done 15 Nov 2026, verified by the owner on the iPad: (1) an aircraft follows a finger-drawn line exactly; (2) Island runway with a touchdown zone, auto-connect and landing roll; (3) two aircraft in the air, danger warnings and collisions; (4) landing counter, collision panel and instant Retry; (5) Island wind rule (C1) with an on/off switch; (6) slow time while drawing (C4) with an on/off switch. Also built: a 1x / 2x speed button and a level target of 8 landings with a Level cleared panel.

M2, done 19 Nov 2026, passed by the owner on the iPad (build from commit bffbc2c). The owner chose to build steps 1 to 5 together and check them at once. (1) Six aircraft kinds as AircraftType assets, tunable in the Inspector; status shown as rings (yellow: needs a line, green with a tick: cleared, red pulsing: danger). (2) Helipad for helicopter and drone, balloon field for the hot air balloon; a line lands only on a matching zone, and matching zones glow while drawing. (3) Carrier ship crossing the sea about every 40 to 60 s (first after 10 s in the test level), fighters only, with the dashed tether (14.2), countdown and missed-ship behaviour. (4) Sounds made in code (no audio files), iOS vibration and the level result screen with stars, time, closest pass and coins (C10). (5) First-time help: one short line at a time with a ghost line showing how to draw, each hint once per install, a ? button to replay it. Commits 23a3230, 54f115e, 86c28e8, 9329c9b, bffbc2c, 7863fc9.

Code in place (Assets/Scripts): InputAdapter turns touch and mouse into press, drag and release in world coordinates and ignores taps on buttons. LevelDirector runs the level from LevelData (Assets/Levels/IslandPrototype.asset). LandingSystem with LandingRules, AirspaceSafety with SeparationRules, SpawnRules, LevelSession, WindSchedule and GameClock hold the rules as plain, unit-tested code. HudView draws the placeholder interface. Earlier modules: PathRecorder and PathPlanner record the line from the aircraft and replace it when it is redrawn in flight. AircraftMotion and AircraftController move the aircraft exactly along the line at constant speed. PathIntegrity checks that the aircraft never leaves the drawn line. PrototypeSetup now only builds the map and systems.

Rules already built: C1 wind, C2 landing target, C3 instant restart, C4 slow draw, C5 (with no path the aircraft flies straight and turns back at the screen edge; when a path ends it keeps its last heading), landscape only (C19), 60 FPS target. Separation since M2 (14.1): collision when centres are closer than the two aircraft's radii added together (airliner 0.25, jumbo 0.35, fighter and drone 0.18, helicopter 0.25, balloon 0.28). Danger warning at 2.4 times that sum, or a predicted pass within 1.2 times it in the next 2 seconds; for two airliners this is the M1 values (0.5, 1.2, 0.6). Wind grace period 1.5 s (14.3). The test level keeps 3 aircraft in the air and spawns all six kinds.

Tests: 172 EditMode unit tests (Assets/Tests/EditMode), adding aircraft kinds, per-kind separation, the wind grace period, pads, the carrier, stars and coins, generated sounds, first-time help and the level-length estimate.

Next: M3 (content and progression). A PROPOSED plan is in section 15; not started until the owner approves it.

Carried into M3: compare the real time to clear the test level with the 65 s estimate (C3); tune the star starting values; feel vibration on an iPhone (iPads have no haptic engine); decide whether players see the Wind and Slow draw switches; only one of the four objective types (C2) exists so far; coins are shown but not saved yet; no level select yet. Android starts in M4.

## 13. M2 PLAN: AIRCRAFT KINDS, CARRIER SHIP AND FUN

Started by the owner on 19 November 2026. He asked for more kinds of aircraft with different speeds, one colour per kind, and ships passing by as a level feature, and left the details to Claude. They are written here so ChatGPT can review them. AGREED items were delegated by the owner; PROPOSED items wait for his confirmation.

### 13.1 AIRCRAFT KINDS (AGREED, details delegated to Claude)

Each kind is data (an AircraftType asset): name, speed, size, colour, silhouette and where it lands. Speeds are relative to the current plane at 1x game speed (0.6 world units per second).

Hot air balloon: the slowest, 0.35x. Lands on the balloon field, a grass circle. Pink and purple stripes, round shape.

Helicopter: 0.6x. Lands on the helipad, a circle marked H. Orange.

Drone: 0.8x, small. Lands on the helipad. Purple.

Airliner (the current plane): 1.0x. Lands on the runway. White.

Jumbo jet: 0.85x and 1.4 times bigger, so it takes more room and is easier to hit. Lands on the runway. Sky blue.

Fighter jet: the fastest, 1.6x, small. Lands on the runway, or on the carrier ship when a level has one. Dark grey.

No kind is green, yellow or red, because those colours mean cleared, needs a new line and danger. Status therefore moves from colouring the whole aircraft to a ring and an icon around it: a green ring with a tick when cleared, a yellow ring when it needs a new line, a red pulsing ring for danger. This also settles the open item about a shape cue besides colour.

A line lands only on a zone that matches the aircraft kind, and the matching zones glow while the player draws. Kinds arrive a few at a time across the levels, not all at once.

### 13.2 CARRIER SHIP (AGREED, confirmed by the owner on 19 Nov 2026)

An aircraft carrier crosses the sea from time to time. While it passes, a fighter jet waiting for it must land on its deck, a moving landing zone with a countdown. If the fighter has not landed when the ship leaves the screen, even if it was about to touch down, its connection is lost: it keeps flying, turns back at the screen edges (like C5) and gets in the way of other traffic until the next ship comes. The owner wants that chaos; it is what makes the feature interesting. It is a feature of some Island levels, not of every level.

Rule change it needs: a line connected to the deck must move with the ship, so its end follows the deck. This would be the only exception to "the line is never changed" (AGREED 15 Nov 2026).

Starting values chosen by Claude, all level data the owner can tune after playing: a ship every 40 to 60 seconds, about 20 seconds to cross the screen, only fighter jets land on it, and a missed ship costs nothing besides the waiting and the extra traffic.

### 13.3 M2 STEPS (ALL DONE 19 Nov 2026; the owner chose to build steps 1 to 5 together and passed M2 as a whole)

1. Aircraft kinds as data, and status shown as a ring and icon; airliner, jumbo jet and fighter jet on the runway. Each kind has its own collision radius (14.1), and wind gets the 1.5-second landing grace period (14.3).

2. Helipad with helicopter and drone; balloon field with hot air balloon; a line lands only on a matching zone.

3. Carrier ship level feature (13.2), with the dashed tether from the end of the drawn line to the deck (14.2).

4. Feel and feedback: sound and vibration for landing, warning and collision; the level result screen (C10).

5. First-time help: the first level teaches by doing, without walls of text; check that a cleared level takes 60 to 120 seconds (C3).

6. M2 pass test: a player who has never seen the game understands it without coaching.

## NEXT STEP

M2 passed on 19 Nov 2026. Next is M3 (content and progression). Claude proposed a plan in section 15 for the owner and ChatGPT to review; building starts once the owner approves it.

## 14. CHATGPT REVIEW RECOMMENDATIONS — 19 NOVEMBER 2026

Status: AGREED by the owner on 19 Nov 2026, after Claude's review (Claude's notes under each item say how it is built). These suggestions do not replace the agreed C1–C21 decisions or section 13. They are specific M2 engineering and gameplay refinements.

### 14.1 TYPE-DEPENDENT AIRCRAFT COLLISION BOUNDARIES (HIGH PRIORITY)

Current reference: section 12 uses a fixed 0.5-world-unit centre-distance collision threshold; section 13.1 introduces six aircraft kinds with different sizes, including a 1.4x jumbo jet.

Proposal: Define collision/separation geometry from each aircraft type's footprint rather than using the same centre-distance threshold for every pair. A simple first version can use per-type collision radii and a collision condition based on the sum of two radii; keep warning and predictive-separation calculations consistent with these sizes. Tune forgiving visual margins against the actual sprites and playtest on the iPad. Avoid size-dependent visual surprises, especially for fighter jets, drones and jumbo jets.

Validation: Test airliner–airliner, jumbo–drone, fighter–jumbo and helicopter–airliner pairs; confirm larger aircraft require visibly greater separation and that warnings precede danger. Include fast crossing cases where aircraft might pass between frames.

Claude's view (19 Nov): Agree; build it into M2 step 1. Each kind gets a collision radius as data. Two aircraft collide when their centres are closer than their two radii added together, and the warning distances grow the same way. Today's rule (collision at 0.5 units between centres) becomes an airliner radius of 0.25; fighter jet and drone get smaller radii, the jumbo jet 1.4 times the airliner's. Passing between frames is not a real risk at these speeds (the fastest aircraft moves about 0.03 units per frame at 60 FPS), and the existing closest-approach check already covers it.

Owner decision: AGREED 19 Nov 2026, as described in Claude's view.

### 14.2 MOVING CARRIER: PRESERVE PATH PREDICTABILITY (HIGH PRIORITY)

Current reference: section 13.2 allows a fighter jet's line endpoint to follow the moving aircraft carrier, as the sole exception to exact path following.

Proposal: Make the moving touchdown point visibly attached to the carrier. Prefer updating only the terminal approach segment or a clearly designated moving-target segment; do not silently reshape the full user-drawn route while the plane is far away. Define what happens if the carrier departs during the final approach: the connection disengages visibly, the aircraft continues according to the already agreed missed-ship behavior, and the player can draw a new path. Avoid sudden positional jumps or accidental landing after the ship leaves.

Validation: Playtest ship movement during drawing, active approach, touchdown and departure; verify no teleporting, no impossible snapping and an understandable missed-ship outcome.

Claude's view (19 Nov): Agree, and it fits the owner's rule. The part the player drew stays exactly as drawn. Only a short connecting segment, from the end of the drawn line to the deck, moves with the ship, shown as a dashed tether. A fighter can connect only once the ship is fully on screen. If the ship leaves first, the tether disappears, the fighter loses its green ring and follows the agreed missed-ship behaviour, and the player can draw a new line.

Owner decision: AGREED 19 Nov 2026, as described in Claude's view.

### 14.3 RECONCILE WIND WITH LANDING DIRECTION (HIGH PRIORITY)

Current reference: the 15 Nov prototype rule permits touchdown from any direction as long as the drawn line reaches the zone; C1 says wind reverses the runway's landing direction.

Clarification required: Does wind change which approach direction is VALID, or merely indicate a preferred direction/alter the challenge? Both rules cannot be enforced literally without specifying an exception. For M2, recommend choosing one coherent rule and making it visible to the player. If direction becomes mandatory, provide an unambiguous approach arrow, sufficient warning before reversal and a grace period for aircraft already committed to land. If it remains cosmetic/informational, describe its actual gameplay effect rather than implying a restriction.

Validation: Test a wind change before path drawing, during approach and immediately before touchdown. No sudden unexplained failure.

Claude's view (19 Nov): The two rules already fit together in the build, but this document does not say so clearly. Wind does not limit the approach angle. It moves the touchdown zone to the other end of the runway (the arrows and highlight move), with a 5-second warning. A line must end in the current zone, from any direction. A cleared aircraft whose line ends at the old end loses its clearance and needs a new line. Proposed addition: a grace period, so an aircraft within about 1.5 seconds of touchdown when the wind changes still lands. Wind affects runways only, not helipads, the balloon field or the carrier.

Owner decision: AGREED 19 Nov 2026, as described in Claude's view.

### 14.4 INTRODUCE THE SIX AIRCRAFT TYPES GRADUALLY (MEDIUM PRIORITY)

Current reference: section 13.1 defines balloon, helicopter, drone, airliner, jumbo jet and fighter jet, each with different speed, size and landing zone.

Proposal: Unlock a new aircraft kind only after players understand the previous kind's destination and handling. Start the first levels with the familiar airliner, introduce a contrasting slower or larger kind next, then helipad/balloon destinations, and reserve the fast fighter/carrier combination for later. Give every new kind a short, interactive first encounter rather than a large tutorial panel. This aligns with section 13.1's agreed principle of not introducing every type simultaneously.

Validation: A new player should identify where each newly introduced kind lands without coaching, and be able to explain its distinguishing behavior after one level.

Claude's view (19 Nov): Agree; it matches 13.1. It guides level design in M3. During M2 the test level may show all kinds together so the owner can try them. Suggested order for the real levels: airliner, jumbo jet, helicopter with the helipad, balloon with the balloon field, drone, fighter jet, and the carrier last.

Owner decision: AGREED 19 Nov 2026, as described in Claude's view.

### RECOMMENDED SEQUENCE FOR REVIEW

First settle 14.1 and 14.3 before balancing M2 collisions/landing rules; use 14.2 when implementing the carrier in M2 step 3; apply 14.4 during level design and first-time-player testing. The owner accepted all four on 19 Nov 2026: 14.1 is built in M2 step 1, 14.3 (clearer rule plus grace period) in step 1, 14.2 in step 3, 14.4 in M3 level design. Claude Code remains the code implementer; these are not instructions to edit code immediately.

## 15. M3 PLAN (PROPOSED BY CLAUDE, 19 NOVEMBER 2026)

Status: PROPOSED, waiting for the owner, with ChatGPT invited to review. Section 7 describes M3 as all three maps and 30 levels. C16 (AGREED) releases Island Airport first and City and Desert later as free updates, so this plan builds everything the first release needs before the other two maps.

### 15.1 M3 STEPS

1. Saves and level select: ProgressionSave keeps stars, unlocked levels, coins and best times on the device, with a version number so later updates can read old saves. A level select screen for Island levels 1 to 10. The result screen's button becomes Next level (C10).

2. Island map art and aircraft animations (15.3, AGREED by the owner on 19 Nov 2026), built before the levels so they are designed on the finished map.

3. Hybrid controls (15.4, AGREED by the owner on 20 Nov 2026): slow/fast for aircraft in the air, and ground control for departures (Taxi, Cross, Take off).

4. Island levels 1 to 10 as data, introducing slow/fast from level 2, departures from level 4 and runway crossings from level 6; new aircraft kinds one at a time (14.4: airliner, jumbo jet, helicopter with the helipad, balloon with the balloon field, drone, fighter jet, carrier last); the four objective types (C2) spread over the levels; wind from about level 3; each level 60 to 120 s (C3).

5. Home screen and progress: controller rank from total safe landings (C8), coin total, Play and Levels buttons, credit "Developed by MHC Studio".

6. Tower Shift for Island (C6): endless traffic until the first collision, unlocked by finishing level 10, best result saved.

7. Daily Flight (C7): a level made on the device from the date, small coin reward, a streak that one missed day does not reset.

8. Cosmetics with coins (C9, C11): a first small set of aircraft skins and line colours, every item earnable by play; stars unlock cosmetic tiers only.

Pass for this part of M3: the Island map and aircraft animations look finished on the iPad at 60 FPS; Island levels 1 to 10, Tower Shift and Daily Flight play offline on the iPad; and progress is still there after closing and reopening the app.

Later in M3 (or as updates after release, per C16): City Airport (runways that close for short periods) and Desert Airport (sandstorms and low-fuel aircraft), levels 11 to 30. The Supporter Pack purchase (C12) belongs to the store releases (M4 and M5).

### 15.2 QUESTIONS FOR THE OWNER

Approve the order above, or change it. Decide whether City and Desert are built in M3 or after the first release. C17: Google Play's closed test needs at least 12 testers for 14 days; recruiting 15 to 20 Android testers should start during M3. The game title is still OPEN and is needed before the store listings. Art source for 15.3: DECIDED 20 Nov 2026. The first try (shapes drawn in code by Claude Code, commit e549288) still looked like the prototype, because code shapes cannot reach a painted look. The owner chose painted images made with ChatGPT's image generator: a hand-painted, top-down Island map (2732 x 2048) and, next, one image per aircraft kind. Claude Code places them in the game, fits the level data to the picture and keeps its animations (lights, rotors, shadows, clouds, effects) on top. A paid artist stays an option before the store release.

### 15.3 MAP ART AND ANIMATIONS (AGREED 19 Nov 2026, details delegated to Claude)

Owner's request: maps with the scenery and creativity of Airport Madness Challenge (free on iOS), but not a copy: original art in our own style, and animated aircraft, all in M3. Section 4 still applies: do not copy another game's maps, art, logos or branding.

Island map: animated sea with gentle waves and foam along the beach; palm trees that sway; sand and rocks; a small terminal with a control tower; taxiways and an apron with parked planes; runway lights that pulse towards the touchdown zone; a windsock that shows the wind direction (ties into C1); boats, and the carrier leaving a white wake; clouds drifting over the map with soft shadows; a few birds.

Aircraft: every aircraft gets a soft shadow on the ground that moves under it as it lands. Fighter jet: flickering afterburner flame, a short speed trail, tilts into turns. Hot air balloon: gentle bobbing and sway, a burner flame puff every few seconds. Jumbo jet and airliner: blinking white strobe lights on the wings and tail, thin contrails. Helicopter: spinning main and tail rotors, a slight wobble as it settles on the helipad. Drone: four spinning rotors and a small blinking light.

Events: smoke and a short screen shake on a collision; a small sparkle and coin pop on each landing; the carrier's deck lights blink during its countdown.

Rules: the status rings stay the clearest thing on screen, so scenery uses softer colours, and aircraft lights are white, not red or green (real wingtip lights are red and green, but those colours mean danger and cleared here). 60 FPS on the iPad. Animation values are data and can be turned off for comparison.

Later: City and Desert scenery when those maps are built; the night version of each map (C9 cosmetic) with lights on.

### 15.4 HYBRID CONTROLS (AGREED 20 Nov 2026, details delegated to Claude)

The owner looked at Airport Madness Challenge (command buttons, speed control, taxi and take-off orders) and Planes Control (drawn lines, themed hazards) and chose a hybrid, built for real in M3 rather than as a separate test. Drawing a line stays the way to guide aircraft in the air; tapping an aircraft opens short command buttons for speed and for ground movement.

Tap or draw: a press on an aircraft that is released within 0.25 s and moves less than 0.15 world units is a tap; anything else starts drawing as before. A tap opens option buttons beside that aircraft (at least 44 points high, readable on an iPhone). Only one aircraft's buttons are open at a time; they close after 3 s or when the player taps elsewhere. Slow draw (C4) also slows time while buttons are open, when its switch is on.

Speed in the air: buttons Slow and Fast. Slow = 0.6 times the kind's speed, Fast = 1.4 times; tapping the active button again returns to normal. A small icon next to the ring shows the current speed. Fast lands sooner but its landing roll is longer, so the runway stays busy for longer; slow opens gaps in traffic. The drawn line never changes (only the speed does), and warnings use the current speed. Values are data per kind.

Departures: some aircraft start at a gate by the terminal instead of entering from the edge (runway kinds only: airliner, jumbo jet, fighter jet). Tap it: Taxi. It follows a fixed taxi route (level data) to the hold line at the end of the runway that is active for the current wind, and stops behind any aircraft in front of it. At the hold line, tap it: Take off. It lines up, rolls along the runway, lifts off and leaves the screen in the runway direction, and counts as a departure. If the wind reverses while it waits, it taxis to the other end by itself.

Runway crossing: where a taxi route crosses the runway it has a crossing point. The aircraft stops there and shows Cross; on Cross it crosses and continues.

Runway busy: the runway is busy while an aircraft lands, rolls, takes off or crosses. A landing or a take-off that meets another aircraft on the runway is a collision. Take off and Cross can still be pressed while the runway is busy (that is the player's decision), but the runway edge lights flash while it is busy so the danger is visible. Aircraft on taxiways never collide with each other; they queue.

Level goal: levels can count departures as well as landings, for example land 6 and launch 3 (an extension of C2). The result screen shows both.

First-time help: one short line each for slow/fast, Taxi, Take off and Cross, the first time each appears.

Kept as before: the status rings, the exact-line rule, collision sizes per kind, the carrier, helipads and balloon field (helicopters, drones and balloons do not use ground control in M3).

### 15.5 MORE AIRCRAFT AND THEMES (PROPOSED by Claude, 20 Nov 2026, from the owner's ideas)

Blimp: a new kind for City Airport, huge and very slow (about 0.3x), lands on a mooring mast. Rocket: a Desert Airport event like the carrier: a launch pad counts down and a no-fly column appears for a few seconds during launch. Spaceships and a space map: a fourth theme would change the agreed three maps and 30 levels, so Claude proposes it as a free update after release (a reason for players to return, C16). Owner decision: PENDING.
