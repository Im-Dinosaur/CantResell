using Fusion;
using UnityEngine;

namespace CantResell
{
    [RequireComponent(typeof(NetworkCharacterController))]
    public sealed class PlayerMovementComponent : MonoBehaviour
    {
        private NetworkCharacterController controller; //Fusion의 충돌과 위치 동기화 담당

        private void Awake() //이동 담당 참조 연결
        {
            controller = GetComponent<NetworkCharacterController>();
        }

        public void movePlayer(Vector2 direction, PlayerHouse boundary) //이동 계산과 방어자의 집 경계 적용
        {
            if (!float.IsFinite(direction.x) || !float.IsFinite(direction.y))
                direction = Vector2.zero;
            controller.Move(new Vector3(direction.x, 0, direction.y).normalized);
            if (boundary != null && !boundary.contains(transform.position, 2.6f))
                teleportPlayer(boundary.clampPosition(transform.position));
        }

        public void teleportPlayer(Vector3 position) //단계 전환과 실패 후 위치 초기화
        {
            controller.Teleport(position);
            controller.Velocity = Vector3.zero;
        }
    }
}