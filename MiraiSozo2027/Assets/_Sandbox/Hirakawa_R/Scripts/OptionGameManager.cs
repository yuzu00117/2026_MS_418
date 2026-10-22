using System;
using UnityEngine;
using UnityEngine.InputSystem;

namespace MiraiSozo.Options
{
    public class OptionGameManager : MonoBehaviour
    {
        public static OptionGameManager Instance { get; private set; }

        [Header("UI Reference")]
        [Tooltip("ポーズメニューのCanvasやパネル")]
        [SerializeField] private GameObject _menuUI;

        private bool _isPaused = false;
        private float _resumeTimeScale;
        private CursorLockMode _resumeCursorLock;
        private bool _resumeCursorVisible;
        private bool _resumePlayerEnabled;
        private bool _resumeUIEnabled;

        public bool IsPaused => _isPaused;

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
                SetPaused(false);
                Actions?.Disable();
                Actions?.Dispose();
                Actions = null;
                ActionsChanged?.Invoke();
                Instance = null;
            }
        }

        private void Update()
        {
            if(Actions.All.Pause.triggered)
            {
                TogglePause();
            }
        }

        public void TogglePause()
        {
            if (InGameOptionMenu.Instance != null)
            {
                InGameOptionMenu.Instance.HandlePause();
                return;
            }
            SetPaused(!_isPaused);
        }

        public void SetPaused(bool paused)
        {
            if (_isPaused == paused || Actions == null) return;
            if (paused) CacheResumeState();
            _isPaused = paused;

            Time.timeScale = _isPaused ? 0f : _resumeTimeScale;
            Cursor.lockState = _isPaused ? CursorLockMode.None : _resumeCursorLock;
            Cursor.visible = _isPaused || _resumeCursorVisible;

            if(_menuUI != null)
            {
                _menuUI.SetActive(_isPaused && InGameOptionMenu.Instance == null);
            }

            ApplyPausedInput();
        }

        private void CacheResumeState()
        {
            _resumeTimeScale = Time.timeScale;
            _resumeCursorLock = Cursor.lockState;
            _resumeCursorVisible = Cursor.visible;
            _resumePlayerEnabled = Actions.Player.enabled;
            _resumeUIEnabled = Actions.UI.enabled;
        }

        private void ApplyPausedInput()
        {
            if(_isPaused)
            {
                Actions.Player.Disable();
                Actions.UI.Enable();
            }
            else
            {
                if (_resumePlayerEnabled) Actions.Player.Enable(); else Actions.Player.Disable();
                if (_resumeUIEnabled) Actions.UI.Enable(); else Actions.UI.Disable();
            }
        }

        public void ResumePause()
        {
            if (InGameOptionMenu.Instance != null)
            {
                InGameOptionMenu.Instance.CloseMenu();
                return;
            }
            if(_isPaused)
            {
                TogglePause();
            }
        }
    }
}
