using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;

namespace MiraiSozo.Options
{
    [DefaultExecutionOrder(-500)]
    public sealed class GameOptions : MonoBehaviour
    {
        private const float BrightnessVolumePriority = 10000f;
        public const string SettingsKey = "Hirakawa.Options.v1";
        public const string BindingsKey = "Hirakawa.Options.Bindings.v1";
        public static GameOptions Instance { get; private set; }
        public static event Action<GameOptions> InstanceChanged;
        public OptionValues Values { get; private set; }
        public event Action Changed;
        public event Action AudioChanged;

        private readonly Dictionary<Camera, CameraState> _cameras = new Dictionary<Camera, CameraState>();
        private readonly List<OptionCameraBehavior> _cameraBehaviors = new List<OptionCameraBehavior>();
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
            public UniversalAdditionalCameraData Data;
        }

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
            DontDestroyOnLoad(gameObject);
            EnsureOptionManager();
            _previousFrameRate = Application.targetFrameRate;
            _previousVSync = QualitySettings.vSyncCount;
            _previousMaster = AudioListener.volume;
            LoadValues();
            CreateBrightnessVolume();
            CacheSceneTargets();
            SceneManager.sceneLoaded += OnSceneLoaded;
            OptionGameManager.ActionsChanged += ApplyBindings;
            Apply();
            ApplyBindings();
            InstanceChanged?.Invoke(this);
        }

        private void EnsureOptionManager()
        {
            if (OptionGameManager.Instance != null) return;
            var managerObject = new GameObject("OptionGameManager");
            managerObject.AddComponent<OptionGameManager>();
            managerObject.transform.SetParent(transform, false);
        }

        private void LoadValues()
        {
            _defaults = new OptionValues();
            var behavior = FindAnyObjectByType<OptionCameraBehavior>();
            if (behavior != null)
            {
                _defaults.sensitivityX = behavior.SensitivityX;
                _defaults.sensitivityY = behavior.SensitivityY;
                _defaults.invertX = behavior.InvertX;
                _defaults.invertY = behavior.InvertY;
            }
            var mainCamera = Camera.main;
            if (mainCamera != null) _defaults.fieldOfView = mainCamera.fieldOfView;
            Values = JsonUtility.FromJson<OptionValues>(JsonUtility.ToJson(_defaults));
            try
            {
                if (PlayerPrefs.HasKey(SettingsKey)) JsonUtility.FromJsonOverwrite(PlayerPrefs.GetString(SettingsKey), Values);
            }
            catch (Exception e)
            {
                Debug.LogWarning("オプション設定を読み込めないため初期値を使用します: " + e.Message);
                Values = JsonUtility.FromJson<OptionValues>(JsonUtility.ToJson(_defaults));
            }
        }

        private void CreateBrightnessVolume()
        {
            var volumeObject = new GameObject("Option Brightness (Exposure)");
            volumeObject.transform.SetParent(transform, false);
            var volume = volumeObject.AddComponent<Volume>();
            volume.isGlobal = true;
            volume.priority = BrightnessVolumePriority;
            _profile = ScriptableObject.CreateInstance<VolumeProfile>();
            volume.sharedProfile = _profile;
            _exposure = _profile.Add<ColorAdjustments>(true);
            // シーン固有の色調整を維持し、明度だけを操作する。
            _exposure.contrast.overrideState = false;
            _exposure.colorFilter.overrideState = false;
            _exposure.hueShift.overrideState = false;
            _exposure.saturation.overrideState = false;
        }

        public void Apply(OptionSettingsCategory category = OptionSettingsCategory.All)
        {
            Values.Sanitize();
            if ((category & OptionSettingsCategory.System) != 0) ApplySystemSettings();
            if ((category & OptionSettingsCategory.Camera) != 0) ApplyCameraSettings();
            if ((category & OptionSettingsCategory.Audio) != 0)
            {
                AudioListener.volume = Values.masterVolume;
                AudioChanged?.Invoke();
            }
            Changed?.Invoke();
        }

        private void ApplySystemSettings()
        {
            QualitySettings.vSyncCount = 0;
            Application.targetFrameRate = Values.frameRate;
            _exposure.postExposure.Override(Values.brightness);
            foreach (var entry in _cameras)
            {
                if (entry.Key == null) continue;
                entry.Key.fieldOfView = Values.fieldOfView;
                if (entry.Value.Data != null) entry.Value.Data.renderPostProcessing = true;
            }
        }

        private void ApplyCameraSettings()
        {
            foreach (var behavior in _cameraBehaviors)
                if (behavior != null) behavior.ApplyOptions(Values.sensitivityX, Values.sensitivityY, Values.invertX, Values.invertY);
        }

        public bool Save() => TrySave(SettingsKey, () => JsonUtility.ToJson(Values));

        public bool SaveBindings()
        {
            if (OptionGameManager.Actions == null) return false;
            return TrySave(BindingsKey, () => OptionGameManager.Actions.asset.SaveBindingOverridesAsJson());
        }

        private static bool TrySave(string key, Func<string> serialize)
        {
            try
            {
                PlayerPrefs.SetString(key, serialize());
                PlayerPrefs.Save();
                return true;
            }
            catch (Exception e)
            {
                Debug.LogWarning("オプション設定を保存できませんでした: " + e.Message);
                return false;
            }
        }

        public bool ResetTab(int tab)
        {
            OptionSettingsCategory category;
            switch (tab)
            {
                case OptionMenuLayout.SystemTab:
                    ResetSystemValues();
                    category = OptionSettingsCategory.System;
                    break;
                case OptionMenuLayout.CameraTab:
                    ResetCameraValues();
                    category = OptionSettingsCategory.Camera;
                    break;
                case OptionMenuLayout.AudioTab:
                    Values.masterVolume = Values.seVolume = Values.bgmVolume = Values.voiceVolume = OptionValues.MaximumNormalizedValue;
                    category = OptionSettingsCategory.Audio;
                    break;
                case OptionMenuLayout.ControlsTab:
                    OptionGameManager.Actions?.asset.RemoveAllBindingOverrides();
                    return SaveBindings();
                default: return false;
            }
            Apply(category);
            return Save();
        }

        private void ResetSystemValues()
        {
            Values.frameRate = _defaults.frameRate;
            Values.brightness = _defaults.brightness;
            Values.fieldOfView = _defaults.fieldOfView;
        }

        private void ResetCameraValues()
        {
            Values.sensitivityX = _defaults.sensitivityX;
            Values.sensitivityY = _defaults.sensitivityY;
            Values.invertX = _defaults.invertX;
            Values.invertY = _defaults.invertY;
            Values.shakeReduction = _defaults.shakeReduction;
        }

        private void ApplyBindings()
        {
            var asset = OptionGameManager.Actions?.asset;
            if (asset == _boundActions) return;
            _boundActions = asset;
            if (asset == null) return;
            try
            {
                if (PlayerPrefs.HasKey(BindingsKey)) asset.LoadBindingOverridesFromJson(PlayerPrefs.GetString(BindingsKey));
            }
            catch (Exception e)
            {
                asset.RemoveAllBindingOverrides();
                Debug.LogWarning("キー設定を読み込めないため初期配置を使用します: " + e.Message);
            }
        }

        private void OnSceneLoaded(Scene scene, LoadSceneMode mode) => RefreshSceneTargets();

        public void RefreshSceneTargets()
        {
            CacheSceneTargets();
            Apply(OptionSettingsCategory.System | OptionSettingsCategory.Camera);
        }

        private void CacheSceneTargets()
        {
            var removed = new List<Camera>();
            foreach (var entry in _cameras) if (entry.Key == null) removed.Add(entry.Key);
            foreach (var camera in removed) _cameras.Remove(camera);
            _cameraBehaviors.Clear();
            _cameraBehaviors.AddRange(FindObjectsByType<OptionCameraBehavior>());
            foreach (var camera in FindObjectsByType<Camera>())
            {
                if (camera.orthographic || (!camera.CompareTag("MainCamera") && camera.GetComponent<OptionCameraBehavior>() == null)) continue;
                if (_cameras.ContainsKey(camera)) continue;
                var data = camera.GetUniversalAdditionalCameraData();
                _cameras.Add(camera, new CameraState { FieldOfView = camera.fieldOfView, PostProcessing = data.renderPostProcessing, Data = data });
            }
        }

        private void OnApplicationPause(bool paused) { if (paused && Instance == this) Save(); }
        private void OnApplicationQuit() { if (Instance == this) Save(); }

        private void OnDestroy()
        {
            if (Instance != this) return;
            try { Save(); }
            finally
            {
                SceneManager.sceneLoaded -= OnSceneLoaded;
                OptionGameManager.ActionsChanged -= ApplyBindings;
                RestoreEnvironment();
                Instance = null;
                InstanceChanged?.Invoke(null);
            }
        }

        private void RestoreEnvironment()
        {
            Application.targetFrameRate = _previousFrameRate;
            QualitySettings.vSyncCount = _previousVSync;
            AudioListener.volume = _previousMaster;
            foreach (var entry in _cameras)
            {
                if (entry.Key == null) continue;
                entry.Key.fieldOfView = entry.Value.FieldOfView;
                if (entry.Value.Data != null) entry.Value.Data.renderPostProcessing = entry.Value.PostProcessing;
            }
            _cameras.Clear();
            _cameraBehaviors.Clear();
            if (_profile != null) Destroy(_profile);
        }
    }
}
