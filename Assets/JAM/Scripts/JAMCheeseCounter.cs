using TMPro;
using UnityEngine;

/// <summary>
/// Drives the "remaining" readout in the HUD. Replaces the template's
/// UpdateCollectibleCount, which counted Collectible2D pickups -- a type this game
/// no longer uses, so it always read zero.
///
/// Put it on the same object as the TextMeshProUGUI. The count comes from
/// JAMSkillCheese.Remaining, so picking a wheel up updates it the same frame.
/// </summary>
[RequireComponent(typeof(TextMeshProUGUI))]
public class JAMCheeseCounter : MonoBehaviour
{
    [Tooltip("Shown before the number. The count is appended to it.")]
    public string label = "Skill cheese remaining: ";

    private TextMeshProUGUI text;
    private int lastShown = -1;

    private void Awake()
    {
        text = GetComponent<TextMeshProUGUI>();
    }

    private void Update()
    {
        // Rebuilding a TMP mesh is not free, so only touch it when the number moves.
        int remaining = JAMSkillCheese.Remaining;
        if (remaining == lastShown) return;

        lastShown = remaining;
        text.text = label + remaining;
    }
}
