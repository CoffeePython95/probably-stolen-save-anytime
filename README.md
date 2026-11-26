# Save Anytime

A **Probably Stolen** mod that adds manual saving and quick loading to a run.

## Features

- Adds **Save Game** and **Load Quicksave** buttons to the pause menu.
- Press **F5** to save at any time during a run.
- Press **F9** to load the manual save.
- Each run keeps exactly one manual save. A new manual save replaces the
  previous one.
- The manual save appears as the first entry in the run's earlier-save list,
  directly below the main latest-save row in the load menu.
- The manual save row is highlighted in a contrasting gold colour and labelled
  `[Quicksave]`.
- Both keys can be changed in the mod's settings or in the game's keybind
  options.
- F9 is debounced: repeated presses cannot queue another reload while a load
  is in progress or while the new scene is settling.
- Save and quick load are only active in a live shop run. The main menu and
  showcase mode do not expose the mod's save button or the F5/F9 actions.

## Install (players)

No build tools are needed.

1. Go to the [latest release](../../releases/latest).
2. Download `SaveAnytime-v<version>.zip`.
3. Extract the `SaveAnytime` folder from the zip into:

   ```text
   <Steam library>\steamapps\common\Probably Stolen\Mods\
   ```

   The result should look like:

   ```text
   Probably Stolen\Mods\SaveAnytime\manifest.xml
   Probably Stolen\Mods\SaveAnytime\SaveAnytime.dll
   ```

4. Start the game, open **Mods**, trust/enable **Save Anytime**, and restart.
   The mod requests the `files` capability because it moves save files.

The mod targets **Probably Stolen 0.46D**.

## Usage

- In a run, press **F5** or use **pause menu -> Save Game**.
- Press **F9** or use **pause menu -> Load Quicksave** to load the manual save.
- The pause-menu **Load Quicksave** button only loads an existing manual save.
  If no manual save exists, it reports that and does nothing. F9 keeps the
  convenience fallback and loads the run's latest regular save when no manual
  save exists.
- After a load, wait for the cooldown message before pressing F9 again. This
  prevents accidental repeated scene reloads on slower saves.
- Change the keys in the mod's **Mods** settings or in
  **Options -> Keybinds**.

## Where saves go

The manual save is a normal history save in the game's save folder:

```text
%USERPROFILE%\AppData\LocalLow\Questing Goose Studio\Probably Stolen\
  save_<slot>_day<day>_<yyyyMMdd-HHmmss>.es3
```

The mod's per-slot map is:

```text
...\ModData\save_anytime\manual_history.txt
```

- The run's normal `save_<slot>.es3` and every other playthrough's saves are
  left untouched.
- A new manual save is written and validated first. Only then is the previous
  manual save for that slot removed.
- The manual save is a real history save, so the game's normal load menu can
  load it too.
- The game's normal history trimming is preserved; the manual save is kept in
  addition to the configured history entries.

## Build from source

Requirements:

- Probably Stolen installed locally.
- .NET 8 SDK.

From this folder:

```powershell
.\build.ps1 -GameDir "E:\SteamLibrary\steamapps\common\Probably Stolen"
```

This produces:

```text
dist\SaveAnytime\                  unpacked mod folder
dist\SaveAnytime-v<version>.zip    release archive
```

To build, package and install directly into the local game:

```powershell
.\build.ps1 -GameDir "E:\SteamLibrary\steamapps\common\Probably Stolen" -Install
```

Then enable/trust the mod in the game's **Mods** screen and restart. Pass
`-GameDir` if the game is installed elsewhere.

The build uses the game's own `Assembly-CSharp.dll`, so it cannot be compiled
on a machine without the game installed.

## License

[MIT](LICENSE)
