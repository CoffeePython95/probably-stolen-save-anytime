using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace SaveAnytime;

/// <summary>
/// Remembers which history file is the current manual save for each slot.
/// The file is pinned to the top of the earlier-day list when the load menu
/// reads it, even after the game rotates new autosaves into history.
/// </summary>
internal static class ManualHistoryStore
{
    private const string FileName = "manual_history.txt";

    private static readonly Dictionary<int, string> bySlot = new Dictionary<int, string>();
    private static bool loaded;

    internal static void Initialize()
    {
        EnsureLoaded();
    }

    internal static string Get(int slotId)
    {
        EnsureLoaded();

        string file;
        return bySlot.TryGetValue(slotId, out file) ? file : null;
    }

    internal static void Set(int slotId, string file)
    {
        if (slotId < 0 || string.IsNullOrEmpty(file))
        {
            return;
        }

        EnsureLoaded();

        string existing;
        if (bySlot.TryGetValue(slotId, out existing) && existing == file)
        {
            return;
        }

        bySlot[slotId] = file;
        Save();
    }

    internal static void Remove(int slotId)
    {
        EnsureLoaded();
        if (bySlot.Remove(slotId))
        {
            Save();
        }
    }

    internal static bool IsPinned(string file)
    {
        if (string.IsNullOrEmpty(file))
        {
            return false;
        }

        EnsureLoaded();
        foreach (string pinned in bySlot.Values)
        {
            if (pinned == file)
            {
                return true;
            }
        }

        return false;
    }

    internal static void RemoveIfFile(int slotId, string file)
    {
        if (string.IsNullOrEmpty(file))
        {
            return;
        }

        EnsureLoaded();

        string existing;
        if (bySlot.TryGetValue(slotId, out existing) && existing == file)
        {
            bySlot.Remove(slotId);
            Save();
        }
    }

    private static void EnsureLoaded()
    {
        if (loaded)
        {
            return;
        }

        loaded = true;

        try
        {
            string folder = ModHelper.GetModDataFolder(ModEntry.ModId);
            string path = Path.Combine(folder, FileName);
            if (!File.Exists(path))
            {
                return;
            }

            string text = File.ReadAllText(path);
            if (string.IsNullOrEmpty(text))
            {
                return;
            }

            string[] lines = text.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
            for (int i = 0; i < lines.Length; i++)
            {
                string line = lines[i].Trim();
                int separator = line.IndexOf('=');
                if (separator <= 0)
                {
                    continue;
                }

                int slotId;
                if (!int.TryParse(line.Substring(0, separator), out slotId) || slotId < 0)
                {
                    continue;
                }

                string file = line.Substring(separator + 1).Trim();
                if (file.Length > 0)
                {
                    bySlot[slotId] = file;
                }
            }
        }
        catch (Exception ex)
        {
            if (ModEntry.Log != null)
            {
                ModEntry.Log.Warning("Could not read manual save history: " + ex.Message);
            }
        }
    }

    private static void Save()
    {
        string text = BuildText();

        try
        {
            string folder = ModHelper.GetModDataFolder(ModEntry.ModId);
            System.IO.Directory.CreateDirectory(folder);
            File.WriteAllText(Path.Combine(folder, FileName), text);
        }
        catch (Exception ex)
        {
            if (ModEntry.Log != null)
            {
                ModEntry.Log.Warning("Could not write manual save history: " + ex.Message);
            }
        }
    }

    private static string BuildText()
    {
        StringBuilder builder = new StringBuilder();
        foreach (KeyValuePair<int, string> pair in bySlot.OrderBy(p => p.Key))
        {
            builder.Append(pair.Key).Append('=').AppendLine(pair.Value);
        }

        return builder.ToString();
    }
}
