# T-030: Google sign-in per platform (account for sync)
Phase: 9 · Depends on: T-029 · Size: M–L

First step of sync (ARCHITECTURE §8, §9; ROADMAP T-030–T-032). This task only connects a Google account and proves
access to the app's hidden Drive folder; the sync engine is T-031. Owner decision 2026-11-02: the storage provider is
replaceable (`ISyncStorage`), Google Drive first; one provider per user.

## Goal
Settings → **Sync** section (new row in the Settings list, T-028d pattern): "Connect Google account" → system sign-in →
the section shows the account e-mail, "Connected", and a **Check connection** button that writes, lists, reads and
deletes a small test file in Drive `appDataFolder`; **Disconnect** removes the tokens (and revokes them at Google).

## Scope — do
1. **Abstractions (ViewModels/Core, no MAUI):**
   - `ISyncAccountService`: `State` (NotConfigured / SignedOut / SignedIn(email) / Error), `SignInAsync`, `SignOutAsync`,
     `GetAccessTokenAsync(ct)` (refreshes when needed), `ProviderName`.
   - `ISyncStorage` (as in ARCHITECTURE §8): `ListAsync(prefix)`, `UploadAsync(path, stream)`, `DownloadAsync(path)`,
     `DeleteAsync(path)` on the app's own folder. Google Drive adapter uses `appDataFolder` and the Drive v3 **REST** API via
     `HttpClient` (no Google .NET client library needed; if you use one, check the licence — Apache-2.0 is fine).
   - Scopes: `https://www.googleapis.com/auth/drive.appdata` plus `openid email` (to show the account). Nothing else.
2. **Windows (unpackaged):** "Desktop app" OAuth client; authorization code flow with **PKCE** and a **loopback** redirect
   (`http://127.0.0.1:<free port>/`), sign-in in the **system browser**; the app listens on the port, exchanges the code.
   Desktop client secret is not confidential (Google's installed-app model) but stays out of the repo (see 4).
3. **Android:** use the Google-supported way for Drive scopes on Android — **Google Identity Services Authorization API**
   (`AuthorizationClient`, Google Play services) through the .NET binding `Xamarin.GooglePlayServices.Auth` (binding MIT;
   the Play services SDK itself is free but closed-source under the Android SDK terms — **owner granted an exception to the
   licence rule for Google Play services only**, see Decisions in PROGRESS). Custom URI scheme redirects are not allowed
   for new Android OAuth clients, so no browser flow on Android. If you find a fully open way that Google supports for
   Drive scopes on Android, describe it in the PR before using it. The Android OAuth client is bound to the package name
   `io.github.gromichek.homeutilities` and the **SHA-1 of the signing certificate** (the owner registers it).
4. **Configuration and secrets** (ARCHITECTURE §9): client ids (Windows desktop id + secret, Android client id / web client
   id if the Authorization API needs a server client id) come from a **git-ignored** `src/HomeUtilities.App/secrets.json`
   (template `secrets.template.json` committed with empty values, documented in the PR) embedded at build time.
   Without values the Sync section says "Sync is not configured in this build" and the button is disabled; **CI builds
   without secrets**. Never log tokens or codes.
5. **Token storage:** refresh token (Windows) / authorization state in `SecureStorage`; verify it works in the **unpackaged**
   Windows app — if not, use DPAPI (`ProtectedData`, current user) in the Windows implementation. Access tokens only in
   memory. Sign-out deletes everything and calls the revoke endpoint.
6. **Errors:** no network, user cancelled, consent denied, token revoked (in the Google account settings), Drive API
   disabled in the Cloud project, clock skew — a clear localized message each, details to the error log. Cancelling the
   browser on Windows must not leave the port listener running (timeout ~5 min).
7. **Settings → Sync** page (UI checklist): state, e-mail, Connect / Check connection / Disconnect (stacked, equal width),
   a short explanation (what is stored where: a hidden app folder in the user's Google Drive, not visible in Drive; only
   this app can see it). `HelpTopic.Sync`, user guide EN + RU (new "Sync" chapter: what works now, what comes in T-031).
8. **Tests:** PKCE (verifier/challenge per RFC 7636 test vector), loopback redirect parsing (code, state check, error),
   token refresh logic with a fake token endpoint (`HttpMessageHandler`), Drive adapter request shapes (multipart upload,
   list with `spaces=appDataFolder`, download, delete) against a fake handler, state transitions of the section VM.

## Scope — do NOT
No sync of data (T-031), no attachments (T-032), no other providers (T-032a), no Google sign-in on iOS (T-033).

## Acceptance criteria
1. `dotnet test HomeUtilities.Tests.slnf` green, zero warnings; CI green **without** secrets.
2. Owner, with his `secrets.json`: on Windows and on the phone — Connect → Google consent (only "See, create and delete its
   own configuration data in your Google Drive") → e-mail shown → Check connection "OK (write, list, read, delete)" →
   restart the app → still connected → Disconnect → signed out; the app is gone from his Google account's third-party
   access list.
3. The PR lists exactly what the owner must enter where (Cloud Console values ↔ `secrets.json` keys).
4. UI checklist of CLAUDE.md filled in the PR.

## Manual test steps for the owner
1. Fill `secrets.json` as described in the PR, rebuild.
2. Windows: Settings → Sync → Connect → browser → choose account → allow → back in the app: e-mail, "Connected".
   Check connection → OK. Restart → still connected. Disconnect → "Not connected".
3. Phone: the same with the Google account picker of Android.
4. Airplane mode → Check connection → clear "no connection" message, no crash.
