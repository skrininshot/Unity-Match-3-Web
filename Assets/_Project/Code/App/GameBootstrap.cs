using System.Collections;
using Match3.Core;
using Match3.Data;
using Match3.Presentation;
using UnityEngine;

namespace Match3.App
{
    /// <summary>
    /// The whole game, assembled at runtime from one object in the scene.
    /// <para>
    /// Nothing is authored in the scene file beyond this component, which keeps the layout and the
    /// wiring in code that can be reviewed and diffed, and means the game can be rebuilt from a
    /// script — which is how the screenshot tooling verifies it.
    /// </para>
    /// </summary>
    public sealed class GameBootstrap : MonoBehaviour
    {
        public const int GameLayer = 0;

        /// <summary>World units kept clear above and below the board for the HUD and buttons.</summary>
        private const float TopSpace = 2.6f;
        private const float BottomSpace = 1.8f;

        private SpriteLibrary _sprites;
        private LevelLibrary _levels;
        private ProgressStore _progress;
        private Match3Game _game;

        private Camera _camera;
        private Transform _boardRoot;
        private BoardView _board;
        private BoardInput _input;

        private HudView _hud;
        private MapScreen _map;
        private PopupView _popup;
        private AudioService _audio;

        private int _levelIndex;
        private bool _busy;
        private bool _built;

        public Match3Game Game => _game;
        public Camera Camera => _camera;
        public BoardView BoardView => _board;
        public LevelLibrary Levels => _levels;
        public int LevelIndex => _levelIndex;

        private void Awake() => Build();

        private void OnDestroy() => _sprites?.Dispose();

        /// <summary>Constructs everything. Safe to call directly; Awake just forwards to it.</summary>
        public void Build()
        {
            if (_built)
                return;

            _built = true;

            _sprites = new SpriteLibrary();
            _levels = LevelResourceLoader.Load();
            _progress = new ProgressStore();
            _game = new Match3Game();

            _audio = AudioService.Create(transform);
            _audio.PlayMusic();

            SetupCamera();

            _boardRoot = new GameObject("board-root").transform;
            _boardRoot.SetParent(transform, false);
            _board = BoardView.Create(_boardRoot, _sprites);

            _input = BoardInput.Create(transform, _camera, _board);
            _input.SwapRequested += OnSwapRequested;
            _input.TapRequested += OnTapRequested;

            // Creation order is draw order: HUD at the back, map over it, popup over everything.
            Canvas canvas = UiKit.CreateCanvas("ui", transform, _camera, 10);

            _hud = new HudView();
            _hud.Build(canvas.transform, _camera, _sprites);
            _hud.MapButton.Layer = GameLayer;
            _hud.RestartButton.Layer = GameLayer;
            _hud.MapButton.Clicked += ShowMap;
            _hud.RestartButton.Clicked += RestartLevel;

            _map = new MapScreen();
            _map.Build(canvas.transform, _camera, _sprites);
            _map.LevelChosen += StartLevel;

            _popup = new PopupView();
            _popup.Build(canvas.transform, _camera, _sprites);

            if (_levels.Count == 0)
            {
                Debug.LogError("No levels were loaded; there is nothing to play.");
                return;
            }

            ShowMap();
        }

        private void SetupCamera()
        {
            _camera = Camera.main;

            if (_camera == null)
            {
                var go = new GameObject("Main Camera") { tag = "MainCamera" };
                go.transform.SetParent(transform, false);
                _camera = go.AddComponent<Camera>();
            }

            _camera.orthographic = true;
            _camera.transform.position = new Vector3(0f, 0f, -10f);
            _camera.clearFlags = CameraClearFlags.SolidColor;
            _camera.backgroundColor = SpriteLibrary.BoardBackground;
            _camera.nearClipPlane = 0.1f;
            _camera.farClipPlane = 50f;
            _camera.orthographicSize = 6f;
        }

        // ------------------------------------------------------------------ screens

        public void ShowMap()
        {
            _popup.Hide();
            _map.Refresh(_levels, _progress);
            _map.SetVisible(true);
            _hud.SetVisible(false);
            _boardRoot.gameObject.SetActive(false);
            _input.AcceptsInput = false;
            UiButton.ActiveLayer = MapScreen.Layer;
        }

        public void StartLevel(int index)
        {
            if (index < 0 || index >= _levels.Count)
                return;

            _levelIndex = index;
            LevelConfig config = _levels[index];

            _game.Load(config);
            _board.Build(_game.Board, _game.SnapshotEntities());
            LayoutBoard(config);

            _hud.SetLevel(index + 1, config);
            _hud.SetMoves(_game.Level.MovesLeft);
            _hud.SetGoals(_game.Level.Goals.Goals);
            _hud.SetVisible(true);

            _map.SetVisible(false);
            _popup.Hide();
            _boardRoot.gameObject.SetActive(true);

            _input.ClearSelection();
            _input.AcceptsInput = true;
            UiButton.ActiveLayer = GameLayer;

            _progress.SetLastPlayed(index);
        }

        public void RestartLevel() => StartLevel(_levelIndex);

        /// <summary>
        /// Fits the board on screen and leaves room for the bar above and the buttons below.
        /// Recomputed per level because levels differ in size.
        /// </summary>
        private void LayoutBoard(LevelConfig config)
        {
            float aspect = _camera.aspect > 0.01f ? _camera.aspect : 16f / 9f;

            float neededHeight = config.Height + TopSpace + BottomSpace;
            float neededWidth = config.Width + 1.2f;

            float size = Mathf.Max(neededHeight * 0.5f, neededWidth * 0.5f / aspect);
            _camera.orthographicSize = size;

            // Keep the gap above the board constant, whichever dimension ended up constraining.
            float boardCentreY = size - TopSpace - config.Height * 0.5f;
            _boardRoot.localPosition = new Vector3(0f, boardCentreY, 0f);
        }

        // ------------------------------------------------------------------ actions

        /// <summary>True while a turn is playing out and input is locked.</summary>
        public bool IsBusy => _busy;

        /// <summary>Requests a swap, exactly as a drag would. Public so tooling can drive the game.</summary>
        public void TrySwap(GridPos a, GridPos b) => OnSwapRequested(a, b);

        /// <summary>Requests a tap, exactly as a click would.</summary>
        public void TryTap(GridPos cell) => OnTapRequested(cell);

        /// <summary>
        /// Redraws the goal row from the current <see cref="LevelRuntime"/> state. Normal play keeps
        /// the HUD in sync via <see cref="GoalProgressEvent"/> as it happens; this is for tooling that
        /// mutates goal progress directly and needs the HUD to catch up.
        /// </summary>
        public void SetGoalDisplay() => _hud.SetGoals(_game.Level.Goals.Goals);

        private void OnSwapRequested(GridPos a, GridPos b)
        {
            if (_busy || _popup.IsVisible || !_game.IsLoaded)
                return;

            TurnResult result = _game.Swap(a, b);
            if (result.Phases.Count > 0)
                StartCoroutine(PlayTurn(result));
        }

        private void OnTapRequested(GridPos cell)
        {
            if (_busy || _popup.IsVisible || !_game.IsLoaded)
                return;

            if (SwapRules.CanActivateBoosterAt(_game.Board, cell))
            {
                TurnResult result = _game.ActivateBooster(cell);
                if (result.Phases.Count > 0)
                    StartCoroutine(PlayTurn(result));
                return;
            }

            // Not a booster: treat the tap as picking the piece up for a click-click swap.
            _input.Select(cell);
        }

        private IEnumerator PlayTurn(TurnResult result)
        {
            _busy = true;
            _input.AcceptsInput = false;
            _input.ClearSelection();

            // The move is spent the moment the action is accepted, so show that immediately rather
            // than at the end of a long cascade.
            if (result.Accepted)
                _hud.SetMoves(result.MovesLeft);

            foreach (TurnPhase phase in result.Phases)
            {
                ApplyHudEvents(phase);
                PlaySound(phase);
                yield return _board.PlayPhase(phase);
            }

            _busy = false;

            switch (result.Outcome)
            {
                case LevelOutcome.Won:
                    ShowVictory();
                    break;
                case LevelOutcome.Lost:
                    ShowDefeat();
                    break;
                default:
                    _input.AcceptsInput = true;
                    break;
            }
        }

        private void ApplyHudEvents(TurnPhase phase)
        {
            foreach (BoardEvent evt in phase.Events)
            {
                switch (evt)
                {
                    case GoalProgressEvent goal:
                        _hud.UpdateGoal(goal.Color, goal.Collected, goal.Required);
                        break;
                    case MovesLeftChangedEvent moves:
                        _hud.SetMoves(moves.MovesLeft);
                        break;
                }
            }
        }

        /// <summary>
        /// One sound per phase, not per event: a cascade round clearing five pieces should be one
        /// satisfying beat, not five overlapping copies of the same clip.
        /// </summary>
        private void PlaySound(TurnPhase phase)
        {
            switch (phase.Kind)
            {
                case PhaseKind.Swap:
                    _audio.PlaySwap();
                    break;

                case PhaseKind.Clear:
                    bool boosterFired = false;
                    foreach (BoardEvent evt in phase.Events)
                    {
                        if (evt is BoosterActivatedEvent activated)
                        {
                            _audio.PlayBooster(activated.Type);
                            boosterFired = true;
                        }
                    }

                    if (!boosterFired)
                        _audio.PlayMatch();
                    break;
            }
        }

        private void ShowVictory()
        {
            _progress.UnlockThrough(_levelIndex + 1);
            _audio.PlayVictory();

            bool hasNext = _levelIndex + 1 < _levels.Count;
            int movesLeft = _game.Level.MovesLeft;

            string message = movesLeft > 0
                ? $"Cleared with {movesLeft} move{(movesLeft == 1 ? "" : "s")} to spare."
                : "Cleared on the very last move.";

            _popup.Show("Level complete", message, new Color(0.45f, 0.92f, 0.55f),
                hasNext ? "Next level" : "Back to map",
                () =>
                {
                    if (hasNext)
                        StartLevel(_levelIndex + 1);
                    else
                        ShowMap();
                },
                "Level map", ShowMap);
        }

        private void ShowDefeat()
        {
            _audio.PlayDefeat();
            _popup.Show("Out of moves", "The goal was not finished in time.",
                new Color(1f, 0.55f, 0.45f),
                "Try again", RestartLevel,
                "Level map", ShowMap);
        }
    }
}
