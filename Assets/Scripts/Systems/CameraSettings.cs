using System;
using UnityEngine;

namespace CyberVeil.Systems
{
    /// <summary>
    /// Persistent, runtime-safe camera preferences shared by the settings menu
    /// and every gameplay camera.
    /// </summary>
    public static class CameraSettings
    {
        public const float MinimumSensitivity = 0.005f;
        public const float MaximumSensitivity = 0.15f;
        public const float SensitivityStep = 0.005f;
        public const float DefaultSensitivity = 0.04f;

        private const string SensitivityKey = "CyberVeil.Camera.MouseSensitivity";
        private const string InvertYKey = "CyberVeil.Camera.InvertY";
        private const string SmoothingKey = "CyberVeil.Camera.MouseSmoothing";

        private static bool loaded;
        private static float sensitivity;
        private static bool invertY;
        private static bool smoothingEnabled;

        public static event Action Changed;

        public static float Sensitivity
        {
            get
            {
                EnsureLoaded();
                return sensitivity;
            }
        }

        public static bool InvertY
        {
            get
            {
                EnsureLoaded();
                return invertY;
            }
        }

        public static bool SmoothingEnabled
        {
            get
            {
                EnsureLoaded();
                return smoothingEnabled;
            }
        }

        public static int SensitivityPercent
        {
            get
            {
                EnsureLoaded();
                return Mathf.RoundToInt(Mathf.InverseLerp(
                    MinimumSensitivity,
                    MaximumSensitivity,
                    sensitivity) * 100f);
            }
        }

        public static void SetSensitivity(float value)
        {
            EnsureLoaded();
            float rounded = Mathf.Round(value / SensitivityStep) * SensitivityStep;
            float clamped = Mathf.Clamp(rounded, MinimumSensitivity, MaximumSensitivity);
            if (Mathf.Approximately(sensitivity, clamped))
                return;

            sensitivity = clamped;
            PlayerPrefs.SetFloat(SensitivityKey, sensitivity);
            SaveAndNotify();
        }

        public static void SetInvertY(bool value)
        {
            EnsureLoaded();
            if (invertY == value)
                return;

            invertY = value;
            PlayerPrefs.SetInt(InvertYKey, invertY ? 1 : 0);
            SaveAndNotify();
        }

        public static void SetSmoothingEnabled(bool value)
        {
            EnsureLoaded();
            if (smoothingEnabled == value)
                return;

            smoothingEnabled = value;
            PlayerPrefs.SetInt(SmoothingKey, smoothingEnabled ? 1 : 0);
            SaveAndNotify();
        }

        public static void Reload()
        {
            loaded = false;
            EnsureLoaded();
            Changed?.Invoke();
        }

        private static void EnsureLoaded()
        {
            if (loaded)
                return;

            sensitivity = Mathf.Clamp(
                PlayerPrefs.GetFloat(SensitivityKey, DefaultSensitivity),
                MinimumSensitivity,
                MaximumSensitivity);
            invertY = PlayerPrefs.GetInt(InvertYKey, 0) != 0;
            smoothingEnabled = PlayerPrefs.GetInt(SmoothingKey, 1) != 0;
            loaded = true;
        }

        private static void SaveAndNotify()
        {
            PlayerPrefs.Save();
            Changed?.Invoke();
        }
    }
}
