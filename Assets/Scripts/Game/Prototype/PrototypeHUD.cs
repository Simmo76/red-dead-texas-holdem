using System.Collections.Generic;
using CinematicPoker.Engine.Poker;
using CinematicPoker.Game.Mobile;
using CinematicPoker.Game.Presentation;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem.UI;
#endif

namespace CinematicPoker.Game.Prototype
{
    /// <summary>
    /// Mobile-friendly HUD for the primitive prototype, built entirely in code
    /// (no prefabs or serialized scene references). Contextual Fold / Check /
    /// Call / Bet / Raise / All-In buttons straight from the engine's
    /// LegalActions, a raise slider with 1/2 Pot / Pot / All-In shortcuts,
    /// an action log, Quick/Cinematic toggle, a win-photos preference toggle
    /// and a new-session button.
    /// </summary>
    public sealed class PrototypeHUD : MonoBehaviour
    {
        private PokerPrototypeScene _scene;
        private PokerTableController _controller;
        private Font _font;

        private RectTransform _root;
        private Text _statusText;
        private Text _logText;
        private Text _modeButtonLabel;
        private Text _winPhotosLabel;
        private WinPhotoCapture _winPhotos;
        private Button _victoryShotButton;
        private Text _victoryShotLabel;
        private int _victoryToken;
        private bool _shotInProgress;
        private GameObject _actionBar;
        private GameObject _sessionOverPanel;
        private Text _sessionOverText;

        private Button _foldButton, _checkButton, _callButton, _betButton, _raiseButton, _allInButton;
        private Text _callLabel, _betLabel, _raiseLabel;

        private GameObject _sliderPanel;
        private Slider _slider;
        private Text _sliderAmountLabel;

        private LegalActions _legal;
        private readonly Queue<string> _logLines = new Queue<string>();

        public void Build(PokerPrototypeScene scene, PokerTableController controller)
        {
            _scene = scene;
            _controller = controller;
            _winPhotos = controller.GetComponent<WinPhotoCapture>();
            _font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

            EnsureEventSystem();
            BuildCanvas();
            BuildStatusAndLog();
            BuildActionBar();
            BuildSliderPanel();
            BuildTopButtons();
            BuildSessionOverPanel();
            BuildVictoryShotButton();

            _controller.HumanTurnStarted += ShowActions;
            HideActions();
        }

        private void OnDestroy()
        {
            if (_controller != null)
                _controller.HumanTurnStarted -= ShowActions;
        }

        // -------------------------------------------------------- public API

        public void OnSessionStarted()
        {
            _logLines.Clear();
            if (_logText != null) _logText.text = "";
            _sessionOverPanel.SetActive(false);
            SetVictoryShotVisible(false);
            HideActions();
        }

        public void SetStatus(string text)
        {
            if (_statusText != null) _statusText.text = text;
        }

        public void AddLog(string line)
        {
            _logLines.Enqueue(line);
            while (_logLines.Count > 9) _logLines.Dequeue();
            if (_logText != null) _logText.text = string.Join("\n", _logLines);
        }

        public void ShowSessionOver(string message)
        {
            HideActions();
            _sessionOverPanel.SetActive(true);
            _sessionOverText.text = message;
        }

        public void HideSessionOver()
        {
            _sessionOverPanel.SetActive(false);
            SetVictoryShotVisible(false);
        }

        /// <summary>
        /// Shows or hides "Victory Screen-Shot!". A show waits one frame so
        /// an automatic end-of-frame win photo does not include the button.
        /// </summary>
        public void SetVictoryShotVisible(bool visible)
        {
            _victoryToken++;
            if (_victoryShotButton == null) return;
            if (!visible)
            {
                _shotInProgress = false;
                _victoryShotButton.gameObject.SetActive(false);
                return;
            }

            int token = _victoryToken;
            StartCoroutine(ShowVictoryNextFrame(token));
        }

        // ---------------------------------------------------------- actions

        private void ShowActions(LegalActions legal)
        {
            _legal = legal;
            _actionBar.SetActive(true);
            _sliderPanel.SetActive(false);

            _foldButton.gameObject.SetActive(legal.CanFold);
            _checkButton.gameObject.SetActive(legal.CanCheck);
            _callButton.gameObject.SetActive(legal.CanCall);
            _betButton.gameObject.SetActive(legal.CanBet);
            _raiseButton.gameObject.SetActive(legal.CanRaise);
            _allInButton.gameObject.SetActive(legal.CanAllIn);

            if (legal.CanCall) _callLabel.text = $"CALL {legal.CallAmount}";
            if (legal.CanBet) _betLabel.text = "BET";
            if (legal.CanRaise) _raiseLabel.text = "RAISE";
        }

        private void HideActions()
        {
            _actionBar.SetActive(false);
            _sliderPanel.SetActive(false);
        }

        private void Submit(PlayerAction action)
        {
            HideActions();
            _controller.SubmitHumanAction(action);
        }

        private void OpenSlider()
        {
            if (_legal == null) return;
            _sliderPanel.SetActive(true);
            _slider.minValue = _legal.MinBetOrRaiseTo;
            _slider.maxValue = _legal.MaxBetOrRaiseTo;
            _slider.value = _legal.MinBetOrRaiseTo;
            UpdateSliderLabel();
        }

        private void ConfirmSlider()
        {
            long amount = (long)_slider.value;
            if (amount >= _legal.MaxBetOrRaiseTo) Submit(PlayerAction.AllIn());
            else if (_legal.CanBet) Submit(PlayerAction.Bet(amount));
            else Submit(PlayerAction.Raise(amount));
        }

        private void SetSliderAmount(long amount)
        {
            _slider.value = Mathf.Clamp(amount, _slider.minValue, _slider.maxValue);
            UpdateSliderLabel();
        }

        private long CurrentPot()
        {
            var game = _controller.Game;
            return game != null && game.Phase == GamePhase.HandInProgress ? game.CurrentRound.PotTotal : 0;
        }

        private void UpdateSliderLabel()
        {
            _sliderAmountLabel.text = ((long)_slider.value).ToString();
        }

        // ------------------------------------------------------ construction

        private static void EnsureEventSystem()
        {
            if (Object.FindFirstObjectByType<EventSystem>() != null) return;
            var go = new GameObject("EventSystem", typeof(EventSystem));
#if ENABLE_INPUT_SYSTEM
            go.AddComponent<InputSystemUIInputModule>();
#else
            go.AddComponent<StandaloneInputModule>();
#endif
        }

        private void BuildCanvas()
        {
            var canvas = gameObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            var scaler = gameObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1600, 720);
            scaler.matchWidthOrHeight = 1f; // landscape phones: match height
            gameObject.AddComponent<GraphicRaycaster>();
            _root = (RectTransform)transform;
        }

        private void BuildStatusAndLog()
        {
            _statusText = CreateText(_root, "Status", new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
                new Vector2(0f, -30f), new Vector2(900f, 44f), 26, TextAnchor.MiddleCenter);
            _statusText.color = new Color(1f, 1f, 1f, 0.95f);

            _logText = CreateText(_root, "Log", new Vector2(0f, 1f), new Vector2(0f, 1f),
                new Vector2(180f, -160f), new Vector2(340f, 250f), 17, TextAnchor.UpperLeft);
            _logText.color = new Color(1f, 1f, 1f, 0.75f);
        }

        private void BuildActionBar()
        {
            _actionBar = new GameObject("ActionBar", typeof(RectTransform));
            var rt = (RectTransform)_actionBar.transform;
            rt.SetParent(_root, false);
            rt.anchorMin = new Vector2(1f, 0f);
            rt.anchorMax = new Vector2(1f, 0f);
            rt.pivot = new Vector2(1f, 0f);
            rt.anchoredPosition = new Vector2(-20f, 20f);
            rt.sizeDelta = new Vector2(920f, 90f);

            float x = 0f;
            const float w = 145f, gap = 8f;

            _foldButton = CreateButton(rt, "FOLD", ref x, w, gap, new Color(0.55f, 0.2f, 0.2f),
                () => Submit(PlayerAction.Fold()), out _);
            _checkButton = CreateButton(rt, "CHECK", ref x, w, gap, new Color(0.25f, 0.42f, 0.3f),
                () => Submit(PlayerAction.Check()), out _);
            _callButton = CreateButton(rt, "CALL", ref x, w, gap, new Color(0.25f, 0.42f, 0.3f),
                () => Submit(PlayerAction.Call()), out _callLabel);
            _betButton = CreateButton(rt, "BET", ref x, w, gap, new Color(0.25f, 0.35f, 0.55f),
                OpenSlider, out _betLabel);
            _raiseButton = CreateButton(rt, "RAISE", ref x, w, gap, new Color(0.25f, 0.35f, 0.55f),
                OpenSlider, out _raiseLabel);
            _allInButton = CreateButton(rt, "ALL-IN", ref x, w, gap, new Color(0.6f, 0.4f, 0.15f),
                () => Submit(PlayerAction.AllIn()), out _);
        }

        private void BuildSliderPanel()
        {
            _sliderPanel = CreatePanel(_root, "SliderPanel", new Vector2(1f, 0f), new Vector2(1f, 0f),
                new Vector2(-20f, 120f), new Vector2(560f, 130f), new Color(0f, 0f, 0f, 0.65f));
            var rt = (RectTransform)_sliderPanel.transform;
            rt.pivot = new Vector2(1f, 0f);

            _slider = CreateSlider(rt, new Vector2(20f, 78f), new Vector2(380f, 30f));
            _slider.wholeNumbers = true;
            _slider.onValueChanged.AddListener(_ => UpdateSliderLabel());

            _sliderAmountLabel = CreateText(rt, "Amount", new Vector2(0f, 0f), new Vector2(0f, 0f),
                new Vector2(475f, 93f), new Vector2(140f, 34f), 24, TextAnchor.MiddleCenter);

            float x = 0f;
            const float w = 122f, gap = 8f;
            var shortcutRow = new GameObject("Shortcuts", typeof(RectTransform));
            var srt = (RectTransform)shortcutRow.transform;
            srt.SetParent(rt, false);
            srt.anchorMin = Vector2.zero;
            srt.anchorMax = Vector2.zero;
            srt.pivot = Vector2.zero;
            srt.anchoredPosition = new Vector2(15f, 10f);
            srt.sizeDelta = new Vector2(540f, 52f);

            CreateButton(srt, "1/2 POT", ref x, w, gap, new Color(0.3f, 0.3f, 0.35f),
                () => SetSliderAmount(CurrentPot() / 2), out _, 52f);
            CreateButton(srt, "POT", ref x, w, gap, new Color(0.3f, 0.3f, 0.35f),
                () => SetSliderAmount(CurrentPot()), out _, 52f);
            CreateButton(srt, "MAX", ref x, w, gap, new Color(0.3f, 0.3f, 0.35f),
                () => SetSliderAmount(_legal != null ? _legal.MaxBetOrRaiseTo : 0), out _, 52f);
            CreateButton(srt, "CONFIRM", ref x, w + 20f, gap, new Color(0.2f, 0.5f, 0.25f),
                ConfirmSlider, out _, 52f);
        }

        private void BuildTopButtons()
        {
            float x = 0f;
            var row = new GameObject("TopButtons", typeof(RectTransform));
            var rt = (RectTransform)row.transform;
            rt.SetParent(_root, false);
            rt.anchorMin = new Vector2(1f, 1f);
            rt.anchorMax = new Vector2(1f, 1f);
            rt.pivot = new Vector2(1f, 1f);
            rt.anchoredPosition = new Vector2(-20f, -15f);
            rt.sizeDelta = new Vector2(620f, 46f);

            CreateButton(rt, "MODE: CINEMATIC", ref x, 240f, 8f, new Color(0.25f, 0.25f, 0.3f),
                ToggleMode, out _modeButtonLabel, 46f);
            if (_winPhotos != null)
            {
                CreateButton(rt, WinPhotosLabelText(), ref x, 230f, 8f, new Color(0.25f, 0.25f, 0.3f),
                    ToggleWinPhotos, out _winPhotosLabel, 46f);
            }
            CreateButton(rt, "RESTART", ref x, 110f, 8f, new Color(0.35f, 0.25f, 0.25f),
                () => _scene.StartNewSession(), out _, 46f);
        }

        private void BuildSessionOverPanel()
        {
            _sessionOverPanel = CreatePanel(_root, "SessionOver", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                Vector2.zero, new Vector2(760f, 220f), new Color(0f, 0f, 0f, 0.8f));

            _sessionOverText = CreateText((RectTransform)_sessionOverPanel.transform, "Message",
                new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -45f),
                new Vector2(720f, 60f), 28, TextAnchor.MiddleCenter);

            float x = 70f;
            CreateButton((RectTransform)_sessionOverPanel.transform, "CONTINUE GAME", ref x, 280f, 16f,
                new Color(0.2f, 0.5f, 0.25f), () => _scene.ContinueSession(), out _, 56f);
            CreateButton((RectTransform)_sessionOverPanel.transform, "RESTART", ref x, 200f, 0f,
                new Color(0.55f, 0.18f, 0.2f), () => _scene.StartNewSession(), out _, 56f);

            _sessionOverPanel.SetActive(false);
        }

        private void BuildVictoryShotButton()
        {
            var row = new GameObject("VictoryShot", typeof(RectTransform));
            var rt = (RectTransform)row.transform;
            rt.SetParent(_root, false);
            rt.anchorMin = new Vector2(0.5f, 0f);
            rt.anchorMax = new Vector2(0.5f, 0f);
            rt.pivot = new Vector2(0.5f, 0f);
            rt.anchoredPosition = new Vector2(0f, 130f);
            rt.sizeDelta = new Vector2(460f, 64f);

            float x = 0f;
            _victoryShotButton = CreateButton(rt, "Victory Screen-Shot!", ref x, 460f, 0f,
                new Color(0.55f, 0.4f, 0.12f), OnVictoryShot, out _victoryShotLabel, 64f);
            _victoryShotButton.gameObject.SetActive(false);
        }

        private void OnVictoryShot()
        {
            if (_winPhotos == null || _shotInProgress) return;
            _shotInProgress = true;
            _victoryShotButton.gameObject.SetActive(false);
            _winPhotos.CaptureVictory();
            StartCoroutine(RestoreVictoryButton(_victoryToken));
        }

        private System.Collections.IEnumerator ShowVictoryNextFrame(int token)
        {
            yield return null;
            if (token != _victoryToken || _victoryShotButton == null) yield break;
            _victoryShotLabel.text = "Victory Screen-Shot!";
            _victoryShotButton.gameObject.SetActive(true);
            _victoryShotButton.transform.parent.SetAsLastSibling();
        }

        private System.Collections.IEnumerator RestoreVictoryButton(int token)
        {
            // CaptureVictory also waits for end of frame and was started
            // first, so the photo is taken while this button is hidden.
            yield return new WaitForEndOfFrame();
            _shotInProgress = false;
            if (token != _victoryToken || _victoryShotButton == null) yield break;
            _victoryShotLabel.text = "Saved to Photos";
            _victoryShotButton.gameObject.SetActive(true);
            _victoryShotButton.transform.parent.SetAsLastSibling();
        }

        private void ToggleMode()
        {
            _controller.Mode = _controller.Mode == PlayMode.Quick ? PlayMode.Cinematic : PlayMode.Quick;
            _modeButtonLabel.text = _controller.Mode == PlayMode.Quick ? "MODE: QUICK" : "MODE: CINEMATIC";
        }

        private void ToggleWinPhotos()
        {
            _winPhotos.CaptureEnabled = !_winPhotos.CaptureEnabled;
            _winPhotosLabel.text = WinPhotosLabelText();
        }

        private string WinPhotosLabelText() =>
            _winPhotos.CaptureEnabled ? "WIN PHOTOS: ON" : "WIN PHOTOS: OFF";

        // ------------------------------------------------------- uGUI helpers

        private GameObject CreatePanel(RectTransform parent, string name, Vector2 anchorMin, Vector2 anchorMax,
            Vector2 anchoredPos, Vector2 size, Color color)
        {
            var go = new GameObject(name, typeof(RectTransform));
            var rt = (RectTransform)go.transform;
            rt.SetParent(parent, false);
            rt.anchorMin = anchorMin;
            rt.anchorMax = anchorMax;
            rt.anchoredPosition = anchoredPos;
            rt.sizeDelta = size;
            var image = go.AddComponent<Image>();
            image.color = color;
            return go;
        }

        private Text CreateText(RectTransform parent, string name, Vector2 anchorMin, Vector2 anchorMax,
            Vector2 anchoredPos, Vector2 size, int fontSize, TextAnchor anchor)
        {
            var go = new GameObject(name, typeof(RectTransform));
            var rt = (RectTransform)go.transform;
            rt.SetParent(parent, false);
            rt.anchorMin = anchorMin;
            rt.anchorMax = anchorMax;
            rt.anchoredPosition = anchoredPos;
            rt.sizeDelta = size;
            var text = go.AddComponent<Text>();
            text.font = _font;
            text.fontSize = fontSize;
            text.alignment = anchor;
            text.color = Color.white;
            text.horizontalOverflow = HorizontalWrapMode.Overflow;
            text.verticalOverflow = VerticalWrapMode.Overflow;
            return text;
        }

        private Button CreateButton(RectTransform parent, string label, ref float x, float width, float gap,
            Color color, UnityEngine.Events.UnityAction onClick, out Text textComponent, float height = 78f)
        {
            var go = new GameObject($"Button_{label}", typeof(RectTransform));
            var rt = (RectTransform)go.transform;
            rt.SetParent(parent, false);
            rt.anchorMin = new Vector2(0f, 0f);
            rt.anchorMax = new Vector2(0f, 0f);
            rt.pivot = new Vector2(0f, 0f);
            rt.anchoredPosition = new Vector2(x, 0f);
            rt.sizeDelta = new Vector2(width, height);
            x += width + gap;

            var image = go.AddComponent<Image>();
            image.color = color;
            var button = go.AddComponent<Button>();
            button.targetGraphic = image;
            button.onClick.AddListener(onClick);

            textComponent = CreateText(rt, "Label", Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero,
                22, TextAnchor.MiddleCenter);
            var textRt = (RectTransform)textComponent.transform;
            textRt.offsetMin = Vector2.zero;
            textRt.offsetMax = Vector2.zero;
            textComponent.text = label;

            return button;
        }

        private Slider CreateSlider(RectTransform parent, Vector2 anchoredPos, Vector2 size)
        {
            var go = new GameObject("Slider", typeof(RectTransform));
            var rt = (RectTransform)go.transform;
            rt.SetParent(parent, false);
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.zero;
            rt.pivot = new Vector2(0f, 0.5f);
            rt.anchoredPosition = anchoredPos;
            rt.sizeDelta = size;

            // Background
            var bg = CreatePanel(rt, "Background", Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero,
                new Color(0.15f, 0.15f, 0.18f));
            var bgRt = (RectTransform)bg.transform;
            bgRt.offsetMin = new Vector2(0f, 10f);
            bgRt.offsetMax = new Vector2(0f, -10f);

            // Fill
            var fillArea = new GameObject("Fill Area", typeof(RectTransform));
            var faRt = (RectTransform)fillArea.transform;
            faRt.SetParent(rt, false);
            faRt.anchorMin = Vector2.zero;
            faRt.anchorMax = Vector2.one;
            faRt.offsetMin = new Vector2(6f, 10f);
            faRt.offsetMax = new Vector2(-6f, -10f);

            var fill = CreatePanel(faRt, "Fill", Vector2.zero, new Vector2(0f, 1f), Vector2.zero,
                new Vector2(10f, 0f), new Color(0.85f, 0.7f, 0.25f));
            var fillRt = (RectTransform)fill.transform;

            // Handle
            var handleArea = new GameObject("Handle Slide Area", typeof(RectTransform));
            var haRt = (RectTransform)handleArea.transform;
            haRt.SetParent(rt, false);
            haRt.anchorMin = Vector2.zero;
            haRt.anchorMax = Vector2.one;
            haRt.offsetMin = new Vector2(12f, 0f);
            haRt.offsetMax = new Vector2(-12f, 0f);

            var handle = CreatePanel(haRt, "Handle", Vector2.zero, new Vector2(0f, 1f), Vector2.zero,
                new Vector2(28f, 0f), Color.white);
            var handleRt = (RectTransform)handle.transform;

            var slider = go.AddComponent<Slider>();
            slider.fillRect = fillRt;
            slider.handleRect = handleRt;
            slider.targetGraphic = handle.GetComponent<Image>();
            slider.direction = Slider.Direction.LeftToRight;
            return slider;
        }
    }
}
