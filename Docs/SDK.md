# SDK setup

The Editor always uses mock services. Real SDKs only run in **iOS device builds** with the matching scripting define:

| Define | Enables | Code |
|---|---|---|
| `HC_MAX` | AppLovin MAX ads + MAX-managed consent (Google UMP form + Apple ATT prompt) | `MaxAdService`, `MaxConsentService` |
| `HC_FIREBASE` | Firebase Analytics (step 2, not integrated yet) | `FirebaseAnalyticsService` |

Without a define (or without IDs) the game still runs: no ads, never a crash.

---

## One-time: accounts and the template test app

The template is tested as its own unregistered app: **`com.sayine.hctemplate`**.

### 1. AppLovin (MAX)
1. Create an account at the AppLovin dashboard (dash.applovin.com) and finish company/payment info later.
2. **Account > General > Keys**: copy the **SDK Key** (one per account, same for every game).
3. **MAX > Mediation > Manage > Ad Units > Create Ad Unit**, platform **iOS**, package name `com.sayine.hctemplate`:
   - `HC Template – Interstitial` (format: Interstitial)
   - `HC Template – Rewarded` (format: Rewarded)
   - (Banner only if you will use one. The template has banners off by default.)
   Copy each **Ad Unit ID**.
4. **MAX > Mediation > Manage > Test Mode**: add your iPhone (IDFA/IDFV, shown in the Mediation Debugger) so it gets test ads.

### 2. Google AdMob (needed for the consent form, and later as an ad network)
1. Create an AdMob account (admob.google.com).
2. **Apps > Add app > iOS > "Not published yet"**, name `HC Template`. Copy the **App ID** (`ca-app-pub-…~…`).
3. **Privacy & messaging > GDPR > Create message**:
   - Apps: HC Template. Languages: English (+ others you target).
   - User consent options: **"Consent or Manage options"** (do **not** enable "Close (do not consent)").
   - Targeting: **Everywhere**.
   - Styling (AppLovin's recommendation): Global → Secondary color `#ffffff`; Buttons → Secondary color `#6e6e6e`.
   - **GDPR settings > Review your ad partners**: include every network you mediate. Publish.
4. Optional: **Privacy & messaging > IDFA explainer** message (shown before the ATT prompt).

### 3. Privacy policy URL
A public page (your site, GitHub Pages, a Notion page, etc.) that covers ads, analytics and the third-party ad partners. It's required by the MAX consent flow and by the App Store. One generic URL can serve all games if it names them or stays app-agnostic.

### 4. Apple
- Apple Developer Program membership, with your Team ID set in Unity (Player Settings > iOS > Signing Team ID) or in Xcode.
- A physical iPhone for testing. Ads and ATT do not work in the Simulator in a meaningful way.

### 5. Tooling (this Mac)
- CocoaPods: installed (1.16.2). If `pod install` fails with an encoding error, add `export LANG=en_US.UTF-8` to `~/.zprofile`.

---

## Install (step 1, done in the Unity Editor)
1. Download the **MAX Unity Plugin installer (UPM)** from AppLovin's Unity integration docs: support.applovin.com → MAX → Unity → Integration → "Download Installer".
2. Unity: **Assets > Import Package > Custom Package…** → the installer. It adds AppLovin's scoped registry and the plugin.
3. **AppLovin > Integration Manager**:
   - **AppLovin SDK Key**: paste.
   - Install the **Google AdMob** adapter and set the **AdMob App ID (iOS)**. The UMP consent form comes from Google.
   - **Privacy Settings**: enable **"MAX Terms and Privacy Policy Flow"**, set the **Privacy Policy URL** (+ optional Terms URL). Keep the default **User Tracking Usage Description** (or override it).
4. Then hand back to Claude: it compiles `MaxAdService`/`MaxConsentService` against the installed plugin, adds `HC_MAX` for iOS, and builds.

## Secrets (ad unit IDs)
`Tools/Template/Setup/Create Secrets From Example` → fill `Assets/_Game/Configs/Resources/Secrets.asset` (git-ignored):
interstitial ID, rewarded ID (banner ID optional), and for debugging the **Google test device hashed ID**. The Google CMP SDK prints it in the Xcode console on first run. With it, the consent form shows on your device even outside the EU.

## Testing on device
- Development build (`Tools/Template/Build iOS (dev)`), run from Xcode.
- Consent form: set **Debug User Geography = GDPR** in Integration Manager (debug builds only) or use the hashed test device ID. The flow only shows to new users, so delete and reinstall the app to see it again.
- ATT prompt appears once per install.
- MAX **Mediation Debugger** shows SDK/adapters/CMP status and test ads.
- App Store review notes: mention that the ATT prompt is shown on iOS 14.5+.

---

## Per game (after `scripts/new-game.sh`)
1. AppLovin: create Interstitial + Rewarded ad units for the game's bundle ID → `Secrets.asset`.
2. AdMob: add the iOS app → set its **App ID** in Integration Manager; add the app to the GDPR message (and IDFA explainer).
3. Privacy policy URL in Integration Manager if it differs per game.
4. `app-ads.txt` on the developer website (AppLovin + every mediated network's lines).
5. Remove your device from Test Mode / test hashed ID before release builds.
