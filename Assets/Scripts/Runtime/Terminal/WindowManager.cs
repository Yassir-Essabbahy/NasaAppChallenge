using System;
using System.Collections.Generic;
using Outpost.Sim;
using UnityEngine;

namespace Outpost.Runtime.Terminal
{
    /// <summary>
    /// Manages up to 4 open Terminal OS windows: prevents duplicates, enforces focus z-order,
    /// triggers 150ms open/close animations, supports read-only tablet mode, and auto-closes oldest unfocused.
    /// </summary>
    public class WindowManager : MonoBehaviour
    {
        public const int MaxOpenWindows = 4;

        private readonly Dictionary<TerminalAppId, WindowView> _windows = new Dictionary<TerminalAppId, WindowView>();
        private readonly List<TerminalAppId> _openOrder = new List<TerminalAppId>();
        private readonly Vector2[] _cascadeOffsets = new Vector2[]
        {
            new Vector2(28f, -14f),
            new Vector2(76f, -34f),
            new Vector2(124f, -54f),
            new Vector2(172f, -74f)
        };

        private GameSession _session;
        private int _cascadeCounter;

        public bool IsReadOnlyMode { get; set; }
        public TerminalAppId? FocusedApp => _openOrder.Count > 0 ? _openOrder[_openOrder.Count - 1] : (TerminalAppId?)null;
        public IReadOnlyList<TerminalAppId> OpenOrder => _openOrder;

        public event Action OnWindowLayoutChanged;
        public event Action<TerminalAppId> OnWindowClosed;

        public void Initialize(GameSession session)
        {
            _session = session;
        }

        public void RegisterWindow(WindowView window)
        {
            if (window == null) return;
            _windows[window.AppId] = window;
        }

        public void OpenWindow(TerminalAppId appId)
        {
            if (!_windows.TryGetValue(appId, out var view)) return;

            if (view.IsOpen)
            {
                FocusWindow(appId);
                return;
            }

            if (_openOrder.Count >= MaxOpenWindows)
            {
                TerminalAppId oldest = _openOrder[0];
                CloseWindow(oldest, silent: true, immediate: true);
            }

            view.SetAnchoredPosition(_cascadeOffsets[_cascadeCounter % _cascadeOffsets.Length]);
            _cascadeCounter++;
            view.gameObject.SetActive(true);
            view.BeginOpenAnimation();
            _openOrder.Add(appId);
            view.OnOpen();

            UpdateFocusAndSiblingOrder();
            if (_session != null && _session.CurrentSnapshot != null)
            {
                _session.MarkAllUnreadLogsForAppRead(appId);
                view.Refresh(_session.CurrentSnapshot);
            }
            OnWindowLayoutChanged?.Invoke();
        }

        public void CloseWindow(TerminalAppId appId)
        {
            CloseWindow(appId, silent: false, immediate: false);
        }

        private void CloseWindow(TerminalAppId appId, bool silent, bool immediate)
        {
            if (!_windows.TryGetValue(appId, out var view)) return;
            if (!view.gameObject.activeSelf && !_openOrder.Contains(appId)) return;

            view.OnClose();
            _openOrder.Remove(appId);

            if (immediate || !view.gameObject.activeInHierarchy)
                view.gameObject.SetActive(false);
            else
                view.BeginCloseAnimation();

            if (!silent)
            {
                UpdateFocusAndSiblingOrder();
                OnWindowLayoutChanged?.Invoke();
                OnWindowClosed?.Invoke(appId);
            }
        }

        public void FocusWindow(TerminalAppId appId)
        {
            if (!_windows.TryGetValue(appId, out var view) || !view.IsOpen) return;

            _openOrder.Remove(appId);
            _openOrder.Add(appId);
            UpdateFocusAndSiblingOrder();
            OnWindowLayoutChanged?.Invoke();
        }

        public void CloseAll()
        {
            for (int i = _openOrder.Count - 1; i >= 0; i--)
            {
                CloseWindow(_openOrder[i], silent: true, immediate: true);
            }
            _openOrder.Clear();
            OnWindowLayoutChanged?.Invoke();
        }

        public void RefreshOpenWindows(SessionSnapshot snapshot)
        {
            if (snapshot == null) return;
            for (int i = 0; i < _openOrder.Count; i++)
            {
                if (_windows.TryGetValue(_openOrder[i], out var view) && view.IsOpen)
                    view.Refresh(snapshot);
            }
        }

        private void UpdateFocusAndSiblingOrder()
        {
            TerminalAppId? focused = FocusedApp;
            for (int i = 0; i < _openOrder.Count; i++)
            {
                var id = _openOrder[i];
                if (_windows.TryGetValue(id, out var view))
                {
                    view.transform.SetAsLastSibling();
                    view.SetFocusedVisual(focused.HasValue && focused.Value == id);
                }
            }
        }
    }
}
