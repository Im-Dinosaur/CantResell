using System;

namespace CantResell
{
    [Serializable]
    public sealed class AuctionState
    {
        public enum Phase { Lobby, Loading, Pitch, Inspection, Bidding, Reveal, Results, Aborted }
        public enum Action { Profile, Ready, Start, Loaded, Inspect, Bid, ReturnToLobby }

        [Serializable]
        public sealed class Player
        {
            public int id; //접속한 플레이어 식별자
            public string name; //표시할 이름
            public int color; //캐릭터 색상 번호
            public bool ready; //게임 시작 준비 상태
            public int cash; //현재 소지금
        }

        [Serializable]
        public sealed class Command
        {
            public int version = 1; //메시지 규격 버전
            public long sequence; //중복 요청을 막는 순번
            public Action action; //요청한 행동
            public int match; //요청 대상 게임 번호
            public int round; //요청 대상 라운드 번호
            public string name; //변경할 플레이어 이름
            public int value; //입찰 금액 또는 색상 번호
            public bool ready; //변경할 준비 상태
        }

        public int version = 1; //메시지 규격 버전
        public long sequence; //상태 갱신 순번
        public int match; //진행 중인 게임 번호
        public Phase phase; //현재 진행 단계
        public int round; //현재 라운드 번호
        public int localSlot = -1; //수신자의 좌석 번호
        public int hostSlot = -1; //방장의 좌석 번호
        public int sellerSlot = -1; //판매자의 좌석 번호
        public int bidderSlot = -1; //최고 입찰자의 좌석 번호
        public int highestBid; //현재 최고 입찰액
        public int minimumRaise; //최소 입찰 증가액
        public int normalReward; //정상 상품의 보상
        public float secondsRemaining; //현재 단계의 남은 시간
        public bool knowsCondition; //수신자가 상품 상태를 아는지 여부
        public bool goodCondition; //알 권한이 있을 때만 전달하는 상품 상태
        public bool inspected; //수신자의 이번 라운드 검사 여부
        public int inspectionTickets; //수신자의 남은 검사권
        public string notice; //진행 상황 안내
        public Player[] players = new Player[4]; //공개 가능한 참가자 정보

        public bool isRevealed() //상품 상태가 전체 공개된 단계인지 확인
        {
            return phase == Phase.Reveal || phase == Phase.Results;
        }
    }
}
