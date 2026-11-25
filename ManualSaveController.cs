using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace SaveAnytime;

internal static class ManualSaveController
{
    // A slot id that the game never creates. SaveGame writes its normal slot
    // file and preview; we move the slot file to a history filename and remove
    // every other trace of the temporary slot afterwards.
    private const int TempSlotId = -424242;

    private const string ModDataFileKey = "manual_save_file";
    private const string ModDataSlotKey = "manual_save_slot";
    private const string HistoryIndexPrefix = "history_";
    private const string PreviewIndexPrefix = "preview_";

    // Keep the load key blocked for a few seconds after a reload starts. This
    // stops key spamming from queueing another scene load while the new scene
    // is still initializing.
    private const float LoadBlockSeconds = 4f;
    private const float LoadSettleSeconds = 3f;
    private const float LoadTimeoutSeconds = 15f;

    private static bool saving;
    private static bool loading;
    private static bool loadKeyLatch;
    private static float loadStartedAt;
    private static float loadBlockedUntil;

    internal static bool IsLoading
    {
        get
        {
            float now = Time.realtimeSinceStartup;
            return (loading && now - loadStartedAt < LoadTimeoutSeconds) || now < loadBlockedUntil;
        }
    }

    internal static void OnGameLoaded()
    {
        saving = false;
        loading = false;
        loadKeyLatch = false;

        // Keep the quick load disabled briefly after the scene has finished
        // loading, even if the initial cooldown already elapsed.
        float now = Time.realtimeSinceStartup;
        loadBlockedUntil = Mathf.Max(loadBlockedUntil, now + LoadSettleSeconds);

        ManualHistoryStore.Initialize();

        // This runs on every load, so avoid a save-folder directory scan here.
        // The fixed temporary slot name is enough; the full scan runs at
        // startup and after each manual save.
        CleanupStaleTempFile();
    }

    internal static void OnDisabled()
    {
        saving = false;
        loading = false;
        loadKeyLatch = false;
        loadBlockedUntil = 0f;
        CleanupStaleTempFile();
    }

    /// <summary>
    /// Returns true once per press. A held load key stays latched until it is
    /// released, so key repeat cannot queue another reload.
    /// </summary>
    internal static bool ShouldTriggerLoad(KeyCode key)
    {
        if (key == KeyCode.None)
        {
            loadKeyLatch = false;
            return false;
        }

        bool held = Input.GetKey(key);
        if (loadKeyLatch)
        {
            if (!held)
            {
                loadKeyLatch = false;
            }

            return false;
        }

        if (!Input.GetKeyDown(key))
        {
            return false;
        }

        loadKeyLatch = true;
        return true;
    }

    internal static void ResetLoadKeyLatch()
    {
        loadKeyLatch = false;
    }

    /// <summary>
    /// Lightweight cleanup for the normal load path. It only checks the fixed
    /// temporary slot filename, so it does not enumerate the save folder.
    /// </summary>
    internal static void CleanupStaleTempFile()
    {
        try
        {
            string tempPath = SaveFiles.AbsolutePath(SaveFiles.Slot(TempSlotId));
            if (File.Exists(tempPath))
            {
                SaveFiles.WithIORetry(delegate { File.Delete(tempPath); });
            }
        }
        catch (Exception ex)
        {
            if (ModEntry.Log != null)
            {
                ModEntry.Log.Warning("Could not remove a stale temporary save file: " + ex.Message);
            }
        }
    }

    /// <summary>
    /// Full cleanup for startup and after a manual save. Also removes any
    /// temporary history files left by an interrupted save.
    /// </summary>
    internal static void CleanupStaleTempFiles()
    {
        CleanupStaleTempFile();

        try
        {
            string[] stale = System.IO.Directory.GetFiles(
                Application.persistentDataPath,
                "save_" + TempSlotId + "_day*.es3");
            for (int i = 0; i < stale.Length; i++)
            {
                string path = stale[i];
                if (File.Exists(path))
                {
                    SaveFiles.WithIORetry(delegate { File.Delete(path); });
                }
            }
        }
        catch (Exception ex)
        {
            if (ModEntry.Log != null)
            {
                ModEntry.Log.Warning("Could not scan for stale temporary history files: " + ex.Message);
            }
        }
    }

    internal static bool SaveManual(out string message)
    {
        message = null;

        if (!ModEntry.Enabled || !Settings.Enabled)
        {
            message = "Save Anytime is disabled.";
            return false;
        }

        if (saving)
        {
            message = "A save is already in progress.";
            return false;
        }

        if (!CanSave())
        {
            message = "There is no active run to save.";
            return false;
        }

        saving = true;
        try
        {
            return SaveManualInternal(out message);
        }
        catch (Exception ex)
        {
            if (ModEntry.Log != null)
            {
                ModEntry.Log.Warning("Manual save failed: " + ex);
            }

            message = "Save failed: " + ex.Message;
            return false;
        }
        finally
        {
            saving = false;
        }
    }

    internal static bool LoadManual(out string message, bool allowLatestFallback = true)
    {
        message = null;

        if (!ModEntry.Enabled || !Settings.Enabled)
        {
            message = "Save Anytime is disabled.";
            return false;
        }

        if (IsLoading)
        {
            message = "Please wait a moment before loading again.";
            return false;
        }

        if (!CanLoad())
        {
            message = "There is no active run to load.";
            return false;
        }

        int slotId = PlayerStore.Instance.saveSlotId;
        string manualFile = FindManualFile(slotId);
        string loadFile = null;
        bool fallback = false;

        if (!string.IsNullOrEmpty(manualFile))
        {
            string manualPath = SaveFiles.AbsolutePath(manualFile);
            string reason;
            if (!SaveFiles.IsSaveFileIntact(manualPath, out reason))
            {
                if (reason == "missing")
                {
                    // FindManualFile checked the file a moment ago. If it
                    // vanished, forget the pin and fall back to the latest
                    // save instead of reporting a damaged manual save.
                    ManualHistoryStore.Remove(slotId);
                }
                else
                {
                    message = "The manual save is damaged and was not loaded.";
                    return false;
                }
            }
            else
            {
                loadFile = manualFile;
            }
        }

        if (loadFile == null)
        {
            if (!allowLatestFallback)
            {
                message = "No quicksave found for this run.";
                return false;
            }

            string latestPath = SaveFiles.AbsolutePath(SaveFiles.Slot(slotId));
            string reason;
            if (!SaveFiles.IsSaveFileIntact(latestPath, out reason))
            {
                message = reason == "missing"
                    ? "No save was found for this run."
                    : "The latest save is damaged and was not loaded.";
                return false;
            }

            fallback = true;
        }

        try
        {
            float now = Time.realtimeSinceStartup;
            loading = true;
            loadStartedAt = now;
            loadBlockedUntil = now + LoadBlockSeconds;

            if (ModEntry.Log != null)
            {
                ModEntry.Log.Log(fallback
                    ? "No manual save found; loading the latest save of slot " + slotId + "."
                    : "Loading manual save " + loadFile + ".");
            }

            NewGameData.Instance.saveSlot = slotId;
            NewGameData.Instance.saveFile = loadFile;
            NewGameData.Instance.isLoadFromMainMenu = true;

            PlayerStore.Instance.Reset();
            EmporiumEntry.ResetData();
            PlayerStore.ResetData();
            StoreStation.ResetData();

            message = fallback
                ? "No manual save found; loading the latest save."
                : "Loading manual save...";

            SceneManager.LoadScene("InventoryScene", LoadSceneMode.Single);
            return true;
        }
        catch (Exception ex)
        {
            loading = false;
            loadBlockedUntil = 0f;
            message = "Load failed: " + ex.Message;
            if (ModEntry.Log != null)
            {
                ModEntry.Log.Warning("Manual load failed: " + ex);
            }

            return false;
        }
    }

    internal static bool HasManualSaveForCurrentRun()
    {
        if (!PlayerStore.HasInstance || PlayerStore.Instance == null)
        {
            return false;
        }

        if (NewGameData.Instance == null ||
            NewGameData.Instance.isShowcase ||
            NewGameData.Instance.isInMainMenu)
        {
            return false;
        }

        int slotId = PlayerStore.Instance.saveSlotId;
        if (slotId < 0)
        {
            return false;
        }

        return !string.IsNullOrEmpty(FindManualFile(slotId));
    }

    private static bool SaveManualInternal(out string message)
    {
        message = null;

        PlayerStore store = PlayerStore.Instance;
        int slotId = store.saveSlotId;
        int dayCount = StoreStation.Instance.dayCounter;

        DateTime stamp;
        string fileName = ChooseHistoryName(slotId, dayCount, out stamp);
        string manualPath = SaveFiles.AbsolutePath(fileName);
        string tempPath = SaveFiles.AbsolutePath(SaveFiles.Slot(TempSlotId));

        string previousFile = SafeGetModData(ModDataFileKey);
        string previousSlot = SafeGetModData(ModDataSlotKey);

        CleanupTempSlot(tempPath);

        try
        {
            // Store the filename in the save itself before writing it. The
            // per-slot map is the primary lookup, but this keeps a copied save
            // self-describing for the in-game quick load.
            SafeSetModData(ModDataFileKey, fileName);
            SafeSetModData(ModDataSlotKey, slotId.ToString(CultureInfo.InvariantCulture));

            store.saveSlotId = TempSlotId;
            store.SaveGame();

            if (!File.Exists(tempPath))
            {
                throw new IOException("The game did not write the temporary save file.");
            }

            string damageReason;
            if (!SaveFiles.IsSaveFileIntact(tempPath, out damageReason))
            {
                throw new IOException("The temporary save is damaged: " + damageReason);
            }

            SaveFiles.WithIORetry(delegate
            {
                if (File.Exists(manualPath))
                {
                    File.Delete(manualPath);
                }

                File.Move(tempPath, manualPath);
            });
        }
        catch
        {
            RestoreModData(previousFile, previousSlot);
            throw;
        }
        finally
        {
            store.saveSlotId = slotId;
            CleanupTempSlot(tempPath);
        }

        SaveHistoryEntry entry = new SaveHistoryEntry
        {
            file = fileName,
            dayCount = dayCount,
            credit = store.playerCash,
            saveDateTime = stamp,
            version = PlayerStore.VERSION
        };

        string previousManual = ManualHistoryStore.Get(slotId);

        try
        {
            List<SaveHistoryEntry> history = SaveFiles.LoadHistory(slotId);
            history.RemoveAll(e => e == null ||
                e.file == fileName ||
                (!string.IsNullOrEmpty(previousManual) && e.file == previousManual));
            history.Insert(0, entry);
            SaveFiles.IndexSave(HistoryIndexPrefix + slotId, history);
        }
        catch (Exception ex)
        {
            // The save file itself is already safely on disk and the quick
            // load can recover it from the run's mod data, so this is not a
            // save failure. It only means the history row may be missing.
            if (ModEntry.Log != null)
            {
                ModEntry.Log.Warning("The manual save was written but its history entry could not be updated: " + ex);
            }
        }

        ManualHistoryStore.Set(slotId, fileName);
        SafeSetModData(ModDataFileKey, fileName);
        SafeSetModData(ModDataSlotKey, slotId.ToString(CultureInfo.InvariantCulture));
        DeletePreviousManual(slotId, previousManual, fileName);

        if (ModEntry.Log != null)
        {
            ModEntry.Log.Log("Manual save " + fileName + " written for slot " + slotId + ".");
        }

        message = "Game saved.";
        return true;
    }

    private static void DeletePreviousManual(int slotId, string previous, string current)
    {
        if (string.IsNullOrEmpty(previous) || previous == current)
        {
            return;
        }

        // Only delete a file that is a valid history name for this slot. This
        // keeps a corrupted or mismatched map entry from removing a real save.
        if (!IsHistoryFileForSlot(previous, slotId))
        {
            return;
        }

        try
        {
            string path = SaveFiles.AbsolutePath(previous);
            if (!File.Exists(path))
            {
                return;
            }

            SaveFiles.WithIORetry(delegate { File.Delete(path); });

            if (ModEntry.Log != null)
            {
                ModEntry.Log.Log("Removed the previous manual save " + previous + ".");
            }
        }
        catch (Exception ex)
        {
            if (ModEntry.Log != null)
            {
                ModEntry.Log.Warning("Could not remove the previous manual save " + previous + ": " + ex.Message);
            }
        }
    }

    private static bool CanSave()
    {
        if (!PlayerStore.HasInstance || PlayerStore.Instance == null)
        {
            return false;
        }

        if (PlayerStore.Instance.saveSlotId < 0)
        {
            return false;
        }

        if (NewGameData.Instance == null ||
            NewGameData.Instance.isShowcase ||
            NewGameData.Instance.isInMainMenu)
        {
            return false;
        }

        if (EmporiumEntry.Instance == null)
        {
            return false;
        }

        if (StoreStation.Instance == null)
        {
            return false;
        }

        return true;
    }

    private static bool CanLoad()
    {
        if (!PlayerStore.HasInstance || PlayerStore.Instance == null)
        {
            return false;
        }

        if (PlayerStore.Instance.saveSlotId < 0)
        {
            return false;
        }

        if (NewGameData.Instance == null ||
            NewGameData.Instance.isShowcase ||
            NewGameData.Instance.isInMainMenu)
        {
            return false;
        }

        return true;
    }

    private static string ChooseHistoryName(int slotId, int dayCount, out DateTime stamp)
    {
        DateTime latest = DateTime.MinValue;
        try
        {
            SavePreviewData preview = SaveUIManager.GetPreview(slotId);
            if (preview != null && preview.lastSaveDateTime.Year > 1)
            {
                latest = preview.lastSaveDateTime;
            }
        }
        catch
        {
            // A missing preview only means there is no latest save to avoid.
        }

        try
        {
            // The preview index can be missing or stale; the slot file's own
            // timestamp is the other value the game can rotate into history.
            string slotPath = SaveFiles.AbsolutePath(SaveFiles.Slot(slotId));
            if (File.Exists(slotPath))
            {
                DateTime written = File.GetLastWriteTime(slotPath);
                if (written > latest)
                {
                    latest = written;
                }
            }
        }
        catch
        {
            // The preview timestamp is still a useful lower bound.
        }

        DateTime now = DateTime.Now;
        DateTime candidate = new DateTime(
            now.Year, now.Month, now.Day, now.Hour, now.Minute, now.Second, now.Kind);

        DateTime latestSecond = new DateTime(
            latest.Year, latest.Month, latest.Day, latest.Hour, latest.Minute, latest.Second, latest.Kind);

        // The game rotates the latest slot file into history using its preview
        // timestamp. Keep our filename away from that timestamp so a later
        // autosave cannot overwrite the manual save.
        if (candidate <= latestSecond)
        {
            candidate = latestSecond.AddSeconds(1);
        }

        for (int i = 0; i < 300; i++)
        {
            DateTime attempt = candidate.AddSeconds(i);
            string name = SaveFiles.HistoryFile(slotId, dayCount, attempt);
            if (!File.Exists(SaveFiles.AbsolutePath(name)))
            {
                stamp = attempt;
                return name;
            }
        }

        throw new IOException("Could not find a free filename for the manual save.");
    }

    private static string FindManualFile(int slotId)
    {
        string file = ManualHistoryStore.Get(slotId);
        if (!string.IsNullOrEmpty(file))
        {
            if (IsHistoryFileForSlot(file, slotId) && File.Exists(SaveFiles.AbsolutePath(file)))
            {
                return file;
            }

            ManualHistoryStore.Remove(slotId);
        }

        string fromSave = SafeGetModData(ModDataFileKey);
        if (!string.IsNullOrEmpty(fromSave) &&
            IsHistoryFileForSlot(fromSave, slotId) &&
            File.Exists(SaveFiles.AbsolutePath(fromSave)))
        {
            ManualHistoryStore.Set(slotId, fromSave);
            return fromSave;
        }

        return null;
    }

    internal static bool IsHistoryFileForSlot(string file, int slotId)
    {
        if (string.IsNullOrEmpty(file))
        {
            return false;
        }

        int parsedSlot;
        int dayCount;
        DateTime stamp;
        string stem = Path.GetFileNameWithoutExtension(file);
        return SaveFiles.TryParseHistoryName(stem, out parsedSlot, out dayCount, out stamp) &&
               parsedSlot == slotId;
    }

    private static void CleanupTempSlot(string tempPath)
    {
        try
        {
            if (File.Exists(tempPath))
            {
                SaveFiles.WithIORetry(delegate { File.Delete(tempPath); });
            }
        }
        catch (Exception ex)
        {
            if (ModEntry.Log != null)
            {
                ModEntry.Log.Warning("Could not remove the temporary save file: " + ex.Message);
            }
        }

        try
        {
            SaveFiles.IndexDeleteKey(PreviewIndexPrefix + TempSlotId);
        }
        catch (Exception ex)
        {
            if (ModEntry.Log != null)
            {
                ModEntry.Log.Warning("Could not remove the temporary preview entry: " + ex.Message);
            }
        }

        try
        {
            SaveUIManager.RemoveCacheEntry(TempSlotId);
        }
        catch (Exception ex)
        {
            if (ModEntry.Log != null)
            {
                ModEntry.Log.Warning("Could not remove the temporary preview cache entry: " + ex.Message);
            }
        }

        try
        {
            List<SaveHistoryEntry> tempHistory = SaveFiles.LoadHistory(TempSlotId);
            for (int i = 0; i < tempHistory.Count; i++)
            {
                SaveHistoryEntry entry = tempHistory[i];
                if (entry == null || string.IsNullOrEmpty(entry.file))
                {
                    continue;
                }

                string path = SaveFiles.AbsolutePath(entry.file);
                if (File.Exists(path))
                {
                    SaveFiles.WithIORetry(delegate { File.Delete(path); });
                }
            }

            SaveFiles.IndexDeleteKey(HistoryIndexPrefix + TempSlotId);
        }
        catch (Exception ex)
        {
            if (ModEntry.Log != null)
            {
                ModEntry.Log.Warning("Could not clean the temporary history entry: " + ex.Message);
            }
        }

        // The temporary slot id is negative, so its history names never parse
        // as valid history entries. Remove any stragglers directly.
        try
        {
            string[] stale = System.IO.Directory.GetFiles(
                Application.persistentDataPath,
                "save_" + TempSlotId + "_day*.es3");
            for (int i = 0; i < stale.Length; i++)
            {
                string path = stale[i];
                if (File.Exists(path))
                {
                    SaveFiles.WithIORetry(delegate { File.Delete(path); });
                }
            }
        }
        catch (Exception ex)
        {
            if (ModEntry.Log != null)
            {
                ModEntry.Log.Warning("Could not scan for temporary history files: " + ex.Message);
            }
        }
    }

    private static string SafeGetModData(string key)
    {
        try
        {
            return ModHelper.GetModData(ModEntry.ModId, key);
        }
        catch
        {
            return null;
        }
    }

    private static void SafeSetModData(string key, string value)
    {
        try
        {
            ModHelper.SetModData(ModEntry.ModId, key, value);
        }
        catch (Exception ex)
        {
            if (ModEntry.Log != null)
            {
                ModEntry.Log.Warning("Could not store manual save metadata: " + ex.Message);
            }
        }
    }

    private static void RestoreModData(string file, string slot)
    {
        try
        {
            if (file == null)
            {
                ModHelper.RemoveModData(ModEntry.ModId, ModDataFileKey);
            }
            else
            {
                ModHelper.SetModData(ModEntry.ModId, ModDataFileKey, file);
            }

            if (slot == null)
            {
                ModHelper.RemoveModData(ModEntry.ModId, ModDataSlotKey);
            }
            else
            {
                ModHelper.SetModData(ModEntry.ModId, ModDataSlotKey, slot);
            }
        }
        catch (Exception ex)
        {
            if (ModEntry.Log != null)
            {
                ModEntry.Log.Warning("Could not restore manual save metadata: " + ex.Message);
            }
        }
    }
}
