using UnityEngine;

public class RestartButton : MonoBehaviour
{
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        
    }

    // Update is called once per frame
    void Update()
    {
        
    }

    public void HandleButtonClick()
    {
        // Start the game switching scene
        GameManager.Instance.Score = 0; // Reset score
        GameManager.Instance.roundNumber += GameManager.Instance.roundSuccessful ? 1 : 0; // Increment round number
        GameManager.Instance.roundSuccessful = false; // Reset round success for the next round
        UnityEngine.SceneManagement.SceneManager.LoadScene("MainScene");
        GameManager.Instance.StartRound(); // Start the next round
    }
}
