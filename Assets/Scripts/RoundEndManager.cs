using System;
using Unity.VisualScripting;
using UnityEngine;

public class RoundEndManager : MonoBehaviour
{

    [SerializeField] TMPro.TextMeshProUGUI roundEndText;
    [SerializeField] TMPro.TextMeshProUGUI scoreText;
    [SerializeField] TMPro.TextMeshProUGUI buttonText;

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        roundEndText.text = GetRoundEndMessage();
        buttonText.text = GetButtonText();
        scoreText.text = "Your score was: " + GameManager.Instance.Score;
    }

    string GetButtonText()
    {
        if (GameManager.Instance.roundSuccessful)
        {
            return "Next";
        }
        // If the round doesn't use a timer and doesn't have a spawn order, it's an infinite round and shouldn't count as a fail.
        else if (!GameManager.Instance.rounds[GameManager.Instance.roundNumber - 1].useTimer && GameManager.Instance.rounds[GameManager.Instance.roundNumber - 1].spawnOrder.Length == 0) 
        {
            return "Again?";
        }
        else
        {
            return "Retry";
        }
    }

    string GetRoundEndMessage()
    {
        if (GameManager.Instance.roundSuccessful)
        {
            return "Round " + GameManager.Instance.roundNumber + " Complete!";
        }
        // If the round doesn't use a timer and doesn't have a spawn order, it's an infinite round and shouldn't count as a fail.
        else if (!GameManager.Instance.rounds[GameManager.Instance.roundNumber - 1].useTimer && GameManager.Instance.rounds[GameManager.Instance.roundNumber - 1].spawnOrder.Length == 0) 
        {
            return "Endless " + GameManager.Instance.roundNumber + " Complete!";
        }
        else
        {
            return "Round " + GameManager.Instance.roundNumber + " Failed!";
        }
    }
}
