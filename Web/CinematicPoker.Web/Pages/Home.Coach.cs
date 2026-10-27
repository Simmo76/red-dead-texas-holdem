using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using CinematicPoker.Engine.Coaching;
using CinematicPoker.Engine.Poker;
using CinematicPoker.Engine.Telemetry;
using CinematicPoker.Web.Coaching;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.JSInterop;

namespace CinematicPoker.Web.Pages
{
    /// <summary>
    /// Post-hand coach hosted by the table page. The engine records public
    /// facts; this page only shows them and switches the existing WebGL
    /// canvas into the separate saloon scene.
    /// </summary>
    public partial class Home : IDisposable
    {
        private const int CoachAnalysisBudgetSeconds = 2;

        private readonly CoachHistoryBuffer _coachHistory = new CoachHistoryBuffer();
        private readonly CoachFlowController _coachFlow = new CoachFlowController();
        private readonly List<string> _coachMetrics = new List<string>();
        private readonly List<CoachCardFace> _coachFaces = new List<CoachCardFace>();

        private HandReviewRecorder _coachRecorder;
        private HandReviewService _coachService;
        private CancellationTokenSource _coachCts = new CancellationTokenSource();
        private DotNetObjectReference<Home> _coachRef;
        private HeroDecisionSnapshot _coachOffer;
        private CompletedHandRecord _coachShownHand;
        private CompletedHandRecord _coachPreviewHand;
        private HandReview _coachReview;
        private ElementReference _coachPanelRef;

        private CoachMode _coachMode = CoachMode.OnRequest;
        private string _coachSessionId = "local";
        private string _coachSceneReason = "";
        private string _coachModel;
        private string _coachClip;
        private string _coachFeedback;
        private bool _coachOpen;
        private bool _coachUseScene;
        private bool? _coach3dOk;
        private bool _coachKeyOpen;
        private bool _coachKeyTracked;
        private bool _coachHighlight;
        private bool _coachTipVisible;
        private bool _coachPreview;
        private bool _coachPreviewOpened;
        private bool _coachLoaded;
        private bool _coachEscapeBound;
        private bool _coachFocusPending;
        private int _coachHandsSinceTip = 2;
        private int _coachRequest;
        private long _coachSeq;
        private string _coachAutoOpenedHandId;
        private DateTime _coachOpenedUtc;

        private bool CanReview =>
            !_isGuest
            && !_coachPreview
            && _coachMode != CoachMode.Off
            && !_coachOpen
            && !_awaitingHuman
            && _coachRecorder?.LastCompleted is { Complete: true };

        private bool CoachCanAdvance =>
            !_coachPreview
            && _coachOpen
            && _coachFlow.OpenedBetweenHands
            && _awaitingNextHand
            && _coachFlow.OpenedHandId != null
            && _coachFlow.OpenedHandId == _coachRecorder?.LastCompleted?.HandId;

        private bool CoachAtShowdown =>
            _coachShownHand != null
            && !_coachShownHand.WonByFolds
            && _coachShownHand.RevealedHands != null
            && _coachShownHand.RevealedHands.Count > 0;

        private string CoachModeLabel => _coachMode switch
        {
            CoachMode.Off => "OFF",
            CoachMode.Notable => "NOTABLE",
            _ => "ON REQUEST"
        };

        private string CoachAdvanceLabel =>
            _game != null && _game.HasHumanLost ? "CONTINUE" : "NEXT HAND";

        // Saloon reviews are Maximilian, the eye-patch bandit. Octagon reviews
        // are Tommy Gunn, a roster name, seated as the bald cage fighter.
        private bool OctagonCoach => _tableScene == "street";

        private string CoachDisplayName => OctagonCoach ? "Tommy Gunn" : "Maximilian";

        private string CoachDisplayNameUpper => OctagonCoach ? "TOMMY GUNN" : "MAXIMILIAN";

        private string CoachInitial => OctagonCoach ? "T" : "M";

        private string GameRootClass =>
            "game3d"
            + (_hudBoardExpanded ? " board-expanded" : "")
            + (_shotBusy ? " shot-busy" : "")
            + (_coachOpen && _coachUseScene ? " coach-open" : "")
            + (_coachOpen && !_coachUseScene ? " coach-panel-only" : "")
            + (_coachOpen && _awaitingHuman ? " coach-live" : "");

        private static string ModeKey(CoachMode mode) => mode switch
        {
            CoachMode.Off => "off",
            CoachMode.Notable => "notable",
            _ => "request"
        };

        private void NoteCoachPreviewFromUrl()
        {
            _coachPreview = Nav.Uri.Contains("coachpreview=1", StringComparison.OrdinalIgnoreCase);
            if (!_coachPreview) return;
            _introStarted = true;
            if (string.IsNullOrEmpty(_playerName))
                _playerName = "Preview";
        }

        private async Task LoadCoachPersistence()
        {
            if (_coachLoaded) return;
            _coachLoaded = true;
            try
            {
                string mode = await JS.InvokeAsync<string>("pokerCoachStore.loadMode");
                _coachMode = mode switch
                {
                    "off" => CoachMode.Off,
                    "notable" => CoachMode.Notable,
                    _ => CoachMode.OnRequest
                };
                string tip = await JS.InvokeAsync<string>("pokerCoachStore.loadTip");
                if (int.TryParse(tip, NumberStyles.Integer, CultureInfo.InvariantCulture, out int handsSince)
                    && handsSince >= 0 && handsSince <= 100)
                    _coachHandsSinceTip = handsSince;

                string historyJson = await JS.InvokeAsync<string>("pokerCoachStore.loadHistory");
                if (!string.IsNullOrWhiteSpace(historyJson))
                {
                    CoachHistoryFile file = JsonSerializer.Deserialize<CoachHistoryFile>(historyJson, CoachJson.Options);
                    if (file != null && file.Version == CoachSchema.Version && file.Hands != null)
                    {
                        foreach (CompletedHandRecord hand in file.Hands)
                        {
                            if (hand != null && hand.SchemaVersion == CoachSchema.Version)
                                _coachHistory.Remember(hand);
                        }
                    }
                }

                string metrics = await JS.InvokeAsync<string>("pokerCoachStore.loadMetrics");
                if (!string.IsNullOrEmpty(metrics))
                {
                    foreach (string line in metrics.Split('\n'))
                    {
                        if (!string.IsNullOrWhiteSpace(line))
                            _coachMetrics.Add(line.Trim());
                    }
                    while (_coachMetrics.Count > 200)
                        _coachMetrics.RemoveAt(0);
                }
            }
            catch (Exception ex) when (ex is JSException || ex is JsonException || ex is InvalidOperationException
                                       || ex is FormatException || ex is NotSupportedException)
            {
                // Storage is optional. The in-memory buffer still reviews this sitting.
            }
        }

        private async Task BindCoachEscape()
        {
            if (_coachEscapeBound) return;
            try
            {
                _coachRef ??= DotNetObjectReference.Create(this);
                await JS.InvokeVoidAsync("pokerCoachStore.bindEscape", _coachRef);
                _coachEscapeBound = true;
            }
            catch (JSException) { }
            catch (InvalidOperationException) { }
        }

        private void EnsureCoachRecorder()
        {
            if (_isGuest || _game == null) return;
            if (string.IsNullOrEmpty(_coachSessionId) || _coachSessionId == "local")
                _coachSessionId = Guid.NewGuid().ToString("N");
            if (_coachRecorder == null)
            {
                _coachRecorder = new HandReviewRecorder(
                    _coachSessionId, 0, _game.Rules.SmallBlind, _game.Rules.BigBlind, _coachHistory);
            }
        }

        private void BeginCoachSession()
        {
            DismissCoachPresentation();
            _coachSessionId = Guid.NewGuid().ToString("N");
            if (_isGuest || _game == null) return;
            if (_coachRecorder == null)
            {
                _coachRecorder = new HandReviewRecorder(
                    _coachSessionId, 0, _game.Rules.SmallBlind, _game.Rules.BigBlind, _coachHistory);
            }
            else
            {
                _coachRecorder.BeginSession(_coachSessionId);
            }
        }

        private void ObserveCoach(PokerEvent evt)
        {
            if (_isGuest || evt == null || _game == null) return;
            EnsureCoachRecorder();
            if (_coachRecorder == null) return;
            var names = new Dictionary<int, string>();
            foreach (var player in _game.Players)
                names[player.Seat] = player.Seat == 0 ? PlayerName : player.Name;
            _coachRecorder.Observe(evt, Round, names);
        }

        private HeroDecisionSnapshot CaptureCoachOffer()
        {
            if (_isGuest || _game == null) return null;
            EnsureCoachRecorder();
            return _coachRecorder?.CaptureOffer(Round, _legal);
        }

        private void AcceptCoachAction(PlayerAction action)
        {
            if (_isGuest || _coachOffer == null) return;
            _coachRecorder?.Accept(_coachOffer, action);
            _coachOffer = null;
        }

        // Solo, on request: auto-open the review after the first two hands
        // while the table holds for the next deal. From the third hand on,
        // the player opens Poker Coach themselves. Closing does not reopen
        // the same hand. Notable mode keeps the tip chip. Off stays quiet.
        private async Task OpenCoachAfterHand()
        {
            if (_isGuest || _coachPreview || _remote.Count > 0) return;
            if (_coachMode != CoachMode.OnRequest || _coachOpen || _awaitingHuman) return;
            CompletedHandRecord hand = _coachRecorder?.LastCompleted;
            if (hand == null || !hand.Complete || hand.HandId == _coachAutoOpenedHandId) return;
            if (hand.HandNumber > 2) return;
            _coachAutoOpenedHandId = hand.HandId;
            await OpenCoachReview();
        }

        private void NoteCoachHandReady()
        {
            if (_isGuest || _coachPreview) return;
            CompletedHandRecord hand = _coachRecorder?.LastCompleted;
            if (hand == null || !hand.Complete) return;
            _ = SaveCoachHistory();
            if (_coachMode == CoachMode.Off) return;
            _ = TrackCoach(CoachAnalytics.Available("local", _coachSessionId, NextSeq(), hand.HandId, ModeKey(_coachMode)));
            if (_coachMode != CoachMode.Notable) return;
            _coachHandsSinceTip++;
            if (_coachHandsSinceTip >= 3 && !_coachOpen)
            {
                _coachHandsSinceTip = 0;
                _coachTipVisible = true;
            }
            _ = SaveCoachTip();
        }

        public async Task CycleCoachMode()
        {
            _coachMode = _coachMode switch
            {
                CoachMode.Off => CoachMode.OnRequest,
                CoachMode.OnRequest => CoachMode.Notable,
                _ => CoachMode.Off
            };
            if (_coachMode != CoachMode.Notable)
                _coachTipVisible = false;
            if (_coachMode == CoachMode.Off && (_coachOpen || _coachFlow.ReviewOpen))
                await CloseCoach(false);
            try { await JS.InvokeVoidAsync("pokerCoachStore.saveMode", ModeKey(_coachMode)); }
            catch (JSException) { }
            catch (InvalidOperationException) { }
            _ = TrackCoach(CoachAnalytics.ModeChanged("local", _coachSessionId, NextSeq(), ModeKey(_coachMode)));
        }

        public async Task OpenCoachReview()
        {
            if (_isGuest) return;
            if (!_coachPreview && _coachMode == CoachMode.Off) return;
            if (!_coachPreview && _awaitingHuman) return;

            CompletedHandRecord hand = _coachPreview ? _coachPreviewHand : _coachRecorder?.LastCompleted;
            if (hand == null || !hand.Complete) return;

            _coachService ??= new HandReviewService();
            _coachShownHand = hand;
            FillFaces(hand);
            _coachReview = _coachService.Explain(hand);
            _coachKeyOpen = false;
            _coachKeyTracked = false;
            _coachHighlight = false;
            _coachFeedback = null;
            _coachSceneReason = "";
            _coachModel = null;
            _coachClip = null;
            _coachTipVisible = false;
            _coachUseScene = _remote.Count == 0;
            _coach3dOk = _coachUseScene ? (bool?)null : false;
            if (!_coachUseScene)
                _coachSceneReason = "shared-table";
            _coachRequest = _coachFlow.Open(_awaitingNextHand && !_coachPreview, hand.HandId);
            _coachOpenedUtc = DateTime.UtcNow;
            _coachOpen = true;
            _coachFocusPending = true;
            ReplaceCts();
            CancellationToken token = _coachCts.Token;
            bool pending = _coachReview.KeyDecision != null && _coachReview.KeyDecision.ReasonCode == "pending";
            _ = TrackCoach(CoachAnalytics.Opened(
                "local", _coachSessionId, NextSeq(), hand.HandId, ModeKey(_coachMode), !pending));
            StateHasChanged();

            if (_coachUseScene)
                await ShowCoachScene(hand, _coachRequest);

            if (!_coachFlow.IsCurrent(_coachRequest) || token.IsCancellationRequested) return;
            DateTime started = DateTime.UtcNow;
            HandReview priced;
            try
            {
                priced = await _coachService.ReviewAsync(
                    hand, SeedFor(hand.HandId), token, DateTime.UtcNow.AddSeconds(CoachAnalysisBudgetSeconds));
            }
            catch (OperationCanceledException)
            {
                return;
            }
            if (!_coachFlow.IsCurrent(_coachRequest)) return;
            _coachReview = priced;
            string code = priced.KeyDecision?.ReasonCode;
            if (code == "equity_unavailable" || code == "cancelled")
            {
                long ms = (long)Math.Max(0, (DateTime.UtcNow - started).TotalMilliseconds);
                _ = TrackCoach(CoachAnalytics.Failed("local", _coachSessionId, NextSeq(), hand.HandId, code, ms));
            }
            StateHasChanged();
        }

        public async Task OpenCoachPreview()
        {
            if (_coachPreviewOpened) return;
            _coachPreviewOpened = true;
            _coachPreviewHand = CoachFixtures.FlushOverTwoPair();
            await OpenCoachReview();
        }

        public async Task CloseCoach(bool nextHand)
        {
            if (!_coachOpen && !_coachFlow.ReviewOpen) return;
            string handId = _coachFlow.OpenedHandId ?? _coachShownHand?.HandId;
            long viewSeconds = ViewSeconds();
            string mode = ModeKey(_coachMode);
            string currentId = _coachRecorder?.LastCompleted?.HandId;
            bool advance = _coachFlow.Close(nextHand && !_coachPreview, _awaitingNextHand, currentId);
            if (_coachPreview) advance = false;
            _coachOpen = false;
            _coach3dOk = null;
            _coachUseScene = false;
            _coachKeyOpen = false;
            _coachHighlight = false;
            _coachShownHand = null;
            _coachFaces.Clear();
            ReplaceCts();
            await TrackCoach(CoachAnalytics.Closed("local", _coachSessionId, NextSeq(), handId, mode, viewSeconds));
            await HideCoachScene();
            if (advance && !_sessionOver)
                FastForward();
            StateHasChanged();
        }

        [JSInvokable]
        public async Task CloseCoachFromKey()
        {
            if (_coachOpen) await CloseCoach(false);
        }

        public async Task OnCoachKey(KeyboardEventArgs e)
        {
            if (e != null && e.Key == "Escape")
                await CloseCoach(false);
        }

        public async Task ToggleKeyDecision()
        {
            _coachKeyOpen = !_coachKeyOpen;
            if (_coachKeyOpen && !_coachKeyTracked)
            {
                _coachKeyTracked = true;
                string code = _coachReview == null ? "none" : _coachReview.LessonKind.ToString();
                _ = TrackCoach(CoachAnalytics.KeyDecision(
                    "local", _coachSessionId, NextSeq(), _coachShownHand?.HandId, code));
            }
            await Task.CompletedTask;
        }

        public async Task ToggleCoachHighlights()
        {
            _coachHighlight = !_coachHighlight;
            if (_coach3dOk == true)
            {
                try
                {
                    await JS.InvokeVoidAsync(
                        "pokerCoach.setHighlights",
                        _coachHighlight ? HighlightUrls() : Array.Empty<string>());
                }
                catch (JSException) { }
                catch (InvalidOperationException) { }
            }
        }

        public void SendCoachFeedback(string code)
        {
            if (string.IsNullOrEmpty(code) || _coachFeedback != null) return;
            _coachFeedback = code;
            _ = TrackCoach(CoachAnalytics.Feedback(
                "local", _coachSessionId, NextSeq(), _coachShownHand?.HandId, code));
        }

        public void DismissCoachTip() => _coachTipVisible = false;

        public async Task FocusCoachPanel()
        {
            if (!_coachFocusPending || !_coachOpen) return;
            try
            {
                await _coachPanelRef.FocusAsync();
                _coachFocusPending = false;
            }
            catch (InvalidOperationException) { }
        }

        private void DismissCoachPresentation()
        {
            bool wasOpen = _coachOpen || _coachFlow.ReviewOpen;
            string handId = _coachFlow.OpenedHandId ?? _coachShownHand?.HandId;
            long viewSeconds = ViewSeconds();
            string mode = ModeKey(_coachMode);
            _coachFlow.Invalidate();
            _coachOpen = false;
            _coach3dOk = null;
            _coachUseScene = false;
            _coachKeyOpen = false;
            _coachHighlight = false;
            _coachShownHand = null;
            _coachFaces.Clear();
            _coachTipVisible = false;
            _coachOffer = null;
            ReplaceCts();
            if (wasOpen)
                _ = TrackCoach(CoachAnalytics.Closed("local", _coachSessionId, NextSeq(), handId, mode, viewSeconds));
            _ = HideCoachScene();
        }

        private async Task ShowCoachScene(CompletedHandRecord hand, int request)
        {
            bool reduced = false;
            try { reduced = await JS.InvokeAsync<bool>("pokerCoachStore.prefersReducedMotion"); }
            catch (JSException) { }
            catch (InvalidOperationException) { }
            if (!_coachFlow.IsCurrent(request)) return;

            CoachSceneResult result;
            try
            {
                result = await JS.InvokeAsync<CoachSceneResult>("pokerCoach.show", CoachPayload(hand, reduced));
            }
            catch (JSException)
            {
                result = new CoachSceneResult { ok = false, reason = "scene-not-ready" };
            }
            catch (InvalidOperationException)
            {
                result = new CoachSceneResult { ok = false, reason = "scene-not-ready" };
            }
            if (!_coachFlow.IsCurrent(request)) return;
            _coach3dOk = result != null && result.ok;
            _coachSceneReason = result?.reason ?? "";
            _coachModel = result?.model;
            _coachClip = result?.clip;
            if (_coach3dOk == true && _coachHighlight)
            {
                try { await JS.InvokeVoidAsync("pokerCoach.setHighlights", HighlightUrls()); }
                catch (JSException) { }
            }
            StateHasChanged();
        }

        private object CoachPayload(CompletedHandRecord hand, bool reduced)
        {
            string[] board = hand?.FinalBoard?.Select(CardImage).ToArray() ?? Array.Empty<string>();
            string[] hero = hand?.HeroHoleCards?.Select(CardImage).ToArray() ?? Array.Empty<string>();
            var revealed = new List<string[]>();
            if (hand?.RevealedHands != null)
            {
                foreach (RevealedHand shown in hand.RevealedHands)
                {
                    if (shown == null || shown.Seat == hand.HeroSeat || shown.HoleCards == null) continue;
                    revealed.Add(shown.HoleCards.Select(CardImage).ToArray());
                }
            }
            return new
            {
                board,
                hero,
                revealed,
                highlights = _coachHighlight ? HighlightUrls() : Array.Empty<string>(),
                reducedMotion = reduced
            };
        }

        private string[] HighlightUrls()
        {
            if (_coachReview?.HighlightCodes == null) return Array.Empty<string>();
            var urls = new List<string>();
            foreach (string code in _coachReview.HighlightCodes)
            {
                try { urls.Add(CardImage(Card.Parse(code))); }
                catch (FormatException) { }
            }
            return urls.ToArray();
        }

        private void FillFaces(CompletedHandRecord hand)
        {
            _coachFaces.Clear();
            if (hand == null) return;
            AddFaces(hand.FinalBoard);
            AddFaces(hand.HeroHoleCards);
            if (hand.RevealedHands == null) return;
            foreach (RevealedHand shown in hand.RevealedHands)
            {
                if (shown == null || shown.Seat == hand.HeroSeat) continue;
                AddFaces(shown.HoleCards);
            }
        }

        private void AddFaces(IReadOnlyList<Card> cards)
        {
            if (cards == null) return;
            foreach (Card card in cards)
            {
                _coachFaces.Add(new CoachCardFace
                {
                    Src = CardImage(card),
                    Alt = SpokenCard(card),
                    Code = BestFiveCards.Code(card)
                });
            }
        }

        private string CoachHot(string code)
        {
            if (!_coachHighlight || string.IsNullOrEmpty(code) || _coachReview?.HighlightCodes == null)
                return null;
            return _coachReview.HighlightCodes.Contains(code) ? "hot" : null;
        }

        private static string SpokenCard(Card card) => card.Rank + " of " + card.Suit;

        private static string CoachPct(double? share)
        {
            if (!share.HasValue) return "not available";
            return Math.Round(share.Value * 100).ToString(CultureInfo.InvariantCulture) + "%";
        }

        private static string CoachChips(double? ev)
        {
            if (!ev.HasValue) return "not available";
            long rounded = (long)Math.Round(ev.Value);
            return rounded > 0 ? "+" + rounded.ToString(CultureInfo.InvariantCulture) : rounded.ToString(CultureInfo.InvariantCulture);
        }

        private async Task SaveCoachHistory()
        {
            try
            {
                var file = new CoachHistoryFile
                {
                    Version = CoachSchema.Version,
                    Hands = _coachHistory.Hands.ToList()
                };
                string json = JsonSerializer.Serialize(file, CoachJson.Options);
                await JS.InvokeVoidAsync("pokerCoachStore.saveHistory", json);
            }
            catch (JSException) { }
            catch (InvalidOperationException) { }
        }

        private async Task SaveCoachTip()
        {
            try { await JS.InvokeVoidAsync("pokerCoachStore.saveTip", _coachHandsSinceTip); }
            catch (JSException) { }
            catch (InvalidOperationException) { }
        }

        private async Task TrackCoach(AnalyticsEvent evt)
        {
            if (evt == null) return;
            _coachMetrics.Add(AnalyticsJson.Serialize(evt));
            while (_coachMetrics.Count > 200)
                _coachMetrics.RemoveAt(0);
            try
            {
                await JS.InvokeVoidAsync("pokerCoachStore.saveMetrics", string.Join("\n", _coachMetrics));
            }
            catch (JSException) { }
            catch (InvalidOperationException) { }
        }

        private async Task HideCoachScene()
        {
            try { await JS.InvokeVoidAsync("pokerCoach.hide"); }
            catch (JSException) { }
            catch (InvalidOperationException) { }
        }

        private void ReplaceCts()
        {
            try { _coachCts.Cancel(); }
            catch (ObjectDisposedException) { }
            _coachCts.Dispose();
            _coachCts = new CancellationTokenSource();
        }

        private long NextSeq() => ++_coachSeq;

        private long ViewSeconds()
        {
            if (_coachOpenedUtc == default) return 0;
            double seconds = (DateTime.UtcNow - _coachOpenedUtc).TotalSeconds;
            if (seconds < 0) return 0;
            if (seconds > 36000) return 36000;
            return (long)seconds;
        }

        private static int SeedFor(string handId)
        {
            unchecked
            {
                int hash = 23;
                if (!string.IsNullOrEmpty(handId))
                {
                    foreach (char c in handId)
                        hash = hash * 31 + c;
                }
                if (hash == 0) hash = 1;
                return hash;
            }
        }

        public void Dispose()
        {
            try { _coachCts.Cancel(); }
            catch (ObjectDisposedException) { }
            _coachCts.Dispose();
            _coachRef?.Dispose();
        }

        private sealed class CoachCardFace
        {
            public string Src;
            public string Alt;
            public string Code;
        }

        private sealed class CoachSceneResult
        {
            public bool ok { get; set; }
            public string reason { get; set; }
            public string model { get; set; }
            public string clip { get; set; }
        }
    }
}
