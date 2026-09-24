using System;
using CinematicPoker.Engine.Poker;
using UnityEngine;
using UnityEngine.UI;

namespace CinematicPoker.Game.UI
{
    /// <summary>
    /// Raise sizing panel: slider plus 1/2 Pot, Pot and All-In shortcuts,
    /// with a Confirm button. Sizes are clamped to the engine's legal range.
    /// </summary>
    public sealed class BetSlider : MonoBehaviour
    {
        [SerializeField] private Slider slider;
        [SerializeField] private Text amountLabel;
        [SerializeField] private Button halfPotButton;
        [SerializeField] private Button potButton;
        [SerializeField] private Button allInButton;
        [SerializeField] private Button confirmButton;
        [SerializeField] private Button cancelButton;

        private LegalActions _legal;
        private Action<long> _onConfirm;
        private long _potSize;

        /// <summary>Pot size is provided by the HUD for shortcut sizing.</summary>
        public void SetPotSize(long pot) => _potSize = pot;

        public void Open(LegalActions legal, Action<long> onConfirm)
        {
            _legal = legal;
            _onConfirm = onConfirm;
            gameObject.SetActive(true);

            slider.minValue = legal.MinBetOrRaiseTo;
            slider.maxValue = legal.MaxBetOrRaiseTo;
            slider.value = legal.MinBetOrRaiseTo;
            slider.onValueChanged.AddListener(_ => UpdateLabel());

            halfPotButton?.onClick.AddListener(() => SetAmount(_potSize / 2));
            potButton?.onClick.AddListener(() => SetAmount(_potSize));
            allInButton?.onClick.AddListener(() => SetAmount(legal.MaxBetOrRaiseTo));
            confirmButton?.onClick.AddListener(Confirm);
            cancelButton?.onClick.AddListener(Close);

            UpdateLabel();
        }

        private void SetAmount(long amount)
        {
            slider.value = Mathf.Clamp(amount, slider.minValue, slider.maxValue);
            UpdateLabel();
        }

        private void UpdateLabel()
        {
            if (amountLabel != null)
                amountLabel.text = ((long)slider.value).ToString();
        }

        private void Confirm()
        {
            long amount = (long)slider.value;
            Close();
            _onConfirm?.Invoke(amount);
        }

        private void Close()
        {
            slider.onValueChanged.RemoveAllListeners();
            halfPotButton?.onClick.RemoveAllListeners();
            potButton?.onClick.RemoveAllListeners();
            allInButton?.onClick.RemoveAllListeners();
            confirmButton?.onClick.RemoveAllListeners();
            cancelButton?.onClick.RemoveAllListeners();
            gameObject.SetActive(false);
        }
    }
}
