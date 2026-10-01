using UnityEngine;

public class Goal : MonoBehaviour
{

    private bool reachGoal = false;
    public GameObject winPanel;
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
    }


    private void OnTriggerEnter2D(Collider2D other)
    {
        if (reachGoal)
        {
            return;
        }

        // Check if is the player who entered. Two ways to qualify, because during a
        // possession the player's own body is hidden with its collider disabled and
        // the thing walking around is an enemy carrying no JAMPlayerController.
        Transform controlled = JAMPlayerController.ControlledTransform;
        bool isPlayer = other.GetComponentInParent<JAMPlayerController>() != null
                     || (controlled != null && other.transform.IsChildOf(controlled));
        if (!isPlayer)
        {
            return;
        }
        
        reachGoal = true;
        winPanel.SetActive(true);
        Time.timeScale = 0f;
    }

    // Update is called once per frame
    void Update()
    {
        
    }
}
