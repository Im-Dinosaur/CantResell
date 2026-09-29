using UnityEngine;

namespace CantResell
{
    public sealed class AuctionItemComponent : MonoBehaviour
    {
        [SerializeField, Range(0, 1)] private float normalChance = 0.5f; //정상 상품 확률
        [SerializeField, Range(0, 4)] private int ticketsPerMatch = 2; //게임당 개인 검사권 수
        private readonly int[] tickets = new int[4]; //호스트가 관리하는 검사권
        private readonly bool[] inspected = new bool[4]; //라운드별 검사 기록
        private bool goodCondition; //호스트만 보관하는 실제 상품 상태

        public void resetMatch() //새 게임의 검사권 초기화
        {
            for (int slot = 0; slot < tickets.Length; slot++) //초기화할 좌석 번호
                tickets[slot] = Mathf.Clamp(ticketsPerMatch, 0, 4);
        }

        public void prepareItem() //상품 상태 추첨과 라운드 검사 기록 초기화
        {
            goodCondition = Random.value < normalChance;
            System.Array.Clear(inspected, 0, inspected.Length);
        }

        public bool tryInspect(int slot, int sellerSlot) //중복 검사와 판매자 검사를 차단하고 검사권 사용
        {
            if (slot < 0 || slot >= 4 || slot == sellerSlot || inspected[slot] || tickets[slot] <= 0)
                return false;
            tickets[slot]--;
            inspected[slot] = true;
            return true;
        }

        public bool knowsCondition(int slot, int sellerSlot, bool revealed) //수신자별 비밀 정보 열람 권한 확인
        {
            return slot >= 0 && slot < 4 && (revealed || slot == sellerSlot || inspected[slot]);
        }

        public bool getCondition() //호스트의 정산용 실제 상품 상태 반환
        {
            return goodCondition;
        }

        public int getTickets(int slot) //개인 검사권 수 반환
        {
            return slot >= 0 && slot < 4 ? tickets[slot] : 0;
        }

        public bool hasInspected(int slot) //개인의 이번 라운드 검사 여부 반환
        {
            return slot >= 0 && slot < 4 && inspected[slot];
        }
    }
}
