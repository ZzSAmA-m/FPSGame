// PointCapture（占领点系统）：
// 继承自 Trigger，实现“据点占领”玩法：玩家停留在据点内时进度增长，满 100 即占领成功并触发事件。
// 支持离开后进度衰减，以及存档集成与仅编辑器使用的自定义 Inspector（PointCaptureEditor）。
/// <summary>
/// This script belongs to cowsins™ as a part of the cowsins´ FPS Engine. All rights reserved. 
/// </summary>
#if UNITY_EDITOR
using UnityEditor;
#endif 
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Events;
namespace cowsins
{
    /// <summary>
    /// Basic Point Capture script
    /// This is the core of the system.
    /// </summary>
    /// <summary>
    /// 占领点：玩家停留在区域内时累积进度，达到 100 即占领并触发事件。
    /// </summary>
    public class PointCapture : Trigger
    {
        [System.Serializable]
        // 占领点自定义事件集合
        public class PointCaptureEvents
        {
            // 占领成功时触发的事件
            public UnityEvent OnCapture;
        }
        public PointCaptureEvents captureEvents; // custom events

        // 占领进度增长的速度
        [Tooltip(" how fast the point will be captured "), SerializeField]
        private float captureSpeed;

        // 若为 true，玩家离开后进度会逐渐衰减
        [Tooltip(" If true, progress will gradually be lost when player leaves the point ")]
        public bool loseProgressIfNotCapturing;

        // 进度衰减速度
        [Tooltip(" Speed of progress loss "), SerializeField]
        private float losingProgressCaptureSpeed;

        // 玩家当前是否正在占领（停留在区域内）
        private bool beingCaptured;

        // 是否已占领
        private bool captured;

        // 当前占领进度（带存档标记）
        [SaveField] private float progress;

        // 进度显示 UI 实例
        private GameObject ui;

        // 初始化进度与占领状态
        private void Start()
        {
            // Initial stuff
            progress = 0;
            captured = false;
        }
        
        // 每帧：玩家不在区域内且开启了进度衰减时，递减进度
        void Update()
        {
            // if player is not inside and we wanna lose progress then lose it
            if (!beingCaptured && progress > 0 && loseProgressIfNotCapturing) progress -= Time.deltaTime * losingProgressCaptureSpeed;
        }
        // 玩家停留在区域内：未占领时累积进度，达到 100 则占领；同时更新进度条 UI
        public override void TriggerStay(Collider other)
        {
            // If we are player and point is not captured, try to cap
            if (!captured)
            {
                // 首次进入时实例化进度 UI
                if (!beingCaptured && ui == null) ui = Instantiate(Resources.Load("PointCaptureUI")) as GameObject; // Instantiate cool UI
                beingCaptured = true;
                progress += Time.deltaTime * captureSpeed;

                // Check if we already captured
                if (progress >= 100)
                {
                    captured = true; 
                    OnCapture();
                }

#if SAVE_LOAD_ADD_ON
                SaveTrigger();
#endif
            }

            // Handle UI, we do not wanna do this if there is no UI currently (We are not inside the point)
            if (ui == null) return;

            // It will show progress while you are in
            Slider slider = ui.transform.Find("Progress").GetComponent<Slider>();
            slider.value = progress;
        }
        // Stop capping
        // 玩家离开：未占领时停止占领并销毁进度 UI
        public override void TriggerExit(Collider other)
        {
            if (!captured)
            {
                beingCaptured = false;
                Destroy(ui.gameObject);
            }
        }

        /// <summary>
        /// This is called whenever you capture the point
        /// </summary>
        public virtual void OnCapture()
        {
            captureEvents.OnCapture.Invoke(); // Call our custom event 

            /*Since it is a public virtual void, you can override this method on a new script that inherits from PointCapture. 
            However, do not forget to use base.OnCapture(); if
            you still want to perform these actions on that new script.

             EXAMPLE: on a new class (public class MyNewPointCapture : PointCapture) we will write the following structure
            public override void OnCapture() { // Write here whatever you want }

             (You can always edit this one if you do not plan to add more different kinds of capture points, just to avoid unncessary scripts)*/

            Debug.Log("You captured the point!");

            Destroy(ui);
            Destroy(this.gameObject);
        }

#if SAVE_LOAD_ADD_ON
        // If the point is captured after loading the progress, Destroy it.
        // 存档加载后：若进度已达 100，则销毁该占领点
        public override void LoadedState()
        {
            if (progress >= 100)
            {
                Destroy(ui);
                Destroy(this.gameObject);
            }
        }
#endif
    }
#if UNITY_EDITOR
    [System.Serializable]
    [CustomEditor(typeof(PointCapture))]
    /// <summary>
    /// PointCapture 的自定义编辑器：显示占领速度、进度衰减与事件相关字段。
    /// </summary>
    public class PointCaptureEditor : Editor
    {

        // 自定义 Inspector：绘制占领速度与事件，并在开启衰减时显示衰减速度字段
        override public void OnInspectorGUI()
        {
            serializedObject.Update();
            PointCapture myScript = target as PointCapture;

            EditorGUILayout.LabelField("SETTINGS", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(serializedObject.FindProperty("captureSpeed"));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("loseProgressIfNotCapturing"));
            if (myScript.loseProgressIfNotCapturing)
            {
                EditorGUILayout.PropertyField(serializedObject.FindProperty("losingProgressCaptureSpeed"));
            }
            EditorGUILayout.PropertyField(serializedObject.FindProperty("captureEvents"));
            serializedObject.ApplyModifiedProperties();
        }
    }
#endif
}