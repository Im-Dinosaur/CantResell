using UnityEngine;

namespace CantResell
{
    public sealed class AuctionRoundComponent : MonoBehaviour
    {
        [SerializeField, Min(1)] private float pitchSeconds = 20; //판매 설명 제한 시간
        [SerializeField, Min(1)] private float inspectionSeconds = 15; //비밀 검사 제한 시간
        [SerializeField, Min(1)] private float biddingSeconds = 25; //경매 입찰 제한 시간
        [SerializeField, Min(1)] private float revealSeconds = 8; //시연 결과 표시 시간
        private double deadline; //호스트 기준 단계 종료 시각
        public AuctionState.Phase phase { get; private set; } //현재 진행 단계
        public int round { get; private set; } //현재 라운드 번호

        public void resetLobby() //대기실 상태로 초기화
        {
            round = 0;
            enterPhase(AuctionState.Phase.Lobby, 0);
        }

        public void beginRound(int roundIndex, double now) //판매자 순서에 맞는 라운드 시작
        {
            round = roundIndex;
            enterPhase(AuctionState.Phase.Pitch, now);
        }

        public void enterPhase(AuctionState.Phase nextPhase, double now) //단계와 제한 시간 설정
        {
            phase = nextPhase;
            float duration = nextPhase switch //단계별 제한 시간
            {
                AuctionState.Phase.Pitch => pitchSeconds,
                AuctionState.Phase.Inspection => inspectionSeconds,
                AuctionState.Phase.Bidding => biddingSeconds,
                AuctionState.Phase.Reveal => revealSeconds,
                AuctionState.Phase.Loading => 45,
                _ => 0
            };
            deadline = duration > 0 ? now + Mathf.Max(1, duration) : 0;
        }

        public bool hasExpired(double now) //호스트 시계로 단계 만료 확인
        {
            return deadline > 0 && now >= deadline;
        }

        public float getRemaining(double now) //표시할 남은 시간 반환
        {
            return deadline > 0 ? (float)System.Math.Max(0, deadline - now) : 0;
        }
    }
}
