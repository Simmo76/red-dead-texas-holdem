using CinematicPoker.Engine.Poker;
using CinematicPoker.Game.Presentation;
using UnityEngine;
using UnityEngine.UI;

namespace CinematicPoker.Game.UI
{
    /// <summary>
    /// Contextual action buttons: Fold / Check / Call / Bet / Raise / All-In.
    /// Only legal actions are shown, straight from the engine's LegalActions —
    /// the UI never computes legality itself.
    /// </summary>
    public sealed class ActionPanel : MonoBehaviour
    {
        [SerializeField] private PokerTableController controller;
        [SerializeField] private Button foldButton;
        [SerializeField] private Button checkButton;
        [SerializeField] private Button callButton;
        [SerializeField] private Button betButton;
        [SerializeField] private Button raiseButton;
        [SerializeField] private Button allInButton;
        [SerializeField] private Text callAmountLabel;
        [SerializeField] private BetSlider betSlider;
        [SerializeField] private CanvasGroup group;

        private LegalActions _legal;

        private void OnEnable()
        {
            if (controller != null)
                controller.HumanTurnStarted += Show;

            foldButton?.onClick.AddListener(() => Submit(PlayerAction.Fold()));
            checkButton?.onClick.AddListener(() => Submit(PlayerAction.Check()));
            callButton?.onClick.AddListener(() => Submit(PlayerAction.Call()));
            betButton?.onClick.AddListener(OpenBetPanel);
            raiseButton?.onClick.AddListener(OpenBetPanel);
            allInButton?.onClick.AddListener(() => Submit(PlayerAction.AllIn()));
        }

        private void OnDisable()
        {
            if (controller != null)
                controller.HumanTurnStarted -= Show;
        }

        private void Show(LegalActions legal)
        {
            _legal = legal;
            SetVisible(true);

            foldButton?.gameObject.SetActive(legal.CanFold);
            checkButton?.gameObject.SetActive(legal.CanCheck);
            callButton?.gameObject.SetActive(legal.CanCall);
            betButton?.gameObject.SetActive(legal.CanBet);
            raiseButton?.gameObject.SetActive(legal.CanRaise);
            allInButton?.gameObject.SetActive(legal.CanAllIn);

            if (callAmountLabel != null)
                callAmountLabel.text = legal.CanCall ? legal.CallAmount.ToString() : string.Empty;
        }

        private void OpenBetPanel()
        {
            if (betSlider != null && _legal != null)
                betSlider.Open(_legal, OnBetConfirmed);
        }

        private void OnBetConfirmed(long toAmount)
        {
            if (_legal == null) return;
            Submit(toAmount >= _legal.MaxBetOrRaiseTo
                ? PlayerAction.AllIn()
                : _legal.CanBet ? PlayerAction.Bet(toAmount) : PlayerAction.Raise(toAmount));
        }

        private void Submit(PlayerAction action)
        {
            SetVisible(false);
            controller.SubmitHumanAction(action);
        }

        private void SetVisible(bool visible)
        {
            if (group == null) return;
            group.alpha = visible ? 1f : 0f;
            group.interactable = visible;
            group.blocksRaycasts = visible;
        }
    }
}
