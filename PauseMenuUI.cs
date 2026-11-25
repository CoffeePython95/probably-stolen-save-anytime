using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace SaveAnytime;

internal static class PauseMenuUI
{
    internal const string SaveButtonName = "ButtonSaveGame";
    internal const string LoadButtonName = "ButtonLoadQuicksave";

    private const string SaveIdleText = "Save Game";
    private const string LoadIdleText = "Load Quicksave";
    private const float PanelHeightIncrease = 50f;

    internal static void Ensure(EscapeUIManager manager)
    {
        if (!ModEntry.Enabled || !Settings.Enabled || manager == null || manager.menuPanel == null)
        {
            return;
        }

        if (NewGameData.Instance == null ||
            NewGameData.Instance.isShowcase ||
            NewGameData.Instance.isInMainMenu)
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

            Transform template = escapeMenu.Find("ButtonOption");
            if (template == null)
            {
                template = FindFirstButton(escapeMenu);
            }

            if (template == null)
            {
                return;
            }

            Transform save = EnsureButton(
                escapeMenu,
                template,
                SaveButtonName,
                SaveIdleText,
                delegate { OnSaveClicked(manager); },
                template);

            Transform load = EnsureButton(
                escapeMenu,
                template,
                LoadButtonName,
                LoadIdleText,
                delegate { OnLoadClicked(manager); },
                save);

            if (save != null && load != null)
            {
                EnsurePanelHeight(escapeMenu, 2);
            }
        }
        catch (Exception ex)
        {
            if (ModEntry.Log != null)
            {
                ModEntry.Log.Warning("Could not add the pause-menu buttons: " + ex);
            }
        }
    }

    internal static void ShowSaveFeedback(bool success)
    {
        PauseButtonFeedback feedback = FindFeedback(SaveButtonName);
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

            DestroyButton(escapeMenu, SaveButtonName);
            DestroyButton(escapeMenu, LoadButtonName);

            PauseMenuMarker marker = escapeMenu.GetComponent<PauseMenuMarker>();
            if (marker != null)
            {
                if (marker.HeightSteps > 0)
                {
                    RectTransform rect = escapeMenu as RectTransform;
                    if (rect != null)
                    {
                        rect.sizeDelta = new Vector2(
                            rect.sizeDelta.x,
                            Mathf.Max(0f, rect.sizeDelta.y - marker.HeightSteps * PanelHeightIncrease));
                    }
                }

                UnityEngine.Object.Destroy(marker);
            }
        }
        catch (Exception ex)
        {
            if (ModEntry.Log != null)
            {
                ModEntry.Log.Warning("Could not remove the pause-menu buttons: " + ex);
            }
        }
    }

    private static Transform EnsureButton(
        Transform escapeMenu,
        Transform template,
        string buttonName,
        string idleText,
        Action onClick,
        Transform after)
    {
        Transform existing = escapeMenu.Find(buttonName);
        if (existing != null)
        {
            PauseButtonFeedback existingFeedback = existing.GetComponent<PauseButtonFeedback>();
            if (existingFeedback != null)
            {
                existingFeedback.ResetLabel();
            }

            return existing;
        }

        GameObject clone = UnityEngine.Object.Instantiate(template.gameObject, escapeMenu);
        clone.name = buttonName;
        int siblingIndex = after != null ? after.GetSiblingIndex() + 1 : template.GetSiblingIndex() + 1;
        clone.transform.SetSiblingIndex(siblingIndex);
        clone.SetActive(true);

        RemoveLocalizers(clone);
        RemoveStoreUI(clone);

        TextMeshProUGUI label = clone.GetComponentInChildren<TextMeshProUGUI>(true);
        if (label != null)
        {
            label.text = idleText;
            label.enableWordWrapping = false;
            label.overflowMode = TextOverflowModes.Overflow;
        }

        Button button = clone.GetComponent<Button>();
        if (button != null)
        {
            button.onClick = new Button.ButtonClickedEvent();
            if (onClick != null)
            {
                button.onClick.AddListener(delegate { onClick(); });
            }
        }

        PauseButtonFeedback feedback = clone.AddComponent<PauseButtonFeedback>();
        feedback.Initialize(label, idleText);
        return clone.transform;
    }

    private static void DestroyButton(Transform escapeMenu, string buttonName)
    {
        Transform button = escapeMenu.Find(buttonName);
        if (button != null)
        {
            UnityEngine.Object.Destroy(button.gameObject);
        }
    }

    private static void OnSaveClicked(EscapeUIManager manager)
    {
        try
        {
            PlayClick(manager);

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

    private static void OnLoadClicked(EscapeUIManager manager)
    {
        try
        {
            PlayClick(manager);

            if (!ModEntry.Enabled)
            {
                return;
            }

            PauseButtonFeedback feedback = FindFeedback(LoadButtonName);

            if (!ManualSaveController.HasManualSaveForCurrentRun())
            {
                if (feedback != null)
                {
                    feedback.ShowNoQuicksave();
                }

                Notify("No quicksave found for this run.", "sec");
                return;
            }

            bool loaded = ManualSaveController.LoadManual(out string message, allowLatestFallback: false);
            if (loaded)
            {
                if (feedback != null)
                {
                    feedback.ShowText("Loading...", 2f);
                }

                Notify(message, "default");
            }
            else
            {
                if (feedback != null)
                {
                    feedback.ShowLoadFailed();
                }

                Notify(message, "sec");
            }
        }
        catch (Exception ex)
        {
            if (ModEntry.Log != null)
            {
                ModEntry.Log.Warning("Pause-menu load failed: " + ex);
            }
        }
    }

    private static void PlayClick(EscapeUIManager manager)
    {
        if (manager == null)
        {
            return;
        }

        try
        {
            manager.OnAnyClick();
        }
        catch
        {
            // A missing click sound must not block the action.
        }
    }

    private static PauseButtonFeedback FindFeedback(string buttonName)
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

            Transform button = escapeMenu.Find(buttonName);
            return button != null ? button.GetComponent<PauseButtonFeedback>() : null;
        }
        catch
        {
            return null;
        }
    }

    private static void EnsurePanelHeight(Transform escapeMenu, int targetSteps)
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

        if (marker.HeightSteps < 0)
        {
            marker.HeightSteps = 0;
        }

        if (marker.HeightSteps >= targetSteps)
        {
            return;
        }

        int add = targetSteps - marker.HeightSteps;
        rect.sizeDelta = new Vector2(
            rect.sizeDelta.x,
            rect.sizeDelta.y + add * PanelHeightIncrease);
        marker.HeightSteps = targetSteps;
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
                // TMPLocalizer depends on LocalizeStringEvent, so destroying
                // the event component logs a dependency error. Disabling both
                // is enough: they can no longer overwrite the button label.
                behaviour.enabled = false;
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
