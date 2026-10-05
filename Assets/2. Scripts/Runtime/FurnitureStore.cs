using System;
using System.Linq;
using UnityEngine;

namespace CantResell
{
    public sealed class FurnitureStore : MonoBehaviour
    {
        [SerializeField] private FurnitureRoundComponent roundComponent; //공동 영업의 날짜와 단계
        [SerializeField] private FurnitureEconomyComponent economyComponent; //공금과 도박 정산
        [SerializeField] private FurnitureInventoryComponent inventoryComponent; //공동 재고와 운반
        [SerializeField] private FurnitureShopComponent shopComponent; //손님과 판매
        [SerializeField] private FurnitureNightComponent nightComponent; //센서와 경찰
        [SerializeField] private FurnitureLeisureComponent leisureComponent; //무료 놀이
        [SerializeField] private FurnitureWorldComponent worldComponent; //월드 표시
        [SerializeField] private FurnitureUIComponent uiComponent; //새 Play 화면
        private Player[] players; //세션에서 생성한 캐릭터
        private Action changed; //상태를 전송할 세션 호출
        private Func<int, string> playerName; //참가자의 표시 이름 조회
        private bool doorOpen; //밤 출입문 열림
        private string outcome = ""; //최근 밤 또는 최종 결과
        public bool running { get; private set; } //공동 가게가 시작됐는지 여부
        public FurnitureRoundComponent rounds => roundComponent; //세션에 제공할 진행 상태
        public FurnitureWorldComponent world => worldComponent; //씬 연결용 월드 진입점
        public bool inputBlocked => uiComponent.inputBlocked; //숫자 입력창의 이동 차단
        public static Vector3 gamblingTable => new Vector3(11, 0, 1); //공금 도박 테이블

        public void initialize(Player[] pawns, Action onChanged, Func<int, string> names) //세션과 게임의 호출 연결
        {
            players = pawns;
            changed = onChanged;
            playerName = names;
            resetMatch();
        }
        public void resetMatch() //기존 세션을 유지하고 공동 게임만 초기화
        {
            running = false;
            outcome = "";
            roundComponent.resetMatch();
            economyComponent.resetMatch();
            inventoryComponent.resetMatch();
            leisureComponent.resetMatch();
        }
        public void beginMatch(double now) //첫날 재고를 가진 공동 영업 시작
        {
            running = true;
            beginDay(now);
        }
        private void beginDay(double now) //야간 수익을 팔 수 있도록 다음 영업 준비
        {
            inventoryComponent.arrangeShop();
            roundComponent.beginDay(now);
            shopComponent.beginDay(now);
            resetPlayers(false);
        }
        private void beginNight(double now) //전원 동시 침입 준비
        {
            inventoryComponent.arrangeShop();
            inventoryComponent.beginNight();
            roundComponent.beginNight(now);
            resetPlayers(true);
            nightComponent.beginNight(now, players);
            doorOpen = false;
            worldComponent.setDoor(false);
            outcome = "가구를 차량에 싣고 함께 철수하세요.";
        }
        private void resetPlayers(bool night) //단계별 시작 위치와 운반 상태 준비
        {
            for (int slot = 0; slot < 4; slot++) //실제 네트워크 캐릭터 초기화
                if (players[slot] != null)
                    players[slot].resetPlayer(night ? FurnitureNightComponent.spawn(slot) : new Vector3(-2 + slot * 1.3f, 0, -1));
        }
        public void tick(double now, float delta) //단계 만료와 담당 시스템 업데이트 조율
        {
            if (!running)
                return;
            if (roundComponent.expired(now))
            {
                if (roundComponent.phase == AuctionState.Phase.Day)
                {
                    if (roundComponent.day >= roundComponent.days)
                    {
                        roundComponent.finish();
                        outcome = economyComponent.cash >= economyComponent.target ? "공동 목표 달성! 가게를 지켜냈습니다." : "목표 공금 부족. 공동 가게 운영에 실패했습니다.";
                    }
                    else
                        beginNight(now);
                }
                else if (roundComponent.phase == AuctionState.Phase.Night)
                    finishNight(false, now, "날이 밝았습니다. 차량에 실은 가구만 가져왔습니다.");
                changed();
            }
            if (roundComponent.phase == AuctionState.Phase.Day)
                shopComponent.tick(now, inventoryComponent);
            else if (roundComponent.phase == AuctionState.Phase.Night)
            {
                FurnitureState.Alarm before = nightComponent.alarm; //진행 알림을 위한 이전 경보
                int captured = nightComponent.tick(now, delta, players); //경찰이 잡은 참가자
                if (nightComponent.alarm == FurnitureState.Alarm.Pursuit)
                {
                    doorOpen = true;
                    worldComponent.setDoor(true);
                }
                if (captured >= 0)
                    finishNight(true, now, playerName(captured) + " 체포! 이번 밤 가구가 몰수됐습니다.");
                else if (nightComponent.allEscaped)
                    finishNight(false, now, "전원 철수 성공! 적재한 가구가 가게 재고가 됐습니다.");
                if (captured >= 0 || before != nightComponent.alarm)
                    changed();
            }
        }
        private void finishNight(bool caught, double now, string message) //밤 종료와 손실 및 다음 날짜 조율
        {
            if (roundComponent.phase != AuctionState.Phase.Night)
                return;
            if (caught)
                economyComponent.chargeFine(roundComponent.day);
            inventoryComponent.finishNight(caught);
            outcome = message;
            beginDay(now);
        }
        public void abort() //세션 이탈 때 운반 정리와 입력 차단
        {
            if (running)
            {
                inventoryComponent.finishNight(true);
                roundComponent.finish(true);
            }
        }
        public bool command(int slot, AuctionState.Action action, int value, double now, out string message) //위치와 단계를 확인해 도박과 놀이 요청 위임
        {
            message = "해당 기계 가까이에서 낮에 사용할 수 있습니다.";
            if (!running || roundComponent.phase != AuctionState.Phase.Day || slot < 0 || slot >= 4 || players[slot] == null)
                return false;
            if (action == AuctionState.Action.Gamble && near(players[slot], gamblingTable))
                return economyComponent.placeBet(slot, value, now, playerName(slot), out message);
            if (action == AuctionState.Action.Play && near(players[slot], FurnitureLeisureComponent.machine))
            {
                int score = leisureComponent.play(slot, now); //무료 놀이 결과
                message = playerName(slot) + " 놀이 기록 " + score + "점";
                return score >= 0;
            }
            return false;
        }
        public void simulatePlayer(Player player, Vector2 direction, bool interact, bool drop, double now) //모든 참가자의 이동과 상호작용을 담당 시스템에 전달
        {
            bool night = roundComponent.phase == AuctionState.Phase.Night; //현재 침입 단계
            if (!running || (roundComponent.phase != AuctionState.Phase.Day && !night) || (night && nightComponent.isEscaped(player.slot)))
                return;
            player.movePlayer(direction, null);
            if (drop)
            {
                Vector3 position = player.transform.position; //현재 바닥에 내려놓을 위치
                position.y = 0;
                inventoryComponent.drop(player.slot, night, position);
                changed();
            }
            if (interact)
                interactPlayer(player, night, now);
            player.carriedId = inventoryComponent.carried(player.slot)?.id ?? 0;
        }
        private void interactPlayer(Player player, bool night, double now) //현재 장소에서 가장 가까운 유효 행동 처리
        {
            if (!night && near(player, FurnitureShopComponent.counter))
            {
                if (shopComponent.deliver(player.slot, now, playerName(player.slot), inventoryComponent, economyComponent))
                    changed();
                return;
            }
            if (night && near(player, FurnitureNightComponent.truck))
            {
                if (!inventoryComponent.loadTruck(player.slot))
                    nightComponent.escape(player.slot);
                changed();
                return;
            }
            if (night && near(player, FurnitureNightComponent.sensorSwitch) && player.canInteract(FurnitureNightComponent.sensorSwitch, now))
            {
                nightComponent.disableSensors(now);
                changed();
                return;
            }
            if (night && near(player, FurnitureNightComponent.door) && player.canInteract(FurnitureNightComponent.door, now))
            {
                if (nightComponent.alarm != FurnitureState.Alarm.Pursuit)
                    doorOpen = !doorOpen;
                worldComponent.setDoor(doorOpen);
                changed();
                return;
            }
            FurnitureState.Furniture closest = inventoryComponent.all.Where(item => item.location == (night ? FurnitureState.Location.House : FurnitureState.Location.Shop))
                .OrderBy(item => Vector3.Distance(item.position, player.transform.position)).FirstOrDefault(); //근처 바닥 가구
            if (closest != null && near(player, closest.position) && player.canInteract(closest.position, now) &&
                !Physics.Linecast(player.transform.position + Vector3.up, closest.position + Vector3.up, ~0, QueryTriggerInteraction.Ignore) &&
                inventoryComponent.pickUp(closest.id, player.slot, night))
                changed();
        }
        public static bool near(Player player, Vector3 position) //월드 행동의 호스트 거리 검증
        {
            Vector3 difference = player.transform.position - position; //높이를 제외한 조작 거리
            difference.y = 0;
            return difference.sqrMagnitude <= 1.6f * 1.6f;
        }
        public FurnitureState snapshot(double now) //공동 상태와 가구를 독립 복사
        {
            return new FurnitureState
            {
                day = roundComponent.day, days = roundComponent.days, cash = economyComponent.cash, targetCash = economyComponent.target,
                orderKind = shopComponent.orderKind, orderSeconds = shopComponent.remaining(now), alarm = nightComponent.alarm,
                policeSeconds = nightComponent.policeRemaining(now), sensorActive = nightComponent.sensorActive(now),
                ownerPosition = nightComponent.ownerPosition, policePosition = nightComponent.policePosition,
                escaped = nightComponent.escapeSnapshot, playScores = leisureComponent.records, leisurePhase = leisureComponent.phase(now),
                leisureClock = leisureComponent.clock(now), leisureCycle = leisureComponent.cycle,
                ledger = economyComponent.ledger, furniture = inventoryComponent.snapshot(), doorOpen = doorOpen,
                success = economyComponent.cash >= economyComponent.target, outcome = outcome
            };
        }
        public void buildUI(RectTransform page, Font font, Color accent) //공통 설정 화면 안에 Play 화면 조립
        {
            uiComponent.build(page, font, accent);
        }
        public void showState(AuctionState state) //표시 담당 구성 요소에 공유 상태 전달
        {
            uiComponent.showState(state);
            worldComponent.showState(state);
        }
    }
}
