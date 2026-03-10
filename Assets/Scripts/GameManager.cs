using UnityEngine;

public class GameManager : MonoBehaviour
{

    public static GameManager Instance { get; private set; }

    public int Score = 0;
    public int roundNumber = 1;

    private void Awake()
    {
        Instance = this;
        DontDestroyOnLoad(gameObject); // Persist across scene loads
    }

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        
    }

    // Update is called once per frame
    void Update()
    {
        
    }
}
