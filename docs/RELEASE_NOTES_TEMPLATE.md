## KeyTapLoop

A small Windows key presser. Repeat or hold a key or shortcut for a count or a time, and it stops the moment you switch windows.

Windows 11, 64-bit. The zip holds one `KeyTapLoop.exe` that carries its own .NET, so there's nothing to install.

### Check your download

Every release has a `.sha256` file. In PowerShell, from the folder with the zip:

```powershell
(Get-FileHash .\KeyTapLoop-win-x64.zip -Algorithm SHA256).Hash.ToLower()
```

It should match the hash in the `.sha256` file. That tells you the file is the one I published. It doesn't tell you who published it, which is what a code signature is for.

### Windows may warn you

Unless this release says it's signed, SmartScreen will probably show "Windows protected your PC" the first time. If you trust where you got it, choose **More info**, then **Run anyway**. You can also build it yourself from the source in this repo.

### Worth knowing

- If keys seem to send but nothing happens, the app you're targeting is probably running as administrator. KeyTapLoop offers a **Restart as administrator** button when it thinks so.
- Windows accepting a key doesn't mean the app reacted to it. The built-in practice pad is the only place KeyTapLoop can prove a key arrived.
- Some apps and games ignore simulated input, and many forbid automation outright. No game is claimed to work.
- No accounts, no telemetry, no network calls.
