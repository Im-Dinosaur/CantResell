using UnityEngine;

namespace CantResell
{
    public sealed class AuctionRoundComponent : MonoBehaviour
    {
        [SerializeField, Range(1, 8)] private int cyclesPerMatch = 3; //낮과 밤을 반복할 회차
        [SerializeField, Min(1)] private float pitchSeconds = 20; //상품 설명 시간
        [SerializeField, Min(1)] private float biddingSeconds = 25; //입찰 시간
        [SerializeField, Min(1)] private float nightSeconds = 45; //각 침입자의 행동 시간
        private double deadline; //호스트 기준 종료 시각
        public AuctionState.Phase phase { get; private set; } //현재 진행 단계
        public int round { get; private set; } //전체 판매 순번
        public float duration { get; private set; } //현재 단계 제한 시간
        public int cycles => Mathf.Clamp(cyclesPerMatch, 1, 8); //유효 회차 수
        public int sellerSlot => round % 4; //각 회차의 판매자

        public void resetLobby() //대기 상태 초기화
        {
            round = 0;
            enterPhase(AuctionState.Phase.Lobby, 0);
        }

        public void beginRound(int index, double now) //판매 순번에 따른 설명 시작
        {
            round = index;
            enterPhase(AuctionState.Phase.Pitch, now);
        }

        public void enterPhase(AuctionState.Phase next, double now) //단계와 제한 시간 변경
        {
            phase = next;
            duration = next switch
            {
                AuctionState.Phase.Pitch => Mathf.Max(1, pitchSeconds),
                AuctionState.Phase.Bidding => Mathf.Max(1, biddingSeconds),
                AuctionState.Phase.Night => Mathf.Max(1, nightSeconds),
                AuctionState.Phase.Loading => 45,
                _ => 0
            };
            deadline = duration > 0 ? now + duration : 0;
        }

        public bool hasExpired(double now) //호스트의 단계 만료 확인
        {
            return deadline > 0 && now >= deadline;
        }

        public float getRemaining(double now) //시간 막대의 남은 시간
        {
            return deadline > 0 ? (float)System.Math.Max(0, deadline - now) : 0;
        }
    }
}