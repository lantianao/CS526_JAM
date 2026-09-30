using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class JAMKillCounter : MonoBehaviour
{
    public TextMeshProUGUI killText; // drag a UI Text object here
    private int kills = 0;

    void Start()
    {
        // Find every enemy already in the scene and listen for its death.
        // (This only catches enemies present when the level starts -- if you
        // later spawn more during play, those would need to call
        // RegisterEnemy() below when they're created.)
        foreach (JAMEnemy enemy in FindObjectsByType<JAMEnemy>(FindObjectsSortMode.None))
        {
            RegisterEnemy(enemy);
        }

        UpdateText();
    }

    public void RegisterEnemy(JAMEnemy enemy)
    {
        // Read JAMHealth straight off the GameObject instead of going
        // through enemy.Health -- that property just returns a field that
        // JAMEnemy itself only sets in ITS OWN Start(), which might not have
        // run yet at this point (script Start() order isn't guaranteed).
        // GetComponent works immediately regardless of that ordering.
        JAMHealth health = enemy.GetComponent<JAMHealth>();
        if (health != null)
        {
            health.Died += OnEnemyDied;
        }
    }

    private void OnEnemyDied(JAMHealth h)
    {
        kills++;
        UpdateText();
    }

    private void UpdateText()
    {
        // JAMHealth invokes the Died event BEFORE it destroys the enemy --
        // if this throws (e.g. killText never got assigned), it would abort
        // that Destroy() call too, leaving the enemy stuck alive. Guard against it.
        if (killText == null)
        {
            Debug.LogWarning("JAMKillCounter: Kill Text isn't assigned in the Inspector.");
            return;
        }

        killText.text = "Enemies killed: " + kills;
    }
}