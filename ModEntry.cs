using System;

namespace SaveAnytime;

/// <summary>
/// Adds a manual save to the pause menu and configurable save/load hotkeys.
/// </summary>
public sealed class ModEntry : IMod
{
    internal const string ModId = "save_anytime";

    internal static bool Enabled { get; private set; }

    internal static ModLog Log { get; private set; }

    public void Init(ModManifest manifest)
    {
        Log = new ModLog(manifest);

        try
        {
            Settings.Initialize();
        }
        catch (Exception ex)
        {
            Log.Error("Could not initialize settings: " + ex);
        }

        try
        {
            ManualHistoryStore.Initialize();
        }
        catch (Exception ex)
        {
            Log.Warning("Could not load the manual save history map: " + ex.Message);
        }

        try
        {
            // Only check the fixed temporary slot name here. The full
            // directory sweep runs from the ScanAndRegisterOrphans prefix,
            // right before the game could import a stale temporary history
            // file as a real run, so startup does not enumerate the save
            // folder at all.
            ManualSaveController.CleanupStaleTempFile();
        }
        catch (Exception ex)
        {
            Log.Warning("Could not clean up stale temporary save files: " + ex.Message);
        }
    }

    public void OnEnable()
    {
        if (Enabled)
        {
            return;
        }

        Enabled = true;
        ModHook.OnGameLoadedLate += OnGameLoaded;

        try
        {
            Settings.RegisterGameOptions();
        }
        catch (Exception ex)
        {
            Log.Warning("Could not register the in-game keybind options: " + ex.Message);
        }

        Log.Log("Save Anytime enabled. Save: " + Settings.SaveKeyName + ", Load: " + Settings.LoadKeyName + ".");
    }

    public void OnDisable()
    {
        if (!Enabled)
        {
            return;
        }

        Enabled = false;
        ModHook.OnGameLoadedLate -= OnGameLoaded;

        try
        {
            Settings.UnregisterGameOptions();
        }
        catch (Exception ex)
        {
            Log.Warning("Could not remove the in-game keybind options: " + ex.Message);
        }

        try
        {
            PauseMenuUI.Remove();
        }
        catch (Exception ex)
        {
            Log.Warning("Could not remove the pause-menu save button: " + ex.Message);
        }

        KeybindUI.CancelAll();
        ManualSaveController.OnDisabled();
        Log.Log("Save Anytime disabled.");
    }

    private static void OnGameLoaded()
    {
        ManualSaveController.OnGameLoaded();
    }
}
