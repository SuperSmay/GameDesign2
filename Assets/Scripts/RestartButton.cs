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
        GameManager.Instance.roundNumber += 1; // Increment round number
        UnityEngine.SceneManagement.SceneManager.LoadScene("MainScene");
    }
}
