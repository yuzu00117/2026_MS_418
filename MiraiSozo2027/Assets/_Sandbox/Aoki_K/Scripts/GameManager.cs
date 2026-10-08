using UnityEngine;

public class GameManager : MonoBehaviour
{
    public static GameManager Instance { get; private set; }

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }
        else
        {
            Destroy(gameObject);
        }
    }

    private bool _isGamePaused = false;

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        
    }

    // Update is called once per frame
    void Update()
    {
        if(_isGamePaused)
        {
            Time.timeScale = 0f; // ゲームを一時停止
        }
        else
        {
            Time.timeScale = 1f; // ゲームを再開
        }
    }

    public void TogglePause()
    {
        _isGamePaused = !_isGamePaused;
    }

    public void ResumePause()
    {
        _isGamePaused = false;
    }
}
