using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

public class GameManager : MonoBehaviour
{
    public static GameManager Instance { get; private set; }

    [Header("UI Reference")]
    [Tooltip("ポーズメニューのCanvasやパネル")]
    [SerializeField] private GameObject _menuUI;

    [Header("Player")]
    [Tooltip("プレイヤーのプレハブ")]
    [SerializeField] private GameObject _playerPrefab;

    private bool _isPaused = false;

    public bool IsPaused => _isPaused;

    //ゲームで使用する用のInput
    public static InputSystem_Actions Actions { get; private set; }

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
            Actions = new InputSystem_Actions();
            Actions.Enable();
            SceneManager.sceneLoaded += OnSceneLoaded;
        }
        else
        {
            Destroy(gameObject);
            return;
        }

    }

    private void OnDestroy()
    {
        if(Instance == this)
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
            Actions?.Dispose();
            Actions = null;
        }
    }

    private void TrySpawnPlayer()
    {
        //プレイヤースタートを検索
        var starts = Object.FindObjectsByType<PlayerStart>(FindObjectsSortMode.None);
        if(starts.Length == 0) return;

        if(PlayerController.Instance != null) return;

        if(_playerPrefab == null)
        {
            Debug.LogError("PlayerPrefabが設定されていません");
            return;
        }

        PlayerStart firstStart = starts[0];
        Instantiate(_playerPrefab, firstStart.transform.position, firstStart.transform.rotation);
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        TrySpawnPlayer();
    }

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        TrySpawnPlayer();
    }

    // Update is called once per frame
    void Update()
    {
        if(Actions.All.Pause.triggered)
        {
            TogglePause();
        }
    }

    public void TogglePause()
    {
        _isPaused = !_isPaused;

        Time.timeScale = _isPaused ? 0f : 1f;
        Cursor.lockState = _isPaused ? CursorLockMode.None : CursorLockMode.Locked;
        Cursor.visible = _isPaused;

        if(_menuUI != null)
        {
            _menuUI.SetActive(_isPaused);
        }

        if(_isPaused)
        {
            Actions.Player.Disable();
            Actions.UI.Enable();
        }
        else
        {
            Actions.Player.Enable();
            Actions.UI.Disable();
        }
    }

    public void ResumePause()
    {
        if(_isPaused)
        {
            TogglePause();
        }
    }
}
