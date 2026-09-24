using System.Collections.Generic;
using CinematicPoker.Engine.Poker;
using UnityEngine;

namespace CinematicPoker.Game.Presentation
{
    /// <summary>
    /// Renders the physical table: hole cards, board, pot and bet chips.
    /// Subscribes to engine events via PokerTableController and uses simple
    /// object pools for cards and chips (mobile performance budget: no bulk
    /// Instantiate/Destroy during play).
    /// </summary>
    public sealed class PokerTableView : MonoBehaviour
    {
        [SerializeField] private PokerTableController controller;
        [SerializeField] private CardView cardPrefab;
        [SerializeField] private ChipView chipPrefab;
        [SerializeField] private Transform[] seatCardAnchors;
        [SerializeField] private Transform[] boardAnchors;
        [SerializeField] private Transform potAnchor;
        [SerializeField] private int humanSeat;

        private readonly Stack<CardView> _cardPool = new Stack<CardView>();
        private readonly Stack<ChipView> _chipPool = new Stack<ChipView>();
        private readonly List<CardView> _activeCards = new List<CardView>();
        private readonly Dictionary<int, int> _seatCardCounts = new Dictionary<int, int>();
        private int _boardCount;

        private void OnEnable()
        {
            if (controller != null)
                controller.EngineEvent += OnEngineEvent;
        }

        private void OnDisable()
        {
            if (controller != null)
                controller.EngineEvent -= OnEngineEvent;
        }

        private void OnEngineEvent(PokerEvent evt)
        {
            switch (evt)
            {
                case HandStarted _:
                    ClearTable();
                    break;
                case CardDealt cd:
                    SpawnHoleCard(cd.Seat, cd.Card);
                    break;
                case FlopDealt f:
                    SpawnBoardCard(f.Card1);
                    SpawnBoardCard(f.Card2);
                    SpawnBoardCard(f.Card3);
                    break;
                case TurnDealt t:
                    SpawnBoardCard(t.Card);
                    break;
                case RiverDealt r:
                    SpawnBoardCard(r.Card);
                    break;
                case ShowdownStarted sd:
                    foreach (var kv in sd.RevealedHands)
                        RevealSeat(kv.Key);
                    break;
            }
        }

        private void SpawnHoleCard(int seat, Card card)
        {
            if (seat >= seatCardAnchors.Length || seatCardAnchors[seat] == null) return;

            _seatCardCounts.TryGetValue(seat, out int count);
            _seatCardCounts[seat] = count + 1;

            CardView view = Rent();
            view.Bind(card, null);
            view.SetRestPosition(seatCardAnchors[seat].position + Vector3.right * (count * 0.07f));
            // Only the human sees their own cards.
            view.SetFaceUp(seat == humanSeat);
        }

        private void SpawnBoardCard(Card card)
        {
            if (_boardCount >= boardAnchors.Length) return;
            CardView view = Rent();
            view.Bind(card, null);
            view.SetRestPosition(boardAnchors[_boardCount++].position);
            view.SetFaceUp(true);
        }

        private void RevealSeat(int seat)
        {
            foreach (CardView card in _activeCards)
                card.SetFaceUp(true); // refined later to per-seat reveal
        }

        private CardView Rent()
        {
            CardView view = _cardPool.Count > 0 ? _cardPool.Pop() : Instantiate(cardPrefab, transform);
            view.gameObject.SetActive(true);
            _activeCards.Add(view);
            return view;
        }

        private void ClearTable()
        {
            foreach (CardView card in _activeCards)
            {
                card.gameObject.SetActive(false);
                _cardPool.Push(card);
            }
            _activeCards.Clear();
            _seatCardCounts.Clear();
            _boardCount = 0;
        }
    }
}
