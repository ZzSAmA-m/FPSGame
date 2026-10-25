using UnityEngine;
namespace GamePlay.Player{
    public sealed class PlayerFPRotation : MonoBehaviour
    {
        public Vector3 Rotation{
            get{
                return transform.rotation.eulerAngles;
            }
            set{
                transform.eulerAngles=value;
            }
        }
    }
}
