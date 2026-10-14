# SDK setup

The Editor always uses mock services. Real SDKs only run in **iOS device builds** with the matching scripting define
(set by `Tools/Template/Setup/Configure AdMob` / `Apply All Project Settings`):

| Define | Enables | Code |
|---|---|---|
| `HC_ADMOB` (on) | Google AdMob ads + Google UMP consent form + Apple ATT prompt | `AdMobAdService`, `UmpConsentService`, `AttBridge` (+ `Plugins/iOS/HCTracking.mm`) |
| `HC_FIREBASE` (off) | Firebase Analytics (step 2, not integrated yet) | `FirebaseAnalyticsService` |

Without IDs the game still runs: no ads, never a crash.

**Test vs live ads:** **development builds always use Google's test ad units**, whatever is in `Secrets`, because clicking
your own live ads can get the AdMob account banned. Release builds use the ad unit IDs from `Secrets.asset`.

Boot order: consent (UMP form, then ATT prompt) → ads init → analytics init → Menu.

---

## Installed
- `com.google.ads.mobile` **11.5.0** from Google's documented OpenUPM registry (`Packages/manifest.json` →
  `scopedRegistries`, scope `com.google`). It pulls in Google's External Dependency Manager. Update by bumping the version
  (check the [release notes](https://github.com/googleads/googleads-mobile-unity/releases) first).
- iOS pods (Google-Mobile-Ads-SDK, GoogleUserMessagingPlatform) are installed by CocoaPods during the Unity build.
  `PodfileCdnOnly` forces the CocoaPods CDN (Google's XMLs add the slow git Specs repo).
- The plugin writes `GADApplicationIdentifier`, `NSUserTrackingUsageDescription` and SKAdNetwork IDs into Info.plist itself.

## One-time: AdMob account + template test app
1. Create an AdMob account (admob.google.com) and complete payment/tax info later (needed before payouts).
2. **Apps > Add app > iOS > "Not published yet"**, name `HC Template`. Copy the **App ID** (`ca-app-pub-…~…`).
3. **Ad units** for that app: **Interstitial** and **Rewarded** (Banner optional). Copy the IDs (`ca-app-pub-…/…`).
4. **Privacy & messaging > GDPR (European regulations) > Create message**:
   - Apps: HC Template. User consent options: **"Consent or Manage options"**. Targeting: **Everywhere**. Publish.
5. Optional: **Privacy & messaging > IDFA explainer** message. It's shown just before Apple's ATT prompt and raises opt-in.
6. A public **privacy policy URL** (required by Apple and Google). One generic page can serve all games.

New accounts often get **limited ad serving** until the app is live on the App Store, linked in AdMob
(**App settings > App store details**), and `app-ads.txt` is verified. Test ads are unaffected.

## Where the IDs go
| What | Where | Committed? |
|---|---|---|
| AdMob **App ID** (iOS) | **Assets > Google Mobile Ads > Settings** → iOS App ID (template default: Google's sample ID) | yes (not secret, per game) |
| Interstitial / Rewarded / Banner **ad unit IDs** | `Tools/Template/Setup/Create Secrets From Example` → `Assets/_Game/Configs/Resources/Secrets.asset` | **no** (git-ignored) |
| ATT prompt text | Google Mobile Ads Settings → User Tracking Usage Description | yes |
| **Test device hashed ID** (forces the consent form on your phone) | `Secrets.asset` → Google Test Device Hashed Id. The UMP SDK prints it in the Xcode console on first run. | no |

## Testing on device
1. `Tools/Template/Build iOS (dev)`, open `Builds/iOS-dev/Unity-iPhone.xcworkspace` (**workspace**, not .xcodeproj), set your signing team, run.
2. First launch: copy the UMP "test device hashed ID" from the Xcode console into `Secrets.asset`, rebuild. The consent
   form then always shows on that phone in dev builds (simulated EEA).
3. The ATT prompt shows once per install (after the consent form). Delete the app to see both again.
4. Expect Google test ads labelled "Test Ad" at game over (interstitial, from session 2 or after 90 s) and on "Continue".

## Per game (after `scripts/new-game.sh`)
1. AdMob: add the iOS app (+ Interstitial/Rewarded ad units) for the game's bundle ID.
2. Set its App ID in **Assets > Google Mobile Ads > Settings**; ad unit IDs into `Secrets.asset`.
3. Add the app to the GDPR message (and IDFA explainer).
4. After release: link the App Store listing in AdMob and publish `app-ads.txt` on the developer website from your store listing.

## Later: mediation
To raise eCPMs once a game has traction, add networks via AdMob mediation or switch to a mediation platform
(Unity LevelPlay; AppLovin MAX if they accept the account). Either one is just another `IAdService` implementation.
