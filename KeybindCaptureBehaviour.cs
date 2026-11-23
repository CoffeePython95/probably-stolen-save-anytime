using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;

namespace SaveAnytime;

/// <summary>
/// Listens for the next keyboard key and writes it into the mod setting.
/// </summary>
public sealed class KeybindCaptureBehaviour : MonoBehaviour
{
    private static readonly KeyCode[] CapturableKeys = BuildKeys();

    private ConfigEntry<string> entry;
    private CustomUIElement button;
    private CustomUIElement hiddenInput;
    private bool capturing;

    internal void Initialize(ConfigEntry<string> entry, CustomUIElement button, CustomUIElement hiddenInput)
    {
        this.entry = entry;
        this.button = button;
        this.hiddenInput = hiddenInput;
        KeybindUI.Register(this);
        RefreshFromEntry();
    }

    internal void BeginCapture()
    {
        if (!ModEntry.Enabled || capturing)
        {
            return;
        }

        capturing = true;
        KeybindUI.BeginCapture(this);
        SetLabel("Press a key...");

        if (EventSystem.current != null)
        {
            EventSystem.current.SetSelectedGameObject(null);
        }
    }

    internal void CancelCapture()
    {
        if (!capturing)
        {
            return;
        }

        capturing = false;
        KeybindUI.EndCapture(this);

        if (hiddenInput != null)
        {
            RefreshFromWidget();
        }
        else
        {
            RefreshFromEntry();
        }
    }

    internal void RefreshFromEntry()
    {
        if (capturing)
        {
            return;
        }

        SetLabel(Settings.KeyDisplay(entry != null ? entry.Value : null));
    }

    internal void RefreshFromWidget()
    {
        if (capturing)
        {
            return;
        }

        string value = hiddenInput != null ? hiddenInput.GetString() : null;
        if (string.IsNullOrEmpty(value) && entry != null)
        {
            value = entry.Value;
        }

        SetLabel(Settings.KeyDisplay(value));
    }

    private void Update()
    {
        if (!capturing)
        {
            return;
        }

        if (!ModEntry.Enabled)
        {
            CancelCapture();
            return;
        }

        if (Input.GetKeyDown(KeyCode.Escape))
        {
            CancelCapture();
            return;
        }

        for (int i = 0; i < CapturableKeys.Length; i++)
        {
            KeyCode key = CapturableKeys[i];
            if (Input.GetKeyDown(key))
            {
                Apply(key);
                return;
            }
        }
    }

    private void Apply(KeyCode key)
    {
        string name = key.ToString();
        capturing = false;
        KeybindUI.EndCapture(this);

        if (entry != null)
        {
            try
            {
                entry.Set(name);
            }
            catch (Exception ex)
            {
                if (ModEntry.Log != null)
                {
                    ModEntry.Log.Warning("Could not save the keybind: " + ex.Message);
                }
            }
        }

        if (hiddenInput != null)
        {
            hiddenInput.SetString(name);
        }

        SetLabel(Settings.KeyDisplay(name));
    }

    private void OnDisable()
    {
        if (capturing)
        {
            CancelCapture();
        }
    }

    private void OnDestroy()
    {
        if (capturing)
        {
            CancelCapture();
        }

        KeybindUI.Unregister(this);
    }

    private void SetLabel(string value)
    {
        if (button != null)
        {
            button.SetText(value);
        }
    }

    private static KeyCode[] BuildKeys()
    {
        List<KeyCode> keys = new List<KeyCode>();
        foreach (KeyCode key in Enum.GetValues(typeof(KeyCode)))
        {
            if (key == KeyCode.None || key == KeyCode.Escape)
            {
                continue;
            }

            string name = key.ToString();
            if (name.StartsWith("Mouse", StringComparison.Ordinal) ||
                name.StartsWith("Joystick", StringComparison.Ordinal) ||
                name.StartsWith("Gamepad", StringComparison.Ordinal))
            {
                continue;
            }

            keys.Add(key);
        }

        return keys.ToArray();
    }
}
