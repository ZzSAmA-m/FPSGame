using GamePlay.HoldSystem;
using UnityEngine;

namespace GamePlay.Player
{
    [DisallowMultipleComponent]
    public sealed class PlayerHUD : MonoBehaviour
    {
        private PlayerController _player;
        private PlayerStatsController _stats;
        private PlayerHolder _holder;
        private GUIStyle _style;

        private void Awake()
        {
            _player = GetComponent<PlayerController>();
            _stats = GetComponent<PlayerStatsController>();
            _holder = GetComponent<PlayerHolder>();
        }

        private void OnGUI()
        {
            if (_player == null || !_player.isClient || !_player.isLocalPlayer ||
                _stats == null || !_stats.TryGetState(out PlayerStatsState state))
                return;

            _style ??= new GUIStyle(GUI.skin.label)
            {
                fontSize = 24,
                alignment = TextAnchor.LowerLeft,
                wordWrap = false,
                richText = false,
                normal = { textColor = Color.white }
            };

            int rounds = 0;
            int reserveRounds = 0;
            if (_holder != null && _holder.CurrentWeapon is Gun gun)
            {
                rounds = gun.Magazine.Rounds;
                reserveRounds = gun.Magazine.ReserveRounds;
            }

            const float margin = 16f;
            float height = _style.lineHeight * 2f + _style.padding.vertical;
            var area = new Rect(margin, Screen.height - margin - height, Screen.width - margin * 2f, height);
            string text = $"[{state.Armor}]{state.Health}\n{rounds}/{reserveRounds}";
            Color previousColor = GUI.color;
            GUI.color = Color.black;
            GUI.Label(new Rect(area.x + 1f, area.y + 1f, area.width, area.height), text, _style);
            GUI.color = Color.white;
            GUI.Label(area, text, _style);
            GUI.color = previousColor;
        }
    }
}
