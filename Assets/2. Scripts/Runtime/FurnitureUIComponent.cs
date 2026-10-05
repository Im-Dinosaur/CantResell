using System.Linq;
using UnityEngine;
using UnityEngine.UI;

namespace CantResell
{
    public sealed class FurnitureUIComponent : MonoBehaviour
    {
        private RectTransform root; //새 Play HUD
        private Font font; //공통 메뉴 글꼴
        private Text summary; //공금과 날짜 및 시간
        private Text order; //손님 주문
        private Text inventory; //공유 재고
        private Text alarm; //밤 경보와 조작 안내
        private Text history; //공금 거래 기록
        private Text result; //공동 성공과 실패
        private RectTransform resultPanel; //공동 결과 배경
        private Text playInfo; //무료 놀이 기록
        private Image playBar; //타이밍 표시
        private InputField wager; //공금 베팅 금액
        private Button bet; //도박 요청
        private Button allIn; //공금 전체 베팅 요청
        private Button play; //무료 놀이 요청
        private Button lobby; //대기실 복귀
        private AuctionState state; //수신한 공유 상태
        private double receivedAt; //타이머 보정 기준
        public bool inputBlocked => wager != null && wager.isFocused; //금액 입력 중 이동 차단

        public void build(RectTransform page, Font sharedFont, Color accent) //기존 설정과 음성 버튼을 유지한 Play HUD 구성
        {
            font = sharedFont;
            root = rect(page, "FurnitureHUD", 0, 0, 1600, 900);
            summary = text(root, "공동 가구점 준비 중", 40, 96, 1500, 42, 25, accent);
            RectTransform left = panel(root, "ShopOrders", 24, 151, 278, 586); //손님과 공동 재고 영역
            order = text(left, "", 15, 15, 248, 125, 20, accent);
            inventory = text(left, "", 15, 157, 248, 410, 17, Color.white);
            RectTransform right = panel(root, "SharedMoney", 1214, 151, 362, 586); //도박과 거래 기록 영역
            text(right, "모두의 공금으로 도박합니다", 16, 14, 330, 42, 20, accent);
            wager = input(right, 16, 66, 140);
            bet = button(right, "베팅", 172, 66, 172, () =>
            {
                if (int.TryParse(wager.text, out int amount))
                    AuctionGame.current.requestStoreGamble(amount);
            });
            allIn = button(right, "공금 전부 베팅", 16, 120, 328, () => AuctionGame.current.requestStoreGamble(state?.store?.cash ?? 0));
            playInfo = text(right, "무료 놀이", 16, 183, 328, 50, 17, Color.white);
            RectTransform track = rect(right, "TimingTrack", 16, 240, 328, 10); //무료 놀이 막대
            track.gameObject.AddComponent<Image>().color = Color.gray;
            RectTransform marker = rect(right, "TimingMarker", 16, 236, 8, 18); //가운데에 맞출 표시
            playBar = marker.gameObject.AddComponent<Image>();
            playBar.color = accent;
            play = button(right, "타이밍 맞추기 (무료)", 16, 267, 328, () => AuctionGame.current.requestStorePlay());
            history = text(right, "", 16, 331, 328, 240, 16, Color.white);
            alarm = text(root, "", 335, 679, 845, 100, 21, accent);
            resultPanel = panel(root, "SharedResult", 335, 260, 845, 365);
            result = text(resultPanel, "", 22, 20, 800, 280, 27, Color.white);
            lobby = button(resultPanel, "대기실로 돌아가기", 260, 310, 320, () => AuctionGame.current.requestLobby());
            resultPanel.gameObject.SetActive(false);
            state = null;
        }
        public void showState(AuctionState next) //공유 잔액과 주문 및 결과 표시
        {
            if (root == null || next.store == null)
                return;
            state = next;
            receivedAt = Time.realtimeSinceStartupAsDouble;
            FurnitureState data = next.store; //수신한 공동 게임 상태
            bool night = next.phase == AuctionState.Phase.Night; //밤 표시 여부
            bool ended = next.phase == AuctionState.Phase.Results || next.phase == AuctionState.Phase.Aborted; //종료 표시 여부
            order.text = night ? "공동 침입\n차량에 적재한 뒤 빈손으로 E를 눌러 철수" : data.orderKind >= 0 ?
                "손님 주문\n" + FurnitureInventoryComponent.names[data.orderKind] + "\n해당 가구를 들고 계산대에서 E" : "다음 손님을 기다립니다.\n재고가 없으면 밤에 확보하세요.";
            inventory.text = "공동 재고 / 침입 가구\n" + string.Join("\n", data.furniture.Where(item => night ? item.location == FurnitureState.Location.House || item.location == FurnitureState.Location.Truck || item.location == FurnitureState.Location.Carried : item.location == FurnitureState.Location.Shop || item.location == FurnitureState.Location.Carried)
                .Take(15).Select(item => "#" + item.id + " " + FurnitureInventoryComponent.names[item.kind] + " " + item.price + (item.location == FurnitureState.Location.Carried ? " · " + next.players[item.carrier]?.name : item.location == FurnitureState.Location.Truck ? " · 적재" : "")));
            history.text = "공금 장부\n" + string.Join("\n", data.ledger);
            playInfo.text = "무료 놀이 최고 기록\n" + string.Join(" / ", data.playScores.Select((score, slot) => (slot + 1) + "번 " + score));
            resultPanel.gameObject.SetActive(ended);
            lobby.interactable = ended && next.localSlot == next.hostSlot;
            if (ended)
                result.text = next.phase == AuctionState.Phase.Aborted ? "참가자 이탈로 게임이 중단됐습니다." :
                    (data.success ? "공동 가게 운영 성공!" : "공동 가게 운영 실패") + "\n\n최종 공금 " + data.cash + " / 목표 " + data.targetCash + "\n\n" + data.outcome;
            wager.gameObject.SetActive(!night && !ended);
            bet.gameObject.SetActive(!night && !ended);
            allIn.gameObject.SetActive(!night && !ended);
            play.gameObject.SetActive(!night && !ended);
        }
        private void Update() //연속 타이머와 현재 위치의 행동 버튼 표시
        {
            if (root == null || state?.store == null)
                return;
            float age = (float)(Time.realtimeSinceStartupAsDouble - receivedAt); //수신 후 경과 시간
            float remaining = Mathf.Max(0, state.secondsRemaining - age); //현재 단계 남은 시간
            FurnitureState data = state.store; //현재 공유 상태
            bool night = state.phase == AuctionState.Phase.Night; //침입 단계 여부
            summary.text = data.day + "/" + data.days + "일 · " + (night ? "공동 도둑질" : state.phase == AuctionState.Phase.Day ? "가구점 영업" : "결과 / 준비") +
                "   " + Mathf.CeilToInt(remaining) + "초   공금 " + data.cash + " / 목표 " + data.targetCash;
            string warning = data.alarm == FurnitureState.Alarm.Quiet ? (data.sensorActive ? "센서 작동 중!" : "센서 꺼짐") :
                data.alarm == FurnitureState.Alarm.Reporting ? "집주인 신고! 경찰 도착 " + Mathf.CeilToInt(Mathf.Max(0, data.policeSeconds - age)) + "초" : "경찰 추격 중! 차량으로 철수하세요."; //경보 안내
            alarm.text = night ? warning + "\nWASD 이동 · E 문/센서/가구/차량 · Q 내려놓기\n" + data.outcome :
                "WASD 이동 · E 가구 들기/주문 배송 · Q 내려놓기\n도박장과 놀이 기계 가까이에서 오른쪽 버튼 사용\n" + data.outcome;
            Player local = AuctionGame.current?.localPlayer; //버튼 사용 위치를 확인할 참가자
            bool daytime = state.phase == AuctionState.Phase.Day; //낮의 활동 가능 여부
            if (daytime && data.orderKind >= 0)
                order.text = "손님 주문 · " + Mathf.CeilToInt(Mathf.Max(0, data.orderSeconds - age)) + "초\n" + FurnitureInventoryComponent.names[data.orderKind] + "\n해당 가구를 들고 계산대에서 E";
            bet.interactable = allIn.interactable = daytime && local != null && FurnitureStore.near(local, FurnitureStore.gamblingTable) && data.cash > 0;
            play.interactable = daytime && local != null && FurnitureStore.near(local, FurnitureLeisureComponent.machine);
            float position = Mathf.PingPong(data.leisureClock + age / Mathf.Max(0.5f, data.leisureCycle), 1); //수신 간격 사이에도 부드럽게 왕복
            playBar.rectTransform.anchoredPosition = new Vector2(16 + position * 320, -236);
        }
        private RectTransform rect(Transform parent, string name, float x, float y, float width, float height) //기준 해상도의 영역 생성
        {
            RectTransform value = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>(); //새 UI 영역
            value.SetParent(parent, false);
            value.anchorMin = value.anchorMax = value.pivot = new Vector2(0, 1);
            value.anchoredPosition = new Vector2(x, -y);
            value.sizeDelta = new Vector2(width, height);
            return value;
        }
        private RectTransform panel(Transform parent, string name, float x, float y, float width, float height) //월드를 가리지 않는 양쪽 패널
        {
            RectTransform value = rect(parent, name, x, y, width, height); //패널 영역
            value.gameObject.AddComponent<Image>().color = new Color(0.03f, 0.04f, 0.07f, 0.92f);
            return value;
        }
        private Text text(Transform parent, string content, float x, float y, float width, float height, int size, Color color) //한글 텍스트 생성
        {
            Text value = rect(parent, "Text", x, y, width, height).gameObject.AddComponent<Text>(); //텍스트 표시
            value.font = font;
            value.text = content;
            value.fontSize = size;
            value.color = color;
            value.raycastTarget = false;
            value.verticalOverflow = VerticalWrapMode.Truncate;
            return value;
        }
        private Button button(Transform parent, string title, float x, float y, float width, UnityEngine.Events.UnityAction clicked) //진입점으로 요청을 보내는 버튼
        {
            RectTransform area = rect(parent, title, x, y, width, 44); //버튼 영역
            Image background = area.gameObject.AddComponent<Image>(); //클릭 배경
            background.color = new Color(0.2f, 0.3f, 0.4f);
            Button value = area.gameObject.AddComponent<Button>(); //요청 버튼
            value.targetGraphic = background;
            value.onClick.AddListener(clicked);
            Text caption = text(area, title, 0, 0, width, 44, 18, Color.white); //버튼 이름
            caption.alignment = TextAnchor.MiddleCenter;
            return value;
        }
        private InputField input(Transform parent, float x, float y, float width) //정수 공금 베팅 입력
        {
            RectTransform area = rect(parent, "SharedWager", x, y, width, 44); //입력 영역
            area.gameObject.AddComponent<Image>().color = new Color(0.1f, 0.15f, 0.2f);
            InputField value = area.gameObject.AddComponent<InputField>(); //베팅 입력
            Text caption = text(area, "", 8, 3, width - 16, 38, 21, Color.white); //입력한 숫자
            value.textComponent = caption;
            value.contentType = InputField.ContentType.IntegerNumber;
            value.characterLimit = 10;
            value.text = "50";
            return value;
        }
    }
}
