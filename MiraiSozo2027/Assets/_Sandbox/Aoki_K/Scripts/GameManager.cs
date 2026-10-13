using UnityEngine;
using UnityEngine.InputSystem;

public class GameManager : MonoBehaviour
{
    public static GameManager Instance { get; private set; }

    [Header("UI Reference")]
    [Tooltip("ポーズメニューのCanvasやパネル")]
    [SerializeField] private GameObject _menuUI;

    private InputSystem_Actions _actions;
    private bool _isPaused = false;

    public bool IsPaused => _isPaused;

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
        _actions = new InputSystem_Actions();
    }

    private void OnEnable()
    {
        _actions.Enable();
    }

    private void OnDisable()
    {
        _actions.Disable();
    }

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        
    }

    // Update is called once per frame
    void Update()
    {
        if(_actions.All.Pause.triggered)
        {
            TogglePause();
        }
    }

    public void TogglePause()
    {
        _isPaused = !_isPaused;

        if(_isPaused)
        {
            //一時停止
            Time.timeScale = 0f;

            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;

            // プレイヤー動作制約
            _actions.Player.Disable();
            _actions.UI.Enable();

            if(_menuUI != null)
            {
                _menuUI.SetActive(true);
            }
        }
        else
        {
            // 再開
            Time.timeScale = 1f;

            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;

            // プレイヤー動作再開
            _actions.Player.Enable();
            _actions.UI.Disable();

            if (_menuUI != null)
            {
                _menuUI.SetActive(false);
            }
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
