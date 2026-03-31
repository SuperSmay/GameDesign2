using System.Linq;
using UnityEngine;
using UnityEngine.UI;

public class UIController : MonoBehaviour
{

    [SerializeField] TMPro.TextMeshProUGUI scoreText;
    [SerializeField] TMPro.TextMeshProUGUI roundText;
    [SerializeField] TMPro.TextMeshProUGUI timerText;
    [SerializeField] TMPro.TextMeshProUGUI livesText;
    [SerializeField] Button nextTextButton;
    [SerializeField] Image preambleImage;
    [SerializeField] TMPro.TextMeshProUGUI preambleText;

    string[] preambles;
    int currentPreambleIndex = 0;
    string[] failureMessages;
    string[] successMessages;
    string[] allLivesLostMessages;
    int endMessageIndex = 0;
    public bool showingPreamble = false;
    public bool showingEndMessage = false;

    public void OnNextButtonClicked()
    {
        if (showingPreamble)
        {
            ShowNextPreamble();
        }
        else if (showingEndMessage)
        {
            ShowNextEndMessage();
        }
    }

    public void ShowNextPreamble()
    {
        if (currentPreambleIndex < preambles.Length)
        {
            // Don't need to pause here - GameManager will pause if showingPreamble is true in its Update loop
            showingPreamble = true;
            preambleImage.gameObject.SetActive(true);
            preambleText.text = preambles[currentPreambleIndex];
            currentPreambleIndex++;
        }
        else
        {
            HidePreamble();
        }
    }

    public void HidePreamble()
    {
        showingPreamble = false;
        preambleImage.gameObject.SetActive(false);
        // We do have to manually unpause the game though
        GameManager.Instance.paused = false;
    }

    public void ShowNextEndMessage()
    {
        if (GameManager.Instance.roundSuccessful)
        {
            if (endMessageIndex < successMessages.Length)
            {
                preambleImage.gameObject.SetActive(true);
                preambleText.text = successMessages[endMessageIndex];
                endMessageIndex++;
            } else
            {
                GameManager.Instance.CloseRound();
            }
        }
        else if (GameManager.Instance.allowedMistakes <= 0)
        {
            if (endMessageIndex < allLivesLostMessages.Length)
            {
                preambleImage.gameObject.SetActive(true);
                preambleText.text = allLivesLostMessages[endMessageIndex];
                endMessageIndex++;
            } else
            {
                GameManager.Instance.CloseRound();
            }
        }
        else
        {
            if (endMessageIndex < failureMessages.Length)
            {
                preambleImage.gameObject.SetActive(true);
                preambleText.text = failureMessages[endMessageIndex];
                endMessageIndex++;
            } else
            {
                GameManager.Instance.CloseRound();
            }
        }
        showingEndMessage = true;
        
    }

    public void InsertStatsIntoEndMessages()
    {
        failureMessages = failureMessages.Append(BuildStatsMessage()).ToArray();
        successMessages = successMessages.Append(BuildStatsMessage()).ToArray();
        allLivesLostMessages = allLivesLostMessages.Append(BuildStatsMessage()).ToArray();
    }

    string BuildStatsMessage()
    {
        string statsMessage = "Your score was: " + GameManager.Instance.Score + "\n";
        statsMessage += "You had " + GameManager.Instance.allowedMistakes + " lives remaining\n";
        foreach (var entry in GameManager.Instance.DeviantBehaviorCounts)
        {
            switch (entry.Key)
            {
                case DeviantType.speeding:
                    statsMessage += "Speeding cars missed: " + entry.Value + "\n";
                    break;
                case DeviantType.runsStop:
                    statsMessage += "Stop sign runners missed: " + entry.Value + "\n";
                    break;
                case DeviantType.swerving:
                    statsMessage += "Swerving cars missed: " + entry.Value + "\n";
                    break;
                case DeviantType.tailgating:
                    statsMessage += "Tailgating cars missed: " + entry.Value + "\n";
                    break;
            }
        }
        return statsMessage;
    }

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        roundText.text = "Round " + GameManager.Instance.roundNumber;
        if (!GameManager.Instance.rounds[GameManager.Instance.roundNumber - 1].useTimer)
        {
            timerText.gameObject.SetActive(false);
        }
        if (GameManager.Instance.rounds[GameManager.Instance.roundNumber - 1].isTutorial)
        {
            livesText.gameObject.SetActive(false);
            scoreText.gameObject.SetActive(false);
        }
    }

    // Update is called once per frame
    void Update()
    {
        timerText.text = "Time: " + Mathf.Max(0, GameManager.Instance.gameDuration - GameManager.Instance.gameTimer).ToString("F1");
        scoreText.text = "Score: " + GameManager.Instance.Score;
        livesText.text = "Lives remaining: " + GameManager.Instance.allowedMistakes;
    }

    public void Initialize(RoundConfig roundConfig)
    {
        preambles = roundConfig.preambles;
        failureMessages = roundConfig.failureMessages;
        successMessages = roundConfig.successMessages;
        allLivesLostMessages = roundConfig.allLivesLostMessages;
        ShowNextPreamble();
    }
}
