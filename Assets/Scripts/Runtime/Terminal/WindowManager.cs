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

        public TerminalAppId? FocusedApp
        {
            get
            {
                for (int i = _openOrder.Count - 1; i >= 0; i--)
                {
                    var id = _openOrder[i];
                    if (_windows.TryGetValue(id, out var view) && !view.IsMinimized && view.gameObject.activeSelf)
                        return id;
                }
                return null;
            }
        }

        public IReadOnlyList<TerminalAppId> OpenOrder => _openOrder;

        public event Action OnWindowLayoutChanged;
        public event Action<TerminalAppId> OnWindowClosed;
        public event Action OnWindowSoundRequested;

        public void Initialize(GameSession session)
        {
            _session = session;
        }

        public void RegisterWindow(WindowView window)
        {
            if (window == null) return;
            _windows[window.AppId] = window;
        }

        public bool IsWindowOpen(TerminalAppId appId)
        {
            return _openOrder.Contains(appId);
        }

        public bool IsWindowMinimized(TerminalAppId appId)
        {
            return _windows.TryGetValue(appId, out var view) && view.IsMinimized;
        }

        public void SetWorkspace(RectTransform workspaceBounds, Vector2 windowSize)
        {
            if (workspaceBounds == null) return;
            foreach (var kvp in _windows)
            {
                if (kvp.Value != null)
                    kvp.Value.SetWorkspace(workspaceBounds, windowSize);
            }
            UpdateFocusAndSiblingOrder();
        }

        public void OpenWindow(TerminalAppId appId)
        {
            if (!_windows.TryGetValue(appId, out var view)) return;

            if (_openOrder.Contains(appId))
            {
                if (view.IsMinimized || !view.gameObject.activeSelf)
                {
                    view.gameObject.SetActive(true);
                    view.BeginOpenAnimation();
                }
                FocusWindow(appId);
                OnWindowSoundRequested?.Invoke();
                return;
            }

            if (_openOrder.Count >= MaxOpenWindows)
            {
                TerminalAppId oldest = PickEvictionTarget();
                CloseWindow(oldest, silent: true, immediate: true);
            }

            view.SetAnchoredPosition(_cascadeOffsets[_cascadeCounter % _cascadeOffsets.Length]);
            _cascadeCounter++;
            view.gameObject.SetActive(true);
            view.BeginOpenAnimation();
            _openOrder.Add(appId);
            view.OnOpen();
            OnWindowSoundRequested?.Invoke();

            UpdateFocusAndSiblingOrder();
            if (_session != null && _session.CurrentSnapshot != null)
            {
                _session.MarkAllUnreadLogsForAppRead(appId);
                view.Refresh(_session.CurrentSnapshot);
            }
            OnWindowLayoutChanged?.Invoke();
        }

        private TerminalAppId PickEvictionTarget()
        {
            // Prefer evicting an already minimized window first; otherwise oldest unfocused.
            for (int i = 0; i < _openOrder.Count; i++)
            {
                if (_windows.TryGetValue(_openOrder[i], out var v) && v.IsMinimized)
                    return _openOrder[i];
            }
            return _openOrder[0];
        }

        public void MinimizeWindow(TerminalAppId appId)
        {
            if (!_windows.TryGetValue(appId, out var view) || !_openOrder.Contains(appId)) return;
            if (view.IsMinimized) return;

            view.BeginMinimizeAnimation();
            OnWindowSoundRequested?.Invoke();
            UpdateFocusAndSiblingOrder();
            OnWindowLayoutChanged?.Invoke();
        }

        public void ToggleWindowFromTaskbar(TerminalAppId appId)
        {
            if (!_windows.TryGetValue(appId, out var view)) return;
            if (!_openOrder.Contains(appId))
            {
                OpenWindow(appId);
                return;
            }

            var focused = FocusedApp;
            if (!view.IsMinimized && focused.HasValue && focused.Value == appId)
                MinimizeWindow(appId);
            else
                OpenWindow(appId);
        }

        public void CloseWindow(TerminalAppId appId)
        {
            CloseWindow(appId, silent: false, immediate: false);
        }

        private void CloseWindow(TerminalAppId appId, bool silent, bool immediate)
        {
            if (!_windows.TryGetValue(appId, out var view)) return;
            if (!view.gameObject.activeSelf && !view.IsMinimized && !_openOrder.Contains(appId)) return;

            view.OnClose();
            _openOrder.Remove(appId);

            if (immediate || !view.gameObject.activeInHierarchy)
            {
                view.IsMinimized = false;
                view.gameObject.SetActive(false);
            }
            else
            {
                view.BeginCloseAnimation();
            }

            if (!silent)
            {
                OnWindowSoundRequested?.Invoke();
                UpdateFocusAndSiblingOrder();
                OnWindowLayoutChanged?.Invoke();
                OnWindowClosed?.Invoke(appId);
            }
        }

        public void FocusWindow(TerminalAppId appId)
        {
            if (!_windows.TryGetValue(appId, out var view) || !_openOrder.Contains(appId)) return;

            if (view.IsMinimized || !view.gameObject.activeSelf)
            {
                view.gameObject.SetActive(true);
                view.BeginOpenAnimation();
            }

            _openOrder.Remove(appId);
            _openOrder.Add(appId);
            UpdateFocusAndSiblingOrder();
            if (_session != null && _session.CurrentSnapshot != null)
                view.Refresh(_session.CurrentSnapshot);
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
                    if (!view.IsMinimized)
                        view.transform.SetAsLastSibling();
                    view.SetFocusedVisual(focused.HasValue && focused.Value == id);
                }
            }
        }
    }
}
