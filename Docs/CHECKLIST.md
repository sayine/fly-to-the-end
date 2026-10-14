# Release Checklist

Run top to bottom for every store build. Tick in the PR / release notes.

## Identity & versioning
- [ ] `Player Settings`: company name, product name, bundle ID changed from the template placeholders (`YourCompany`, `HC Template`, `com.yourcompany.hctemplate`). `BuildScript` warns if they weren't.
- [ ] Version bumped (`Player Settings > Version`) and build number bumped (`Tools/Template/Bump Build Number`, or `-buildVersion` / `-buildNumber` in CI). App Store Connect rejects a reused build number.
- [ ] `Docs/SPEC.md` matches what shipped (nothing out-of-scope snuck in).

## Look & feel
- [ ] App icon set (1024×1024, no alpha/transparency).
- [ ] Splash screen: background color/logo set (Unity splash per license tier).
- [ ] Orientation locked to portrait (`Tools/Template/Setup/Apply Player Settings` enforces it).
- [ ] UI checked on a notched/Dynamic Island iPhone (safe area), a small iPhone (SE), and iPad 4:3 if shipping on iPad.
- [ ] Loading/Menu title reads correctly (it shows Product Name automatically; long names may need a smaller font).

## Ads & consent
- [ ] SDK defines set for the store build: `HC_MAX`, `HC_CONSENT` (and `HC_FIREBASE` if used) for iOS.
- [ ] `Assets/_Game/Configs/Resources/Secrets.asset` exists locally/CI with **production** ad unit IDs (never committed).
- [ ] **Test ads OFF**: MAX test mode / test device IDs removed, Mediation Debugger not reachable in release.
- [ ] Consent flow verified on a fresh install in an EEA region (VPN or UMP debug geography) and ATT prompt shows on iOS.
- [ ] Settings > "Privacy options" button opens the UMP form when required.
- [ ] `AdConfig.asset` frequency rules reviewed (first interstitial: session ≥2 or ≥90 s; cooldown 45 s).
- [ ] Rewarded "Continue" hidden when no ad is loaded (airplane mode test) and the game still works with no network.
- [ ] `app-ads.txt` published on the developer website domain listed in the store; MAX/network lines included.
- [ ] iOS: `NSUserTrackingUsageDescription` present in Info.plist; SKAdNetwork IDs added (MAX Integration Manager).

## Analytics
- [ ] Standard events visible in the provider's debug view: `session_start`, `level_start`, `level_end`, `game_over`, `rewarded_offered`, `rewarded_completed`, `interstitial_shown`, `tutorial_complete`.
- [ ] Per-game Firebase config file (`GoogleService-Info.plist`) belongs to THIS game.

## Store & legal
- [ ] Privacy policy URL live and linked in the App Store listing and in-app (Settings).
- [ ] App Store Connect App Privacy ("nutrition label") reflects ad/analytics SDKs; SDK privacy manifests (`PrivacyInfo.xcprivacy`) present.
- [ ] Age rating questionnaire done; target audience is not "children" unless the ad stack is configured for it.
- [ ] Store screenshots (portrait): 6.9"/6.7" iPhone (and iPad 13" if supported).
- [ ] Store listing: subtitle, description, keywords, support URL, preview video (optional).

## Build & device QA
- [ ] `Tools/Template/Build iOS` (release, not dev); Xcode project builds with the latest Xcode, signing team set, archive uploads via Organizer/Transporter.
- [ ] Played a full loop on the oldest iPhone you support: 60 FPS in gameplay (Unity Profiler on a dev build / Xcode GPU report), no GC spikes during play.
- [ ] Interrupts: background/foreground during play auto-pauses; incoming call; Control Center pull-down; relaunch keeps best score/settings.
- [ ] Sound and vibration toggles persist across restarts.
- [ ] No `Development Build` flag, no debug overlays (`Demo_Tilt` is disabled in Build Settings).
- [ ] EditMode + PlayMode tests green (`scripts/unity-test.sh EditMode`, `scripts/unity-test.sh PlayMode`).
