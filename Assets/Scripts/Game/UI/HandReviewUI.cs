using System.Text;
using CinematicPoker.Game.Coaching;
using UnityEngine;
using UnityEngine.UI;

namespace CinematicPoker.Game.UI
{
    /// <summary>
    /// Post-hand "REVIEW HAND" screen for Improve My Game: hole cards, board,
    /// pot, bet faced, estimated equity, pot odds and the "What Would They
    /// Have Done?" personality comparison. Never shown in "Just Play".
    /// </summary>
    public sealed class HandReviewUI : MonoBehaviour
    {
        [SerializeField] private Text summaryLabel;
        [SerializeField] private Text comparisonLabel;
        [SerializeField] private Button closeButton;

        private void Awake()
        {
            closeButton?.onClick.AddListener(() => gameObject.SetActive(false));
        }

        public void Show(DecisionAnalysis analysis)
        {
            gameObject.SetActive(true);

            if (summaryLabel != null)
            {
                var sb = new StringBuilder();
                sb.Append("You: ");
                foreach (var c in analysis.HoleCards) sb.Append(c).Append(' ');
                sb.AppendLine();
                sb.Append("Board: ");
                foreach (var c in analysis.Board) sb.Append(c).Append(' ');
                sb.AppendLine();
                sb.AppendLine($"Pot: {analysis.Pot}");
                if (analysis.BetFaced > 0)
                    sb.AppendLine($"Bet faced: {analysis.BetFaced}");
                sb.AppendLine($"Estimated equity: {analysis.Equity:P0}");
                if (analysis.BetFaced > 0)
                    sb.AppendLine($"Pot odds: {analysis.PotOdds:P0}");
                summaryLabel.text = sb.ToString();
            }

            if (comparisonLabel != null)
            {
                var sb = new StringBuilder();
                foreach (var kv in analysis.PersonalityComparisons)
                    sb.AppendLine($"{kv.Key}:\n{kv.Value}\n");
                comparisonLabel.text = sb.ToString();
            }
        }
    }
}
