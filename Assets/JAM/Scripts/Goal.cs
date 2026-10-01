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

        // check if is the player who entered
        if (other.GetComponent<JAMPlayerController>() == null) 
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
