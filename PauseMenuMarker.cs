using UnityEngine;

namespace SaveAnytime;

/// <summary>
/// Marks the pause-menu panel and tracks how many 50px button slots this mod
/// has added, so the height can be restored exactly when the mod is disabled.
/// </summary>
public sealed class PauseMenuMarker : MonoBehaviour
{
    internal int HeightSteps;
}
