using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;

namespace SaveAnytime;

/// <summary>
/// Builds the mod-settings keybind rows and tracks the one row that is
/// currently listening for a key.
/// </summary>
internal static class KeybindUI
{
    private static readonly List<KeybindCaptureBehaviour> active = new List<KeybindCaptureBehaviour>();

    private static KeybindCaptureBehaviour activeCapture;
    private static int suppressThroughFrame = -1;

    internal static bool AnyCapturing
    {
        get { return activeCapture != null || Time.frameCount <= suppressThroughFrame; }
    }

    internal static void Register(KeybindCaptureBehaviour behaviour)
    {
        if (behaviour != null && !active.Contains(behaviour))
        {
            active.Add(behaviour);
        }
    }

    internal static void Unregister(KeybindCaptureBehaviour behaviour)
    {
        active.Remove(behaviour);
        if (activeCapture == behaviour)
        {
            activeCapture = null;
        }
    }

    internal static void BeginCapture(KeybindCaptureBehaviour behaviour)
    {
        if (activeCapture != null && activeCapture != behaviour)
        {
            activeCapture.CancelCapture();
        }

        activeCapture = behaviour;
    }

    internal static void EndCapture(KeybindCaptureBehaviour behaviour)
    {
        if (activeCapture == behaviour)
        {
            activeCapture = null;
        }

        // Keep StoreUIManager.Update suppressed for the rest of this frame so
        // the captured key cannot also trigger a game action.
        suppressThroughFrame = Time.frameCount;
    }

    internal static void CancelAll()
    {
        if (activeCapture != null)
        {
            activeCapture.CancelCapture();
        }

        activeCapture = null;
        suppressThroughFrame = -1;
    }

    internal static void RefreshLabelsFromWidgets()
    {
        Prune();
        for (int i = 0; i < active.Count; i++)
        {
            active[i].RefreshFromWidget();
        }
    }

    internal static void RefreshLabelsFromEntries()
    {
        Prune();
        for (int i = 0; i < active.Count; i++)
        {
            active[i].RefreshFromEntry();
        }
    }

    internal static void Build(CustomUIBuilder builder, ModConfig config, ConfigEntry entry)
    {
        ConfigEntry<string> keyEntry = entry as ConfigEntry<string>;
        if (keyEntry == null)
        {
            BuildFallback(builder, config, entry);
            return;
        }

        builder.BeginRow(4f, TextAnchor.MiddleLeft);
        AddLabel(builder, config, entry);
        builder.AddFlexibleSpace();

        KeybindCaptureBehaviour capture = null;
        builder.AddButton(
            Settings.KeyDisplay(keyEntry.Value),
            delegate { capture?.BeginCapture(); },
            "keybind_button_" + entry.Key);

        CustomUIElement button = builder.Last;
        button.SetFixedWidth(245f);
        button.SetPreferredSize(245f, 34f);
        if (button.text != null)
        {
            button.text.alignment = TextAlignmentOptions.Center;
            button.text.enableWordWrapping = false;
            button.text.overflowMode = TextOverflowModes.Overflow;
            button.text.fontSize = 18f;
        }

        // ModConfigWindow.Apply/ResetWidgets look up elements by entry key.
        // This hidden input carries the serialized key value for them.
        builder.AddInput("", keyEntry.Value ?? "", null, entry.Key);
        CustomUIElement hiddenInput = builder.Last;
        hiddenInput.SetFixedWidth(1f);
        hiddenInput.SetPreferredSize(1f, 1f);
        if (hiddenInput.root != null)
        {
            hiddenInput.root.gameObject.SetActive(false);
        }

        builder.End();

        if (button.root != null)
        {
            capture = button.root.AddComponent<KeybindCaptureBehaviour>();
            capture.Initialize(keyEntry, button, hiddenInput);
        }
    }

    private static void BuildFallback(CustomUIBuilder builder, ModConfig config, ConfigEntry entry)
    {
        builder.BeginRow(4f, TextAnchor.MiddleLeft);
        AddLabel(builder, config, entry);
        builder.AddFlexibleSpace();
        builder.AddInput("", ConfigEntry.Format(entry.BoxedValue), null, entry.Key);
        builder.Last.SetFixedWidth(245f);
        builder.End();
    }

    private static void AddLabel(CustomUIBuilder builder, ModConfig config, ConfigEntry entry)
    {
        string text = SafeLabel(entry);

        if (entry.Tooltip != null && config != null)
        {
            string link = "modcfg:" + config.ModId + ":" + entry.Key;
            TMPIconHover.CustomLinks[link] = entry.Tooltip;
            text = text + " <link=\"" + link + "\"><sprite name=\"info\" tint=1></link>";
        }

        builder.AddLabel(text, "label_" + entry.Key);
    }

    private static string SafeLabel(ConfigEntry entry)
    {
        try
        {
            return entry.Label != null ? entry.Label() : entry.Key;
        }
        catch (Exception ex)
        {
            if (ModEntry.Log != null)
            {
                ModEntry.Log.Warning("Could not read a setting label: " + ex.Message);
            }

            return entry.Key;
        }
    }

    private static void Prune()
    {
        for (int i = active.Count - 1; i >= 0; i--)
        {
            if (active[i] == null)
            {
                active.RemoveAt(i);
            }
        }
    }
}
