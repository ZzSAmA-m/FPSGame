// ============================================================
// EnemyHealth.cs —— 基础敌人生命（血量 / 护盾 / 死亡）系统
// 任何需要被子弹命中的敌人对象都可挂载此组件（或继承自它），
// 它实现了 IDamageable 接口，负责接收伤害、扣除护盾与生命、
// 更新血条 UI、触发受击 / 死亡事件以及死亡后的销毁处理。
// 此外还支持可选的存档（SAVE_LOAD_ADD_ON）与自定义 Inspector。
// ============================================================
/// <summary>
/// This script belongs to cowsins™ as a part of the cowsins´ FPS Engine. All rights reserved. 
/// </summary>
#if UNITY_EDITOR
using UnityEditor;
#endif
#if SAVE_LOAD_ADD_ON
using cowsins.SaveLoad;
using System.Collections.Generic;
#endif
using UnityEngine;
using UnityEngine.UI;
using System.Collections;
using UnityEngine.Events;

namespace cowsins
{
    /// <summary>
    /// Super simple enemy script that allows any object with this component attached to receive damage,aim towards the player and shoot at it.
    /// This is not definitive and it will 100% be modified and re structured in future updates
    /// </summary>
    /// <summary>敌人生命系统：管理敌人血量 / 护盾、受击、死亡与相关 UI / 事件。</summary>
    public class EnemyHealth : Identifiable, IDamageable
    {
        [System.Serializable]
        /// <summary>敌人事件集合：包含出生（OnSpawn）、受击（OnDamaged）与死亡（OnDeath）三个 UnityEvent。</summary>
        public class Events
        {
            public UnityEvent OnSpawn, OnDamaged, OnDeath;
        }

        [Tooltip("Name of the enemy. This will appear on the killfeed"), SerializeField]
        // 敌人名称（死亡时显示在击杀信息流 Killfeed 中）。
        protected string _name;

        // 当前生命值（Inspector 中只读显示，可被存档）。
        [ReadOnly, SaveField] public float health;

        // 当前护盾值（Inspector 中只读显示，可被存档）。
        [ReadOnly, SaveField] public float shield;

        [Tooltip("initial enemy health "), SerializeField]
        // 初始最大生命值。
        protected float maxHealth;

        [Tooltip("initial enemy shield"), SerializeField]
        // 初始最大护盾值。
        protected float maxShield;

        // 死亡时是否销毁整个游戏物体。
        [Tooltip("When the object dies, decide if it should be destroyed or not."), SerializeField] private bool destroyOnDie;

        // 死亡时若不销毁物体，是否销毁 EnemyHealth 组件本身。
        [Tooltip("When the enemy dies, remove EnemyHealthComponent if true."), SerializeField] private bool destroyComponentOnDie;

        // 死亡时在敌人位置实例化的死亡特效。
        [SerializeField] private GameObject deathEffect;

        [Tooltip("Object that Contains Health & Shield Bar"), SerializeField]
        // 承载血条 / 护盾条 UI 的容器对象。
        public GameObject enemyStatusContainer;

        [Tooltip("display enemy status via UI"), SerializeField]
        // 血条与护盾条 UI 的 Image 引用。
        public Image healthBar, shieldBar;

        [Tooltip("If true, it will display the UI with the shield and health sliders."), SerializeField]
        // 是否显示血条 / 护盾条 UI。
        private bool showUI;

        // 是否显示伤害飘字。
        public bool showDamagePopUps;

        [Tooltip("If true, it will display the KillFeed UI."), SerializeField]
        // 死亡时是否显示击杀信息流（Killfeed）UI。
        protected bool showKillFeed;

        [SerializeField]
        // 死亡音效。
        protected AudioClip dieSFX;

        // 敌人事件集合（出生 / 受击 / 死亡），供外部订阅。
        public Events events;

        // 是否已死亡。
        protected bool isDead;

        // 以下为供外部（如自定义 Inspector）读取状态的只读属性。
        public bool IsDead => isDead;

        public bool DestroyOnDie => destroyOnDie;
        public bool ShowUI => showUI;
        public Image HealthSlider => healthBar;
        public Image ShieldSlider => shieldBar;


        /// <summary>初始化敌人状态：将生命 / 护盾设为最大值，触发出生事件并初始化血条显示。</summary>
        public virtual void Start()
        {
            // Status initial settings
            health = maxHealth;
            shield = maxShield;

            // Spawn
            events.OnSpawn.Invoke();

            // UI 
            if (healthBar != null) healthBar.fillAmount = health / maxHealth;
            if (shieldBar != null) shieldBar.fillAmount = shield / maxShield;
        }

        /// <summary>
        /// Since it is IDamageable, it can take damage, if a shot is landed, damage the enemy
        /// </summary>
        /// <summary>接收伤害：先扣除护盾，护盾耗尽后扣除生命，更新血条并触发受击事件，生命归零时触发死亡。</summary>
        public virtual void Damage(float _damage, bool isHeadshot)
        {
            if (isDead) return;

            float damage = Mathf.Abs(_damage);
            float oldDmg = damage;
            if (damage <= shield) // Shield will be damaged
            {
                shield -= damage;
            }
            else
            {
                damage = damage - shield;
                shield = 0;
                health -= damage;
                UpdateHealthBar();
            }
            UpdateShieldBar();

            // Custom event on damaged
            events.OnDamaged.Invoke();
            UIEvents.onEnemyHit?.Invoke(isHeadshot, showDamagePopUps, transform.position, oldDmg);
#if SAVE_LOAD_ADD_ON
            StoreData();
#endif
            if (health <= 0 && !isDead) Die();
        }

        /// <summary>平滑更新血条填充量到当前生命占比。</summary>
        public virtual void UpdateHealthBar()
        {
            if (healthBar != null)
            {
                if (healthLerpCoroutine != null) StopCoroutine(healthLerpCoroutine);
                healthLerpCoroutine = StartCoroutine(LerpBar(healthBar, healthBar.fillAmount, health / maxHealth, 0.2f));
            }
        }

        /// <summary>平滑更新护盾条填充量到当前护盾占比。</summary>
        public virtual void UpdateShieldBar()
        {
            if (shieldBar != null)
            {
                if (shieldLerpCoroutine != null) StopCoroutine(shieldLerpCoroutine);
                shieldLerpCoroutine = StartCoroutine(LerpBar(shieldBar, shieldBar.fillAmount, shield / maxShield, 0.15f));
            }
        }

        /// <summary>处理死亡：标记死亡、触发事件、播放音效、可选显示击杀信息与死亡特效，并按配置销毁物体或组件。</summary>
        public virtual void Die()
        {
            isDead = true;
            // Custom event on damaged
            events.OnDeath.Invoke();

            SoundManager.Instance.PlaySound(dieSFX, 0, 1, false);
            // Does it display killfeed on death? 
            if (showKillFeed)
            {
                UIEvents.onEnemyKilled.Invoke(_name);
            }
            if (deathEffect) Instantiate(deathEffect, transform.position, Quaternion.identity);

            if (destroyOnDie)
            {
                Destroy(this.gameObject);
            }
            else if(destroyComponentOnDie)
            {
                Destroy(this);
            }
        }

        // 血条平滑过渡的协程引用（护盾条同理见下方字段），用于在重新过渡前停止旧协程。
        private Coroutine healthLerpCoroutine;
        private Coroutine shieldLerpCoroutine;

        // 血条 / 护盾条平滑过渡协程：在 duration 时间内把 bar.fillAmount 从 startValue 插值到 targetValue。
        // 参数 bar：目标 Image；startValue：起始填充量；targetValue：目标填充量；duration：过渡时长（秒）。
        private IEnumerator LerpBar(Image bar, float startValue, float targetValue, float duration)
        {
            float time = 0f;
            while (time < duration)
            {
                if (bar == null) yield break;

                time += Time.deltaTime;
                float t = time / duration;
                bar.fillAmount = Mathf.Lerp(startValue, targetValue, t);
                yield return null;
            }

            bar.fillAmount = targetValue;
        }


#if SAVE_LOAD_ADD_ON
        // If dead, destroy the Enemy.
        /// <summary>读取存档后调用：若已死亡且配置了销毁则销毁物体，否则恢复血条显示。</summary>
        public override void LoadedState()
        {
            if (health <= 0 && destroyOnDie) Destroy(this.gameObject);
            else
            {
                healthBar.fillAmount = health / maxHealth;
                shieldBar.fillAmount = shield / maxShield;
            }
        }

        // Store the enemy into the enemy health data
        /// <summary>将敌人生命 / 护盾数据写入存档管理器。</summary>
        public override void StoreData()
        {
            if (GameDataManager.instance == null) return;

            if (GameDataManager.instance.enemyHealthData == null)
            {
                GameDataManager.instance.enemyHealthData = new Dictionary<string, CustomSaveData>();
            }
            GameDataManager.instance.enemyHealthData[UniqueIDValue] = SaveFields();
        }
#endif
    }
#if UNITY_EDITOR
    [System.Serializable]
    [CustomEditor(typeof(EnemyHealth))]
    /// <summary>EnemyHealth 的自定义 Inspector：以可折叠分组展示身份、属性与 UI 相关配置。</summary>
    public class EnemyEditor : Editor
    {
        private bool showIdentity = false;
        private bool showStats = false;
        private bool showUIFoldout = false;
        /// <summary>绘制自定义 Inspector：以折叠分组展示身份 / 属性 / UI 设置，并支持创建默认敌人 UI。</summary>
        public override void OnInspectorGUI()
        {
            serializedObject.Update();
            EnemyHealth myScript = (EnemyHealth)target;

            Texture2D myTexture = Resources.Load<Texture2D>("CustomEditor/enemyHealth_CustomEditor") as Texture2D;
            GUILayout.Label(myTexture);
            EditorGUI.indentLevel++;
            EditorGUILayout.BeginVertical(GUI.skin.GetStyle("HelpBox"));
            {
                // Identity foldout
                showIdentity = EditorGUILayout.Foldout(showIdentity, "IDENTITY", true);
                if (showIdentity)
                {
                    EditorGUI.indentLevel++;
                    EditorGUILayout.PropertyField(serializedObject.FindProperty("_name"));
                    EditorGUILayout.Space(6);
                    EditorGUI.indentLevel--;
                }

            }
            EditorGUILayout.EndVertical();
            EditorGUILayout.Space(6);
            EditorGUILayout.BeginVertical(GUI.skin.GetStyle("HelpBox"));
            {
                // Stats foldout
                showStats = EditorGUILayout.Foldout(showStats, "STATISTICS", true);
                if (showStats)
                {
                    EditorGUI.indentLevel++;
                    EditorGUILayout.PropertyField(serializedObject.FindProperty("maxHealth"));
                    EditorGUILayout.PropertyField(serializedObject.FindProperty("maxShield"));

                    if (!myScript.DestroyOnDie)
                    {
                        EditorGUILayout.HelpBox("DestroyOnDie is set to false, your object won’t be destroyed once you kill it.", MessageType.Warning);
                    }

                    EditorGUILayout.PropertyField(serializedObject.FindProperty("destroyOnDie"));
                    if (myScript.DestroyOnDie)
                    {
                        EditorGUI.indentLevel++;
                        EditorGUILayout.PropertyField(serializedObject.FindProperty("deathEffect"));
                        EditorGUI.indentLevel--;
                    }
                    else
                    {
                        EditorGUI.indentLevel++;
                        EditorGUILayout.PropertyField(serializedObject.FindProperty("destroyComponentOnDie"));
                        EditorGUI.indentLevel--;
                    }
                    EditorGUILayout.PropertyField(serializedObject.FindProperty("dieSFX"));
                    EditorGUILayout.Space(6);
                    EditorGUI.indentLevel--;
                }

            }
            EditorGUILayout.EndVertical();
            EditorGUILayout.Space(6);
            EditorGUILayout.BeginVertical(GUI.skin.GetStyle("HelpBox"));
            {
                showUIFoldout = EditorGUILayout.Foldout(showUIFoldout, "UI", true);
                if (showUIFoldout)
                {
                    EditorGUI.indentLevel++;
                    EditorGUILayout.PropertyField(serializedObject.FindProperty("showUI"));
                    if (myScript.ShowUI)
                    {
                        EditorGUI.indentLevel++;
                        EditorGUILayout.PropertyField(serializedObject.FindProperty("enemyStatusContainer"));
                        EditorGUILayout.PropertyField(serializedObject.FindProperty("healthBar"));
                        EditorGUILayout.PropertyField(serializedObject.FindProperty("shieldBar"));
                        if (!myScript.HealthSlider && !myScript.ShieldSlider)
                        {
                            EditorGUILayout.Space(10);
                            EditorGUILayout.HelpBox("Your Enemy UI is null, create a new custom Canvas or Create the Default UI", MessageType.Error);
                            EditorGUILayout.Space(5);
                            if (GUILayout.Button("Create Default UI"))
                            {
                                CreateDefaultUI(myScript.transform);
                            }
                            EditorGUILayout.Space(10);
                        }
                        EditorGUI.indentLevel--;
                    }

                    EditorGUILayout.PropertyField(serializedObject.FindProperty("showDamagePopUps"));
                    EditorGUILayout.PropertyField(serializedObject.FindProperty("showKillFeed"));
                    EditorGUILayout.Space(6);
                    EditorGUI.indentLevel--;
                }


            }
            EditorGUILayout.EndVertical();

            EditorGUILayout.Space(6);
            EditorGUILayout.PropertyField(serializedObject.FindProperty("events"));
            EditorGUI.indentLevel--;


            serializedObject.ApplyModifiedProperties();
        }



        // 在敌人子物体下实例化默认的敌人状态 UI 预制体，并自动关联血条 / 护盾条引用。
        private void CreateDefaultUI(Transform myTransform)
        {
            // Path to EnemyStatusSlider prefab
            string prefabPath = "Assets/Cowsins/Prefabs/Others/UI/EnemyStatusSliders.prefab";

            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);

            if (prefab != null)
            {
                // Instantiate the prefab 
                GameObject instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab, myTransform);
                instance.transform.localPosition += new Vector3(0, 5, 0);
                Undo.RegisterCreatedObjectUndo(instance, "Create Default UI");

                SerializedProperty statusContainerProperty = serializedObject.FindProperty("enemyStatusContainer");
                SerializedProperty healthSliderProperty = serializedObject.FindProperty("healthBar");
                SerializedProperty shieldSliderProperty = serializedObject.FindProperty("shieldBar");

                statusContainerProperty.objectReferenceValue = instance.gameObject;
                healthSliderProperty.objectReferenceValue = instance.transform.Find("HealthBar/HealthFill").GetComponent<Image>();
                shieldSliderProperty.objectReferenceValue = instance.transform.Find("ShieldBar/ShieldFill").GetComponent<Image>();

                serializedObject.ApplyModifiedProperties();

                EditorUtility.SetDirty(target);
            }
            else
            {
                Debug.LogError("<color=red>[COWSINS]</color> Prefab at path <b><color=cyan>{prefabPath}</color></b> " +
                               "could not be found. Did you move, rename or delete EnemyStatusSlider.prefab?");
            }
        }
    }
#endif
}