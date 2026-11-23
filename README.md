# Save Anytime

A **Probably Stolen** mod that adds manual saving and quick loading to a run.

## Features

- Adds a **Save Game** button to the pause menu.
- Press **F5** to save at any time during a run.
- Press **F6** to load the manual save.
- Each run keeps exactly one manual save. A new manual save replaces the
  previous one.
- The manual save appears as the first entry in the run's earlier-save list,
  directly below the main latest-save row in the load menu.
- Both keys can be changed in the mod's settings or in the game's keybind
  options.

## How it works

The mod asks the game to write a normal save into a temporary slot, moves that
file to a valid earlier-day history filename, and removes the temporary slot's
preview and index entries. The manual save is a regular game save, so the
normal load menu can open it too.

The mod keeps a small map of the manual save for each slot in
`ModData\save_anytime\manual_history.txt`. A Harmony postfix pins that entry to
the top of the history list when the load menu reads it, and the entry is
protected when the game trims old history saves. When a new manual save is
made, the previous manual save is removed from the history list and deleted.
The save also records its own filename in the run's mod data, so the in-game
quick load can recover the entry if the map is lost.

## Build and install

From this folder:

```powershell
.\build.ps1 -GameDir "E:\SteamLibrary\steamapps\common\Probably Stolen" -Install
```

Then enable and trust **Save Anytime** in the game's **Mods** screen, and
restart the game. Pass `-GameDir` if the game is installed elsewhere.

## Notes

- Save and quick load are only active in a live shop run. The main menu and
  showcase mode do not expose the mod's save button or the F5/F6 actions.
- The mod needs the `files` capability because it moves save files.
- Loading a manual save reloads the `InventoryScene`, which avoids duplicating
  items that are already present in the live scene.
- If no manual save exists for the current run, **F6** loads the run's latest
  regular save instead.
