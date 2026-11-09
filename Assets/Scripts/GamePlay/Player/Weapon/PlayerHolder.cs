using System;
using System.Collections.Generic;
#if UNITY_EDITOR
using System.Text;
using UnityEngine.InputSystem;
#endif
using GamePlay.HoldSystem;
using UnityEngine;

namespace GamePlay.Player
{
    /// <summary>管理单件长枪、手枪、刀和多件道具；切换持握时保留各栏的武器状态。</summary>
    [DisallowMultipleComponent]
    public sealed class PlayerHolder : MonoBehaviour
    {
        [SerializeField, Tooltip("角色骨骼的 wpn 节点；留空时在子节点中自动查找。")]
        private Transform _weaponMount;
        [SerializeField, Tooltip("初始刀；留空时使用 Resources/Weapons/DefaultKnife。")]
        private WeaponSO _defaultKnife;
        [SerializeField, Tooltip("额外的初始装备。单武器栏后项替换前项，道具追加；初始始终选中刀。")]
        private List<WeaponSO> _initialWeapons = new();

#if UNITY_EDITOR
        [SerializeField, Tooltip("在屏幕右下角显示本地玩家的武器调试信息，P 键切换，仅 Unity Editor 生效。")]
        private bool _showWeaponDebugGUI = true;
        private PlayerController _debugPlayerController;
        private GUIStyle _debugTextStyle;
        private readonly StringBuilder _debugText = new(1024);
        private readonly GUIContent _debugContent = new();
        private Vector2 _debugScrollPosition;
        private int _debugTextFrame = -1;
#endif

        private readonly List<WeaponEntry> _utilities = new();
        private IReadOnlyList<WeaponEntry> _utilityView;
        private bool _initialized;
        private bool _changing;
        private bool _destroying;

        public Transform WeaponMount => _weaponMount;
        public bool IsInitialized => _initialized;
        public WeaponEntry Rifle { get; private set; }
        public WeaponEntry Pistol { get; private set; }
        public WeaponEntry Knife { get; private set; }
        public IReadOnlyList<WeaponEntry> Utilities => _utilityView ??= _utilities.AsReadOnly();
        public int WeaponCount => (Rifle != null ? 1 : 0) + (Pistol != null ? 1 : 0) + (Knife != null ? 1 : 0);
        public int ItemCount => WeaponCount + _utilities.Count;
        public WeaponEntry CurrentEntry { get; private set; }
        public WeaponSlot? CurrentSlot => CurrentEntry?.Slot;
        public WeaponSO CurrentWeaponSO => CurrentEntry?.Definition;
        public WeaponBase CurrentWeapon => CurrentEntry?.Weapon;
        public IWeaponPresentationResources CurrentPresentationResources => CurrentEntry?.PresentationResources;
        public WeaponController CurrentController { get; private set; }
        public event Action OnWeaponChanged;
        public event Action OnInventoryChanged;

        private void Start() => InitializeLoadout();

        /// <summary>建立至少有一把刀的装备栏并选中刀；重复调用不会重置已有弹药或选择。</summary>
        public bool InitializeLoadout()
        {
            if (_destroying) return false;
            if (_initialized) return true;
            if (_changing) return false;
            _changing = true;
            try
            {
                WeaponSO definition = _defaultKnife != null ? _defaultKnife : Resources.Load<WeaponSO>("Weapons/DefaultKnife");
                if (definition == null || definition.Slot != WeaponSlot.Knife)
                    return Fail("默认刀必须是刀栏的 WeaponSO。");
                if (!TryCreateEntry(definition, out WeaponEntry knife)) return false;

                WeaponEntry rifle = null;
                WeaponEntry pistol = null;
                var utilities = new List<WeaponEntry>();
                if (_initialWeapons != null)
                {
                    foreach (WeaponSO initial in _initialWeapons)
                    {
                        if (initial == null) continue;
                        if (!TryCreateEntry(initial, out WeaponEntry entry)) continue;
                        switch (entry.Slot)
                        {
                            case WeaponSlot.Rifle: rifle = entry; break;
                            case WeaponSlot.Pistol: pistol = entry; break;
                            case WeaponSlot.Knife: knife = entry; break;
                            case WeaponSlot.Utility: utilities.Add(entry); break;
                        }
                    }
                }
                if (!TryCreateController(knife, out WeaponController controller)) return false;

                Rifle = rifle;
                Pistol = pistol;
                Knife = knife;
                _utilities.AddRange(utilities);
                CurrentEntry = knife;
                CurrentController = controller;
                _initialized = true;
                OnWeaponChanged?.Invoke();
                OnInventoryChanged?.Invoke();
                return true;
            }
            finally { _changing = false; }
        }

        /// <summary>获得新武器并持握；单武器栏替换原条目，道具栏追加。切换已有武器请用 SelectSlot。</summary>
        public bool EquipWeapon(WeaponSO definition) => AddWeapon(definition, true);

        /// <summary>获得装备；替换当前栏位时，即使 equip 为 false 也会同步更换手中实例。</summary>
        public bool AddWeapon(WeaponSO definition, bool equip = false)
        {
            if (!InitializeLoadout() || _changing || _destroying) return false;
            _changing = true;
            try
            {
                if (!TryCreateEntry(definition, out WeaponEntry entry)) return false;
                WeaponEntry replaced = entry.Slot == WeaponSlot.Utility ? null : GetEntry(entry.Slot);
                bool select = equip || ReferenceEquals(replaced, CurrentEntry);
                WeaponController controller = null;
                if (select && !TryCreateController(entry, out controller)) return false;

                StoreEntry(entry);
                if (select) ChangeCurrent(entry, controller);
                else RetireWeapon(replaced?.Weapon);
                if (select) OnWeaponChanged?.Invoke();
                OnInventoryChanged?.Invoke();
                return true;
            }
            finally { _changing = false; }
        }

        /// <summary>切换已拥有的栏位。道具由下标选择；空栏返回 false，不修改当前装备。</summary>
        public bool SelectSlot(WeaponSlot slot, int utilityIndex = 0)
        {
            if (!InitializeLoadout() || _changing || _destroying) return false;
            WeaponEntry entry = GetEntry(slot, utilityIndex);
            if (entry == null) return false;
            if (ReferenceEquals(entry, CurrentEntry)) return true;
            _changing = true;
            try
            {
                if (!TryCreateController(entry, out WeaponController controller)) return false;
                ChangeCurrent(entry, controller);
                OnWeaponChanged?.Invoke();
                return true;
            }
            finally { _changing = false; }
        }

        /// <summary>移除一个条目；不能移除最后一把非道具武器。移除当前条目会优先切回刀。</summary>
        public bool RemoveWeapon(WeaponSlot slot, int utilityIndex = 0)
        {
            if (!InitializeLoadout() || _changing || _destroying) return false;
            WeaponEntry entry = GetEntry(slot, utilityIndex);
            if (entry == null) return false;
            if (slot != WeaponSlot.Utility && WeaponCount <= 1)
                return false;
            _changing = true;
            try
            {
                bool current = ReferenceEquals(entry, CurrentEntry);
                WeaponEntry fallback = null;
                WeaponController controller = null;
                if (current)
                {
                    fallback = Knife != entry && Knife != null ? Knife :
                        Rifle != entry && Rifle != null ? Rifle : Pistol;
                    if (fallback == null || !TryCreateController(fallback, out controller)) return false;
                }

                switch (slot)
                {
                    case WeaponSlot.Rifle: Rifle = null; break;
                    case WeaponSlot.Pistol: Pistol = null; break;
                    case WeaponSlot.Knife: Knife = null; break;
                    case WeaponSlot.Utility: _utilities.RemoveAt(utilityIndex); break;
                }
                if (current) ChangeCurrent(fallback, controller);
                else RetireWeapon(entry.Weapon);
                if (current) OnWeaponChanged?.Invoke();
                OnInventoryChanged?.Invoke();
                return true;
            }
            finally { _changing = false; }
        }

        /// <summary>移除当前条目并选中剩余武器；最后一把武器不能卸下。</summary>
        public bool UnequipWeapon()
        {
            if (!InitializeLoadout() || CurrentEntry == null) return false;
            int index = CurrentEntry.Slot == WeaponSlot.Utility ? _utilities.IndexOf(CurrentEntry) : 0;
            return RemoveWeapon(CurrentEntry.Slot, index);
        }

        private WeaponEntry GetEntry(WeaponSlot slot, int utilityIndex = 0) => slot switch
        {
            WeaponSlot.Rifle => Rifle,
            WeaponSlot.Pistol => Pistol,
            WeaponSlot.Knife => Knife,
            WeaponSlot.Utility => utilityIndex >= 0 && utilityIndex < _utilities.Count ? _utilities[utilityIndex] : null,
            _ => null
        };

        private void StoreEntry(WeaponEntry entry)
        {
            switch (entry.Slot)
            {
                case WeaponSlot.Rifle: Rifle = entry; break;
                case WeaponSlot.Pistol: Pistol = entry; break;
                case WeaponSlot.Knife: Knife = entry; break;
                case WeaponSlot.Utility: _utilities.Add(entry); break;
            }
        }

        private bool TryCreateEntry(WeaponSO definition, out WeaponEntry entry)
        {
            entry = null;
            if (definition == null) return Fail("没有提供 WeaponSO。");
            if (definition.Slot < WeaponSlot.Rifle || definition.Slot > WeaponSlot.Utility)
                return Fail("武器的装备栏类型无效。");
            WeaponController prefab = definition.ControllerPrefab;
            if (prefab == null || prefab.transform.parent != null || !prefab.gameObject.activeSelf)
                return Fail("武器配置需要提供启用的模型预制体根节点控制器。");
            try
            {
                WeaponBase weapon = definition.CreateWeapon();
                if (weapon == null || !prefab.CanBind(weapon))
                    return Fail("WeaponSO 创建的武器与预制体控制器类型不匹配。");
                entry = new WeaponEntry(definition, weapon);
                return true;
            }
            catch (Exception exception)
            {
                Debug.LogException(exception, this);
                return false;
            }
        }

        private bool TryCreateController(WeaponEntry entry, out WeaponController controller)
        {
            controller = null;
            if (!ResolveMount()) return Fail("找不到角色的 wpn 节点，请配置 Weapon Mount。");
            WeaponController prefab = entry.Definition.ControllerPrefab;
            if (prefab == null || prefab.transform.parent != null || !prefab.gameObject.activeSelf || !prefab.CanBind(entry.Weapon))
                return Fail("装备的预制体配置无效，保留当前武器。");
            try
            {
                controller = Instantiate(prefab, _weaponMount, false);
                controller.name = prefab.name;
                controller.transform.localPosition = Vector3.zero;
                controller.transform.localRotation = Quaternion.identity;
                controller.Bind(this, entry);
                return true;
            }
            catch (Exception exception)
            {
                ReleaseController(controller);
                controller = null;
                Debug.LogException(exception, this);
                return false;
            }
        }

        private void ChangeCurrent(WeaponEntry entry, WeaponController controller)
        {
            ReleaseController(CurrentController);
            RetireWeapon(CurrentWeapon);
            CurrentEntry = entry;
            CurrentController = controller;
        }

        private bool ResolveMount()
        {
            if (_weaponMount != null) return _weaponMount.IsChildOf(transform);
            foreach (Transform child in GetComponentsInChildren<Transform>(true))
            {
                if (child.name != "wpn") continue;
                _weaponMount = child;
                return true;
            }
            return false;
        }

        private void RetireWeapon(WeaponBase weapon)
        {
            try
            {
                if (weapon is Gun gun) gun.Unequip();
            }
            catch (Exception exception) { Debug.LogException(exception, this); }
        }

        private static void ReleaseController(WeaponController controller)
        {
            if (controller == null) return;
            try { controller.Unbind(); }
            catch (Exception exception) { Debug.LogException(exception, controller); }
            controller.gameObject.SetActive(false);
            controller.transform.SetParent(null, false);
            if (Application.isPlaying) Destroy(controller.gameObject);
            else DestroyImmediate(controller.gameObject);
        }

        private bool Fail(string message)
        {
            Debug.LogWarning($"[{nameof(PlayerHolder)}] {message}", this);
            return false;
        }

#if UNITY_EDITOR
        private void Update()
        {
            if (!IsLocalDebugPlayer()) return;
            Keyboard keyboard = Keyboard.current;
            if (keyboard == null || !keyboard.pKey.wasPressedThisFrame) return;
            _showWeaponDebugGUI = !_showWeaponDebugGUI;
            _debugTextFrame = -1;
        }

        private bool IsLocalDebugPlayer()
        {
            if (_debugPlayerController == null) _debugPlayerController = GetComponent<PlayerController>();
            // 使用 Mirror 的本地玩家身份，避免远端角色和纯服务端响应快捷键或绘制面板。
            return _debugPlayerController != null && _debugPlayerController.isClient && _debugPlayerController.isLocalPlayer;
        }

        private void OnGUI()
        {
            if (!_showWeaponDebugGUI || !Application.isPlaying || Screen.width <= 80 || Screen.height <= 80) return;
            if (!IsLocalDebugPlayer()) return;

            _debugTextStyle ??= new GUIStyle(GUI.skin.label)
            {
                fontSize = 14,
                wordWrap = true,
                richText = false,
                normal = { textColor = Color.white }
            };
            // OnGUI 一帧会收到多个事件，文本只按帧构建一次。
            if (_debugTextFrame != Time.frameCount)
            {
                RebuildDebugText();
                _debugTextFrame = Time.frameCount;
            }

            const float margin = 12f;
            const float padding = 10f;
            float width = Mathf.Min(440f, Screen.width - margin * 2f);
            float textWidth = width - padding * 2f - 20f;
            float textHeight = _debugTextStyle.CalcHeight(_debugContent, textWidth);
            float height = Mathf.Min(textHeight + padding * 2f, Mathf.Min(600f, Screen.height - margin * 2f));
            var panel = new Rect(Screen.width - width - margin, Screen.height - height - margin, width, height);

            Color previousColor = GUI.color;
            GUI.color = new Color(0.04f, 0.04f, 0.04f, 0.85f);
            GUI.DrawTexture(panel, Texture2D.whiteTexture);
            GUI.color = Color.white;
            var viewport = new Rect(panel.x + padding, panel.y + padding, width - padding * 2f, height - padding * 2f);
            _debugScrollPosition = GUI.BeginScrollView(viewport, _debugScrollPosition, new Rect(0f, 0f, textWidth, textHeight));
            GUI.Label(new Rect(0f, 0f, textWidth, textHeight), _debugContent, _debugTextStyle);
            GUI.EndScrollView();
            GUI.color = previousColor;
        }

        private void RebuildDebugText()
        {
            _debugText.Clear();
            _debugText.Append("武器调试 [P 隐藏] | 本地玩家 ").Append(_debugPlayerController.netId)
                .Append(" | ").AppendLine(_initialized ? "已初始化" : "未初始化");
            _debugText.AppendLine().AppendLine("当前武器");
            AppendDebugEntry(CurrentEntry, "手持");
            _debugText.Append("控制器：").AppendLine(CurrentController != null ?
                $"{CurrentController.name} ({CurrentController.GetType().Name})" : "无");
            if (CurrentWeapon is Gun gun)
            {
                _debugText.Append("动作 Tick：准备 ").Append(gun.Config.PrepareTicks)
                    .Append(" / 射击间隔 ").Append(gun.Config.ShotIntervalTicks)
                    .Append(" / 换弹 ").Append(gun.Config.ReloadTicks).AppendLine();
            }

            _debugText.AppendLine().Append("武器库（").Append(ItemCount).AppendLine(" 件，* 为当前武器）");
            AppendDebugEntry(Rifle, "长枪");
            AppendDebugEntry(Pistol, "手枪");
            AppendDebugEntry(Knife, "刀");
            _debugText.Append("道具（").Append(_utilities.Count).AppendLine(" 件）");
            for (int i = 0; i < _utilities.Count; i++)
                AppendDebugEntry(_utilities[i], $"道具 {i + 1}");
            _debugContent.text = _debugText.ToString();
        }

        private void AppendDebugEntry(WeaponEntry entry, string label)
        {
            _debugText.Append(entry != null && ReferenceEquals(entry, CurrentEntry) ? "* " : "  ")
                .Append('[').Append(label).Append("] ");
            if (entry == null)
            {
                _debugText.AppendLine("空");
                return;
            }

            WeaponBase weapon = entry.Weapon;
            _debugText.Append(weapon.Name).Append(" | ").Append(entry.Slot).AppendLine();
            _debugText.Append("  配置：").AppendLine(entry.Definition != null ? entry.Definition.name : "缺失");
            if (!string.IsNullOrEmpty(weapon.Info))
                _debugText.Append("  介绍：").AppendLine(weapon.Info);
            if (weapon is Gun gun)
            {
                Magazine magazine = gun.Magazine;
                _debugText.Append("  状态：").Append(gun.GunState)
                    .Append(" | 弹匣 ").Append(magazine.Rounds).Append('/').Append(magazine.Capacity)
                    .Append(" | 备弹 ").Append(magazine.ReserveRounds)
                    .Append(" | 总弹量 ").Append(magazine.TotalBullets).AppendLine();
            }
        }
#endif

        private void OnDestroy()
        {
            _destroying = true;
            ReleaseController(CurrentController);
            RetireWeapon(Rifle?.Weapon);
            RetireWeapon(Pistol?.Weapon);
            RetireWeapon(Knife?.Weapon);
            foreach (WeaponEntry item in _utilities) RetireWeapon(item.Weapon);
            CurrentController = null;
            CurrentEntry = Rifle = Pistol = Knife = null;
            _utilities.Clear();
        }
    }
}
