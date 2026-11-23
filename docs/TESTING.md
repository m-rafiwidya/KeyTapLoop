# Manual Windows test checklist

The automated tests (`dotnet test`) cover the run logic with a fake keyboard and a virtual clock. They never send real keys. Everything below needs a real Windows 11 x64 machine, because it exercises `SendInput`, global shortcuts, focus tracking and the UI.

Record what you actually observe. A call to `SendInput` that succeeds is **not** a pass on its own: for every item that involves another app, look at that app and confirm the keys visibly appeared or took effect.

Use Notepad, or any plain text box, as the target unless noted. Record each result as `Pass`, `Fail` or `Not run`, plus a note.

| # | Check | Result | Notes |
| --- | --- | --- | --- |
| 1a | Notepad target, key `F`, count `5`, 200 ms period. Exactly five `f` characters appear in Notepad. | | |
| 1b | Same run on the practice pad. The pad shows `5 tap cycles`, and the result card says `Practice pad saw 5 of 5`. | | |
| 1c | Each tap is visibly separate: five characters appear one at a time, about 200 ms apart, not all at once. | | |
| 2a | Minimise KeyTapLoop. Click into Notepad, press `Ctrl + F1`. The run starts and Notepad stays focused. | | |
| 2b | Press `Ctrl + F2` during the start-delay countdown. Nothing is sent. Result: `Stopped by you`. | | |
| 2c | Press `Ctrl + F2` during playback. Typing stops at once. Result: `Stopped by you`. | | |
| 2d | Press `Ctrl + F1` twice quickly. Only one run happens, and no doubled characters appear. | | |
| 3a | Long run (60 s) in Notepad. `Alt + Tab` to a second editor. No further characters appear in the second window. Result: `Stopped because focus changed`. | | |
| 3b | Alt+Tab back to Notepad. No characters resume. | | |
| 3c | Repeat with a quick switch away and straight back. The run still ends. | | |
| 3d | Repeat in Hold mode, switching away while the key is held. The key is released (type normally afterwards to confirm). | | |
| 4a | Time limit only (5 s, count off). The run ends at about 5 s and no taps start after the limit. | | |
| 4b | Count limit only. The run ends right after the last tap. | | |
| 4c | Both set (count `100`, time `3` s). The time limit ends the run first. Then count `3`, time `60` s: the count ends it first. | | |
| 4d | Freeze the machine briefly mid-run (for example, drag the Notepad window around or briefly load the CPU). No burst of catch-up characters follows. | | |
| 5a | Hold `F` for 10 s, press `Ctrl + F2` partway. Typing works normally afterwards, with no stuck key. | | |
| 5b | Hold the chord `Ctrl + Shift + F`, then stop. Ctrl and Shift are not stuck. Click in Notepad and type normally. | | |
| 5c | Close the target mid-run. The result is `Target closed`, no key is stuck, and the app asks for a new target. | | |
| 5d | Physically hold `Shift`, then start a run whose chord includes `Shift`. The run refuses to start and says a key is already held. KeyTapLoop never releases your Shift. | | |
| 6a | Clear the period field, type `abc`, and set period lower than key-down. Each shows an error next to its control, and Start does nothing. | | |
| 6b | Set the Start shortcut to something another app owns. The conflict is reported and the old shortcut stays active. | | |
| 6c | Try `Alt + Tab`, `Alt + F4` and `Ctrl + F1` as the output key. Each is rejected with a reason. | | |
| 6d | Close the target, then press Start. It asks for a new target. | | |
| 6e | Corrupt `%LOCALAPPDATA%\KeyTapLoop\settings.json` (for example, put `{ not json` in it) and launch. The app opens with defaults and says so. | | |
| 6f | Relaunch after a normal session. The app opens idle, with no window chosen and nothing running. | | |
| 7a | On a clean Windows 11 x64 profile or machine with no .NET installed, unzip and run `KeyTapLoop.exe`. It starts. | | |
| 7b | Repeat the five-press practice pad test. | | |

## Added after the first Windows trial

| # | Check | Result | Notes |
| --- | --- | --- | --- |
| 8a | Turn off both limits in Repeat mode. The hint says it runs until you stop it. Start, then press the Stop shortcut. It stops with `Stopped by you`. | | |
| 8b | Hold mode with no time limit. The key stays down until Stop, then is released (type normally afterwards). | | |
| 9a | Set the start delay to `0`, click into Notepad, press `Ctrl + F1`. It starts at once, with no countdown. | | |
| 9b | Set the start delay to `3`, press `Ctrl + F1` from Notepad. It still starts at once. The delay only applies to the Start button. | | |
| 10a | Choose **Any window**. Click into Notepad, press `Ctrl + F1`. Characters appear. Alt+Tab to another editor: characters continue there, and the run does not end. | | |
| 10b | Any window, Start button with delay `0`. It refuses, saying KeyTapLoop is in front. It never types into KeyTapLoop itself. | | |
| 10c | Any window: click KeyTapLoop during a run. The run ends. | | |
| 11a | Choose an app running as administrator as the target. The warning and **Restart as administrator** button appear. | | |
| 11b | Click **Restart as administrator** and accept the prompt. The new window’s title says (administrator) and the old one closes. Choose the same app again: a run now visibly types into it. | | |
| 11c | Decline the prompt. KeyTapLoop stays as it was and says so. | | |
| 12a | With **Send as hardware scan codes** on, the Notepad five-tap test still gives exactly five `f`. | | |
| 12b | With it off, the same test still gives exactly five `f`. | | |
| 12c | Arrow keys and Space in scan-code mode behave correctly in Notepad. | | |

## Added with the toggle shortcut, sounds and themes

| # | Check | Result | Notes |
| --- | --- | --- | --- |
| 13a | Click into Notepad, press `Ctrl + F3`. A run starts at once. Press `Ctrl + F3` again. It stops with `Stopped by you`. | | |
| 13b | Press `Ctrl + F3` during the Start button’s countdown. The countdown is cancelled and nothing is sent. | | |
| 13c | Open **Shortcuts…**. All three can be changed. Setting two to the same keys is refused with a message. | | |
| 13d | Make the output key `Ctrl + F3`. It is refused as one of KeyTapLoop’s own shortcuts. | | |
| 14a | With **Sounds** on, a rising tone plays when a run starts, and a falling tone when it completes or you stop it. | | |
| 14b | Alt+Tab away mid-run in chosen-window mode. A low double beep plays and the result says focus changed. | | |
| 14c | Turn **Sounds** off. Nothing plays. Turning it on plays a short preview. | | |
| 14d | The tones are audible with a full-screen game in front, and are not harsh at normal volume. | | |
| 15a | Set Windows to dark mode, start KeyTapLoop with Theme on **Auto**. It opens dark, with a dark title bar, and nothing is unreadable. | | |
| 15b | With the app open, switch Windows between light and dark. KeyTapLoop follows within a moment. | | |
| 15c | Click **Theme** to cycle Auto → Light → Dark → Auto. Every control recolours at once, including the practice pad if open. | | |
| 15d | In both themes check: toggles, the key list (open it), checkboxes (ticked and unticked), disabled fields, error text, the result card, and the Shortcuts window. Note anything faint, clipped or wrong. | | |
| 15e | The practice pad’s text area and key log have dark scrollbars in dark mode. | | |
| 16a | The key list opens on click, Space, Enter or Down. Up and Down step through keys. Typing a letter or digit jumps to it. Escape closes the list. | | |
| 16b | Operate the whole window with the keyboard only. Focus is always visible and the tab order is sensible. | | |

## Also worth checking

- **Tab and Escape as output.** Pick them from the key list (not Capture) and run five taps into the practice pad. The pad shows a tab and an Escape event.
- **Keyboard-only use.** Tab through every control in a sensible order, operate the toggles with Space, and use Capture with `Escape` to cancel and `Tab` to move on.
- **Elevated target.** Choose an app running as administrator. KeyTapLoop warns that Windows may ignore input to it.
- **Display scaling.** Check the window at 100%, 125% and 150% scaling for clipped text.
- **Shutdown.** Close KeyTapLoop mid-hold. The held key is released.

## Known limits

- A focus change between the last foreground check and Windows delivering a key can't be ruled out. The checks narrow the gap but don't close it.
- A forced process kill or a system shutdown can't guarantee a held key is released.
- Clicking the on-screen Stop button reports `Stopped because focus changed`, because clicking the window moves focus. Use the Stop shortcut for `Stopped by you`.
