# Putting ProClicker on GitHub

Everything here is done in your web browser. You don't need to install Git.

The app looks for updates at `github.com/Purpletxt/ProClicker`, so the repository **must be named `ProClicker`**, belong to your **Purpletxt** account, and be **Public**. A private repository won't work, because the app can't see its releases without logging in.

---

## Part 1: Create the repository (one time)

1. Go to <https://github.com> and sign in as **Purpletxt**.
2. Click the **+** in the top-right corner, then **New repository**.
3. Fill it in:
   - **Repository name:** `ProClicker`
   - **Description:** `Advanced auto clicker for Windows 11` (optional)
   - **Public**
   - Leave "Add a README", ".gitignore" and "license" **off**, because you already have these files.
4. Click **Create repository**.

## Part 2: Upload the code (one time)

1. On the new, empty repository page, click the **uploading an existing file** link.
2. Open your `Desktop\ProClicker` folder and drag these files into the browser:
   - `ProClicker.cs`
   - `build.bat`
   - `publish.bat`
   - `publish.ps1`
   - `logo.png`
   - `icon.ico`
   - `README.md`
   - `GITHUB-GUIDE.md`
   - `.gitignore`

   **Don't** upload any `.exe` files or the `release` folder. Those go in releases (Part 3).
3. At the bottom, type a message like `First version`, then click **Commit changes**.

## Part 3: Publish your first release (version 1.0.0)

1. In `Desktop\ProClicker`, double-click **`publish.bat`**. It builds the app and opens a `release` folder containing:
   - `ProClicker.exe`
   - `ProClicker.exe.sha256` (a fingerprint the app uses to check that downloads aren't corrupted or tampered with)
2. On your repository page, find **Releases** in the right-hand column and click **Create a new release**.
3. Click **Choose a tag**, type `v1.0.0`, then click **Create new tag: v1.0.0 on publish**.
   The tag must be `v` followed by the exact version from `ProClicker.cs`.
4. **Release title:** `ProClicker 1.0.0`
5. **Description:** what's in this version. This text appears on the update screen inside the app. You can use lists, for example:
   ```
   ## What's new
   - First public release
   ```
6. Drag **both** files from the `release` folder into the "Attach binaries" box at the bottom. Wait until both finish uploading.
7. Click **Publish release**.

Share this link with people. It always points to the newest version:
**<https://github.com/Purpletxt/ProClicker/releases/latest>**

## Part 4: Pushing an update (every time)

1. Make your changes to the app.
2. Open `ProClicker.cs`, find this line near the top, and raise the number:
   ```csharp
   public const string Version = "1.0.0";   // change to "1.0.1"
   ```
   Use `1.0.1` for fixes, `1.1.0` for new features, and `2.0.0` for big changes.
3. Double-click **`publish.bat`**.
4. Upload your changed source files: on the repository page click **Add file**, then **Upload files**, drag in the changed files (usually `ProClicker.cs`), and click **Commit changes**.
5. Create a new release as in Part 3, with tag `v1.0.1`, notes about what changed, and both files from the `release` folder attached.
6. Click **Publish release**.

The next time anyone opens ProClicker, it shows the update screen. When they click **Update now**, it downloads the new version, checks its fingerprint, swaps it in and restarts. Their settings and profiles are kept.

## Testing that updates work

1. After publishing 1.0.0, download `ProClicker.exe` from your release page and put it somewhere like `Desktop\ProClicker-test\`.
2. Publish a 1.0.1 release (Part 4).
3. Open the 1.0.0 copy. Within a few seconds it should show "Update available". Click **Update now**. It should restart as 1.0.1 and show "Updated to version 1.0.1".

You can also check by hand at any time: **Settings > Updates > Check now**.

## Good to know

- **The file names matter.** The app looks for a file called exactly `ProClicker.exe` in the release. Don't rename it when uploading.
- **SmartScreen warning:** new, unsigned apps show "Windows protected your PC". Users click **More info**, then **Run anyway**. A code-signing certificate removes this, but costs money each year.
- **Where users keep the app:** it can only update itself in a folder it's allowed to write to, such as Desktop, Documents or Downloads. If it's in `Program Files`, the update screen tells the user to move it.
- **"Skip this version"** hides that one update from automatic checks. The user can still install it with **Check now**.
- **Drafts and pre-releases** are ignored by the app. Only full published releases count.
- **Keep your GitHub account secure** (turn on two-factor authentication). Whoever controls the account controls what your users download.
