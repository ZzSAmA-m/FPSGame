using Core.ZLog;
using Sirenix.OdinInspector;
using UnityEngine;
#if UNITY_EDITOR
using System.Collections.Generic;
using Mirror;
using Network.Client;
using UnityEngine.InputSystem;
#endif

namespace Network.Server
{
    [RequireComponent(typeof(ServerManager))]
    public class ServerManagerDebugger : MonoBehaviour
    {
        private ServerManager _manager;
#if UNITY_EDITOR
        [SerializeField, Tooltip("显示底部服务端连接面板；运行时按 F8 切换。")]
        private bool _showConnectionsGUI = true;
        private readonly List<NetworkConnectionToClient> _debugConnections = new();
        private readonly List<DebugClientRow> _debugRows = new();
        private readonly GUIContent[] _debugHeaders =
        {
            new("ID"), new("地址"), new("状态"), new("玩家 netId"), new("RTT")
        };
        private readonly GUIContent _debugTooltip = new();
        private static readonly float[] DebugColumnWidths = { 0.16f, 0.29f, 0.29f, 0.14f, 0.12f };
        private GUIStyle _debugLabelStyle;
        private GUIStyle _debugTooltipStyle;
        private Vector2 _debugScroll;
        private bool _debugWasVisible;
        private double _debugNextRefresh;
        private string _debugTitle;
#endif

        void Awake()
        {
            _manager=GetComponent<ServerManager>();
        }
        [Button]
        public void LogClientSessions()
        {
            ZLog.Log<ServerManagerDebugger>(_manager.Sessions.ToString());
        }

#if UNITY_EDITOR
        private void Update()
        {
            if (!Application.isPlaying || !NetworkServer.active)
            {
                _debugWasVisible = false;
                return;
            }

            if (Keyboard.current != null && Keyboard.current.f8Key.wasPressedThisFrame)
                _showConnectionsGUI = !_showConnectionsGUI;
            if (!_showConnectionsGUI)
            {
                _debugWasVisible = false;
                return;
            }

            double now = Time.unscaledTimeAsDouble;
            if (!_debugWasVisible || now >= _debugNextRefresh)
            {
                RefreshDebugRows();
                _debugNextRefresh = now + 0.25d;
            }
            _debugWasVisible = true;
        }

        private void RefreshDebugRows()
        {
            _debugConnections.Clear();
            _debugConnections.AddRange(NetworkServer.connections.Values);
            _debugConnections.Sort((left, right) => left.connectionId.CompareTo(right.connectionId));
            _debugRows.Clear();
            foreach (NetworkConnectionToClient connection in _debugConnections)
            {
                string state = _manager.Sessions.TryGet(connection.connectionId, out ClientSession session)
                    ? session.State.ToString() : "无会话";
                string player = connection.identity != null ? connection.identity.netId.ToString() : "无玩家";
                string rtt = connection is LocalConnectionToClient ? "本地" :
                    connection.rtt > 0d ? $"{connection.rtt * 1000d:F1} ms" : "待测";
                _debugRows.Add(new DebugClientRow(connection.connectionId.ToString(), connection.address, state, player, rtt));
            }
            _debugTitle = $"服务端连接：{_debugRows.Count} | F8 显示/隐藏";
        }

        private void OnGUI()
        {
            if (!_showConnectionsGUI || !Application.isPlaying || !NetworkServer.active ||
                Screen.width <= 120 || Screen.height <= 100) return;

            _debugLabelStyle ??= new GUIStyle(GUI.skin.label)
            {
                fontSize = 12,
                wordWrap = false,
                richText = false,
                clipping = TextClipping.Clip,
                normal = { textColor = Color.white }
            };
            _debugTooltipStyle ??= new GUIStyle(_debugLabelStyle) { wordWrap = true };

            const float margin = 12f;
            const float padding = 10f;
            const float rowHeight = 20f;
            float width = Mathf.Min(600f, Screen.width - margin * 2f);
            float contentHeight = Mathf.Max(1, _debugRows.Count) * rowHeight;
            float height = Mathf.Min(180f, Mathf.Min(Screen.height - margin * 2f,
                padding * 2f + rowHeight * 2f + contentHeight));
            var panel = new Rect((Screen.width - width) * 0.5f, Screen.height - height - margin, width, height);
            float contentWidth = width - padding * 2f - 20f;
            Color previousColor = GUI.color;
            GUI.color = new Color(0.04f, 0.04f, 0.04f, 0.85f);
            GUI.DrawTexture(panel, Texture2D.whiteTexture);
            GUI.color = Color.white;

            GUI.Label(new Rect(panel.x + padding, panel.y + padding, width - padding * 2f, rowHeight),
                _debugTitle ?? "服务端连接", _debugLabelStyle);
            DrawDebugColumns(new Rect(panel.x + padding, panel.y + padding + rowHeight, contentWidth, rowHeight), _debugHeaders);
            var viewport = new Rect(panel.x + padding, panel.y + padding + rowHeight * 2f,
                width - padding * 2f, height - padding * 2f - rowHeight * 2f);
            _debugScroll = GUI.BeginScrollView(viewport, _debugScroll, new Rect(0f, 0f, contentWidth, contentHeight));
            if (_debugRows.Count == 0)
                GUI.Label(new Rect(0f, 0f, contentWidth, rowHeight), "暂无客户端连接", _debugLabelStyle);
            for (int i = 0; i < _debugRows.Count; i++)
                DrawDebugColumns(new Rect(0f, i * rowHeight, contentWidth, rowHeight), _debugRows[i].Cells);
            GUI.EndScrollView();

            if (viewport.Contains(Event.current.mousePosition)) DrawDebugTooltip();
            GUI.color = previousColor;
        }

        private void DrawDebugColumns(Rect row, GUIContent[] cells)
        {
            float x = row.x;
            for (int i = 0; i < cells.Length; i++)
            {
                float width = row.width * DebugColumnWidths[i];
                GUI.Label(new Rect(x, row.y, width - 4f, row.height), cells[i], _debugLabelStyle);
                x += width;
            }
        }

        private void DrawDebugTooltip()
        {
            if (Event.current.type != EventType.Repaint || string.IsNullOrEmpty(GUI.tooltip)) return;
            _debugTooltip.text = GUI.tooltip;
            float width = Mathf.Min(400f, Mathf.Min(Screen.width - 24f,
                _debugTooltipStyle.CalcSize(_debugTooltip).x + 16f));
            float height = Mathf.Min(Screen.height - 24f, _debugTooltipStyle.CalcHeight(_debugTooltip, width - 16f) + 12f);
            Vector2 mouse = Event.current.mousePosition;
            var tooltip = new Rect(Mathf.Clamp(mouse.x + 14f, 12f, Screen.width - width - 12f),
                Mathf.Clamp(mouse.y + 18f, 12f, Screen.height - height - 12f), width, height);
            GUI.color = new Color(0.04f, 0.04f, 0.04f, 0.95f);
            GUI.DrawTexture(tooltip, Texture2D.whiteTexture);
            GUI.color = Color.white;
            GUI.Label(new Rect(tooltip.x + 8f, tooltip.y + 6f, width - 16f, height - 12f), _debugTooltip, _debugTooltipStyle);
        }

        private sealed class DebugClientRow
        {
            public readonly GUIContent[] Cells;

            public DebugClientRow(string id, string address, string state, string player, string rtt)
            {
                Cells = new[]
                {
                    new GUIContent(id), new GUIContent(address, address), new GUIContent(state),
                    new GUIContent(player), new GUIContent(rtt)
                };
            }
        }
#endif
    }
}
