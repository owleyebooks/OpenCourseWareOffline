# Release Checklist (OCW Offline)

Step-by-step checklists for cutting the first store release. **Nothing here is executed now**; this is the runbook for release day. Current version in code: `ApplicationDisplayVersion 0.1`, Android `ApplicationVersion 1`.

Conventions used below: `[ ]` = unchecked task. Fill in the `[PLACEHOLDER]` values before starting.

---

## A. Android (Google Play)

### A1. Signing key (one-time, do this first)

- [ ] Decide where the release keystore will live and who holds it (losing it = cannot update the app under the same package name `com.owleyebooks.ocwoffline` ever again).
- [ ] Generate the keystore with `keytool` (JDK's keytool; **documented here (do NOT run as part of drafting)**):
  ```
  keytool -genkeypair -v \
    -keystore ocw-offline-release.keystore \
    -alias ocwoffline \
    -keyalg RSA -keysize 4096 \
    -validity 10950 \
    -storepass [STORE_PASSWORD] \
    -keypass [KEY_PASSWORD] \
    -dname "CN=[YOUR NAME], OU=[ORG UNIT], O=[ORGANIZATION], L=[CITY], ST=[STATE], C=[COUNTRY]"
  ```
  Validity 10950 days = 30 years, which satisfies Play's requirement that the key be valid past 2033.
- [ ] Record the keystore location, alias, and passwords in the **Secure Vault** (never in the repo, never in chat logs). This checklist must never contain the real passwords.
- [ ] Back up the keystore file to a second durable location (encrypted).
- [ ] For CI-signed release AABs: add these secrets to the GitHub repo (Settings > Secrets and variables > Actions). The `android.yml` release job signs the AAB only when they exist; without them the AAB builds unsigned.
  - `ANDROID_KEYSTORE_BASE64`: `base64 -w0 ocw-offline-release.keystore`
  - `ANDROID_KEY_ALIAS`: the key alias
  - `ANDROID_STORE_PASS`: the keystore password
  - `ANDROID_KEY_PASS`: the key password

### A2. Play App Signing

- [ ] Create the app in the Google Play Console (package `com.owleyebooks.ocwoffline`, title per `docs/store-listing.md`).
- [ ] Enroll in **Play App Signing** (required for new apps): Play re-signs the AAB with its own key; upload your keystore's public certificate when prompted.
- [ ] Keep the local keystore: it remains your *upload key*. If it is ever compromised, you can request an upload-key reset, but the app-signing key stays with Google.

### A3. Versioning

- [ ] Bump `ApplicationDisplayVersion` in `OcwOffline.csproj` (user-facing version name, e.g. `1.0`).
- [ ] Bump `ApplicationVersion` (the `versionCode`, integer) in `OcwOffline.csproj`, which must increase monotonically for every Play upload. Never reuse or decrease it.
- [ ] Record version name + code in the release notes / changelog for this release.

### A4. Build the AAB

- [ ] Build a release AAB (not APK) signed with the upload keystore:
  ```
  dotnet publish work/OcwOffline/OcwOffline.csproj \
    -f net10.0-android \
    -c Release \
    /p:AndroidPackageFormat=aab \
    /p:AndroidKeyStore=true \
    /p:AndroidSigningKeyStore=[PATH_TO_KEYSTORE] \
    /p:AndroidSigningKeyAlias=ocwoffline \
    /p:AndroidSigningStorePass=[STORE_PASSWORD] \
    /p:AndroidSigningKeyPass=[KEY_PASSWORD]
  ```
  (Prefer passing passwords via environment variables or MSBuild response files rather than shell history.)
- [ ] Verify the AAB: `jarsigner -verify` / `apksigner verify --print-certs` on the output, and confirm `versionCode`/`versionName` via `bundletool dump manifest`.
- [ ] Smoke-test the release build on a physical device: catalog loads, a download completes and resumes after interruption, video plays, artifact opens.

### A5. Play Console listing & rollout

- [ ] Complete the store listing (copy from `docs/store-listing.md`: title, short + full description, screenshots, feature graphic, icon).
- [ ] Fill in the **Data safety** section so it matches `docs/privacy-policy.md`: no data collected, no data shared. (Network requests go to MIT's servers; declare honestly per Play's definitions.)
- [ ] Complete the content-rating (IARC) questionnaire; expected rating: **Everyone**.
- [ ] Set the privacy-policy URL (hosted version of `docs/privacy-policy.md`) and support contact.
- [ ] Upload the AAB to an **internal testing** track first; promote to closed → open → production only after the smoke test passes on the store-distributed build.

---

## B. iOS (App Store)

### B1. Apple Developer Program (one-time)

- [ ] Enroll in the Apple Developer Program (paid membership, individual or organization). An organization account is recommended if this is more than a personal project.
- [ ] Accept the latest program license agreements in App Store Connect (a stale agreement blocks submissions).

### B2. Certificates, identifiers, provisioning profiles (one-time setup, annual-ish maintenance)

- [ ] In the Developer portal, register the App ID: `com.owleyebooks.ocwoffline` (explicit, not wildcard).
- [ ] Create an **Apple Distribution certificate** (or use Xcode automatic signing, which manages this for you).
- [ ] Create an **App Store provisioning profile** for the App ID, or let Xcode manage profiles automatically (recommended unless CI needs manual profiles).
- [ ] Note: distribution certificates expire yearly, so calendar a renewal reminder. An expired cert does not remove the app from sale but blocks new submissions until renewed.

### B3. App Store Connect record (one-time)

- [ ] Create the app record in App Store Connect: bundle ID `com.owleyebooks.ocwoffline`, SKU of your choice, primary language, and the Education category.
- [ ] Fill in the listing: name (per `docs/store-listing.md`), subtitle (≤30 chars), keywords (≤100 chars), full description, support URL, marketing URL (optional).
- [ ] Set the privacy-policy URL (hosted version of `docs/privacy-policy.md`) and complete the **App Privacy** nutrition-label questionnaire, which must match the policy: no data collected, no tracking.
- [ ] Answer the age-rating questionnaire; expected rating: **4+**.
- [ ] Upload screenshots for the required device sizes and the 1024×1024 app icon (no transparency or rounded corners; Apple masks it).

### B4. Versioning

- [ ] Set `CFBundleShortVersionString` (marketing version, e.g. `1.0`) via `ApplicationDisplayVersion` in the csproj.
- [ ] Set `CFBundleVersion` (build number, must increase for every upload to App Store Connect). Coordinate with the Android `versionCode` scheme so both platforms share one build number sequence.
- [ ] Record version + build number in the release notes / changelog.

### B5. Build, archive, upload

- [ ] Archive a Release build for `net10.0-ios` on a Mac (or CI Mac runner) with the distribution certificate/profile:
  ```
  dotnet publish work/OcwOffline/OcwOffline.csproj \
    -f net10.0-ios \
    -c Release \
    /p:ArchiveOnBuild=true \
    /p:CodesignKey="[DISTRIBUTION CERT NAME]" \
    /p:CodesignProvision="[PROFILE NAME OR UUID]"
  ```
- [ ] Validate the archive in Xcode Organizer (or `altool`/`notarytool`-era `xcrun` validation) before uploading.
- [ ] Upload to App Store Connect (Xcode Organizer → Distribute App, or Transporter).
- [ ] In App Store Connect: attach the build to the version, fill in "What's New", submit for review.
- [ ] Use **TestFlight** (internal, then external testers) before submitting for review on the first release.

### B6. Post-approval

- [ ] Choose release mode: manual release (recommended for v1; you press the button after approval) vs. automatic.
- [ ] After release, verify the live listing: title, screenshots, privacy-policy link, and that the downloaded app launches clean on a device that never had the dev build.

---

## C. Shared pre-release gates (both platforms)

- [ ] Re-verify `docs/privacy-policy.md` §5 (no analytics) against the actual package list, since a newly added SDK can silently change the data story.
- [ ] Confirm `docs/store-listing.md` title matches the in-app `ApplicationTitle` and the store listing name.
- [ ] Confirm the MIT trademark/attribution disclaimer is in the live listing text (see store-listing.md trademark notes).
- [ ] Confirm the privacy-policy URL is live and reachable before submitting either store form.
- [ ] Tag the release commit and record the store version/build numbers alongside it.

---

*Draft notes for the maintainer (remove before publishing):*
- Current code identity: `com.owleyebooks.ocwoffline`, display version `0.1`, Android versionCode `1`. First public release should bump to `1.0` / versionCode `2`+ (or keep code `1` if it was never uploaded anywhere; versionCode only needs to increase relative to what Play has seen).
- No secrets, keystores, certificates, or passwords belong in this repo, ever. The keytool/dotnet commands above are documentation; credentials live in the Secure Vault.
