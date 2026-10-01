using UnityEngine;

namespace CantResell
{
    public sealed class PlayerHouse : MonoBehaviour
    {
        private HouseDoorComponent door; //집의 문 담당 구성 요소
        public int owner { get; private set; } //집 주인의 좌석
        public Vector3 doorPosition => door.transform.position - Vector3.up; //문 조작 위치
        public Vector3 spawnPosition => transform.position; //집 내부의 플레이어 시작 위치

        public void initialize(int slot, HouseDoorComponent entrance) //집 주인과 출입문 구성
        {
            owner = slot;
            door = entrance;
        }

        public bool contains(Vector3 position, float halfSize = 2.5f) //집 내부인지 확인
        {
            Vector3 offset = position - transform.position; //집 중심에서 거리
            return Mathf.Abs(offset.x) <= halfSize && Mathf.Abs(offset.z) <= halfSize;
        }

        public Vector3 clampPosition(Vector3 position) //방어자가 집 밖으로 나가지 않도록 경계 적용
        {
            Vector3 offset = position - transform.position; //집 기준 위치
            return transform.position + new Vector3(Mathf.Clamp(offset.x, -2.4f, 2.4f), Mathf.Clamp(offset.y, 0, 1), Mathf.Clamp(offset.z, -2.4f, 2.4f));
        }

        public Vector3 getStoragePosition(int index) //보관품의 일정한 실내 위치
        {
            return transform.position + new Vector3(-1.75f + index % 6 * 0.7f, 0.4f, -1.75f + index / 6 * 0.7f);
        }

        public void showDoor(bool open, int strength) //문 담당 구성 요소로 공개 상태 전달
        {
            door.showDoor(open, strength);
        }
    }
}