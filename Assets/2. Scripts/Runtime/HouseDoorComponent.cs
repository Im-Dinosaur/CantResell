using UnityEngine;

namespace CantResell
{
    public sealed class HouseDoorComponent : MonoBehaviour
    {
        [SerializeField] private Collider obstacle; //닫힌 문의 충돌 경계
        [SerializeField] private Renderer surface; //문 상태를 표시할 표면

        public void initialize(Collider collider, Renderer renderer) //생성한 문의 참조 연결
        {
            obstacle = collider;
            surface = renderer;
        }

        public void showDoor(bool open, int strength) //전체 참가자에게 동일한 문 충돌과 외형 적용
        {
            if (obstacle != null)
                obstacle.enabled = !open;
            if (surface != null)
                surface.enabled = !open;
        }
    }
}