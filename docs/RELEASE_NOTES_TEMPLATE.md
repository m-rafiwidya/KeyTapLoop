## KeyTapLoop

Free Auto Key Presser for Windows. Repeat or hold one key or shortcut in a chosen window, with count and time limits, global shortcuts, and a run that ends when focus moves.

**Requires Windows 11 x64.** The zip contains a self-contained `KeyTapLoop.exe`, so .NET does not need to be installed.

### Verify your download

Each release includes a `.sha256` file. In PowerShell, from the folder with the zip:

```powershell
(Get-FileHash .\KeyTapLoop-win-x64.zip -Algorithm SHA256).Hash.ToLower()
```

The result should match the hash in the `.sha256` file. A hash shows the file matches the release. It is not a publisher signature.

### Windows may show a warning

The executable is not code-signed, so Windows SmartScreen can show "Windows protected your PC" the first time you run it. If you trust this source, choose **More info**, then **Run anyway**. You can also build it yourself from the source in this repository.

### Good to know

- Windows accepting a key event does not prove the target app reacted. Use the built-in practice pad to confirm delivery, and check the target window.
- Some apps ignore simulated input, and windows running as administrator can block it. No claim is made for any particular game.
- No accounts, no telemetry, no network calls.
