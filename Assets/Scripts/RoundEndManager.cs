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
        else
        {
            return "Round " + GameManager.Instance.roundNumber + " Failed!";
        }
    }
}
