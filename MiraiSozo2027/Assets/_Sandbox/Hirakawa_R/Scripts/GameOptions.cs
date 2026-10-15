using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;

namespace MiraiSozo.Options
{
    [Serializable]
    public sealed class OptionValues
    {
        public int frameRate = 60;
        public float brightness;
        public float fieldOfView = 60f;
        public float sensitivityX = 0.2f;
        public float sensitivityY = 0.2f;
        public bool invertX;
        public bool invertY;
        public float shakeReduction;
        public float masterVolume = 1f;
        public float seVolume = 1f;
        public float bgmVolume = 1f;
        public float voiceVolume = 1f;

        public void Sanitize()
        {
            if (frameRate != -1 && frameRate != 30 && frameRate != 60 && frameRate != 120 && frameRate != 144)
                frameRate = 60;
            brightness = Clamp(brightness, -2f, 2f, 0f);
            fieldOfView = Clamp(fieldOfView, 40f, 100f, 60f);
            sensitivityX = Clamp(sensitivityX, 0.02f, 1f, 0.2f);
            sensitivityY = Clamp(sensitivityY, 0.02f, 1f, 0.2f);
            shakeReduction = Clamp(shakeReduction, 0f, 1f, 0f);
            masterVolume = Clamp(masterVolume, 0f, 1f, 1f);
            seVolume = Clamp(seVolume, 0f, 1f, 1f);
            bgmVolume = Clamp(bgmVolume, 0f, 1f, 1f);
            voiceVolume = Clamp(voiceVolume, 0f, 1f, 1f);
        }

        private static float Clamp(float value, float min, float max, float fallback)
        {
            return float.IsNaN(value) || float.IsInfinity(value) ? fallback : Mathf.Clamp(value, min, max);
        }
    }

    /// <summary>設定の保存と反映。メニュー用Prefabと一緒に各ゲームシーンへ配置できる。</summary>
    [DefaultExecutionOrder(-500)]
    public sealed class GameOptions : MonoBehaviour
    {
        public const string SettingsKey = "Hirakawa.Options.v1";
        public const string BindingsKey = "Hirakawa.Options.Bindings.v1";
        public static GameOptions Instance { get; private set; }
        public OptionValues Values { get; private set; }
        public event Action Changed;

        private readonly Dictionary<Camera, CameraState> _cameras = new Dictionary<Camera, CameraState>();
        private OptionValues _defaults;
        private InputActionAsset _boundActions;
        private VolumeProfile _profile;
        private ColorAdjustments _exposure;
        private float _previousMaster;
        private int _previousFrameRate;
        private int _previousVSync;

        private struct CameraState
        {
            public float FieldOfView;
            public bool PostProcessing;
        }

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
            DontDestroyOnLoad(gameObject);
            if (OptionGameManager.Instance == null)
            {
                var managerObject = new GameObject("OptionGameManager");
                managerObject.AddComponent<OptionGameManager>();
                managerObject.transform.SetParent(transform, false);
            }

            _previousFrameRate = Application.targetFrameRate;
            _previousVSync = QualitySettings.vSyncCount;
            _previousMaster = AudioListener.volume;
            _defaults = new OptionValues();
            var cameraBehavior = FindAnyObjectByType<OptionCameraBehavior>();
            if (cameraBehavior != null)
            {
                _defaults.sensitivityX = cameraBehavior.SensitivityX;
                _defaults.sensitivityY = cameraBehavior.SensitivityY;
                _defaults.invertX = cameraBehavior.InvertX;
                _defaults.invertY = cameraBehavior.InvertY;
            }
            if (Camera.main != null) _defaults.fieldOfView = Camera.main.fieldOfView;
            Values = JsonUtility.FromJson<OptionValues>(JsonUtility.ToJson(_defaults));
            try
            {
                if (PlayerPrefs.HasKey(SettingsKey))
                    JsonUtility.FromJsonOverwrite(PlayerPrefs.GetString(SettingsKey), Values);
            }
            catch (Exception e)
            {
                Debug.LogWarning("オプション設定を読み込めないため初期値を使用します: " + e.Message);
                Values = JsonUtility.FromJson<OptionValues>(JsonUtility.ToJson(_defaults));
            }

            var volumeObject = new GameObject("Option Brightness (Exposure)");
            volumeObject.transform.SetParent(transform, false);
            var volume = volumeObject.AddComponent<Volume>();
            volume.isGlobal = true;
            volume.priority = 10000f;
            _profile = ScriptableObject.CreateInstance<VolumeProfile>();
            volume.sharedProfile = _profile;
            _exposure = _profile.Add<ColorAdjustments>(true);
            // 明度以外のカラー調整は既存Volumeから継承する。
            _exposure.contrast.overrideState = false;
            _exposure.colorFilter.overrideState = false;
            _exposure.hueShift.overrideState = false;
            _exposure.saturation.overrideState = false;
            SceneManager.sceneLoaded += OnSceneLoaded;
            Apply();
            ApplyBindings();
        }

        private void Update()
        {
            // GameManagerが後から生成・交換された場合も保存したキー設定を反映する。
            ApplyBindings();
        }

        public void Apply()
        {
            Values.Sanitize();
            QualitySettings.vSyncCount = 0;
            Application.targetFrameRate = Values.frameRate;
            AudioListener.volume = Values.masterVolume;
            _exposure.postExposure.Override(Values.brightness);
            ApplyCameras();
            Changed?.Invoke();
        }

        public void Save()
        {
            PlayerPrefs.SetString(SettingsKey, JsonUtility.ToJson(Values));
            PlayerPrefs.Save();
        }

        public void SaveBindings()
        {
            if (OptionGameManager.Actions == null) return;
            PlayerPrefs.SetString(BindingsKey, OptionGameManager.Actions.asset.SaveBindingOverridesAsJson());
            PlayerPrefs.Save();
        }

        public void ResetTab(int tab)
        {
            switch (tab)
            {
                case 0:
                    Values.frameRate = _defaults.frameRate;
                    Values.brightness = _defaults.brightness;
                    Values.fieldOfView = _defaults.fieldOfView;
                    break;
                case 1:
                    Values.sensitivityX = _defaults.sensitivityX;
                    Values.sensitivityY = _defaults.sensitivityY;
                    Values.invertX = _defaults.invertX;
                    Values.invertY = _defaults.invertY;
                    Values.shakeReduction = _defaults.shakeReduction;
                    break;
                case 2:
                    Values.masterVolume = Values.seVolume = Values.bgmVolume = Values.voiceVolume = 1f;
                    break;
                case 3:
                    OptionGameManager.Actions?.asset.RemoveAllBindingOverrides();
                    SaveBindings();
                    break;
            }
            Apply();
            Save();
        }

        private void ApplyBindings()
        {
            var asset = OptionGameManager.Actions?.asset;
            if (asset == null || asset == _boundActions) return;
            _boundActions = asset;
            try
            {
                if (PlayerPrefs.HasKey(BindingsKey))
                    asset.LoadBindingOverridesFromJson(PlayerPrefs.GetString(BindingsKey));
            }
            catch (Exception e)
            {
                asset.RemoveAllBindingOverrides();
                Debug.LogWarning("キー設定を読み込めないため初期配置を使用します: " + e.Message);
            }
        }

        private void OnSceneLoaded(Scene scene, LoadSceneMode mode) => ApplyCameras();

        private void ApplyCameras()
        {
            foreach (var behavior in FindObjectsByType<OptionCameraBehavior>())
                behavior.ApplyOptions(Values.sensitivityX, Values.sensitivityY, Values.invertX, Values.invertY);

            // UI・ミニマップ用カメラの投影設定は変更しない。
            foreach (var camera in FindObjectsByType<Camera>())
            {
                if (camera.orthographic || (!camera.CompareTag("MainCamera") && camera.GetComponent<OptionCameraBehavior>() == null))
                    continue;
                var additional = camera.GetUniversalAdditionalCameraData();
                if (!_cameras.ContainsKey(camera))
                    _cameras.Add(camera, new CameraState { FieldOfView = camera.fieldOfView, PostProcessing = additional.renderPostProcessing });
                camera.fieldOfView = Values.fieldOfView;
                additional.renderPostProcessing = true;
            }
        }

        private void OnApplicationPause(bool paused) { if (paused && Instance == this) Save(); }
        private void OnApplicationQuit() { if (Instance == this) Save(); }

        private void OnDestroy()
        {
            if (Instance != this) return;
            Save();
            SceneManager.sceneLoaded -= OnSceneLoaded;
            Application.targetFrameRate = _previousFrameRate;
            QualitySettings.vSyncCount = _previousVSync;
            AudioListener.volume = _previousMaster;
            foreach (var entry in _cameras)
            {
                if (entry.Key == null) continue;
                entry.Key.fieldOfView = entry.Value.FieldOfView;
                entry.Key.GetUniversalAdditionalCameraData().renderPostProcessing = entry.Value.PostProcessing;
            }
            if (_profile != null) Destroy(_profile);
            Instance = null;
        }
    }
}
