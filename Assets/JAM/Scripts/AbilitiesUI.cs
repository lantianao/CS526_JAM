using UnityEngine;
using TMPro;

public class JAMAbilityIcon : MonoBehaviour
{
    public JAMAbility ability; // pick which ability this text represents, in the Inspector

    public TextMeshProUGUI statusText; // Reference to the TextMeshProUGUI component
    private JAMSkillProgression skills;
    private JAMSkill skill;

    void Start()
    {
        statusText = GetComponent<TextMeshProUGUI>();
        if (statusText == null)
        {
            Debug.LogError("JAMAbilityIcon script requires a TextMeshProUGUI component on the same GameObject.");
            return;
        }

        skills = FindFirstObjectByType<JAMSkillProgression>();
        skill = FindFirstObjectByType<JAMSkill>();

        if (skills == null || skill == null)
        {
            Debug.LogError("JAMAbilityIcon couldn't find JAMSkillProgression or JAMSkill in the scene.");
        }
    }

    void Update()
    {
        if (statusText == null || skills == null || skill == null) return;

        string name = JAMSkillProgression.NameOf(ability);

        if (skills.IsUnlocked(ability))
        {
            float cooldown = skill.GetCooldownRemaining(ability);
            statusText.text = cooldown > 0f
                ? $"{name}\nCooldown: {cooldown:0.0}s"
                : $"{name}\nReady";
        }
        else if (skills.PendingUnlocks > 0)
        {
            // The player has an unlock token to spend -- pressing this ability's
            // own key is what spends it, same as JAMSkillProgression's own comment says.
            statusText.text = $"{name}\nPress {(int)ability} to unlock!";
        }
        else
        {
            int needed = CollectiblesNeededForNextUnlock();
            statusText.text = $"{name}\nLocked - {needed} more to unlock";
        }
    }

    private int CollectiblesNeededForNextUnlock()
    {
        foreach (int threshold in skills.unlockThresholds)
        {
            if (skills.Collected < threshold)
            {
                return threshold - skills.Collected;
            }
        }
        return 0; // every threshold already reached
    }
}