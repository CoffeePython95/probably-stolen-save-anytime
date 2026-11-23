using System.Collections;
using TMPro;
using UnityEngine;

namespace SaveAnytime;

/// <summary>
/// Shows temporary feedback on the pause-menu save button.
/// </summary>
public sealed class SaveButtonFeedback : MonoBehaviour
{
    private TextMeshProUGUI label;
    private string idleText = "Save Game";
    private Coroutine routine;

    internal void Initialize(TextMeshProUGUI label, string idleText)
    {
        this.label = label;
        this.idleText = string.IsNullOrEmpty(idleText) ? "Save Game" : idleText;
        ResetLabel();
    }

    internal void ResetLabel()
    {
        if (routine != null)
        {
            StopCoroutine(routine);
            routine = null;
        }

        if (label != null)
        {
            label.text = idleText;
        }
    }

    internal void ShowSaved()
    {
        Show("Saved!", 1.5f);
    }

    internal void ShowFailed()
    {
        Show("Save failed", 2f);
    }

    private void Show(string text, float seconds)
    {
        if (!isActiveAndEnabled || label == null)
        {
            return;
        }

        if (routine != null)
        {
            StopCoroutine(routine);
        }

        routine = StartCoroutine(ShowRoutine(text, seconds));
    }

    private IEnumerator ShowRoutine(string text, float seconds)
    {
        label.text = text;
        yield return new WaitForSecondsRealtime(seconds);
        label.text = idleText;
        routine = null;
    }

    private void OnDisable()
    {
        if (routine != null)
        {
            StopCoroutine(routine);
            routine = null;
        }
    }
}
