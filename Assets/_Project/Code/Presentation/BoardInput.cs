using System;
using Match3.Core;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Match3.Presentation
{
    /// <summary>
    /// Turns pointer gestures into game actions.
    /// <para>
    /// Two gestures, matching what players expect from the reference games: drag a piece towards a
    /// neighbour to swap, or tap a booster to set it off. Click-then-click on two adjacent cells
    /// also swaps, which is what people try on a desktop.
    /// </para>
    /// <para>
    /// Reads <see cref="Pointer"/> directly, so mouse, touch and pen all work through one path and
    /// the game needs no EventSystem at all.
    /// </para>
    /// </summary>
    public sealed class BoardInput : MonoBehaviour
    {
        /// <summary>Screen-space distance before a press counts as a drag rather than a tap.</summary>
        private const float DragThresholdPixels = 12f;

        private Camera _camera;
        private BoardView _board;

        private bool _pressing;
        private GridPos _pressCell;
        private Vector2 _pressScreen;
        private bool _dragConsumed;
        private GridPos? _selected;

        /// <summary>Raised for a legal-looking swap request; the game decides whether it is legal.</summary>
        public event Action<GridPos, GridPos> SwapRequested;

        public event Action<GridPos> TapRequested;

        /// <summary>Set false while a turn is animating or a popup is open.</summary>
        public bool AcceptsInput { get; set; } = true;

        public static BoardInput Create(Transform parent, Camera camera, BoardView board)
        {
            var go = new GameObject("board-input");
            go.transform.SetParent(parent, false);

            var input = go.AddComponent<BoardInput>();
            input._camera = camera;
            input._board = board;
            return input;
        }

        public void ClearSelection()
        {
            _selected = null;
            _board.SetSelection(null);
        }

        private void Update()
        {
            Pointer pointer = Pointer.current;
            if (pointer == null)
                return;

            if (!AcceptsInput)
            {
                if (_pressing)
                    CancelPress();
                return;
            }

            Vector2 screen = pointer.position.ReadValue();

            if (pointer.press.wasPressedThisFrame)
                BeginPress(screen);
            else if (_pressing && pointer.press.isPressed)
                ContinuePress(screen);
            else if (_pressing && pointer.press.wasReleasedThisFrame)
                EndPress(screen);
        }

        private void BeginPress(Vector2 screen)
        {
            if (!TryGetCell(screen, out GridPos cell))
                return;

            _pressing = true;
            _dragConsumed = false;
            _pressCell = cell;
            _pressScreen = screen;
        }

        private void ContinuePress(Vector2 screen)
        {
            if (_dragConsumed)
                return;

            if ((screen - _pressScreen).sqrMagnitude < DragThresholdPixels * DragThresholdPixels)
                return;

            // Snap the drag to the dominant axis so a sloppy diagonal still does what was meant.
            Vector2 delta = screen - _pressScreen;
            GridPos target = Mathf.Abs(delta.x) > Mathf.Abs(delta.y)
                ? new GridPos(_pressCell.X + (delta.x > 0 ? 1 : -1), _pressCell.Y)
                : new GridPos(_pressCell.X, _pressCell.Y + (delta.y > 0 ? 1 : -1));

            _dragConsumed = true;
            ClearSelection();
            SwapRequested?.Invoke(_pressCell, target);
        }

        private void EndPress(Vector2 screen)
        {
            _pressing = false;

            if (_dragConsumed)
                return;

            if (!TryGetCell(screen, out GridPos cell) || cell != _pressCell)
            {
                ClearSelection();
                return;
            }

            if (_selected.HasValue)
            {
                GridPos previous = _selected.Value;
                ClearSelection();

                if (previous == cell)
                    return; // tapping the selection again just clears it

                if (previous.IsOrthogonalNeighbourOf(cell))
                {
                    SwapRequested?.Invoke(previous, cell);
                    return;
                }
            }

            TapRequested?.Invoke(cell);
        }

        private void CancelPress()
        {
            _pressing = false;
            _dragConsumed = false;
            ClearSelection();
        }

        /// <summary>Called by the game when a tap did not activate anything, so it becomes a selection.</summary>
        public void Select(GridPos cell)
        {
            _selected = cell;
            _board.SetSelection(cell);
        }

        private bool TryGetCell(Vector2 screen, out GridPos cell)
        {
            cell = default;
            if (_camera == null || _board == null)
                return false;

            var screenPoint = new Vector3(screen.x, screen.y, Mathf.Abs(_camera.transform.position.z));
            Vector3 world = _camera.ScreenToWorldPoint(screenPoint);
            return _board.WorldToCell(world, out cell);
        }
    }
}
