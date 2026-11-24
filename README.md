<p align="center"><img src="docs/logo.png" alt="KeyTapLoop logo: a keycap inside a loop arrow" width="112"></p>

# KeyTapLoop — Free Auto Key Presser for Windows

KeyTapLoop repeats or holds one key, or one shortcut. By default it is tied to a window you choose, runs for a count or a time you set (or until you stop it), shows what happened, and ends the moment you switch to another window.

It is a small, portable Windows app. No installer, no account, no network access, no telemetry.

## What it does

- **Repeat or hold.** Tap a key or shortcut again and again, or hold it down for a set time.
- **Count and time limits.** Stop after a number of taps, after a number of seconds, or whichever comes first. Leave both off and it runs until you press Stop.
- **One chosen window (the default).** Pick a target and the run is bound to that window and its process. Switching tabs inside the window is fine. Switching to a different window ends the run for good, even if you switch straight back. Starting again is always a deliberate click.
- **Or any window.** Choose **Any window** and keys go to whichever window is in front, with no focus check. Switching windows won’t end the run, so use Stop or a limit. KeyTapLoop never types into its own window.
- **Global shortcuts.** `Ctrl + F1` starts, `Ctrl + F2` stops and `Ctrl + F3` toggles (starts a run, or stops the one that is running), from anywhere. You can change all three under **Settings**. Shortcuts begin immediately, with no delay.
- **Sound cues.** A short rising tone when a run starts, a falling tone when it finishes or you stop it, and a low double beep if something else ended it (focus moved, the target closed, input failed). Turn them off in **Settings**.
- **Light and dark.** The window follows your Windows light or dark setting by default. Pick Auto, Light or Dark in **Settings**.
- **Clear results.** Every run ends with one of `Completed`, `Stopped by you`, `Stopped because focus changed`, `Target closed` or `Couldn’t send input`, with the elapsed time and the number of complete taps Windows accepted.
- **A practice pad.** A built-in window that shows the key events it actually receives, so you can see for yourself that a key arrived.

Nothing runs on launch. KeyTapLoop always opens idle and remembers only your key and timing preferences, never a window.

## Download and run

1. Download `KeyTapLoop-v0.1.0-win-x64.zip` from the [Releases](../../releases) page and unzip it anywhere.
2. Optional: check the download against the published hash. In PowerShell:
   ```powershell
   (Get-FileHash .\KeyTapLoop-v0.1.0-win-x64.zip -Algorithm SHA256).Hash.ToLower()
   ```
   The result should match the `.sha256` file on the release page. A hash confirms you got the file the release published. It is not a publisher signature.
3. Run `KeyTapLoop.exe`. It is self-contained, so .NET does not need to be installed.

The executable is not code-signed yet, so Windows SmartScreen may show a warning the first time. Choose **More info**, then **Run anyway**, if you trust the source. You can also build it yourself from this repository. Signing is planned: see [docs/CODE_SIGNING.md](docs/CODE_SIGNING.md). Even once signed, a new app can show the warning for a while until Windows builds a reputation for it.

Requires Windows 11 x64.

## First run

1. Start the app and click **Use practice pad**. A second window opens.
2. Leave the key as `F` and the defaults as they are: 200 ms between taps, 40 ms key-down, stop after 20 taps. Set the count to `5`.
3. Click **Start**. A three-second countdown runs while the pad comes to the front. Five `f` characters appear in the pad, and the pad reports `5 tap cycles`.
4. To use a real app, open it, click **Choose window**, then switch to that app. KeyTapLoop takes whichever window is in front when its three-second countdown ends. Nothing is sent during selection.
5. Either press **Start** and switch to the target during the start delay, or click into the target and press `Ctrl + F1`, which starts at once. With a chosen window, `Ctrl + F1` only starts a run when that window is already in front. The start delay applies to the Start button only, and can be `0` if you always use the shortcut.

### Default shortcuts

| Action | Shortcut |
| --- | --- |
| Start | `Ctrl + F1` |
| Stop | `Ctrl + F2` |
| Toggle (start or stop) | `Ctrl + F3` |

Change them under **Settings…**. Each needs Ctrl, Alt or Shift, and all three must differ. A shortcut that another app already owns is reported, and your previous ones stay active. The key you send can’t be one of these shortcuts.

## How a run behaves

- **Period** is start to start, so a 200 ms period with a 40 ms key-down time is a tap every 200 ms. The period must be longer than the key-down time.
- **Stop limits.** With both a count and a time set, whichever is reached first ends the run. With neither, it runs until you press Stop, or until focus leaves the chosen window.
- **No catch-up bursts.** If the computer stalls, missed taps are skipped rather than fired together.
- **Focus (chosen window).** Right before every new key-down, KeyTapLoop checks that the chosen window still exists and is in front. Windows also reports focus changes, and any change ends the run. There is a short gap between a check and Windows delivering the key, so this narrows the chance of a stray key rather than ruling it out.
- **Cleanup.** If a run ends while a key is down, KeyTapLoop releases the keys it pressed, last pressed first. If Windows refuses a release, the result says so. It never adopts a key you were already holding. If one of the keys in your shortcut is held when you press Start, the run refuses to begin.
- **Start shortcut released first.** When you start with `Ctrl + F1`, the first key is held back until you let go of the shortcut. If it stays held for 1.5 seconds, the run fails with a clear message.
- **Stop with the mouse (chosen window).** Clicking the app’s own Stop button moves focus to KeyTapLoop, so that run reports `Stopped because focus changed`. Use `Ctrl + F2` for `Stopped by you`.

## Limits worth knowing

KeyTapLoop sends ordinary simulated keyboard input with the documented Windows `SendInput` call, with normal user permissions.

- **“Accepted” is not “received”.** Windows accepting a key event only means it was queued. The count KeyTapLoop shows is complete taps Windows accepted. Whether the target reacted is up to the target, so always look at the window. Only the practice pad can confirm a key arrived.
- **Some apps ignore simulated input.** That includes some games, anti-cheat systems and apps that read hardware input directly. There is no claim of support for any particular game.
- **Administrator windows.** Windows silently drops input sent to a window running as administrator, and it reports success anyway. This is the most common reason keys “send” with no effect. KeyTapLoop warns when a chosen window probably runs as administrator and offers **Restart as administrator**, after which the keys can get through. Only do that for software you trust.
- **Scan codes.** Games that read the keyboard directly often ignore ordinary key events. By default KeyTapLoop sends hardware scan codes, which suits them. If an ordinary app ever misbehaves, turn **Send hardware scan codes** off in **Settings**.
- **Foreground only.** Keys go to whatever has focus. There is no background delivery.
- **Check the rules.** Many online games and services forbid automated input, use anti-cheat that watches for it, and ban accounts for it. KeyTapLoop makes no claim of support for any game. Use it only where automation is allowed, and not on an account you can’t afford to lose.

## Privacy

No accounts, no telemetry, no network calls. Settings live in `%LOCALAPPDATA%\KeyTapLoop\settings.json` and hold your key, timing and shortcuts. Window handles and running state are never saved. Delete the file to reset.

## Build from source

Requires the [.NET 10 SDK](https://dotnet.microsoft.com/download). The code builds on Windows, and the logic tests also run on Linux and macOS.

```bash
dotnet build -c Release
```

```bash
dotnet test
```

Publish the portable build and its hash on Windows:

```powershell
./scripts/publish.ps1
```

That writes `artifacts/KeyTapLoop-v0.1.0-win-x64.zip` and a matching `.sha256` file. The raw command is:

```bash
dotnet publish src/KeyTapLoop -c Release -r win-x64 --self-contained -o artifacts/KeyTapLoop
```

### What is tested

- **Automated** (`dotnet test`): run limits, timing, skipped slots, cancellation, key-release cleanup, focus-change handling, input validation and settings recovery. These use a virtual clock and a fake keyboard, so they never send real keys.
- **Manual on Windows:** the real `SendInput`, global shortcuts, focus tracking and the UI. The step-by-step checklist is in [docs/TESTING.md](docs/TESTING.md).

## Demo script (about 20 seconds)

1. Open KeyTapLoop. Click **Use practice pad**, set the count to `5`, and click **Start**. Five `f` characters appear and the pad shows `5 tap cycles`.
2. Set a time limit of `30` seconds and turn off the count. Click **Start**, then press `Alt + Tab` to another window about two seconds in.
3. The run ends with `Stopped because focus changed`. Switch back: nothing resumes.

## Possible later work

Not in v0.1, recorded only as ideas:

- A minimal two- or three-step `Press` / `Wait` sequence editor, if people ask for it.
- Anything larger, such as a recorder, mouse automation, several jobs at once or an updater, only after v0.1 proves useful.

## License

MIT. See [LICENSE](LICENSE).
