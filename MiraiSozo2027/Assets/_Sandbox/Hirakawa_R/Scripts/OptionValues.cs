using System;
using System.Collections.Generic;
using UnityEngine;

namespace MiraiSozo.Options
{
    [Serializable]
    public sealed class OptionValues
    {
        public const int MinimumFrameRate = 30;
        public const int DefaultFrameRate = 60;
        public const int HighFrameRate = 120;
        public const int MaximumFrameRate = 144;
        public const int UnlimitedFrameRate = -1;
        public const float MinimumBrightness = -2f;
        public const float MaximumBrightness = 2f;
        public const float DefaultBrightness = 0f;
        public const float MinimumFieldOfView = 40f;
        public const float MaximumFieldOfView = 100f;
        public const float DefaultFieldOfView = 60f;
        public const float MinimumSensitivity = 0.02f;
        public const float MaximumSensitivity = 1f;
        public const float DefaultSensitivity = 0.2f;
        public const float MinimumNormalizedValue = 0f;
        public const float MaximumNormalizedValue = 1f;
        public static readonly IReadOnlyList<int> FrameRates = Array.AsReadOnly(new[]
        {
            MinimumFrameRate, DefaultFrameRate, HighFrameRate, MaximumFrameRate, UnlimitedFrameRate
        });

        // JsonUtilityの保存用データ。既存の保存JSONとの互換性のためフィールド名を維持する。
        public int frameRate = DefaultFrameRate;
        public float brightness = DefaultBrightness;
        public float fieldOfView = DefaultFieldOfView;
        public float sensitivityX = DefaultSensitivity;
        public float sensitivityY = DefaultSensitivity;
        public bool invertX;
        public bool invertY;
        public float shakeReduction;
        public float masterVolume = MaximumNormalizedValue;
        public float seVolume = MaximumNormalizedValue;
        public float bgmVolume = MaximumNormalizedValue;
        public float voiceVolume = MaximumNormalizedValue;

        public void Sanitize()
        {
            bool supported = false;
            foreach (int candidate in FrameRates) supported |= frameRate == candidate;
            if (!supported) frameRate = DefaultFrameRate;
            brightness = Clamp(brightness, MinimumBrightness, MaximumBrightness, DefaultBrightness);
            fieldOfView = Clamp(fieldOfView, MinimumFieldOfView, MaximumFieldOfView, DefaultFieldOfView);
            sensitivityX = Clamp(sensitivityX, MinimumSensitivity, MaximumSensitivity, DefaultSensitivity);
            sensitivityY = Clamp(sensitivityY, MinimumSensitivity, MaximumSensitivity, DefaultSensitivity);
            shakeReduction = Clamp(shakeReduction, MinimumNormalizedValue, MaximumNormalizedValue, MinimumNormalizedValue);
            masterVolume = Clamp(masterVolume, MinimumNormalizedValue, MaximumNormalizedValue, MaximumNormalizedValue);
            seVolume = Clamp(seVolume, MinimumNormalizedValue, MaximumNormalizedValue, MaximumNormalizedValue);
            bgmVolume = Clamp(bgmVolume, MinimumNormalizedValue, MaximumNormalizedValue, MaximumNormalizedValue);
            voiceVolume = Clamp(voiceVolume, MinimumNormalizedValue, MaximumNormalizedValue, MaximumNormalizedValue);
        }

        private static float Clamp(float value, float min, float max, float fallback)
        {
            return float.IsNaN(value) || float.IsInfinity(value) ? fallback : Mathf.Clamp(value, min, max);
        }
    }
}
