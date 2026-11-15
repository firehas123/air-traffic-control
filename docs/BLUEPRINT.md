# AIR TRAFFIC CONTROL — MASTER GAME DEVELOPMENT BLUEPRINT

> Markdown copy of the planning document, exported 15 November 2026. The Google Doc is the source of truth:
> https://docs.google.com/document/d/1grpGR4kCDI6xIENAf8Av_w_nv_V3wDn8k-94nV7S0ZY/edit

Version 0.2 | 15 November 2026 | Living game design document | Developed by MHC Studio

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

M1 — Flight prototype: one map, path drawing, route following, landing, collision, restart. Pass: one complete playable session. STATUS: IN PROGRESS, step 1 of 6 done 15 Nov 2026 (see section 12).

M2 — Fun/UX: controls, danger warnings, tutorial, balance and visual/audio feedback. Pass: a new tester understands play without coaching.

M3 — Content/progression: all maps, 30 levels, stars, coins, achievements, unlocks and local saves. Pass: full campaign playable offline; saved progress resumes correctly.

M4 — Android release: QA, device compatibility/performance, disclosures, artwork and optional test ads. Pass: Play testing and release checklist completed.

M5 — iOS release: iPhone/iPad QA, store compliance, ads/IAP as appropriate. Pass: App Store-ready build.

LATER — PC/consoles: mouse/controller UX, performance, SDK/porting feasibility and a separate go/no-go assessment.

**IMMEDIATE BACKLOG**

- [x] Create Unity 2D project and Git repository.
- [x] Build a blank Unity scene onto the iPad from the MacBook.

- [x] Implement finger path drawing and debug route line.
- [x] Move an aircraft along the drawn path at constant speed (follows the line exactly, per owner decision 15 Nov 2026).

- [ ] Add Island Airport and valid landing detection.
- [ ] Spawn multiple aircraft; warning and collision system.

- [ ] Add score/restart; test with an actual player.
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

Visual style — PROPOSED colorful minimalist 2D.

Helicopters — OPEN when/how to introduce distinct landing requirements.

Collision consequences — PROPOSED failure/restart with clear warning.

Star thresholds — PROPOSED safety + efficiency; requires tuning.

Rewarded-ad choice — OPEN; cosmetics/retry, not mandatory.

Supporter purchase — OPEN; optional.

Android device testing strategy — Testing on a real phone starts once the iOS version is ready (owner, 15 Nov 2026).

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

15 Nov 2026 — AGREED: The game is developed under the name MHC Studio. The credit appears in the repository, the app's company name and the in-game credits. The game will be listed on the MHC website and resume site as in development (MHC inbox draft task 190); the link goes to the GitHub repository until the App Store link exists.

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

Updated by Claude on 15 November 2026 from the GitHub repository. Read this section before suggesting code, so suggestions build on what already exists.

Repository: https://github.com/firehas123/air-traffic-control (private, branch main). It holds the code and a markdown copy of this document (docs/BLUEPRINT.md). This Google Doc stays the source of truth for decisions. The repository is the source of truth for code.

Who works where: the owner tests on the iPad and approves decisions. Claude Code (CLI) writes, builds and installs the code, following CLAUDE.md in the repository. Claude (chat) and ChatGPT review, propose and update this document. Before suggesting code, check the latest commits in the repository.

M0, done 15 Nov 2026: app name Air Traffic Control, bundle ID com.firehas123.atcprototype. Assets/Editor/BuildScript.cs exports the Xcode project to Builds/iOS, so builds run from the command line. Verified on iPad Pro 12.9 (6th gen).

M1 step 1, done 15 Nov 2026: one aircraft follows a path drawn with a finger. Verified on the iPad by the owner.

Code in place (Assets/Scripts): InputAdapter turns touch and mouse into press, drag and release in world coordinates. PathRecorder and PathPlanner record the line from the aircraft and replace it when it is redrawn in flight. AircraftMotion and AircraftController move the aircraft exactly along the line at constant speed. PathIntegrity checks that the aircraft never leaves the drawn line. PrototypeSetup creates the scene objects for now and will be replaced by LevelDirector.

Rules already built: C5 (with no path the aircraft flies straight and turns back at the screen edge; when a path ends it keeps its last heading), landscape only (C19), 60 FPS target.

Tests: 33 EditMode unit tests for path recording, exact following and edge turn-back (Assets/Tests/EditMode).

M1 remaining steps: 2. runway and landing detection (LandingSystem). 3. a second aircraft with danger warnings and collisions (AirspaceSafety). 4. landing counter and restart in under two seconds (C3). M1 passes when one complete session is playable.

Scheduled by the owner on 15 Nov 2026: step 5, the Island wind rule (C1), and step 6, slowing time while drawing (C4), each with an on/off switch. Android testing on a real phone (C18) starts once the iOS version is ready.

## NEXT STEP

Continue M1 one step at a time, each built, installed and checked on the iPad before the next starts. Step 2: Island Airport runway and landing detection (LandingSystem). Step 3: a second aircraft with danger warnings and collisions (AirspaceSafety). Step 4: landing counter and restart in under two seconds (C3). Step 5: Island wind rule (C1) with an on/off switch. Step 6: slow time while drawing (C4) with an on/off switch.
