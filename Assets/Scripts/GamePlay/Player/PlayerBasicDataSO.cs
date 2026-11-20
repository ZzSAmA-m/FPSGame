using System;
using Core.Utility;
using UnityEngine;
namespace GamePlay.Player
{
    [CreateAssetMenu(fileName = "PlayerBasicDataSO", menuName = "Scriptable Objects/PlayerBasicDataSO")]
    public class PlayerBasicDataSO : ScriptableObject, ISerializationCallbackReceiver
    {
        [SerializeField]private EPlayerHandState _ePlayerHandState;          //玩家手持物状态
        [SerializeField, Min(1), Tooltip("出生时的生命值。")]
        private int _initialHealth = 100;
        [SerializeField, Min(1), Tooltip("生命值上限。")]
        private int _maxHealth = 100;
        [SerializeField, Min(0), Tooltip("出生时的护甲值。")]
        private int _initialArmor;
        [SerializeField, Min(0), Tooltip("护甲值上限。")]
        private int _maxArmor = 100;
        [SerializeField, Range(0f, 1f), Tooltip("由护甲等额分摊的伤害比例，护甲不足的部分扣除生命。")]
        private float _armorAbsorptionRatio = 0.5f;

        // 旧资产只有一个血量字段，加载时将其迁移为初始生命与生命上限。
        [SerializeField, HideInInspector] private int _playerHealth = -1;

        public EPlayerHandState playerHandState =>_ePlayerHandState;
        public int playerHealth => InitialHealth;
        public int InitialHealth => _initialHealth;
        public int MaxHealth => _maxHealth;
        public int InitialArmor => _initialArmor;
        public int MaxArmor => _maxArmor;
        public float ArmorAbsorptionRatio => _armorAbsorptionRatio;

        private void OnValidate() => ValidateValues();

        public void OnBeforeSerialize() { }

        public void OnAfterDeserialize() => ValidateValues();

        private void ValidateValues()
        {
            if (_playerHealth >= 0)
            {
                _maxHealth = Math.Max(1, _playerHealth);
                _initialHealth = _maxHealth;
                _playerHealth = -1;
            }

            _maxHealth = Math.Max(1, _maxHealth);
            _initialHealth = Math.Min(_maxHealth, Math.Max(1, _initialHealth));
            _maxArmor = Math.Max(0, _maxArmor);
            _initialArmor = Math.Min(_maxArmor, Math.Max(0, _initialArmor));
            if (float.IsNaN(_armorAbsorptionRatio) || float.IsInfinity(_armorAbsorptionRatio))
                _armorAbsorptionRatio = 0.5f;
            _armorAbsorptionRatio = Math.Min(1f, Math.Max(0f, _armorAbsorptionRatio));
        }
    }

}
