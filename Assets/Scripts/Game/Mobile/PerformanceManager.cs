using UnityEngine;

namespace CinematicPoker.Game.Mobile
{
    public enum QualityMode
    {
        /// <summary>30 FPS, reduced resolution — long flights, low battery.</summary>
        BatterySaver,
        /// <summary>45/60 FPS where supported.</summary>
        Balanced,
        /// <summary>60 FPS.</summary>
        High,
        /// <summary>Capable tablets/phones only.</summary>
        Ultra
    }

    /// <summary>
    /// Applies quality modes: target frame rate, render scale and quality level.
    /// Sustained thermal performance matters more than short benchmarks, so a
    /// future pass will also monitor Device thermal APIs and step down.
    /// </summary>
    public sealed class PerformanceManager : MonoBehaviour
    {
        [SerializeField] private QualityMode mode = QualityMode.Balanced;

        public QualityMode Mode
        {
            get => mode;
            set { mode = value; Apply(); }
        }

        private void Start() => Apply();

        private void Apply()
        {
            switch (mode)
            {
                case QualityMode.BatterySaver:
                    Application.targetFrameRate = 30;
                    QualitySettings.SetQualityLevel(0, applyExpensiveChanges: false);
                    break;
                case QualityMode.Balanced:
                    Application.targetFrameRate = 60;
                    QualitySettings.SetQualityLevel(1, applyExpensiveChanges: false);
                    break;
                case QualityMode.High:
                    Application.targetFrameRate = 60;
                    QualitySettings.SetQualityLevel(2, applyExpensiveChanges: false);
                    break;
                case QualityMode.Ultra:
                    Application.targetFrameRate = Mathf.Max(60, Screen.currentResolution.refreshRateRatio.numerator > 0
                        ? (int)Screen.currentResolution.refreshRateRatio.value
                        : 60);
                    QualitySettings.SetQualityLevel(3, applyExpensiveChanges: false);
                    break;
            }
        }
    }
}
