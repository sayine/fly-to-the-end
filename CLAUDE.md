# CLAUDE.md — Hypercasual Unity Template

This repo is a **template** for shipping a new hypercasual mobile game every 3-4 days.
Monetization is **ads only** (no IAP). Every decision should favor: speed, small scope, stable builds, easy cloning for the next game.

## Stack
- Unity 6 (6000.x LTS), URP (mobile-optimized), C#
- New Input System (touch + accelerometer/gyro for tilt)
- Platform: **iOS only** (IL2CPP, ARM64, iOS 15+ as required by current Xcode)
- Ads: Google AdMob + Google UMP consent + Apple ATT (behind `IAdService`/`IConsentService`; see `Docs/SDK.md`). AppLovin MAX did not accept the account; mediation can come later.
- Consent: Google UMP + Apple ATT
- Analytics: Unity Analytics or Firebase (behind `IAnalyticsService`)

## Hard rules
1. **Never wire things by hand in the Inspector** if it can be done in code or an Editor script. Prefer `[RequireComponent]`, `OnValidate` auto-reference, and Editor menu scripts under `Tools/Template/`.
2. All gameplay tuning lives in **ScriptableObject configs** (`Assets/_Game/Configs`), never hardcoded.
3. Every service is accessed through an **interface** with an **Editor/mock implementation**, so the game runs in the Editor with zero SDKs.
4. No `Find*`, `GetComponent` in `Update`, LINQ, or allocations in hot paths. Use object pooling for anything spawned repeatedly.
5. Use **assembly definitions** (`Template.Core`, `Template.Services`, `Template.Game`, `Template.Editor`).
6. Namespaces match folders. Private fields `_camelCase`, `[SerializeField] private` instead of public fields.
7. Target **60 FPS** on the oldest supported iPhone. Keep draw calls low (SRP Batcher on, GPU instancing, atlas sprites).
8. Never commit secrets (ad unit IDs, API keys). Keep them in a git-ignored `Secrets.asset`; commit a `Secrets.example.asset`.
9. Do not add packages or SDKs not listed here without asking.
10. After any change: the project must compile with **zero errors**, and the Boot scene must play in the Editor.

## Folder structure
```
Assets/
  _Template/            # Reusable core. Do not put game-specific code here.
    Core/               # EventBus, StateMachine, ObjectPool, Singleton-free ServiceLocator, Extensions
    Services/           # Ads, Analytics, Consent, Save, Audio, Haptics, SceneLoader, RemoteConfig
    UI/                 # Screen manager, base screens (Loading, Menu, HUD, Pause, GameOver, Settings)
    Editor/             # Build scripts, scene/prefab generators, validators
  _Game/                # Everything specific to the current game
    Configs/            # GameConfig, LevelConfig, AdConfig (ScriptableObjects)
    Scripts/
    Prefabs/
    Art/  Audio/  Scenes/
  Scenes/
    Boot.unity          # Initializes services, then loads Menu
    Menu.unity
    Game.unity
Docs/
  SPEC.md               # Per-game spec (see template below)
  CHECKLIST.md          # Release checklist
```

## Architecture
- **Boot scene** creates all services once (`ServiceLocator.Register<T>()`), then loads the Menu. Services are plain C# classes or `DontDestroyOnLoad` MonoBehaviours created by the bootstrapper.
- **Game flow** is a simple state machine: `Boot → Menu → Playing → Paused → GameOver → (Rewarded Continue | Retry | Menu)`.
- **Communication** between systems goes through a lightweight typed `EventBus` (struct events, no allocations).
- **UI** uses a `ScreenManager` with one prefab per screen; screens never reference gameplay classes directly, they listen to events.
- **Save**: JSON via `ISaveService` (PlayerPrefs backing is fine). Store: best score, settings, ad-free flags none (no IAP), tutorial-seen, session count.

## Services (all behind interfaces, all with Editor mocks)
| Service | Responsibility |
|---|---|
| `IAdService` | `ShowInterstitial()`, `ShowRewarded(Action<bool>)`, `ShowBanner()` (optional), cooldown logic, consent gating |
| `IConsentService` | UMP + ATT flow on first launch, must complete before ad init |
| `IAnalyticsService` | `LogEvent(name, params)`; standard events below |
| `ISaveService` | Typed load/save |
| `IAudioService` | SFX pool, music, mute setting |
| `IHapticService` | Light/medium/heavy impact, respects setting |
| `ISceneLoader` | Async load with fade |

### Ad rules (in `AdConfig`)
- Interstitial: first one not before **session 2 or 90 s**, min cooldown **45 s**, shown at natural breaks (game over), never during active play.
- Rewarded: always optional ("Continue", "2x reward"), always has a graceful fallback if no ad is loaded (button hidden or disabled).
- Preload next ad right after one is shown.
- Ad calls must never block the main thread or crash the game if the SDK fails; log and continue.

### Standard analytics events
`level_start`, `level_end` (result, duration), `game_over`, `rewarded_offered`, `rewarded_completed`, `interstitial_shown`, `session_start`, `tutorial_complete`. D1/D7 retention and session length come from the analytics provider.

## Input
- Touch: single-finger drag/tap abstraction (`IPlayerInput`).
- Tilt: accelerometer/gravity abstraction with **calibration on start**, low-pass smoothing, dead zone, and sensitivity in config. Provide an Editor keyboard/mouse fallback.
- Gameplay code depends on `IPlayerInput`, never on `Input` directly.

## Build & release
- Editor menu: `Tools/Template/Build iOS`, `Build iOS (dev)`.
- Batchmode entry point: `Template.Editor.BuildScript.BuildIOS` so CI/CLI can run it.
- Release checklist lives in `Docs/CHECKLIST.md` (icon, splash, orientation lock, store screenshots, privacy policy URL, ads.txt/app-ads.txt, test ads off, version bump).

## Per-game workflow (3-4 day cycle)
1. Write `Docs/SPEC.md` (template below) — **one core mechanic only**.
2. Day 1: greybox prototype with placeholder art, core loop playable.
3. Day 2: juice (SFX, particles, haptics, camera feel), menu, game over loop.
4. Day 3: ad integration test, analytics, build on real device, perf pass.
5. Day 4: store assets, final build, soft launch / CPI test.

### SPEC.md template
```
# Game: <name>
Core mechanic (1 sentence):
Controls:
Win/lose condition:
Difficulty curve:
Session length target:
Ad placements:
Art style (flat/low-poly, palette):
Out of scope (explicitly):
```

## How you (Claude Code) should work here
- Read this file and `Docs/SPEC.md` before starting any task.
- For multi-file work, state a short plan first, then implement in small compilable steps.
- Prefer editing the template layer (`_Template`) only when the change benefits **all** games; otherwise stay in `_Game`.
- Run/compile checks after each step (`Unity -batchmode -quit` or the project's configured check) and fix errors before moving on.
- If a requirement is ambiguous, pick the simplest option, note the assumption in your reply, and continue.
- Keep scope tiny. If a feature isn't in `SPEC.md`, don't build it.
