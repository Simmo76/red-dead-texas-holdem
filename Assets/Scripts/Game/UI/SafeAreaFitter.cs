using UnityEngine;

namespace CinematicPoker.Game.UI
{
    /// <summary>
    /// Fits a RectTransform to the device safe area: iPhone notches and
    /// Dynamic Island, Android cutouts and navigation bars, all aspect ratios
    /// from 4:3 tablets to 20:9 phones. Landscape-only game.
    /// </summary>
    [RequireComponent(typeof(RectTransform))]
    public sealed class SafeAreaFitter : MonoBehaviour
    {
        private Rect _applied;

        private void OnEnable() => Apply();

        private void Update()
        {
            if (Screen.safeArea != _applied)
                Apply();
        }

        private void Apply()
        {
            Rect safeArea = Screen.safeArea;
            _applied = safeArea;

            var rect = (RectTransform)transform;
            Vector2 anchorMin = safeArea.position;
            Vector2 anchorMax = safeArea.position + safeArea.size;
            anchorMin.x /= Screen.width;
            anchorMin.y /= Screen.height;
            anchorMax.x /= Screen.width;
            anchorMax.y /= Screen.height;

            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }
    }
}
