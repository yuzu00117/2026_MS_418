using UnityEngine;
using UnityEngine.InputSystem;

public class GameManager : MonoBehaviour
{
    public static GameManager Instance { get; private set; }

    [Header("UI Reference")]
    [Tooltip("ポーズメニューのCanvasやパネル")]
    [SerializeField] private GameObject _menuUI;

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
            Actions?.Dispose();
            Actions = null;
        }
    }

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        
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
