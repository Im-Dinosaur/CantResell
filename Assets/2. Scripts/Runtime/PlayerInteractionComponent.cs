using UnityEngine;

namespace CantResell
{
    public sealed class PlayerInteractionComponent : MonoBehaviour
    {
        [SerializeField, Min(0.5f)] private float interactionDistance = 1.6f; //문과 상품을 조작할 거리
        private double nextInteraction; //지렛대 연타 제한 시각

        public bool canInteract(Vector3 position, double now) //거리를 확인하고 상호작용 연타 제한
        {
            if (now < nextInteraction || Vector3.Distance(transform.position, position) > interactionDistance)
                return false;
            nextInteraction = now + 0.4;
            return true;
        }
    }
}