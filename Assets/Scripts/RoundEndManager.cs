using System;
using Unity.VisualScripting;
using UnityEngine;

public class RoundEndManager : MonoBehaviour
{

    [SerializeField] TMPro.TextMeshProUGUI roundEndText;
    [SerializeField] TMPro.TextMeshProUGUI scoreText;

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        roundEndText.text = "Round " + GameManager.Instance.roundNumber + " Over!";
        scoreText.text = "Your score was: " + GameManager.Instance.Score;
    }

    // Update is called once per frame
    void Update()
    {
        
    }
}
