using System;
using UnityEngine;

namespace SaveAnytime;

internal static class Settings
{
    private const string SectionGeneral = "General";
    private const string SectionKeybinds = "Keybinds";
    private const string VanillaKeybindSection = "keybind";

    private static ConfigEntry<bool> enabled;
    private static ConfigEntry<string> saveKey;
    private static ConfigEntry<string> loadKey;

    internal static void Initialize()
    {
        ModConfig config = ModHelper.GetConfig(ModEntry.ModId);

        if (enabled != null)
        {
            enabled.OnChanged -= OnEnabledChanged;
        }

        enabled = config.Define(
            "enabled",
            true,
            SectionGeneral,
            () => "Enabled",
            () => "Turn Save Anytime on or off.");
        enabled.OnChanged += OnEnabledChanged;

        saveKey = config.Define(
            "save_key",
            "F5",
            SectionKeybinds,
            () => "Save game key",
            null,
            () => "Press this key during a run to create a manual save.");

        loadKey = config.Define(
            "load_key",
            "F9",
            SectionKeybinds,
            () => "Load game key",
            null,
            () => "Press this key during a run to load the latest manual save.");
    }

    internal static bool Enabled
    {
        get { return enabled == null || enabled.Value; }
    }

    internal static KeyCode SaveKey
    {
        get { return ParseKey(saveKey, KeyCode.F5); }
    }

    internal static KeyCode LoadKey
    {
        get { return ParseKey(loadKey, KeyCode.F9); }
    }

    internal static string SaveKeyName
    {
        get { return KeyDisplay(saveKey != null ? saveKey.Value : "F5"); }
    }

    internal static string LoadKeyName
    {
        get { return KeyDisplay(loadKey != null ? loadKey.Value : "F9"); }
    }

    internal static void SetSaveKey(KeyCode key)
    {
        if (saveKey == null)
        {
            return;
        }

        saveKey.Set(key.ToString());
        KeybindUI.RefreshLabelsFromEntries();
    }

    internal static void SetLoadKey(KeyCode key)
    {
        if (loadKey == null)
        {
            return;
        }

        loadKey.Set(key.ToString());
        KeybindUI.RefreshLabelsFromEntries();
    }

    internal static void RegisterGameOptions()
    {
        if (saveKey == null || loadKey == null)
        {
            return;
        }

        ModHelper.AddGameOptionKeyBind(
            ModEntry.ModId,
            VanillaKeybindSection,
            "save_game",
            () => "Save Game",
            () => SaveKey,
            SetSaveKey,
            () => "Create a manual save at any time during a run.");

        ModHelper.AddGameOptionKeyBind(
            ModEntry.ModId,
            VanillaKeybindSection,
            "load_game",
            () => "Load Game",
            () => LoadKey,
            SetLoadKey,
            () => "Load the latest manual save of the current run.");
    }

    internal static void UnregisterGameOptions()
    {
        ModHelper.RemoveGameOptions(ModEntry.ModId);
    }

    private static void OnEnabledChanged(bool value)
    {
        if (value)
        {
            return;
        }

        try
        {
            PauseMenuUI.Remove();
        }
        catch (Exception ex)
        {
            if (ModEntry.Log != null)
            {
                ModEntry.Log.Warning("Could not remove the pause-menu save button: " + ex.Message);
            }
        }
    }

    internal static string KeyDisplay(string value)
    {
        if (string.IsNullOrEmpty(value))
        {
            return "None";
        }

        KeyCode key;
        if (Enum.TryParse(value, true, out key))
        {
            return key == KeyCode.None ? "None" : KeyBindHelper.KeyName(key);
        }

        return value;
    }

    internal static bool IsKeyboardKey(KeyCode key)
    {
        if (key == KeyCode.None)
        {
            return false;
        }

        string name = key.ToString();
        return !name.StartsWith("Mouse", StringComparison.Ordinal) &&
               !name.StartsWith("Joystick", StringComparison.Ordinal) &&
               !name.StartsWith("Gamepad", StringComparison.Ordinal);
    }

    private static KeyCode ParseKey(ConfigEntry<string> entry, KeyCode fallback)
    {
        if (entry == null || string.IsNullOrEmpty(entry.Value))
        {
            return fallback;
        }

        KeyCode key;
        if (Enum.TryParse(entry.Value, true, out key))
        {
            return key;
        }

        return fallback;
    }
}
