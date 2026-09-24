using UnityEngine;

namespace CinematicPoker.Game.Presentation
{
    /// <summary>
    /// A pooled stack of chips. Chip visuals change per environment (ChipStyle)
    /// but always map to the same logical value system in the engine.
    /// </summary>
    public sealed class ChipView : MonoBehaviour
    {
        public long Value { get; private set; }

        public void SetValue(long value)
        {
            Value = value;
            // Stack height/denomination visuals resolved by the table view.
            float height = Mathf.Clamp(Mathf.Log10(Mathf.Max(1, value)) * 0.01f, 0.005f, 0.08f);
            transform.localScale = new Vector3(1f, height * 100f, 1f);
        }
    }
}
