using System.Collections.Generic;
using UnityEngine;

public enum JAMAbility
{
    Teleport = 1,
    TimeFreeze = 2,
    Possession = 3
}

/// <summary>
/// The player's skill economy: skill cheese comes in, ability unlocks go out.
///
/// Reaching each threshold grants one unlock, and the player decides where to
/// spend it. There is deliberately no separate choice UI -- an ability's own key
/// doubles as its unlock button, so pressing 2 on a locked Time Freeze buys it.
/// JAMPlayerController.UseAbility routes the press.
///
/// Put this on the Player. With no such component present, JAMPlayerController
/// treats every ability as unlocked, which keeps test scenes playable.
/// </summary>
public class JAMSkillProgression : MonoBehaviour
{
    [Tooltip("Cumulative skill cheese counts. Reaching each one grants a single unlock, spendable on any ability the player has not bought yet.")]
    public int[] unlockThresholds = { 1, 3, 7 };

    [Tooltip("Draw the placeholder progress readout. Turn off once there is real UI.")]
    public bool showOverlay = true;

    public int Collected { get; private set; }
    public int UnlocksSpent { get; private set; }

    /// <summary>Unlocks earned by cheese but not yet assigned to an ability.</summary>
    public int PendingUnlocks => EarnedUnlocks - UnlocksSpent;

    private static readonly JAMAbility[] AllAbilities =
    {
        JAMAbility.Teleport,
        JAMAbility.TimeFreeze,
        JAMAbility.Possession
    };

    private readonly HashSet<JAMAbility> unlocked = new HashSet<JAMAbility>();

    private int EarnedUnlocks
    {
        get
        {
            int earned = 0;
            foreach (int threshold in unlockThresholds)
            {
                if (Collected >= threshold) earned++;
            }
            return earned;
        }
    }

    public bool IsUnlocked(JAMAbility ability) => unlocked.Contains(ability);

    public void Collect(int amount)
    {
        if (amount <= 0) return;
        Collected += amount;
    }

    /// <summary>Spends a pending unlock on <paramref name="ability"/>.</summary>
    /// <returns>True if it was actually unlocked by this call.</returns>
    public bool TryUnlock(JAMAbility ability)
    {
        if (PendingUnlocks <= 0 || unlocked.Contains(ability)) return false;

        unlocked.Add(ability);
        UnlocksSpent++;
        return true;
    }

    public static string NameOf(JAMAbility ability)
    {
        switch (ability)
        {
            case JAMAbility.Teleport:   return "Teleport";
            case JAMAbility.TimeFreeze: return "Time Freeze";
            case JAMAbility.Possession: return "Possession";
            default:                    return ability.ToString();
        }
    }

    // Placeholder readout so the mechanic is testable before any real UI exists.
    private void OnGUI()
    {
        if (!showOverlay) return;

        GUILayout.BeginArea(new Rect(12f, 12f, 260f, 132f), GUI.skin.box);

        GUILayout.Label($"Skill cheese: {Collected}");

        int pending = PendingUnlocks;
        GUILayout.Label(pending > 0
            ? $"Unlocks to spend: {pending}  -- press 1 / 2 / 3"
            : "Unlocks to spend: 0");

        foreach (JAMAbility ability in AllAbilities)
        {
            string state = IsUnlocked(ability)
                ? "ready"
                : pending > 0 ? "press to unlock" : "locked";
            GUILayout.Label($"[{(int)ability}] {NameOf(ability)} - {state}");
        }

        GUILayout.EndArea();
    }
}
