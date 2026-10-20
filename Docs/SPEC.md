# Game: Paper Flight (working title — rename after CPI test)

Copy this file to `Docs/SPEC.md` in the template project before starting.

## Core mechanic (1 sentence)
The player flicks a paper plane across a big 3D room, then steers it with phone tilt, riding fan wind and warm updrafts to stay in the air as long as possible.

## Why this works as hypercasual
- Instantly readable from a 15 s video: flick → glide → near miss with a fan → crash or long save.
- Two simple inputs in sequence (flick, then tilt). No buttons, no UI during flight.
- Every run is short (10-40 s) with a clear "one more try, I can beat that time" loop.
- Score is a single number: seconds airborne.

## Controls
**Phase 1 — Launch (flick)**
- Player touches near the plane, drags and releases. Swipe vector sets launch:
  - Swipe speed → launch speed (clamped between `MinLaunchSpeed` and `MaxLaunchSpeed`).
  - Swipe direction (up/down/left/right on screen) → launch pitch and yaw (clamped to a forward cone so the plane never launches backwards into the camera).
- Show a faint predicted-direction arrow while dragging (no full trajectory, keep it skill-based).
- Very weak swipes launch at `MinLaunchSpeed` so a tap never produces a dead run.

**Phase 2 — Flight (tilt)**
- Tilt left/right (gravity X) = bank. Banking turns the plane (yaw rate proportional to bank angle).
- Tilt forward/back (gravity Y/Z relative to calibration) = pitch. Nose down trades altitude for speed; nose up trades speed for lift and climb, until stall.
- Calibration: the phone's orientation at launch release = neutral. Recalibrate every run.
- Smoothing, dead zone and sensitivity (separately for bank and pitch) in `GameConfig`.
- Editor fallback: arrow keys / WASD and mouse drag for the flick.
- Portrait orientation locked.

## Flight model (arcade, NOT realistic aerodynamics)
Use a Rigidbody with custom forces applied in `FixedUpdate`. Keep it simple, readable and tunable:
- `Gravity` scaled down (e.g. 0.5x) so flights last long enough to be fun.
- `Lift = LiftCoef * speed² * f(angleOfAttack)`; `f` is a curve in config, peaks around a small positive AoA, drops sharply past `StallAngle`.
- `Drag = DragCoef * speed²` (+ extra induced drag when pitching up hard).
- Forward vector slowly aligns with velocity (weathervane effect) so the plane feels stable and "paper-like".
- Banking produces yaw turn: `yawRate = BankAngle * TurnRate`.
- **Stall**: speed below `StallSpeed` or AoA above `StallAngle` → nose drops, lift collapses, plane tumbles gently. Recoverable by pitching down to regain speed.
- Optional flutter: small noise on rotation scaled by turbulence level.
- Starting glide ratio ≈ 8-12:1 in still air. Without wind help, a run should last about 4-8 s; wind and thermals extend it. **The skill is reading the room and riding the air.**
- All constants in `FlightConfig` ScriptableObject, with live-tuning debug panel (dev builds only).

## Room and wind (the heart of the game)
- One large 3D room (living room / loft). Room feels huge relative to the plane (plane ≈ 0.25 m, room ≈ 20 m × 14 m × 5 m). Tune scale until flights feel long.
- Furniture and small/large props: sofa, table, bookshelf, lamp, plant, boxes, balls, books, hanging mobile. Low-poly, flat colors. All props are static colliders (only the ceiling-fan blades and mobile move).
- **Wind volumes** (custom trigger colliders; do NOT use Unity's `WindZone`, it only affects trees/particles):
  - **Floor/oscillating fan**: directional cone of wind, strength falls off with distance, optional left-right sweeping.
  - **Ceiling fan**: strong downdraft below, weak updraft around its rim. Rotating blades are a crash hazard.
  - **Heater / radiator / sunny window spot**: vertical updraft column (thermal) → free altitude.
  - **AC vent**: downdraft zone, a hazard.
  - **Open window gust**: periodic gust, timed.
- Wind force = `direction * strength * falloff(distance) * (1 + turbulence * noise(time, position))`.
- Turbulence uses Perlin noise sampled on position and time; stronger near fan edges.
- **Readability**: wind must be visible. Use soft particle streaks / dust motes in each wind volume, rotating blades, a visible heat shimmer or sparkles above thermals. If a player can't see the wind, the game fails.
- **Layouts**: 3 room layouts (different fan/thermal/prop positions) rotated by run number. Each run also randomizes fan on/off timing and sweep phase within layout rules, seeded and logged.
- **Crash**: any contact with a collider (floor, furniture, wall, fan blade) ends the run. Short settle (0.6 s) then Game Over. Walls/ceiling contact counts as crash.

## Score and goals
- Score = seconds airborne (one decimal). Secondary stat shown on Game Over: distance flown.
- Best time saved locally.
- Milestone toasts at 10 s, 20 s, 30 s, 45 s, 60 s (visual only, no rewards).
- No gems, coins or extra currencies.

## Difficulty / progression
- No levels. Difficulty comes from layout rotation and wind randomization.
- Layout 1 (easy, run 1-3): one fan, one big thermal, few props, forgiving.
- Layout 2: ceiling fan + AC downdraft + more props.
- Layout 3: oscillating fan + timed window gust + dense clutter.
- After run 3, rotate layouts in order; occasionally (weighted) reuse the easy layout so players can post a high score.
- First run is assisted: slightly more lift, and a one-time hint animation (ghost finger flick, then tilt icon). No text-heavy tutorial.

## Session length target
- Average run 15-30 s, median 4-6 runs in the first session.

## Feel / juice (must-have, Day 2)
- Paper plane model with slightly flexing wings (cheap vertex or bone wobble), subtle trail.
- Camera: chase cam behind and above, smoothed, with FOV rising with speed, gentle roll with banking; cinematic slow orbit at the moment of launch.
- **Depth cue**: soft blob shadow projected on the floor/props under the plane + a small altitude indicator line. Essential for judging height.
- Wind-ride feedback: sparkles and a whoosh when entering a thermal, light haptic pulses.
- Crash: 0.3 s slow-mo, paper crumple SFX, light haptic. Plane tumbles realistically (let physics play it out).
- Small props react softly if hit (optional, only if cheap): nudge, no gameplay effect.
- Music: calm lo-fi loop. SFX: whoosh scaling with speed, fan hum (3D audio, louder near fans, which also helps readability), crash, UI.

## Art style
- Flat/low-poly cozy room, warm light, no textures, URP Simple Lit with baked/fake lighting. No realtime shadows except the blob shadow for the plane.
- Pastel palette per layout (e.g. morning, sunset, night with lamp glow).
- Strong contrast between the plane (bright white/colored) and the background.

## Ad placements (AdMob, values in `AdConfig`)
- **Interstitial**: on Retry tap after Game Over, only if cooldown allows (≥45 s since last, not before session 2 or 90 s of total play).
- **Rewarded "Boost launch"**: before launching, optional button gives +20% launch speed and a short starting updraft for that run. Offered on Game Over → Retry screen.
- **Rewarded "Second chance"**: once per run, on Game Over, respawns the plane at the crash altitude/position (or last safe point) with an upward gust and 2 s invulnerability. Button hidden if no ad loaded.
- No banner.
- Preload the next ad right after one is shown.
- Ad SDK code stays behind `IAdService`.

### AdMob notes (differs from the template default)
- Template defaults to AppLovin MAX behind `IAdService`. Add a new `AdMobAdService : IAdService` using the official Google Mobile Ads Unity plugin (verify the current install method in Google's docs) and select it via `AdConfig`/bootstrapper. Keep the mock implementation for Editor.
- Consent: use Google's UMP SDK (included with Google Mobile Ads in Unity) + iOS ATT before ad requests.
- Use Google test ad unit IDs and registered test devices during development; never click your own live ads.
- Ad unit IDs in the git-ignored `Secrets.asset`.
- Publish `app-ads.txt`. Not child-directed; set the correct content rating and tag-for-child-treatment settings.

## Analytics events
`session_start`, `run_start` (layout id, seed), `launch` (speed, pitch, yaw), `run_end` (duration, distance, crash_cause, layout id, seed), `thermal_entered`, `rewarded_offered`, `rewarded_completed` (type), `interstitial_shown`, `settings_changed`. Goals: D1 ≥ 35-40%, runs per session, ads per session.

## UI screens
- Menu: title, best time, big "Tap to Play" (plays a short room fly-through), settings (sound, haptics, tilt sensitivity).
- Pre-launch HUD: plane on a launch pad/desk, ghost-finger hint (first runs only), optional "Boost (ad)" button.
- In-flight HUD: only the timer (top center). Nothing else.
- Game Over: time, best, distance, "Second chance (ad)", "Retry" (largest), Menu.
- Safe-area aware, portrait only.

## Out of scope (explicitly, do NOT build)
- IAP, ad removal, shop/skins/paper types, leaderboards, achievements, accounts, multiplayer.
- Full aerodynamic simulation, soft-body paper deformation, destructible props.
- Multiple rooms beyond the 3 layouts, story, missions, collectibles.
- Localization beyond English UI.
- Complex tutorial text.

## Technical notes
- Fixed timestep at 50 Hz; Rigidbody with continuous collision detection on the plane; all forces in `FixedUpdate`.
- Wind volumes as pooled/precomputed data (center, shape, direction, strength, falloff); sample wind once per physics step, no per-frame allocations.
- Plane collider: simple capsule/box, slightly forgiving. Props use simple primitive colliders only (no mesh colliders on furniture).
- Keep draw calls low: static batching for room, GPU instancing for props, baked lightmaps or flat unlit-ish look.
- Target 60 FPS on a mid-range Android device.
- Determinism isn't required, but log seed + launch parameters so odd runs can be reproduced.
- Dev-only debug panel: sliders for lift, drag, gravity scale, wind strength, tilt sensitivity; save/load presets to `FlightConfig`.

## Acceptance criteria (Definition of Done)
- [ ] Flick launch feels fair: any reasonable swipe produces a flight; strong swipes feel faster, not broken.
- [ ] Tilt steering feels responsive, calibrates at any holding angle, no gimbal/jitter issues.
- [ ] A skilled player can reach 30 s+ on layout 1; a first-time player reaches 5-10 s.
- [ ] All wind types are visibly telegraphed and clearly affect the plane.
- [ ] Depth cue (shadow/altitude line) makes height judgeable.
- [ ] 60 FPS steady, no GC spikes during a 5-minute session (Profiler check).
- [ ] AdMob test ads: interstitial cooldown, both rewarded flows, graceful no-fill.
- [ ] Consent flow completes before the first ad request.
- [ ] Analytics events fire correctly.
- [ ] Works with sound off and haptics off.
- [ ] Release checklist (`Docs/CHECKLIST.md`) complete.

## 4-day plan (physics tuning is the risk, timebox it)
- **Day 1**: Room greybox, plane Rigidbody with flight model + flick launch + tilt steering + chase camera + crash detection + timer. Debug tuning panel. Goal: feels like gliding paper.
- **Day 2**: Wind volumes (floor fan, thermal, ceiling fan, AC), wind VFX, blob shadow, props/layout 1, juice (trail, SFX, haptics, slow-mo), menu/HUD/Game Over.
- **Day 3**: Layouts 2-3, first-run assist and hint, AdMob + consent + analytics + save, device build, perf pass.
- **Day 4**: Tuning on real devices, icon, store screenshots, 15-20 s CPI video, final build, soft launch / CPI test.
- **Timebox rule**: if flight feel is not fun by end of Day 2, cut scope (drop ceiling fan blades and gust) and simplify the model rather than extending the schedule. If still not fun, shelve the game.

## CPI test notes
- Creative: first 3 s = flick launch, then a near miss with a fan and a thermal lift, ending on a long-time score pop-up ("37.2 s").
- Variants: different room palette / plane color; also test one variant showing only the dramatic crash slow-mo.
- Decision rule: if CPI is above your threshold after a few hundred impressions, shelve and move to the next prototype.

## Prompt to start development (paste into Claude Code after the template exists)
> Read `CLAUDE.md` and `Docs/SPEC.md`. Implement the game described in the spec on top of the template, following the 4-day plan. Start with Day 1 only: one greybox room, a paper plane Rigidbody with the arcade flight model from the spec (configurable via `FlightConfig`), flick-to-launch input, tilt steering via `IPlayerInput` with per-run calibration, chase camera, crash detection, and the airborne timer. Include a dev-only live tuning panel. Use Editor scripts to generate the scene and prefabs. Keep everything compiling, and stop at the end of Day 1 with a list of what to test on a real device and which `FlightConfig` values matter most for the feel.
