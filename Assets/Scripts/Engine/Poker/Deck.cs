using System;
using System.Collections.Generic;

namespace CinematicPoker.Engine.Poker
{
    public interface IDeck
    {
        int Remaining { get; }
        Card Draw();
    }

    /// <summary>
    /// A standard 52-card deck with Fisher-Yates shuffle and optional
    /// deterministic seeding for reproducible tests and bug reports.
    /// </summary>
    public sealed class Deck : IDeck
    {
        private readonly List<Card> _cards = new List<Card>(52);
        private readonly Random _rng;
        private int _next;

        public Deck() : this(new Random()) { }

        public Deck(int seed) : this(new Random(seed)) { }

        public Deck(Random rng)
        {
            _rng = rng ?? throw new ArgumentNullException(nameof(rng));
            Reset();
        }

        public int Remaining => _cards.Count - _next;

        /// <summary>All 52 cards in current order (dealt and undealt). Exposed for tests.</summary>
        public IReadOnlyList<Card> Cards => _cards;

        public void Reset()
        {
            _cards.Clear();
            for (int i = 0; i < 52; i++)
                _cards.Add(Card.FromIndex(i));
            _next = 0;
        }

        /// <summary>Fisher-Yates shuffle over the full deck.</summary>
        public void Shuffle()
        {
            for (int i = _cards.Count - 1; i > 0; i--)
            {
                int j = _rng.Next(i + 1);
                (_cards[i], _cards[j]) = (_cards[j], _cards[i]);
            }
            _next = 0;
        }

        public Card Draw()
        {
            if (_next >= _cards.Count)
                throw new InvalidOperationException("Deck is empty.");
            return _cards[_next++];
        }
    }

    /// <summary>
    /// A deck with a predetermined card order, used by tests to rig
    /// specific hole cards and board runouts. Once the rigged cards run
    /// out, the remaining unseen cards are dealt in index order so tests
    /// only need to specify the cards they care about.
    /// </summary>
    public sealed class StackedDeck : IDeck
    {
        private readonly Queue<Card> _rigged;
        private readonly HashSet<int> _used = new HashSet<int>();
        private int _fallbackCursor;

        public StackedDeck(IEnumerable<Card> topToBottom)
        {
            _rigged = new Queue<Card>(topToBottom);
            foreach (Card card in _rigged)
            {
                if (!_used.Add(card.Index))
                    throw new ArgumentException($"Duplicate card {card} in stacked deck.");
            }
        }

        public static StackedDeck Parse(params string[] cards)
        {
            var list = new List<Card>();
            foreach (string c in cards)
                list.Add(Card.Parse(c));
            return new StackedDeck(list);
        }

        public int Remaining
        {
            get
            {
                int unseen = 0;
                for (int i = _fallbackCursor; i < 52; i++)
                    if (!_used.Contains(i)) unseen++;
                return _rigged.Count + unseen;
            }
        }

        public Card Draw()
        {
            if (_rigged.Count > 0)
                return _rigged.Dequeue();

            while (_fallbackCursor < 52)
            {
                int index = _fallbackCursor++;
                if (!_used.Contains(index))
                {
                    _used.Add(index);
                    return Card.FromIndex(index);
                }
            }

            throw new InvalidOperationException("StackedDeck is empty.");
        }
    }
}
