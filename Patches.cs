using System;
using System.Collections.Generic;
using System.IO;
using HarmonyLib;
using UnityEngine;

namespace SaveAnytime;

/// <summary>
/// Keeps the newest manual save at the top of the earlier-day list, right
/// below the run's main latest-save row.
/// </summary>
[HarmonyPatch(typeof(SaveFiles), "LoadHistory", new Type[] { typeof(int) })]
internal static class SaveFilesLoadHistoryPatch
{
    [HarmonyPostfix]
    internal static void Postfix(int slotId, List<SaveHistoryEntry> __result)
    {
        if (!ModEntry.Enabled || __result == null || __result.Count == 0)
        {
            return;
        }

        try
        {
            string pinned = ManualHistoryStore.Get(slotId);
            if (string.IsNullOrEmpty(pinned))
            {
                return;
            }

            int index = __result.FindIndex(e => e != null && e.file == pinned);
            if (index < 0)
            {
                // Keep the pin if the file is still a valid history file for
                // this slot. The game's history index may simply be out of
                // date; the quick load can still use the file.
                if (ManualSaveController.IsHistoryFileForSlot(pinned, slotId) &&
                    File.Exists(SaveFiles.AbsolutePath(pinned)))
                {
                    return;
                }

                ManualHistoryStore.Remove(slotId);
                return;
            }

            if (index == 0)
            {
                return;
            }

            SaveHistoryEntry entry = __result[index];
            __result.RemoveAt(index);
            __result.Insert(0, entry);
        }
        catch (Exception ex)
        {
            if (ModEntry.Log != null)
            {
                ModEntry.Log.Warning("Could not pin the manual save: " + ex.Message);
            }
        }
    }
}

/// <summary>
/// Drops the manual-save pin before the slot's history is trimmed, and deletes
/// the manual file if it was not part of the game's history index.
/// </summary>
[HarmonyPatch(typeof(SaveFiles), "DeleteSlot", new Type[] { typeof(int) })]
internal static class SaveFilesDeleteSlotPatch
{
    [HarmonyPrefix]
    internal static void Prefix(int slotId, out string __state)
    {
        __state = null;

        try
        {
            __state = ManualHistoryStore.Get(slotId);
            ManualHistoryStore.Remove(slotId);
        }
        catch (Exception ex)
        {
            if (ModEntry.Log != null)
            {
                ModEntry.Log.Warning("Could not clear the manual save pin: " + ex.Message);
            }
        }
    }

    [HarmonyPostfix]
    internal static void Postfix(int slotId, string __state)
    {
        if (string.IsNullOrEmpty(__state))
        {
            return;
        }

        try
        {
            // If another prefix skipped the original DeleteSlot, the slot file
            // is still present and the manual save must stay untouched.
            if (File.Exists(SaveFiles.AbsolutePath(SaveFiles.Slot(slotId))))
            {
                return;
            }

            string path = SaveFiles.AbsolutePath(__state);
            if (File.Exists(path))
            {
                SaveFiles.WithIORetry(delegate { File.Delete(path); });
            }
        }
        catch (Exception ex)
        {
            if (ModEntry.Log != null)
            {
                ModEntry.Log.Warning("Could not delete the manual save file: " + ex.Message);
            }
        }
    }
}

/// <summary>
/// Keeps the pinned manual save when the game trims old history entries.
/// The manual save is kept in addition to the game's normal history.
/// </summary>
[HarmonyPatch(typeof(SaveFiles), "TrimHistory", new Type[]
{
    typeof(List<SaveHistoryEntry>),
    typeof(int)
})]
internal static class SaveFilesTrimHistoryPatch
{
    [HarmonyPrefix]
    internal static bool Prefix(List<SaveHistoryEntry> history, int keepCount)
    {
        if (!ModEntry.Enabled || history == null || history.Count == 0)
        {
            return true;
        }

        SaveHistoryEntry pinned = null;
        for (int i = history.Count - 1; i >= 0; i--)
        {
            SaveHistoryEntry candidate = history[i];
            if (candidate != null && ManualHistoryStore.IsPinned(candidate.file))
            {
                if (pinned == null)
                {
                    pinned = candidate;
                }

                history.RemoveAt(i);
            }
        }

        if (pinned == null)
        {
            return true;
        }

        int otherKeepCount = Mathf.Max(0, keepCount);
        while (history.Count > otherKeepCount)
        {
            SaveHistoryEntry last = history[history.Count - 1];
            history.RemoveAt(history.Count - 1);
            DeleteHistoryFile(last);
        }

        history.Insert(0, pinned);
        return false;
    }

    private static void DeleteHistoryFile(SaveHistoryEntry entry)
    {
        if (entry == null || string.IsNullOrEmpty(entry.file))
        {
            return;
        }

        try
        {
            string path = SaveFiles.AbsolutePath(entry.file);
            if (File.Exists(path))
            {
                SaveFiles.WithIORetry(delegate { File.Delete(path); });
            }
        }
        catch (Exception ex)
        {
            if (ModEntry.Log != null)
            {
                ModEntry.Log.Warning("Could not delete an old history file: " + ex.Message);
            }
        }
    }
}

/// <summary>
/// Drops the pin if the game restores the manual history file as the latest save.
/// </summary>
/// <summary>
/// Removes an orphaned temporary slot file before the game scans the save
/// folder, so it can never be imported as a real run.
/// </summary>
[HarmonyPatch(typeof(SaveFiles), "ScanAndRegisterOrphans", new Type[] { })]
internal static class SaveFilesScanAndRegisterOrphansPatch
{
    [HarmonyPrefix]
    internal static void Prefix()
    {
        ManualSaveController.CleanupStaleTempFile();
    }
}

[HarmonyPatch(typeof(SaveFiles), "RestoreFromHistory", new Type[] { typeof(int), typeof(string) })]
internal static class SaveFilesRestoreFromHistoryPatch
{
    [HarmonyPostfix]
    internal static void Postfix(int slotId, string historyName)
    {
        try
        {
            ManualHistoryStore.RemoveIfFile(slotId, historyName);
        }
        catch (Exception ex)
        {
            if (ModEntry.Log != null)
            {
                ModEntry.Log.Warning("Could not clear the restored manual save pin: " + ex.Message);
            }
        }
    }
}

/// <summary>
/// Colours the manual save's history row in the vanilla load menu.
/// </summary>
[HarmonyPatch(typeof(LoadGameUI), "BuildExpanded")]
internal static class LoadGameUIBuildExpandedPatch
{
    [HarmonyPostfix]
    internal static void Postfix(object[] __args)
    {
        if (!ModEntry.Enabled || __args == null || __args.Length < 2)
        {
            return;
        }

        try
        {
            List<SaveHistoryEntry> history = __args[1] as List<SaveHistoryEntry>;
            if (history == null || history.Count == 0)
            {
                return;
            }

            SaveHistoryEntry entry = history[0];
            if (entry == null || !ManualHistoryStore.IsPinned(entry.file))
            {
                return;
            }

            CustomUIManager customUI = CustomUIManager.Instance;
            CustomUIWindow window = customUI != null ? customUI.GetWindow(LoadGameUI.WINDOW_ID) : null;
            if (window == null || window.root == null)
            {
                return;
            }

            ManualSaveRowStyle.Apply(window.root.transform, entry);
        }
        catch (Exception ex)
        {
            if (ModEntry.Log != null)
            {
                ModEntry.Log.Warning("Could not colour the manual save row: " + ex.Message);
            }
        }
    }
}

/// <summary>
/// Adds the Save button to the pause menu when it opens.
/// </summary>
[HarmonyPatch(typeof(EscapeUIManager), "OpenEscapeMenu", new Type[] { })]
internal static class EscapeUIManagerOpenPatch
{
    [HarmonyPostfix]
    internal static void Postfix(EscapeUIManager __instance)
    {
        if (!ModEntry.Enabled || !Settings.Enabled)
        {
            return;
        }

        PauseMenuUI.Ensure(__instance);
    }
}

/// <summary>
/// Handles the configured save/load keys after the game's own input pass.
/// </summary>
[HarmonyPatch(typeof(StoreUIManager), "Update", new Type[] { })]
internal static class StoreUIManagerUpdateHotkeysPatch
{
    [HarmonyPostfix]
    internal static void Postfix(StoreUIManager __instance)
    {
        if (!ModEntry.Enabled || !Settings.Enabled || __instance == null)
        {
            return;
        }

        if (KeybindUI.AnyCapturing || KeyBindHelper.SuppressHotkeys)
        {
            return;
        }

        try
        {
            if (__instance.IsTyping())
            {
                return;
            }

            if (NewGameData.Instance == null ||
                NewGameData.Instance.isShowcase ||
                NewGameData.Instance.isInMainMenu)
            {
                return;
            }

            if (__instance.gameOptionUIManager != null && __instance.gameOptionUIManager.IsOpen)
            {
                return;
            }

            if (RunSummaryUI.IsOpen || WildNetworkUI.IsOpen)
            {
                return;
            }

            if (EndOfDayUIManager.Instance != null && EndOfDayUIManager.Instance.IsOpen)
            {
                return;
            }

            CustomUIManager customUI = CustomUIManager.Instance;
            if (customUI != null)
            {
                if (customUI.IsOpen("mod_config:" + ModEntry.ModId) || customUI.HasEscapeTarget())
                {
                    return;
                }
            }

            KeyCode saveKey = Settings.SaveKey;
            KeyCode loadKey = Settings.LoadKey;

            if (saveKey == loadKey && saveKey != KeyCode.None)
            {
                if (Settings.IsKeyboardKey(saveKey) && Input.GetKeyDown(saveKey))
                {
                    PauseMenuUI.Notify("Save and Load are bound to the same key.", "sec");
                }

                return;
            }

            if (Settings.IsKeyboardKey(saveKey) && Input.GetKeyDown(saveKey))
            {
                bool saved = ManualSaveController.SaveManual(out string message);
                PauseMenuUI.ShowSaveFeedback(saved);
                PauseMenuUI.Notify(message, saved ? "default" : "sec");
                return;
            }

            if (Settings.IsKeyboardKey(loadKey) && ManualSaveController.ShouldTriggerLoad(loadKey))
            {
                bool loaded = ManualSaveController.LoadManual(out string message);
                if (!loaded)
                {
                    ManualSaveController.ResetLoadKeyLatch();
                }

                PauseMenuUI.Notify(message, loaded ? "default" : "sec");
            }
        }
        catch (Exception ex)
        {
            if (ModEntry.Log != null)
            {
                ModEntry.Log.Warning("Hotkey handling failed: " + ex.Message);
            }
        }
    }
}

/// <summary>
/// Stops the game's input pass while the mod settings window is capturing a key.
/// </summary>
[HarmonyPatch(typeof(StoreUIManager), "Update", new Type[] { })]
internal static class StoreUIManagerUpdateCapturePatch
{
    [HarmonyPrefix]
    internal static bool Prefix()
    {
        if (!ModEntry.Enabled)
        {
            return true;
        }

        return !KeybindUI.AnyCapturing;
    }
}

/// <summary>
/// Stops custom windows from consuming Escape while a mod keybind is being
/// captured. This matters most in the main menu, where the game's input pass
/// is not StoreUIManager.Update.
/// </summary>
[HarmonyPatch(typeof(CustomUIManager), "HandleEscape", new Type[] { })]
internal static class CustomUIManagerHandleEscapePatch
{
    [HarmonyPrefix]
    internal static bool Prefix()
    {
        if (!ModEntry.Enabled)
        {
            return true;
        }

        return !KeybindUI.AnyCapturing;
    }
}

/// <summary>
/// Replaces the stock text field for our keybind settings with a capture button.
/// </summary>
[HarmonyPatch(typeof(ModConfigWindow), "BuildEntry", new Type[]
{
    typeof(CustomUIBuilder),
    typeof(ModConfig),
    typeof(ConfigEntry)
})]
internal static class ModConfigWindowBuildEntryPatch
{
    [HarmonyPrefix]
    internal static bool Prefix(CustomUIBuilder b, ModConfig config, ConfigEntry entry)
    {
        if (config == null || entry == null || config.ModId != ModEntry.ModId)
        {
            return true;
        }

        if (entry.Key != "save_key" && entry.Key != "load_key")
        {
            return true;
        }

        if (!(entry is ConfigEntry<string>))
        {
            return true;
        }

        KeybindUI.Build(b, config, entry);
        return false;
    }
}

/// <summary>
/// Refreshes the visible keybind buttons after the mod settings are applied.
/// </summary>
[HarmonyPatch(typeof(ModConfigWindow), "Apply", new Type[]
{
    typeof(CustomUIWindow),
    typeof(ModConfig)
})]
internal static class ModConfigWindowApplyPatch
{
    [HarmonyPostfix]
    internal static void Postfix(CustomUIWindow window, ModConfig config)
    {
        if (config == null || config.ModId != ModEntry.ModId)
        {
            return;
        }

        KeybindUI.RefreshLabelsFromEntries();
    }
}

/// <summary>
/// Refreshes the visible keybind buttons after the mod settings are reset.
/// </summary>
[HarmonyPatch(typeof(ModConfigWindow), "ResetWidgets", new Type[]
{
    typeof(CustomUIWindow),
    typeof(ModConfig)
})]
internal static class ModConfigWindowResetPatch
{
    [HarmonyPostfix]
    internal static void Postfix(CustomUIWindow window, ModConfig config)
    {
        if (config == null || config.ModId != ModEntry.ModId)
        {
            return;
        }

        KeybindUI.RefreshLabelsFromWidgets();
    }
}
