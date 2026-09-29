using System;
using System.Linq;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

namespace CantResell
{
    public sealed class AuctionUIComponent : MonoBehaviour
    {
        [SerializeField] private Color panelColor = new Color(0.055f, 0.075f, 0.12f, 0.95f); //메뉴 패널 색상
        [SerializeField] private Color accentColor = new Color(0.98f, 0.65f, 0.24f); //주요 버튼 강조 색상
        private AuctionGame game; //사용자 요청을 전달할 진입점
        private Font font; //운영체제에서 불러올 한글 표시 글꼴
        private Canvas canvas; //화면 크기에 대응할 캔버스
        private RectTransform page; //현재 씬의 UI 루트
        private string sceneName; //표시 중인 씬 이름
        private AuctionState state; //로컬 플레이어용 화면 정보
        private double receivedAt; //남은 시간 보정 기준 시각
        private bool busy; //접속 중 입력 차단 여부
        private InputField nicknameInput; //플레이어 이름 입력창
        private RectTransform roomRows; //실시간 방 목록의 행 영역
        private Text roomListStatus; //지역과 목록 상태 안내
        private Text roomPageLabel; //방 목록 페이지 표시
        private AuctionNetworkComponent.Room[] rooms = Array.Empty<AuctionNetworkComponent.Room>(); //최근 수신한 방 목록
        private int roomPage; //방 목록의 현재 페이지
        private RectTransform modal; //열려 있는 방 생성 또는 참가 팝업
        private Text modalMessage; //팝업 내부 오류 표시
        private CanvasGroup modalInputs; //접속 중 팝업의 중복 입력 차단
        private CanvasGroup homeInputs; //팝업 뒤 메뉴의 키보드 및 마우스 입력 차단
        private CanvasGroup browserInputs; //팝업 뒤 목록의 키보드 및 마우스 입력 차단
        private bool settingsOpen; //설정 창의 배경 입력 차단 상태
        private Text voiceLabel; //음성 연결 상태 표시
        private Image microphoneLevel; //마이크 테스트 입력 막대
        private Button microphoneButton; //마이크 송신 전환 버튼
        private Button microphoneTestButton; //마이크 테스트 전환 버튼
        private InputField bidInput; //입찰 금액 입력창
        private Text roomLabel; //현재 방 코드 표시
        private Text phaseLabel; //현재 단계 표시
        private Text timerLabel; //남은 시간 표시
        private Text noticeLabel; //진행 안내 표시
        private Text secretLabel; //개인에게만 보이는 상품 상태
        private Text bidLabel; //현재 최고 입찰 표시
        private Text resultLabel; //최종 순위 표시
        private readonly Text[] playerLabels = new Text[4]; //참가자별 상태 표시
        private Button createButton; //방 만들기 버튼
        private Button joinButton; //방 참가 버튼
        private Button readyButton; //준비 상태 변경 버튼
        private Button startButton; //게임 시작 버튼
        private Button inspectButton; //검사권 사용 버튼
        private Button bidButton; //입찰 요청 버튼
        private Button lobbyButton; //대기실 복귀 버튼

        public void initialize(AuctionGame owner) //UI와 입력 시스템 초기화
        {
            game = owner;
            font = Font.CreateDynamicFontFromOSFont(new[] { "Malgun Gothic", "Apple SD Gothic Neo", "Noto Sans CJK KR", "Arial" }, 24);
            GameObject canvasObject = new GameObject("AuctionCanvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster)); //지속되는 화면 캔버스
            canvasObject.transform.SetParent(transform, false);
            canvas = canvasObject.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 20;
            CanvasScaler scaler = canvasObject.GetComponent<CanvasScaler>(); //다양한 해상도에 대응하는 배율 설정
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1600, 900);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.Expand;
            GameObject eventObject = new GameObject("AuctionEventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule)); //새 Input System의 UI 입력
            eventObject.transform.SetParent(transform, false);
            eventObject.GetComponent<InputSystemUIInputModule>().AssignDefaultActions();
            AudioListener.volume = PlayerPrefs.GetFloat("CantResell.Volume", 0.8f);
        }

        public void showScene(string nextScene) //씬 역할에 맞는 화면 생성
        {
            if (canvas == null || (page != null && sceneName == nextScene))
                return;
            if (page != null)
            {
                page.gameObject.SetActive(false);
                Destroy(page.gameObject);
            }
            sceneName = nextScene;
            state = null;
            Array.Clear(playerLabels, 0, playerLabels.Length);
            readyButton = startButton = inspectButton = bidButton = lobbyButton = null;
            createButton = joinButton = null;
            secretLabel = bidLabel = phaseLabel = timerLabel = resultLabel = null;
            roomRows = modal = null;
            roomListStatus = roomPageLabel = modalMessage = voiceLabel = null;
            microphoneButton = microphoneTestButton = null;
            microphoneLevel = null;
            modalInputs = null;
            homeInputs = browserInputs = null;
            settingsOpen = false;
            page = createRect(canvas.transform, "Page", 0, 0, 1600, 900);
            page.anchorMin = page.anchorMax = page.pivot = new Vector2(0.5f, 0.5f);
            page.anchoredPosition = Vector2.zero;
            roomLabel = createText(page, "", 58, 869, 1484, 28, 15, new Color(0.72f, 0.66f, 0.56f));
            noticeLabel = createText(page, "", 58, 816, 1484, 48, 20);
            if (nextScene == "Home")
                buildHome();
            else if (nextScene == "StandBy")
                buildLobby();
            else
            {
                createText(page, "반품 불가!", 52, 28, 430, 62, 42, accentColor);
                createButtonAt(page, "방 나가기", 1370, 34, 175, 48, game.leaveRoom, false);
                buildPlay();
                buildVoiceControls(false);
            }
        }

        private void buildHome() //시작 화면의 방 생성과 참가 메뉴 구성
        {
            RectTransform panel = createPanel(page, "HomeMenu", 40, 50, 350, 744); //레퍼런스의 왼쪽 세로 메뉴
            panel.GetComponent<Image>().color = new Color(0.055f, 0.035f, 0.025f, 0.9f);
            homeInputs = panel.gameObject.AddComponent<CanvasGroup>();
            createText(panel, "NO RETURNS", 28, 27, 295, 32, 19, accentColor);
            Text title = createText(panel, "반품\n불가!", 25, 73, 300, 210, 82, new Color(1, 0.34f, 0.23f)); //붉은 간판 느낌의 제목
            title.lineSpacing = 0.93f;
            createText(panel, "믿고 사셨다고요?", 30, 280, 292, 45, 23);
            createText(panel, "플레이어 이름", 30, 354, 290, 32, 17);
            nicknameInput = createInput(panel, PlayerPrefs.GetString("CantResell.Name", "플레이어"), 30, 393, 290, false);
            nicknameInput.name = "Nickname";
            nicknameInput.characterLimit = 12;
            createButton = createButtonAt(panel, "방 만들기", 30, 479, 290, 51, showCreateRoom);
            joinButton = createButtonAt(panel, "참가하기", 30, 545, 290, 51, game.refreshRooms, false);
            createButtonAt(panel, "설정", 30, 611, 140, 51, showSettings, false);
            createButtonAt(panel, "게임 나가기", 180, 611, 140, 51, quitGame, false).GetComponentInChildren<Text>().fontSize = 20;
            createText(panel, "4인 온라인 경매 게임", 30, 690, 290, 30, 17, accentColor);
            RectTransform browser = createPanel(page, "RoomBrowser", 1070, 50, 490, 744); //테이블 오른쪽 방 목록
            browserInputs = browser.gameObject.AddComponent<CanvasGroup>();
            createText(browser, "열려 있는 경매장", 25, 27, 330, 52, 31, accentColor);
            roomListStatus = createText(browser, "참가하기를 눌러 방 목록을 불러오세요.", 25, 89, 440, 65, 18);
            roomRows = createRect(browser, "RoomRows", 25, 166, 440, 466);
            createButtonAt(browser, "이전", 25, 660, 88, 44, () => changeRoomPage(-1), false).GetComponentInChildren<Text>().fontSize = 18;
            roomPageLabel = createText(browser, "1 / 1", 120, 668, 70, 35, 18);
            createButtonAt(browser, "다음", 196, 660, 88, 44, () => changeRoomPage(1), false).GetComponentInChildren<Text>().fontSize = 18;
            createButtonAt(browser, "새로고침", 308, 660, 155, 44, game.refreshRooms, false).GetComponentInChildren<Text>().fontSize = 18;
            showRooms(rooms, "");
            showMessage("목록에서 방을 선택해 참가하세요. 같은 방에서 음성으로 대화할 수 있습니다.");
            setBusy(busy);
        }

        private string saveNickname() //입장 시 사용할 이름을 로컬 설정에 저장
        {
            PlayerPrefs.SetString("CantResell.Name", nicknameInput.text);
            PlayerPrefs.Save();
            return nicknameInput.text;
        }

        public void showRoomListStatus(string message) //목록 연결 및 오류 상태 표시
        {
            if (roomListStatus != null)
                roomListStatus.text = message;
        }

        public void showRooms(AuctionNetworkComponent.Room[] nextRooms, string region) //전체 목록 교체와 사라진 방 정리
        {
            rooms = nextRooms ?? Array.Empty<AuctionNetworkComponent.Room>();
            roomPage = Mathf.Clamp(roomPage, 0, Math.Max(0, (rooms.Length - 1) / 5));
            showRoomListStatus((string.IsNullOrEmpty(region) ? "" : "지역 " + region.ToUpperInvariant() + "  ·  ") + rooms.Length + "개의 방\n공개 방은 바로 입장 · 비공개 방은 비밀번호 필요");
            renderRoomRows();
        }

        private void changeRoomPage(int direction) //많은 방 목록을 다섯 개씩 이동
        {
            roomPage = Mathf.Clamp(roomPage + direction, 0, Math.Max(0, (rooms.Length - 1) / 5));
            renderRoomRows();
        }

        private void renderRoomRows() //목록 행의 제목과 참가 가능 상태 표시
        {
            if (roomRows == null)
                return;
            foreach (Transform child in roomRows) //기존 목록 행 제거
            {
                child.gameObject.SetActive(false);
                Destroy(child.gameObject);
            }
            if (rooms.Length == 0)
                createText(roomRows, "아직 열린 방이 없습니다.\n\n첫 번째 경매장을 만들어 보세요.", 12, 100, 415, 180, 24);
            for (int index = roomPage * 5; index < Math.Min(rooms.Length, (roomPage + 1) * 5); index++) //현재 페이지의 방 번호
            {
                AuctionNetworkComponent.Room room = rooms[index]; //버튼이 참가할 방
                Button row = createButtonAt(roomRows, "Room_" + room.code, 0, (index % 5) * 92, 440, 80, () => selectRoom(room), false); //방 선택 행
                row.GetComponentInChildren<Text>().text = "";
                row.interactable = !busy && room.open && room.players < room.capacity;
                createText(row.transform, room.title, 16, 7, 330, 35, 21);
                createText(row.transform, room.locked ? "비공개 · 비밀번호 필요" : "공개 · 누구나 참가", 16, 47, 325, 28, 16, accentColor);
                createText(row.transform, room.players + "/" + room.capacity + "\n" + (!room.open ? "진행 중" : room.players >= room.capacity ? "가득 참" : "입장"), 348, 9, 85, 67, 18);
                if (room.locked)
                    drawLock(row.transform, 310, 49);
            }
            if (roomPageLabel != null)
                roomPageLabel.text = (roomPage + 1) + " / " + Math.Max(1, (rooms.Length + 4) / 5);
        }

        private void drawLock(Transform parent, float x, float y) //폰트에 의존하지 않는 비공개 자물쇠 표시
        {
            createPanel(parent, "LockTop", x + 4, y, 12, 12).GetComponent<Image>().color = accentColor;
            createPanel(parent, "LockHole", x + 7, y + 3, 6, 7).GetComponent<Image>().color = panelColor;
            createPanel(parent, "LockBody", x, y + 9, 20, 13).GetComponent<Image>().color = accentColor;
        }

        private RectTransform openRoomModal(string title) //배경 입력을 차단하는 방 설정 팝업 생성
        {
            if (modal != null || busy)
                return null;
            modal = createPanel(page, "RoomModal", 0, 0, 1600, 900);
            modal.GetComponent<Image>().color = new Color(0, 0, 0, 0.76f);
            setHomeInputAvailable(false);
            RectTransform box = createPanel(modal, "Dialog", 475, 145, 650, 595); //가운데 팝업 본체
            modalInputs = box.gameObject.AddComponent<CanvasGroup>();
            createText(box, title, 36, 27, 580, 55, 34, accentColor);
            modalMessage = createText(box, "", 36, 416, 578, 78, 20, new Color(1, 0.59f, 0.45f));
            createButtonAt(box, "취소", 36, 514, 275, 52, closeRoomModal, false);
            return box;
        }

        private void closeRoomModal() //방 설정 입력을 폐기하고 목록으로 복귀
        {
            if (modal != null)
            {
                modal.gameObject.SetActive(false);
                Destroy(modal.gameObject);
            }
            modal = null;
            modalInputs = null;
            modalMessage = null;
            setHomeInputAvailable(!busy && !settingsOpen);
        }

        private void showCreateRoom() //방 제목과 공개 여부 및 비밀번호 입력 팝업
        {
            RectTransform box = openRoomModal("새 경매장 만들기"); //방 생성 팝업
            if (box == null)
                return;
            createText(box, "방 제목", 36, 98, 575, 30, 19);
            InputField title = createInput(box, "", 36, 135, 578, false); //방 제목 입력
            title.name = "RoomTitle";
            title.characterLimit = 30;
            title.onValueChanged.AddListener(_ => { if (modalMessage != null) modalMessage.text = ""; });
            bool locked = false; //이번 생성의 비공개 선택 상태
            Button publicButton = createButtonAt(box, "공개 · 선택됨", 36, 224, 275, 50, null); //공개 선택
            Button privateButton = createButtonAt(box, "비공개", 339, 224, 275, 50, null, false); //비공개 선택
            Text passwordLabel = createText(box, "비밀번호 · 문자·숫자 1~8자", 36, 303, 578, 35, 19); //비밀번호 입력 안내
            InputField password = createInput(box, "", 36, 348, 578, false); //비공개 방 비밀번호
            configurePassword(password);
            password.onValueChanged.AddListener(_ => { if (modalMessage != null) modalMessage.text = ""; });
            password.gameObject.SetActive(false);
            passwordLabel.gameObject.SetActive(false);
            void selectVisibility(bool value) //공개 선택에 따른 비밀번호 표시 갱신
            {
                locked = value;
                publicButton.GetComponentInChildren<Text>().text = value ? "공개" : "공개 · 선택됨";
                privateButton.GetComponentInChildren<Text>().text = value ? "비공개 · 선택됨" : "비공개";
                publicButton.targetGraphic.color = value ? new Color(0.17f, 0.125f, 0.095f) : accentColor;
                privateButton.targetGraphic.color = value ? accentColor : new Color(0.17f, 0.125f, 0.095f);
                publicButton.GetComponentInChildren<Text>().color = value ? Color.white : new Color(0.08f, 0.09f, 0.13f);
                privateButton.GetComponentInChildren<Text>().color = value ? new Color(0.08f, 0.09f, 0.13f) : Color.white;
                password.gameObject.SetActive(value);
                passwordLabel.gameObject.SetActive(value);
                if (!value)
                    password.text = "";
            }
            publicButton.onClick.AddListener(() => selectVisibility(false));
            privateButton.onClick.AddListener(() => selectVisibility(true));
            createButtonAt(box, "생성하기", 339, 514, 275, 52, () => game.createRoom(saveNickname(), title.text, locked, password.text));
            title.ActivateInputField();
        }

        private void configurePassword(InputField input) //문자와 숫자만 허용하는 숨김 비밀번호 입력 설정
        {
            input.name = "RoomPassword";
            input.contentType = InputField.ContentType.Password;
            input.characterLimit = 8;
            input.onValidateInput = (text, index, character) => char.IsLetterOrDigit(character) ? character : '\0';
        }

        private void selectRoom(AuctionNetworkComponent.Room room) //목록 선택 후 공개 방 입장 또는 비밀번호 요청
        {
            if (busy)
                return;
            if (!room.locked)
            {
                game.joinRoom(saveNickname(), room, "");
                return;
            }
            RectTransform box = openRoomModal("비공개 경매장 입장"); //비밀번호 확인 팝업
            if (box == null)
                return;
            createText(box, room.title, 36, 115, 578, 87, 27);
            createText(box, "방 비밀번호 · 문자·숫자 최대 8자", 36, 253, 578, 42, 20);
            InputField password = createInput(box, "", 36, 313, 578, false); //입장 비밀번호 입력
            configurePassword(password);
            createButtonAt(box, "입장하기", 339, 514, 275, 52, () => game.joinRoom(saveNickname(), room, password.text));
            password.ActivateInputField();
        }

        private void buildLobby() //참가자 목록과 준비 및 색상 선택 구성
        {
            createButtonAt(page, "방 나가기", 55, 35, 180, 51, game.leaveRoom, false);
            Text heading = createText(page, "경매 시작 전", 570, 33, 460, 56, 32, accentColor); //상단 중앙 제목
            heading.alignment = TextAnchor.MiddleCenter;
            createText(page, "누구의 말을 믿으시겠어요?", 626, 97, 440, 39, 20);
            readyButton = createButtonAt(page, "준비하기", 1185, 35, 170, 51, game.requestReady);
            startButton = createButtonAt(page, "게임 시작", 1370, 35, 175, 51, game.requestStart);
            startButton.GetComponentInChildren<Text>().fontSize = 18;
            readyButton.interactable = startButton.interactable = false;
            RectTransform panel = createPanel(page, "LobbyControls", 1170, 112, 375, 80); //캐릭터 머리 위의 작은 선택 영역
            createText(panel, "내 캐릭터 색상", 15, 5, 345, 25, 17);
            string[] colors = { "코랄", "민트", "노랑", "보라" }; //선택할 색상 이름
            for (int index = 0; index < colors.Length; index++) //색상 버튼 번호
            {
                int selectedColor = index; //버튼이 전달할 고정 색상 번호
                createButtonAt(panel, colors[index], 15 + index * 89, 37, 79, 32,
                    () => game.selectColor(selectedColor), false).GetComponentInChildren<Text>().fontSize = 16;
            }
            for (int slot = 0; slot < 4; slot++) //캐릭터 아래 좌석별 이름과 준비 상태
            {
                RectTransform seat = createPanel(page, "Seat" + slot, 153 + slot * 326, 633, 316, 94); //좌석 이름표
                seat.GetComponent<Image>().color = new Color(0.04f, 0.03f, 0.025f, 0.7f);
                playerLabels[slot] = createText(seat, "빈 자리\n참가자를 기다리는 중", 8, 8, 300, 84, 21);
                playerLabels[slot].alignment = TextAnchor.MiddleCenter;
            }
            buildVoiceControls(true);
        }

        private void buildVoiceControls(bool allowTest) //대기실과 게임에서 계속 사용할 음성 조작 영역
        {
            RectTransform bar = createPanel(page, "VoiceControls", 58, 752, 1484, 59); //하단 음성 상태 표시줄
            voiceLabel = createText(bar, "음성 연결 대기 중", 16, 13, allowTest ? 600 : 1030, 38, 19);
            if (allowTest)
            {
                RectTransform track = createPanel(bar, "MicrophoneTrack", 675, 23, 196, 12); //마이크 입력 크기 배경
                track.GetComponent<Image>().color = new Color(0.19f, 0.17f, 0.14f);
                microphoneLevel = createPanel(track, "MicrophoneLevel", 0, 0, 0, 12).GetComponent<Image>();
                microphoneLevel.color = new Color(0.42f, 0.88f, 0.65f);
                microphoneTestButton = createButtonAt(bar, "마이크 테스트", 906, 8, 244, 43, game.toggleMicrophoneTest, false);
                microphoneTestButton.GetComponentInChildren<Text>().fontSize = 19;
            }
            microphoneButton = createButtonAt(bar, "마이크 켜기", 1180, 8, 286, 43, game.toggleMicrophone, false);
            microphoneButton.GetComponentInChildren<Text>().fontSize = 19;
        }

        private void buildPlayerList() //네 좌석의 공개 정보 표시 영역 생성
        {
            RectTransform panel = createPanel(page, "Players", 60, 120, 355, 655); //참가자 상태 패널
            createText(panel, "참가자", 24, 20, 305, 50, 29);
            for (int slot = 0; slot < 4; slot++) //표시할 좌석 번호
                playerLabels[slot] = createText(panel, "빈 자리", 24, 95 + slot * 132, 307, 115, 23);
        }

        private void buildPlay() //경매 정보와 개인 행동 화면 구성
        {
            buildPlayerList();
            RectTransform panel = createPanel(page, "AuctionControls", 1130, 120, 410, 655); //게임 조작 패널
            phaseLabel = createText(panel, "입장 중", 26, 22, 280, 45, 29);
            timerLabel = createText(panel, "", 310, 24, 77, 45, 28, accentColor);
            secretLabel = createText(panel, "상품: 토스터\n상태: 알 수 없음", 26, 87, 358, 100, 25);
            bidLabel = createText(panel, "입찰을 기다리고 있습니다.", 26, 210, 358, 103, 23);
            inspectButton = createButtonAt(panel, "비밀 검사", 26, 331, 358, 55, game.requestInspection);
            bidInput = createInput(panel, "10", 26, 405, 180, true);
            bidButton = createButtonAt(panel, "입찰하기", 219, 405, 165, 56, submitBid);
            resultLabel = createText(panel, "", 26, 324, 358, 235, 23);
            resultLabel.gameObject.SetActive(false);
            lobbyButton = createButtonAt(panel, "대기실로 돌아가기", 26, 580, 358, 51, game.requestLobby, false);
            lobbyButton.gameObject.SetActive(false);
            inspectButton.interactable = bidButton.interactable = bidInput.interactable = false;
        }

        private void submitBid() //정수 금액을 확인하고 입찰 요청
        {
            if (int.TryParse(bidInput.text, out int amount)) //입력한 입찰 금액
                game.requestBid(amount);
            else
                showMessage("입찰 금액을 숫자로 입력해 주세요.");
        }

        public void showState(AuctionState nextState, string room) //호스트가 허용한 정보만 화면에 표시
        {
            state = nextState;
            receivedAt = Time.realtimeSinceStartupAsDouble;
            if (page == null)
                return;
            roomLabel.text = string.IsNullOrWhiteSpace(room) ? "" : "ROOM  ·  " + room;
            noticeLabel.text = state.notice;
            for (int slot = 0; slot < 4; slot++) //각 참가자의 표시 내용 갱신
            {
                if (playerLabels[slot] == null)
                    continue;
                AuctionState.Player player = state.players[slot]; //좌석의 공개 플레이어 정보
                if (player == null)
                {
                    playerLabels[slot].text = (slot + 1) + "번 · 빈 자리";
                    continue;
                }
                string suffix = slot == state.localSlot ? " (나)" : ""; //로컬 플레이어 표시
                string role = slot == state.hostSlot ? " · 방장" : ""; //방장 표시
                string detail = sceneName == "StandBy" ? (player.ready ? "준비 완료" : "준비 중") :
                    player.cash + " 코인" + (slot == state.sellerSlot ? "  · 판매자" : ""); //씬별 참가자 상태
                playerLabels[slot].text = (slot + 1) + "번 · " + player.name + suffix + role + "\n" + detail;
            }
            if (readyButton != null)
            {
                bool lobby = state.phase == AuctionState.Phase.Lobby; //현재 준비 가능 여부
                readyButton.interactable = lobby;
                readyButton.GetComponentInChildren<Text>().text = state.players[state.localSlot].ready ? "준비 취소" : "준비하기";
                startButton.interactable = lobby && state.localSlot == state.hostSlot && state.players.All(player => player != null && player.ready);
                startButton.GetComponentInChildren<Text>().text = state.localSlot == state.hostSlot ? "게임 시작" : "방장이 시작합니다";
            }
            if (phaseLabel == null)
                return;
            phaseLabel.text = (state.round + 1) + "/4 · " + phaseName(state.phase);
            secretLabel.text = "상품: 토스터\n" + (state.knowsCondition ? (state.goodCondition ? "정상 제품" : "불량 제품") : "상태: 알 수 없음") +
                (state.knowsCondition && !state.isRevealed() ? " · 나만 확인" : "");
            bidLabel.text = "최고 입찰  " + state.highestBid + " 코인\n" +
                (state.bidderSlot >= 0 && state.players[state.bidderSlot] != null ? state.players[state.bidderSlot].name : "입찰자 없음") +
                "\n정상 보상  " + state.normalReward + " 코인";
            inspectButton.interactable = state.phase == AuctionState.Phase.Inspection && state.localSlot != state.sellerSlot && !state.inspected && state.inspectionTickets > 0;
            inspectButton.GetComponentInChildren<Text>().text = "비밀 검사 · " + state.inspectionTickets + "회 남음";
            int nextMinimum = state.highestBid + state.minimumRaise; //다음 입찰의 최소 금액
            bidButton.interactable = state.phase == AuctionState.Phase.Bidding && state.localSlot != state.sellerSlot && state.players[state.localSlot].cash >= nextMinimum;
            bidInput.interactable = bidButton.interactable;
            if (!bidInput.isFocused)
                bidInput.SetTextWithoutNotify(nextMinimum.ToString());
            bool ended = state.phase == AuctionState.Phase.Results || state.phase == AuctionState.Phase.Aborted; //종료 화면 여부
            inspectButton.gameObject.SetActive(!ended);
            bidButton.gameObject.SetActive(!ended);
            bidInput.gameObject.SetActive(!ended);
            resultLabel.gameObject.SetActive(ended);
            lobbyButton.gameObject.SetActive(ended);
            lobbyButton.interactable = state.localSlot == state.hostSlot;
            if (ended)
                resultLabel.text = state.phase == AuctionState.Phase.Aborted ? "게임이 중단되었습니다.\n방장이 대기실로 돌아갈 수 있습니다." : createRanking();
        }

        private string createRanking() //동점 공동 순위를 포함한 최종 결과 작성
        {
            AuctionState.Player[] ranked = state.players.Where(player => player != null).OrderByDescending(player => player.cash).ToArray(); //소지금순 참가자 목록
            return string.Join("\n\n", ranked.Select(player => (1 + ranked.Count(other => other.cash > player.cash)) + "위  " + player.name + "  " + player.cash + " 코인"));
        }

        private void Update() //상태 수신 사이의 남은 시간을 부드럽게 표시
        {
            if (voiceLabel != null && game.voice != null)
            {
                voiceLabel.text = game.voice.status;
                microphoneButton.GetComponentInChildren<Text>().text = game.voice.microphoneEnabled ? "마이크 끄기" : "마이크 켜기";
                microphoneButton.interactable = game.voice.connected;
                if (microphoneTestButton != null)
                {
                    microphoneTestButton.interactable = game.voice.connected;
                    microphoneTestButton.GetComponentInChildren<Text>().text = game.voice.testing ? "테스트 종료" : "마이크 테스트";
                    microphoneLevel.rectTransform.sizeDelta = new Vector2(game.voice.inputLevel * 196, 12);
                }
            }
            if (timerLabel != null && state != null)
                timerLabel.text = state.secondsRemaining > 0 ? Mathf.CeilToInt(Mathf.Max(0, state.secondsRemaining - (float)(Time.realtimeSinceStartupAsDouble - receivedAt))) + "초" : "";
        }

        public void showMessage(string message) //사용자에게 현재 상황 안내
        {
            if (noticeLabel != null)
                noticeLabel.text = message;
            if (modalMessage != null)
                modalMessage.text = message;
        }

        public void setBusy(bool value) //중복 접속 버튼 입력 차단
        {
            busy = value;
            if (createButton != null)
                createButton.interactable = !value;
            if (joinButton != null)
                joinButton.interactable = !value;
            if (modalInputs != null)
                modalInputs.interactable = !value;
            if (nicknameInput != null)
                nicknameInput.interactable = !value;
            setHomeInputAvailable(!value && modal == null && !settingsOpen);
            renderRoomRows();
        }

        private void setHomeInputAvailable(bool value) //팝업 밖 메뉴의 탐색과 제출 입력 제어
        {
            if (homeInputs != null)
                homeInputs.interactable = value;
            if (browserInputs != null)
                browserInputs.interactable = value;
        }

        private void showSettings() //음량과 전체 화면 설정 창 표시
        {
            if (settingsOpen || modal != null)
                return;
            settingsOpen = true;
            setHomeInputAvailable(false);
            RectTransform overlay = createPanel(page, "Settings", 0, 0, 1600, 900); //뒤쪽 메뉴 입력을 막을 설정 배경
            overlay.GetComponent<Image>().color = new Color(0.025f, 0.035f, 0.06f, 0.97f);
            createText(overlay, "설정", 540, 215, 520, 70, 40, accentColor);
            Text volumeLabel = createText(overlay, "전체 음량", 540, 325, 520, 48, 24); //음량 값 표시
            RectTransform track = createPanel(overlay, "Volume", 540, 394, 520, 25); //음량 슬라이더 배경
            track.GetComponent<Image>().color = new Color(0.2f, 0.24f, 0.32f);
            Slider slider = track.gameObject.AddComponent<Slider>(); //설정 음량 입력
            RectTransform handle = createPanel(track, "Handle", 0, -9, 24, 44); //슬라이더 손잡이
            handle.GetComponent<Image>().color = accentColor;
            slider.handleRect = handle;
            slider.targetGraphic = handle.GetComponent<Image>();
            slider.minValue = 0;
            slider.maxValue = 1;
            slider.value = AudioListener.volume;
            slider.onValueChanged.AddListener(value =>
            {
                AudioListener.volume = value;
                PlayerPrefs.SetFloat("CantResell.Volume", value);
                volumeLabel.text = "전체 음량  " + Mathf.RoundToInt(value * 100) + "%";
            });
            Button fullscreen = createButtonAt(overlay, "전체 화면: " + (Screen.fullScreen ? "켜짐" : "꺼짐"), 540, 470, 520, 56, null, false); //화면 모드 변경 버튼
            fullscreen.onClick.AddListener(() =>
            {
                Screen.fullScreen = !Screen.fullScreen;
                fullscreen.GetComponentInChildren<Text>().text = "전체 화면 전환 요청됨";
            });
            createButtonAt(overlay, "닫기", 540, 580, 520, 56, () =>
            {
                PlayerPrefs.Save();
                settingsOpen = false;
                setHomeInputAvailable(!busy);
                overlay.gameObject.SetActive(false);
                Destroy(overlay.gameObject);
            });
        }

        private void quitGame() //빌드 실행 종료
        {
#if UNITY_EDITOR
            showMessage("Unity 에디터에서는 상단 Play 버튼으로 실행을 종료해 주세요.");
#else
            Application.Quit();
#endif
        }

        private string phaseName(AuctionState.Phase phase) //단계 이름을 한국어로 표시
        {
            return phase switch
            {
                AuctionState.Phase.Pitch => "판매 설명", AuctionState.Phase.Inspection => "비밀 검사",
                AuctionState.Phase.Bidding => "입찰", AuctionState.Phase.Reveal => "시연 결과",
                AuctionState.Phase.Results => "최종 순위", AuctionState.Phase.Aborted => "중단", _ => "입장 중"
            };
        }

        private RectTransform createRect(Transform parent, string name, float x, float y, float width, float height) //기준 해상도의 UI 영역 생성
        {
            RectTransform rect = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>(); //생성한 UI 위치 정보
            rect.SetParent(parent, false);
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0, 1);
            rect.anchoredPosition = new Vector2(x, -y);
            rect.sizeDelta = new Vector2(width, height);
            return rect;
        }

        private RectTransform createPanel(Transform parent, string name, float x, float y, float width, float height) //배경 패널 생성
        {
            RectTransform rect = createRect(parent, name, x, y, width, height); //패널 위치
            rect.gameObject.AddComponent<Image>().color = panelColor;
            return rect;
        }

        private Text createText(Transform parent, string content, float x, float y, float width, float height, int size, Color? color = null) //한글 텍스트 생성
        {
            Text label = createRect(parent, "Label", x, y, width, height).gameObject.AddComponent<Text>(); //표시할 텍스트
            label.font = font;
            label.fontSize = size;
            label.color = color ?? new Color(0.94f, 0.95f, 0.98f);
            label.text = content;
            label.supportRichText = false;
            label.raycastTarget = false;
            label.horizontalOverflow = HorizontalWrapMode.Wrap;
            label.verticalOverflow = VerticalWrapMode.Truncate;
            return label;
        }

        private Button createButtonAt(Transform parent, string title, float x, float y, float width, float height, UnityEngine.Events.UnityAction action, bool accent = true) //입력 버튼 생성
        {
            RectTransform rect = createPanel(parent, title, x, y, width, height); //버튼 영역
            Image background = rect.GetComponent<Image>(); //버튼 배경
            background.color = accent ? accentColor : new Color(0.17f, 0.125f, 0.095f, 0.96f);
            Button button = rect.gameObject.AddComponent<Button>(); //클릭 입력
            button.targetGraphic = background;
            ColorBlock colors = button.colors; //비활성 버튼 표시 설정
            colors.disabledColor = new Color(0.4f, 0.4f, 0.4f, 0.6f);
            button.colors = colors;
            Text label = createText(rect, title, 8, 0, width - 16, height, 23, accent ? new Color(0.08f, 0.09f, 0.13f) : Color.white); //버튼 이름
            label.alignment = TextAnchor.MiddleCenter;
            if (action != null)
                button.onClick.AddListener(action);
            return button;
        }

        private InputField createInput(Transform parent, string value, float x, float y, float width, bool numeric) //문자 또는 숫자 입력창 생성
        {
            RectTransform rect = createPanel(parent, "Input", x, y, width, 56); //입력창 배경
            rect.GetComponent<Image>().color = new Color(0.12f, 0.095f, 0.08f);
            Text label = createText(rect, "", 14, 4, width - 28, 48, 24); //입력 중인 텍스트
            label.alignment = TextAnchor.MiddleLeft;
            InputField input = rect.gameObject.AddComponent<InputField>(); //텍스트 입력 컴포넌트
            input.textComponent = label;
            input.targetGraphic = rect.GetComponent<Image>();
            input.contentType = numeric ? InputField.ContentType.IntegerNumber : InputField.ContentType.Standard;
            input.characterLimit = numeric ? 6 : 16;
            input.text = value;
            return input;
        }

        private void OnDestroy() //런타임 글꼴 해제
        {
            if (font != null)
                Destroy(font);
        }
    }
}
