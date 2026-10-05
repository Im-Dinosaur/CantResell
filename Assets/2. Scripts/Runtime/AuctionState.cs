using System;

namespace CantResell
{
    [Serializable]
    public sealed class AuctionState
    {
        public enum Phase { Lobby, Loading, Pitch, Bidding, Night, Results, Aborted, Day }
        public enum Action { Profile, Ready, Start, Loaded, Bid, ReturnToLobby, EndNightTurn, Gamble, Play }
        public enum ItemKind { CoffeeMachine, Table, Lamp, Turntable, Speaker, Tools, Workbench, Cabinet, Bed, Heater, Lock, Hammer, Crowbar }
        public enum ItemStatus { Stock, Stored, Carried, Retired }

        [Serializable]
        public sealed class Player
        {
            public int id; //접속 식별자
            public string name; //낮에 표시할 이름
            public int color; //낮에 표시할 스킨 색상
            public bool ready; //준비 상태
            public int cash; //현재 소지금
            public int score = -1; //결과에서만 공개할 목표 점수
            public string objective; //결과에서만 공개할 목표
        }

        [Serializable]
        public sealed class Item
        {
            public int id; //중복되지 않는 상품 번호
            public ItemKind kind; //공개 상품 종류
            public int owner; //보관 또는 판매하는 좌석
            public ItemStatus status; //판매와 보관 상태
            public int carrier = -1; //운반하는 좌석
            public bool known; //수신자가 원가와 상태를 아는지 여부
            public int cost; //권한이 있을 때만 전달할 원가
            public int condition; //권한이 있을 때만 전달할 성능 비율
        }

        [Serializable]
        public sealed class Command
        {
            public int version = 3; //메시지 규격 버전
            public long sequence; //중복 요청 방지 순번
            public Action action; //요청 행동
            public int match; //게임 번호
            public int round; //판매 순번
            public int nightTurn = -1; //지연된 밤 행동을 막을 턴 번호
            public Phase phase; //지연된 낮밤 활동을 막을 요청 단계
            public string name; //변경할 이름
            public int value; //입찰액 또는 색상
            public bool ready; //준비 상태
        }

        public int version = 3; //메시지 규격 버전
        public FurnitureState store; //공동 가구점의 공유 상태
        public long sequence; //상태 갱신 순번
        public int match; //게임 번호
        public Phase phase; //현재 단계
        public int round; //판매 순번
        public int cycles = 3; //전체 회차
        public int localSlot = -1; //수신자의 좌석
        public int hostSlot = -1; //방장의 좌석
        public int sellerSlot = -1; //현재 판매자
        public int bidderSlot = -1; //최고 입찰자
        public int highestBid; //최고 입찰액
        public int minimumRaise; //최소 입찰 증가액
        public float secondsRemaining; //단계 남은 시간
        public float phaseDuration; //시간 막대의 전체 길이
        public int lotId; //현재 경매 상품
        public int nightTurn = -1; //현재 밤 행동 순번
        public int activeIntruder = -1; //집을 나갈 수 있는 좌석
        public int[] nightOrder = Array.Empty<int>(); //밤의 무작위 순서
        public int[] doorStrength = new int[4]; //집별 자물쇠 내구도
        public bool[] doorOpen = new bool[4]; //집별 문 열림
        public string goalTitle; //본인의 비공개 목표
        public string goalDescription; //목표별 상품과 배점
        public int goalScore; //알고 있는 상품으로 확인한 점수
        public int unknownItems; //성능이 공개되지 않은 보유품 수
        public string notice; //진행 안내
        public Player[] players = new Player[4]; //참가자 정보
        public Item[] items = Array.Empty<Item>(); //수신자 권한으로 가린 상품 목록
    }
}
