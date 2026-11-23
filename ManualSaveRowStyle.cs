using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace SaveAnytime;

/// <summary>
/// Applies a contrasting colour to the manual-save row in the load menu.
/// The manual entry is always the first history row, but matching the date
/// text makes the lookup safe if the menu rebuilds an old expanded box.
/// </summary>
internal static class ManualSaveRowStyle
{
    private static readonly Color RowBackground = new Color(1f, 0.72f, 0.18f, 0.30f);
    private static readonly Color InfoColor = new Color(1f, 0.82f, 0.35f, 1f);
    private static readonly Color DateColor = new Color(1f, 0.90f, 0.62f, 1f);
    private static readonly Color RuleColor = new Color(1f, 0.72f, 0.18f, 0.65f);
    private static readonly Color ButtonBackground = new Color(0.36f, 0.24f, 0.07f, 1f);

    internal static void Apply(Transform windowRoot, SaveHistoryEntry entry)
    {
        if (windowRoot == null || entry == null)
        {
            return;
        }

        string expectedDate = entry.saveDateTime.ToString("dd/MM/yyyy - HH:mm:ss");
        Transform[] all = windowRoot.GetComponentsInChildren<Transform>(true);
        for (int i = 0; i < all.Length; i++)
        {
            Transform expanded = all[i];
            if (expanded.name != "Expanded")
            {
                continue;
            }

            Transform rows = expanded.Find("Rows");
            if (rows == null)
            {
                continue;
            }

            for (int r = 0; r < rows.childCount; r++)
            {
                Transform row = rows.GetChild(r);
                if (row.name != "Row")
                {
                    continue;
                }

                Transform date = row.Find("Date");
                TextMeshProUGUI dateText = date != null ? date.GetComponent<TextMeshProUGUI>() : null;
                if (dateText == null || dateText.text != expectedDate)
                {
                    continue;
                }

                ApplyRow(row);
            }
        }
    }

    private static void ApplyRow(Transform row)
    {
        Image background = row.GetComponent<Image>();
        if (background == null)
        {
            background = row.gameObject.AddComponent<Image>();
        }

        background.color = RowBackground;
        background.raycastTarget = false;

        ColorText(row.Find("Info"), InfoColor);
        ColorText(row.Find("Date"), DateColor);

        Transform rule = row.Find("Rule");
        if (rule != null)
        {
            Image ruleImage = rule.GetComponent<Image>();
            if (ruleImage != null)
            {
                ruleImage.color = RuleColor;
            }
        }

        Transform load = row.Find("Load");
        if (load != null)
        {
            Image loadBackground = load.GetComponent<Image>();
            if (loadBackground != null)
            {
                loadBackground.color = ButtonBackground;
            }
        }
    }

    private static void ColorText(Transform target, Color color)
    {
        if (target == null)
        {
            return;
        }

        TextMeshProUGUI text = target.GetComponent<TextMeshProUGUI>();
        if (text != null)
        {
            text.color = color;
        }
    }
}
