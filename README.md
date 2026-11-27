# Save Anytime

A **Probably Stolen** mod that adds manual saving and quick loading to a run.

## Features

- Adds **Save Game** and **Load Quicksave** buttons to the pause menu.
- Press **F5** to save during the morning, before opening the shop.
- Press **F9** to load the manual save.
- Saving is deliberately limited to the morning. The game does not include the
  live shop, current customer, counter items, or negotiation state in its save
  files, so a save made after opening the shutter cannot be restored correctly.
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

## Why saves are morning-only

The game's save file stores the player store, the station, and the inventory
contents, but it does not store the live shop session. The current customer,
the customer queue, items on the front counter, items in negotiation windows,
open dialogs, the open shutter, and the scene setup for an open shop are all
left out or explicitly marked non-serializable.

The game itself only autosaves in the morning, after the night ends and before
the shop opens. Loading a save made while the shop was open leaves the scene in
a contradictory state: the save says the shop is open, but the reloaded scene
starts closed, with no customer queue and missing counter items. That is why
items can be misplaced and machines or fixtures can stop responding.

Because restoring the exact customer, dialog, negotiation and open-shop state
would require reimplementing a large part of the game's runtime state, the mod
only allows a save when the shop is in its morning state. Existing quicksaves
created by older versions while the shop was open are refused by the mod's load
button; make a new quicksave in the morning instead.

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

- In a run, during the morning before opening the shop, press **F5** or use
  **pause menu -> Save Game**.
- If the shop is already open, the mod refuses to save and tells you to save
  the next morning. The pause-menu button shows `Morning only` in that case.
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
