using UnityEngine;
using UnityEngine.InputSystem;
using System;
using UnityEngine.SceneManagement;
using MiraiSozo.Options;
using System.Linq;

[DefaultExecutionOrder(-600)]
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

    //ポーズ前の状態保存
    private float _resumeTimeScale = 1f;
    private CursorLockMode _resumeCursorLock = CursorLockMode.Locked;
    private bool _resumeCursorVisible = false;
    private bool _resumePlayerEnabled = true;

    //ゲームで使用する用のInput
    public static InputSystem_Actions Actions { get; private set; }

    public static event Action ActionsChanged;

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
            Actions = new InputSystem_Actions();
            Actions.Enable();
            ActionsChanged?.Invoke();
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
            Actions?.Disable();
            Actions?.Dispose();
            Actions = null;
            ActionsChanged?.Invoke();
            Instance = null;
        }
    }

    private void TrySpawnPlayer()
    {
        //プレイヤースタートを検索
        var starts = GameObject.FindObjectsByType<PlayerStart>(FindObjectsSortMode.None);
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
        if(Actions == null) return;
        
        // ポーズ入力の検出(Actions.All.Pause)
        if(Actions.All.Pause.triggered)
        {
            if(!_isPaused)
            {
                SetPaused(true);
            }
            else
            {
                //ポーズ中ならInGameOptionMenuにCancel処理を委譲
                if(InGameOptionMenu.Instance != null && InGameOptionMenu.Instance.IsOpen)
                {
                    InGameOptionMenu.Instance.Back();
                }
                else
                {
                    SetPaused(false);
                }
            }
        }
    }

    public void SetPaused(bool paused)
    {
        if(_isPaused == paused || Actions == null) return;

        if (paused)
        {
            _resumeTimeScale = Time.timeScale;
            _resumeCursorLock = Cursor.lockState;
            _resumeCursorVisible = Cursor.visible;
            _resumePlayerEnabled = Actions.Player.enabled;

            _isPaused = true;
            Time.timeScale = 0f;
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;

            Actions.Player.Disable();
            Actions.UI.Enable();

            if(InGameOptionMenu.Instance != null)
            {//UI表示
                InGameOptionMenu.Instance.OpenMenu();
            }
        }
        else
        {
            _isPaused = false;
            Time.timeScale = _resumeTimeScale;
            Cursor.lockState = _resumeCursorLock;
            Cursor.visible = _resumeCursorVisible;

            if(_resumePlayerEnabled) Actions.Player.Enable();
            Actions.UI.Disable();

            //UIの非表示
            if(InGameOptionMenu.Instance != null && InGameOptionMenu.Instance.IsOpen)
            {
                InGameOptionMenu.Instance.CloseMenu();
            }
            if(_menuUI != null)
            {
                if(_menuUI != null) _menuUI.SetActive(false);
            }

        }
    }

    public void TogglePause() => SetPaused(!_isPaused);
    public void ResumePause() => SetPaused(false);
}
