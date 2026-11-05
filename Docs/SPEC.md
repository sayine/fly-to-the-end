# Game: Paper Flight (hybrid casual)

The game started as a one-mechanic hypercasual prototype (see git history for the original spec) and is now
moving to **hybrid casual**: the same flight core, plus a house to explore, a level list with missions, stars, and a
collection of 20 planes to unlock. Monetization stays **ads only** (no IAP, no currencies to buy).

## Core mechanic (1 sentence)
Throw a paper plane with one swipe, then steer it with one finger through a big house, riding fans and warm air to
complete each level's mission.

## Controls (built, keep)
- **Throw:** swipe up anywhere on the screen (swipe speed → throw speed, swipe direction → aim, clamped to a forward cone).
- **Steer:** put a finger down anywhere and drag from that point: left/right = bank, up/down = pitch (drag up = nose up).
  Releasing eases back to level flight. The top 14% of the screen is left to HUD buttons.
- First-time ghost-finger hint ("Swipe up to throw", then "Drag to steer"), shown until the player has steered.

## Flight model (built, keep)
Arcade paper-plane model (`FlightConfig`): lift/drag with a capped lift, weathervane AoA, bank-to-turn, stall with
recovery, plus arcade assists (cruise push, slow neutral sink) so an untouched glide lasts ~40-60 s and runs end on a
crash, not on the floor. **Each plane scales these values** (see Planes).

## Wind (built, keep)
Wind zones never crash the plane; they push it (fans sideways, thermals up, AC vents down) and are shown with
flowing ribbons, dust motes and floor rings. Strengths in `WindConfig`, per-zone scale for gentle helper fans.

---

## 1. The house (one connected map)
One big single-floor house, built by the scene generator. Furniture is generated at real size × 1.6 so the 25 cm
plane feels like a toy. Ceilings 6 m, hallways 6 m wide, 5 m tall, doorways 5 m wide.

```
 z ↑ (north)
        ┌────────────┬────────────┐
        │ Bedroom 1  │ Bedroom 2  │   bed, wardrobe, desk / bunk bed, toy shelf
        └─────┬──────┴──────┬─────┘
 ┌────────────┤             ├────────────┐
 │  Kitchen   ═   Hallway   ═   Study    │   counters, island, fridge / desk, bookcases, globe
 └────────────┤   (spine)   ├────────────┘
        ┌─────┴─────────────┴─────┐
        │       Living room       │   launch hub: sofa, TV, shelves (exists)
        └─────────────────────────┘
```

| Room | Size (W × D m) | Wind | Typical missions |
|---|---|---|---|
| Living room | 26 × 24 | floor fan, radiator thermal, AC vent | tutorial levels, airtime, gems |
| Hallway (spine) | 6 × 40 | helper fans | reach-a-room, rings |
| Kitchen | 20 × 22 | extractor hood updraft, oven thermal, fridge cold downdraft | land on table/counter, gems |
| Bedroom 1 | 18 × 18 | ceiling fan, window gust | land on bed, narrow passages |
| Bedroom 2 | 16 × 18 | desk fan, radiator | rings through toy shelves, gems |
| Study | 16 × 18 | desk fan, lamp thermal | narrow gaps between bookcases, land on desk |

Each level chooses a **start point** (a shelf, counter, desk or bed in some room) and spawns its own mission items,
so new levels need no new geometry.

## 2. Levels and missions
- A level is a `LevelConfig` asset: id, room, start point, mission type + parameters, star thresholds, the mission
  items to place, optional wind changes (fans on/off, strength), and a time limit where relevant.
- Levels unlock in order. Each completed level awards **1-3 stars** (best result is kept). Failing (crash before the
  goal, or time out) offers Retry and the rewarded "Second chance".
- **Mission types** (first set):

| Mission | Goal | Stars |
|---|---|---|
| Airtime | Stay in the air N seconds | N, 1.5N, 2N seconds |
| Land on target | Touch down softly on a target (table, bed, counter, desk) | landed; landed slow; landed in the bullseye |
| Collect gems | Fly through gems placed along a route | 50%, 80%, 100% of the gems |
| Rings / narrow passage | Fly through rings or gaps in order (tight spaces between furniture) | all rings; under time A; under time B |
| Reach a room | Fly from the start room into a target room | reached; reached with ≥ X s airtime; reached under time T |

- **Landing rule:** touching the target surface with speed < `LandSpeed` and a gentle descent angle counts as a
  landing (success); anything else is a crash.
- Launch content: **30 levels** (about 5 per room), ramping in difficulty and introducing one mission type at a time.
- **Free Flight** (the original endless mode) stays as a separate mode with its best-time record.

## 3. Planes (20 at launch)
Each plane is a `PlaneConfig` asset: name, look (fold shape + colors/pattern), stats, unlock rule. Stats are shown as
1-5 bars in the hangar and map to multipliers on `FlightConfig`:

| Stat | What it changes |
|---|---|
| **Speed** | cruise speed and throw speed |
| **Agility** | turn rate and bank/pitch response |
| **Glide** | neutral sink rate (flight duration) |
| **Lift** | climb authority before stalling |
| **Stability** | flutter and how hard wind pushes it (heavier feel) |

Look: procedural paper planes (different fold shapes: dart, glider, wide wing, delta, stunt, swallow…) with colors
and printed patterns; RC planes and drones are a later expansion (out of scope for now).

| # | Plane | Shape | Speed | Agility | Glide | Lift | Stability | Unlock |
|---|---|---|---|---|---|---|---|---|
| 1 | Classic Dart | dart | 3 | 3 | 3 | 3 | 3 | start |
| 2 | Classic Dart (Sky) | dart, blue | 3 | 3 | 3 | 3 | 3 | start |
| 3 | Notebook Glider | glider, lined paper | 2 | 3 | 4 | 3 | 3 | 5 stars |
| 4 | Swallow | swallow | 3 | 4 | 3 | 3 | 2 | complete level 5 |
| 5 | Sticky Note | small square wing, yellow | 2 | 5 | 2 | 3 | 2 | 12 stars |
| 6 | Newspaper Bomber | wide wing, newsprint | 2 | 2 | 4 | 3 | 5 | complete level 10 |
| 7 | Origami Crane Jet | delta, red | 4 | 3 | 2 | 3 | 3 | 20 stars |
| 8 | Weekly Streak | dart, gold | 3 | 4 | 4 | 3 | 3 | check-in day 7 |
| 9 | Map Glider | glider, map print | 3 | 3 | 4 | 4 | 3 | 30 stars |
| 10 | Stunt Flyer | stunt | 3 | 5 | 2 | 4 | 2 | complete level 15 |
| 11 | Kite Wing | wide wing, striped | 2 | 3 | 5 | 4 | 3 | 40 stars |
| 12 | Banknote | slim dart, green | 5 | 3 | 2 | 2 | 3 | complete level 20 |
| 13 | Fortnight Flyer | delta, silver | 4 | 4 | 3 | 4 | 3 | check-in day 14 |
| 14 | Comic Strip | swallow, comic print | 3 | 4 | 3 | 4 | 4 | 50 stars |
| 15 | Blueprint | delta, blueprint | 4 | 4 | 3 | 3 | 4 | complete level 25 |
| 16 | Night Owl | glider, dark blue | 3 | 3 | 5 | 4 | 3 | 60 stars |
| 17 | Tissue Floater | wide wing, white | 1 | 3 | 5 | 5 | 1 | 70 stars |
| 18 | Cardboard Tank | wide dart, brown | 2 | 2 | 3 | 2 | 5 | complete level 30 |
| 19 | Rainbow | stunt, rainbow | 4 | 5 | 4 | 4 | 3 | 80 stars |
| 20 | Golden Jet | delta, gold | 5 | 5 | 4 | 4 | 4 | 90 stars (all levels, 3 stars each) |

Numbers are a first pass, to tune after play-testing; 30 levels × 3 stars = 90 stars max.

## 4. Meta and retention
- **Stars:** total stars unlock planes (table above). Shown on the level map and in the hangar.
- **Daily check-in:** counts **total days the game was opened** (missed days don't reset). Day 7 and day 14 award
  planes; other days show the progress bar toward the next reward. Uses local date; no server.
- **Hangar:** pick a plane, see its stats, see how to unlock locked ones. **Rewarded "Try it"**: fly a locked plane for
  one level.
- **Notifications** (later, optional): "Your daily check-in is ready".

## 5. Ads (AdMob, values in `AdConfig`)
- **Interstitial:** between levels (on Next/Retry), never during flight, ≥ 45 s cooldown, not before session 2 or 90 s.
- **Rewarded "Second chance":** once per level attempt after a crash: respawn at the last safe point with 2 s invulnerability.
- **Rewarded "Try it":** fly a locked plane for one level (hangar and level start).
- **Rewarded "Boost throw"** (Free Flight): +20% throw speed for one run.
- No banner. Ad code stays behind `IAdService`; graceful fallback if no ad is loaded (button hidden).

## 6. Save data (`ISaveService`, game key)
Per-level best stars and best time; total stars; unlocked planes; selected plane; check-in day count and last check-in
date; Free Flight best time; tutorial seen; settings.

## 7. Analytics events
`session_start`, `level_start` (level, room, mission, plane), `level_end` (level, result, stars, duration, crash_cause),
`plane_unlocked` (plane, source), `plane_selected`, `checkin` (day), `rewarded_offered` / `rewarded_completed` (type),
`interstitial_shown`, `tutorial_complete`, `free_flight_end` (duration).

## 8. UI screens
Menu (backdrop: house fly-through, Play / Free Flight / Hangar, check-in badge) · Level map (rooms as sections, stars
per level, locked levels) · Level intro (mission text + icon, star goals, plane, "Try it") · In-flight HUD (mission
progress only: timer / gems / rings) · Level complete (stars, Next, Retry) · Level failed (Second chance, Retry) ·
Hangar (plane grid, stats bars, unlock conditions) · Daily check-in popup · Settings.

## Out of scope (for now)
IAP, coins/shop, leaderboards, accounts, multiplayer, RC planes and drones (later expansion), multiple floors,
procedural level generation, localization beyond English.

## Technical notes
- Content is generated by Editor scripts (house, wind, mission items, configs) — no hand wiring (CLAUDE.md rule 1).
- `LevelConfig`, `PlaneConfig`, `MissionConfig` ScriptableObjects in `Assets/_Game/Configs`; a `LevelCatalog` and
  `PlaneCatalog` list them.
- One Game scene with the whole house; the level controller places start point, mission items and wind settings.
  Static batching for the house; mission items pooled.
- 60 FPS on the oldest supported iPhone; the bigger house must keep draw calls low (static batching, shared
  materials, LOD-free low-poly Meshy models).

## Roadmap (phases; each ends compiling, tested, committed)
1. **House:** bigger living room, kitchen, 2 bedrooms, study, hallway spine; room wind; Meshy furniture for the new
   rooms (~25 models, ~375 credits); chase camera across all rooms.
2. **Levels:** `LevelConfig`, level controller, the 5 mission types, landing detection, gems and rings, stars, save,
   level map + intro + complete/failed screens, first 10 levels.
3. **Planes:** `PlaneConfig` + stat multipliers, 20 procedural paper planes, hangar, unlock rules, daily check-in,
   rewarded "Try it".
4. **Content and polish:** levels 11-30, Free Flight mode wiring, ads + analytics events, tuning on device.

## Acceptance criteria (per phase)
- Compiles with zero errors; EditMode + PlayMode tests pass; Boot scene plays in the Editor.
- Phase 2: every mission type has a PlayMode test that completes and fails it.
- Phase 3: unlock rules unit-tested (stars, level, check-in day counting across date changes).
- 60 FPS steady and no GC spikes during a 5-minute session (Profiler on device).
