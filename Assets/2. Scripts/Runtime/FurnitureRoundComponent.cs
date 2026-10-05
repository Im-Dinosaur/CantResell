using UnityEngine;

namespace CantResell
{
    public sealed class FurnitureRoundComponent : MonoBehaviour
    {
        [SerializeField, Range(2, 4)] private int tradingDays = 4; //마지막 판매까지 진행할 날짜
        [SerializeField, Min(1)] private float daySeconds = 180; //낮 영업 시간
        [SerializeField, Min(1)] private float nightSeconds = 180; //밤 침입 시간
        private double deadline; //호스트의 단계 종료 시각
        public int day { get; private set; } //현재 영업일
        public int days => Mathf.Clamp(tradingDays, 2, 4); //전체 영업일
        public AuctionState.Phase phase { get; private set; } //현재 진행 단계
        public float duration { get; private set; } //현재 단계의 전체 시간

        public void resetMatch() //진행과 날짜 초기화
        {
            day = 0;
            phase = AuctionState.Phase.Lobby;
            deadline = 0;
            duration = 0;
        }
        public void beginDay(double now) //다음 날짜의 판매 시작
        {
            day++;
            enterPhase(AuctionState.Phase.Day, Mathf.Max(1, daySeconds), now);
        }
        public void beginNight(double now) //같은 날짜의 공동 침입 시작
        {
            enterPhase(AuctionState.Phase.Night, Mathf.Max(1, nightSeconds), now);
        }
        public void finish(bool aborted = false) //결과 표시와 월드 입력 종료
        {
            enterPhase(aborted ? AuctionState.Phase.Aborted : AuctionState.Phase.Results, 0, 0);
        }
        private void enterPhase(AuctionState.Phase next, float seconds, double now) //단계와 종료 시각 설정
        {
            phase = next;
            duration = seconds;
            deadline = seconds > 0 ? now + seconds : 0;
        }
        public float remaining(double now) //현재 단계의 남은 시간
        {
            return deadline > 0 ? (float)System.Math.Max(0, deadline - now) : 0;
        }
        public bool expired(double now) //호스트의 단계 종료 확인
        {
            return deadline > 0 && now >= deadline;
        }
    }
}
