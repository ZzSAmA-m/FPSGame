using System;
using System.Collections.Generic;
using Core.Event;
using Core.Utility;
using Mirror;
using Sirenix.OdinInspector;
using UnityEngine;
using UnityEngine.UI;
namespace GamePlay.Player{
    public class PlayerModelController : MonoBehaviour
    {
        [SerializeField]private Transform TPModelParent;
        [SerializeField]private Transform FPModelParent;
        [SerializeField]private Transform SourceModel;

        [SerializeField,Sirenix.OdinInspector.ReadOnly]private EPlayerModelType _e_PlayerModelType=EPlayerModelType.TP;
        [Space(20)]

        /// <summary>
    /// 第一人称,第三人称模型
    /// </summary>
        [SerializeField,Space(20)]private List<GameObject> FpModels,TpModels;


        




        /// <summary>
        /// 切换玩家模型,给外界提供的接口
        /// </summary>
        /// <param name="type">模型类型,第一人称/第三人称</param>
        [Button(nameof(SwitchModel)),Tooltip("切换玩家第一/第三人称模型"),PropertySpace(20)]
        public void SwitchModel(EPlayerModelType type=EPlayerModelType.FP)
        {
            switch (type)
            {
                case EPlayerModelType.FP:
                    foreach(var obj in TpModels){
                        obj.SetActive(false);
                    }
                    foreach(var obj in FpModels)
                    {
                        obj.SetActive(true);
                    }
                    this._e_PlayerModelType=type;
                    SourceModel.parent=FPModelParent;
                    SourceModel.localPosition=Vector3.zero;
                    SourceModel.localRotation=Quaternion.Euler(Vector3.zero);
                    break;
                case EPlayerModelType.TP:
                    foreach(var obj in TpModels){
                        obj.SetActive(true);
                    }
                    foreach(var obj in FpModels)
                    {
                        obj.SetActive(false);
                    }
                    this._e_PlayerModelType=type;
                    SourceModel.parent=TPModelParent;
                    SourceModel.localPosition=Vector3.zero;
                    SourceModel.localRotation=Quaternion.Euler(Vector3.zero);
                    break;
                default:
                    Debug.LogError($"[{nameof(PlayerModelController)}] 切换模型到{type} 失败!");
                    break;

            }
        }


    
    
    }

}