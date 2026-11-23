using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace SaveAnytime;

internal static class PauseMenuUI
{
    internal const string ButtonName = "ButtonSaveGame";

    private const string IdleText = "Save Game";
    private const float PanelHeightIncrease = 50f;

    internal static void Ensure(EscapeUIManager manager)
    {
        if (!ModEntry.Enabled || !Settings.Enabled || manager == null || manager.menuPanel == null)
        {
            return;
        }

        if (NewGameData.Instance == null || NewGameData.Instance.isShowcase)
        {
            Remove();
            return;
        }

        try
        {
            Transform escapeMenu = FindEscapeMenu(manager.menuPanel.transform);
            if (escapeMenu == null)
            {
                return;
            }

            Transform existing = escapeMenu.Find(ButtonName);
            if (existing != null)
            {
                EnsurePanelHeight(escapeMenu);

                SaveButtonFeedback existingFeedback = existing.GetComponent<SaveButtonFeedback>();
                if (existingFeedback != null)
                {
                    existingFeedback.ResetLabel();
                }

                return;
            }

            Transform template = escapeMenu.Find("ButtonOption");
            if (template == null)
            {
                template = FindFirstButton(escapeMenu);
            }

            if (template == null)
            {
                return;
            }

            GameObject clone = UnityEngine.Object.Instantiate(template.gameObject, escapeMenu);
            clone.name = ButtonName;
            clone.transform.SetSiblingIndex(template.GetSiblingIndex() + 1);
            clone.SetActive(true);

            RemoveLocalizers(clone);
            RemoveStoreUI(clone);

            TextMeshProUGUI label = clone.GetComponentInChildren<TextMeshProUGUI>(true);
            if (label != null)
            {
                label.text = IdleText;
                label.enableWordWrapping = false;
                label.overflowMode = TextOverflowModes.Overflow;
            }

            Button button = clone.GetComponent<Button>();
            if (button != null)
            {
                button.onClick = new Button.ButtonClickedEvent();
                button.onClick.AddListener(delegate { OnSaveClicked(manager); });
            }

            SaveButtonFeedback feedback = clone.AddComponent<SaveButtonFeedback>();
            feedback.Initialize(label, IdleText);

            EnsurePanelHeight(escapeMenu);
        }
        catch (Exception ex)
        {
            if (ModEntry.Log != null)
            {
                ModEntry.Log.Warning("Could not add the pause-menu save button: " + ex);
            }
        }
    }

    internal static void ShowSaveFeedback(bool success)
    {
        SaveButtonFeedback feedback = FindFeedback();
        if (feedback == null)
        {
            return;
        }

        if (success)
        {
            feedback.ShowSaved();
        }
        else
        {
            feedback.ShowFailed();
        }
    }

    internal static void Notify(string message, string color)
    {
        if (string.IsNullOrEmpty(message))
        {
            return;
        }

        try
        {
            StoreUIManager ui = StoreUIManager.Instance;
            if (ui != null)
            {
                ui.Notify(message, color);
            }
        }
        catch (Exception ex)
        {
            if (ModEntry.Log != null)
            {
                ModEntry.Log.Warning("Could not show a notification: " + ex.Message);
            }
        }
    }

    internal static void Remove()
    {
        try
        {
            StoreUIManager ui = StoreUIManager.Instance;
            EscapeUIManager manager = ui != null ? ui.escapeUIManager : null;
            if (manager == null || manager.menuPanel == null)
            {
                return;
            }

            Transform escapeMenu = FindEscapeMenu(manager.menuPanel.transform);
            if (escapeMenu == null)
            {
                return;
            }

            Transform button = escapeMenu.Find(ButtonName);
            if (button != null)
            {
                UnityEngine.Object.Destroy(button.gameObject);
            }

            PauseMenuMarker marker = escapeMenu.GetComponent<PauseMenuMarker>();
            if (marker != null)
            {
                if (marker.HeightAdjusted)
                {
                    RectTransform rect = escapeMenu as RectTransform;
                    if (rect != null)
                    {
                        rect.sizeDelta = new Vector2(
                            rect.sizeDelta.x,
                            Mathf.Max(0f, rect.sizeDelta.y - PanelHeightIncrease));
                    }
                }

                UnityEngine.Object.Destroy(marker);
            }
        }
        catch (Exception ex)
        {
            if (ModEntry.Log != null)
            {
                ModEntry.Log.Warning("Could not remove the pause-menu save button: " + ex);
            }
        }
    }

    private static void OnSaveClicked(EscapeUIManager manager)
    {
        try
        {
            if (manager != null)
            {
                try
                {
                    manager.OnAnyClick();
                }
                catch
                {
                    // A missing click sound must not block the save.
                }
            }

            if (!ModEntry.Enabled)
            {
                return;
            }

            bool saved = ManualSaveController.SaveManual(out string message);
            ShowSaveFeedback(saved);
            Notify(message, saved ? "default" : "sec");
        }
        catch (Exception ex)
        {
            if (ModEntry.Log != null)
            {
                ModEntry.Log.Warning("Pause-menu save failed: " + ex);
            }
        }
    }

    private static SaveButtonFeedback FindFeedback()
    {
        try
        {
            StoreUIManager ui = StoreUIManager.Instance;
            EscapeUIManager manager = ui != null ? ui.escapeUIManager : null;
            if (manager == null || manager.menuPanel == null)
            {
                return null;
            }

            Transform escapeMenu = FindEscapeMenu(manager.menuPanel.transform);
            if (escapeMenu == null)
            {
                return null;
            }

            Transform button = escapeMenu.Find(ButtonName);
            return button != null ? button.GetComponent<SaveButtonFeedback>() : null;
        }
        catch
        {
            return null;
        }
    }

    private static void EnsurePanelHeight(Transform escapeMenu)
    {
        RectTransform rect = escapeMenu as RectTransform;
        if (rect == null)
        {
            return;
        }

        PauseMenuMarker marker = escapeMenu.GetComponent<PauseMenuMarker>();
        if (marker == null)
        {
            marker = escapeMenu.gameObject.AddComponent<PauseMenuMarker>();
        }

        if (marker.HeightAdjusted)
        {
            return;
        }

        marker.HeightAdjusted = true;
        rect.sizeDelta = new Vector2(rect.sizeDelta.x, rect.sizeDelta.y + PanelHeightIncrease);
    }

    private static Transform FindEscapeMenu(Transform menuPanel)
    {
        if (menuPanel == null)
        {
            return null;
        }

        Transform fallback = null;
        Transform[] all = menuPanel.GetComponentsInChildren<Transform>(true);
        for (int i = 0; i < all.Length; i++)
        {
            Transform current = all[i];
            if (current.name != "_EscapeMenu")
            {
                continue;
            }

            if (current.Find("ButtonOption") != null)
            {
                return current;
            }

            if (fallback == null)
            {
                fallback = current;
            }
        }

        if (fallback != null)
        {
            return fallback;
        }

        // Fallback for a renamed panel: use the transform that directly owns
        // the Options button.
        for (int i = 0; i < all.Length; i++)
        {
            Transform current = all[i];
            if (current.Find("ButtonOption") != null)
            {
                return current;
            }
        }

        return null;
    }

    private static Transform FindFirstButton(Transform parent)
    {
        for (int i = 0; i < parent.childCount; i++)
        {
            Transform child = parent.GetChild(i);
            if (child.name != "Autosave (1)" && child.GetComponent<Button>() != null)
            {
                return child;
            }
        }

        return null;
    }

    private static void RemoveLocalizers(GameObject clone)
    {
        MonoBehaviour[] behaviours = clone.GetComponentsInChildren<MonoBehaviour>(true);
        for (int i = 0; i < behaviours.Length; i++)
        {
            MonoBehaviour behaviour = behaviours[i];
            if (behaviour == null)
            {
                continue;
            }

            string typeName = behaviour.GetType().Name;
            if (typeName == "LocalizeStringEvent" || typeName == "TMPLocalizer")
            {
                behaviour.enabled = false;
                UnityEngine.Object.Destroy(behaviour);
            }
        }
    }

    private static void RemoveStoreUI(GameObject clone)
    {
        StoreUI[] storeUIs = clone.GetComponentsInChildren<StoreUI>(true);
        for (int i = 0; i < storeUIs.Length; i++)
        {
            StoreUI storeUI = storeUIs[i];
            if (storeUI == null)
            {
                continue;
            }

            storeUI.enabled = false;
            UnityEngine.Object.Destroy(storeUI);
        }
    }
}
