using System;
using System.Linq;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace CantResell
{
    [DisallowMultipleComponent]
    public sealed class AuctionGame : MonoBehaviour
    {
        [SerializeField] private AuctionRoundComponent roundComponent; //게임 단계와 시간 담당
        [SerializeField] private AuctionItemComponent itemComponent; //상품 추첨 담당
        [SerializeField] private AuctionBidComponent bidComponent; //입찰 검증 담당
        [SerializeField] private AuctionEconomyComponent economyComponent; //자금과 정산 담당
        [SerializeField] private AuctionNetworkComponent networkComponent; //Fusion 연결과 메시지 담당
        [SerializeField] private AuctionUIComponent uiComponent; //메뉴와 게임 화면 담당
        [SerializeField] private AuctionItemViewComponent viewComponent; //테이블과 집 공간 담당
        [SerializeField] private AuctionVoiceComponent voiceComponent; //방 음성 및 마이크 담당
        [SerializeField] private AuctionSettingsComponent settingsComponent; //음량과 화면 설정 저장 담당
        [SerializeField] private AuctionAudioComponent audioComponent; //음악과 효과음 재생 담당
        [SerializeField] private AuctionInventoryComponent inventoryComponent; //상품 소유권과 비밀 담당
        [SerializeField] private AuctionObjectiveComponent objectiveComponent; //개인 목표와 점수 담당
        [SerializeField] private AuctionNightComponent nightComponent; //밤 행동 순서와 침입·방어 담당
        [SerializeField] private FurnitureStore storeComponent; //공동 가구점 게임 진입점
        private readonly Player[] pawns = new Player[4]; //실제 이동하는 네 캐릭터
        public static AuctionGame current => instance; //네트워크 캐릭터가 사용하는 진입점
        public AuctionState displayStateValue => localState; //로컬 화면과 캐릭터에 허용된 상태
        public AuctionItemComponent.Definition[] catalog => itemComponent.catalog; //공개 상품 도감
        public Player localPlayer => localState?.localSlot >= 0 ? pawns[localState.localSlot] : null; //본인의 이동 캐릭터
        public AuctionVoiceComponent voice => voiceComponent; //UI에 제공할 음성 상태
        public FurnitureStore store => storeComponent; //새 Play 화면과 게임 연결
        private AuctionState.Phase currentPhase => storeComponent != null && storeComponent.running ? storeComponent.rounds.phase : roundComponent.phase; //현재 활성 게임의 단계
        public float masterVolume => settingsComponent.masterVolume; //설정창의 전체 음량
        public float musicVolume => settingsComponent.musicVolume; //설정창의 음악 음량
        public float effectsVolume => settingsComponent.effectsVolume; //설정창의 효과음 음량
        public Vector2Int[] resolutions => settingsComponent.resolutions; //선택 가능한 화면 크기
        public Vector2Int selectedResolution => settingsComponent.selectedResolution; //적용한 화면 크기
        public bool fullscreen => settingsComponent.fullscreen; //전체 화면 설정
        public bool displayPending => settingsComponent.displayPending; //해상도 확인 대기 여부
        public int displaySeconds => settingsComponent.displaySeconds; //해상도 원복까지 남은 초
        public string displayMessage => settingsComponent.displayMessage; //화면 설정 결과 안내
        private static AuctionGame instance; //씬 사이에 유지할 진입점
        private readonly AuctionState.Player[] players = new AuctionState.Player[4]; //호스트의 참가자 정보
        private readonly bool[] loaded = new bool[4]; //참가자의 게임 씬 준비 여부
        private readonly long[] receivedCommands = new long[4]; //참가자별 마지막 처리 요청
        private readonly double[] commandTimes = new double[4]; //과도한 요청을 제한할 처리 시각
        private AuctionState localState; //로컬 플레이어에게 허용된 화면 상태
        private int match; //새 게임마다 증가하는 번호
        private long stateSequence; //호스트의 상태 순번
        private long commandSequence; //로컬 요청 순번
        private long lastStateSequence = -1; //역순 상태 수신 방지 번호
        private double nextSnapshotTime; //다음 시간 동기화 시각
        private double nextPresenceTime; //다음 프로필과 로딩 상태 확인 시각
        private string notice = "4명이 모여 준비하면 시작할 수 있습니다."; //공개 진행 안내
        private string nickname = "플레이어"; //로컬 플레이어 이름
        private int avatarColor; //로컬 캐릭터 색상
        private bool returningHome; //중복 종료 방지 상태

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void resetStaticState() //도메인 재로드를 끈 에디터에서도 싱글턴 초기화
        {
            instance = null;
        }

        private void Awake() //컴포넌트 연결과 씬 유지 설정
        {
            if (instance != null && instance != this)
            {
                Destroy(gameObject);
                return;
            }
            instance = this;
            if (roundComponent == null || itemComponent == null || bidComponent == null || economyComponent == null ||
                networkComponent == null || uiComponent == null || viewComponent == null || voiceComponent == null || settingsComponent == null || audioComponent == null || inventoryComponent == null || objectiveComponent == null || nightComponent == null)
            {
                Debug.LogError("AuctionGame의 Component 참조를 모두 연결해 주세요.", this);
                enabled = false;
                return;
            }
            DontDestroyOnLoad(gameObject);
            Application.runInBackground = true;
            roundComponent.resetLobby();
            economyComponent.resetMatch();
            settingsComponent.initialize(restoreDisplay: !Application.isBatchMode);
            audioComponent.applyVolumes(masterVolume, musicVolume, effectsVolume);
            audioComponent.initialize();
            nightComponent.initialize(inventoryComponent, viewComponent, pawns, publishState);
            if (storeComponent != null)
                storeComponent.initialize(pawns, publishState, slot => players[slot]?.name ?? "플레이어");
            networkComponent.initialize(this);
            uiComponent.initialize(this);
            SceneManager.sceneLoaded += onSceneLoaded;
        }

        private void Start() //현재 씬의 화면 구성
        {
            if (instance == this && enabled)
                showScene(SceneManager.GetActiveScene().name);
        }

        private void OnDestroy() //씬 이벤트 연결 해제
        {
            if (instance != this)
                return;
            SceneManager.sceneLoaded -= onSceneLoaded;
            stopVoice();
            _ = networkComponent.disconnect();
            instance = null;
        }

        private void onSceneLoaded(Scene scene, LoadSceneMode mode) //새 씬에 화면과 연출 연결
        {
            showScene(scene.name);
        }

        private void showScene(string sceneName) //각 씬의 표시 담당 구성 요소 호출
        {
            if (sceneName != "Home" && sceneName != "StandBy" && sceneName != "Play")
                return;
            uiComponent.showScene(sceneName);
            viewComponent.showScene(sceneName);
            if (sceneName != "StandBy")
                voiceComponent.endTest();
            if (sceneName == "Home" && !Application.isBatchMode && !returningHome)
                refreshRooms();
            if (localState != null)
                displayState();
            if (sceneName != "Home" && !networkComponent.isConnected && !networkComponent.isConnecting)
                uiComponent.showMessage("Home 씬에서 방을 만들거나 참가해 주세요.");
        }

        private void Update() //호스트 진행과 화면 동기화 조율
        {
            if (instance != this || returningHome || !networkComponent.isConnected)
                return;
            double now = Time.realtimeSinceStartupAsDouble; //시간 배율에 영향을 받지 않는 현재 시각
            if (networkComponent.isHost)
            {
                if (storeComponent != null && storeComponent.running)
                    storeComponent.tick(now, Time.deltaTime);
                else if (roundComponent.hasExpired(now))
                    advancePhase(now);
                if (now >= nextSnapshotTime)
                {
                    publishState();
                    nextSnapshotTime = now + (storeComponent != null && storeComponent.running ? 0.2 : 1);
                }
            }
            if (now >= nextPresenceTime)
            {
                nextPresenceTime = now + 1;
                if (localState == null || localState.localSlot < 0 || localState.players[localState.localSlot]?.name != nickname)
                    sendAction(AuctionState.Action.Profile, avatarColor);
                if (localState?.phase == AuctionState.Phase.Loading && SceneManager.GetActiveScene().name == "Play")
                    sendAction(AuctionState.Action.Loaded);
            }
        }

        public async void refreshRooms() //Home 방 목록 연결과 오류 표시 조율
        {
            if (returningHome || networkComponent.isConnecting || networkComponent.isConnected)
                return;
            uiComponent.setBusy(true);
            uiComponent.showRoomListStatus("방 목록에 연결하고 있습니다…");
            string error = await networkComponent.refreshRooms(); //목록 연결 결과
            if (this == null)
                return;
            uiComponent.setBusy(false);
            if (error != null)
                roomListUnavailable(error);
        }

        public void receiveRooms(AuctionNetworkComponent.Room[] rooms) //네트워크 목록을 UI로 전달
        {
            uiComponent.showRooms(rooms, networkComponent.region);
        }

        public void roomListUnavailable(string message) //실효된 목록을 지우고 재시도 안내
        {
            uiComponent.showRooms(Array.Empty<AuctionNetworkComponent.Room>(), "");
            uiComponent.showRoomListStatus(message);
        }

        public void createRoom(string playerName, string title, bool locked, string password) //제목과 공개 설정으로 새 방 요청
        {
            title = AuctionNetworkComponent.cleanTitle(title);
            if (title.Length == 0)
            {
                uiComponent.showMessage("방 제목을 입력해 주세요.");
                return;
            }
            connectRoom(true, playerName, new AuctionNetworkComponent.Room { code = Guid.NewGuid().ToString("N").ToUpperInvariant(), title = title, locked = locked }, password);
        }

        public void joinRoom(string playerName, AuctionNetworkComponent.Room room, string password) //선택한 방으로 참가 요청
        {
            if (room == null || !room.open || room.players >= room.capacity)
            {
                uiComponent.showMessage("참가할 수 없는 방입니다. 목록을 새로고침해 주세요.");
                return;
            }
            connectRoom(false, playerName, room, password);
        }

        private async void connectRoom(bool host, string playerName, AuctionNetworkComponent.Room room, string password) //방 생성과 참가의 공통 흐름
        {
            if (networkComponent.isConnecting || networkComponent.isConnected || returningHome)
                return;
            nickname = cleanName(playerName);
            if (room.locked && !AuctionNetworkComponent.isValidPassword(password))
            {
                uiComponent.showMessage("비밀번호는 문자·숫자로 1~8자를 입력해 주세요.");
                return;
            }
            Array.Clear(players, 0, players.Length);
            Array.Clear(receivedCommands, 0, receivedCommands.Length);
            Array.Clear(commandTimes, 0, commandTimes.Length);
            localState = null;
            lastStateSequence = -1;
            stateSequence = 0;
            commandSequence = 0;
            roundComponent.resetLobby();
            economyComponent.resetMatch();
            notice = "4명이 모여 준비하면 시작할 수 있습니다.";
            storeComponent?.resetMatch();
            uiComponent.setBusy(true);
            uiComponent.showMessage(host ? "방을 만들고 있습니다…" : "방에 참가하고 있습니다…");
            string error = await networkComponent.connect(host, room.code, room.title, room.locked, password); //Photon 접속 결과
            if (this == null)
                return;
            uiComponent.setBusy(false);
            if (error != null)
            {
                connectionLost(error);
                return;
            }
            sendAction(AuctionState.Action.Profile, avatarColor);
            if (networkComponent.isHost)
                publishState();
        }

        public void prepareVoice(Fusion.NetworkRunner runner) //입장할 방의 음성 구성 연결
        {
            voiceComponent.attach(runner);
        }

        public void setVolume(AuctionSettingsComponent.VolumeChannel channel, float value) //음량 변경을 저장과 실제 재생에 함께 반영
        {
            settingsComponent.setVolume(channel, value);
            audioComponent.applyVolumes(masterVolume, musicVolume, effectsVolume);
        }

        public void applyDisplay(Vector2Int resolution, bool useFullscreen) //사용자가 선택한 화면 설정 미리보기
        {
            settingsComponent.previewDisplay(resolution, useFullscreen);
        }

        public void confirmDisplay() //화면 설정 유지 요청
        {
            settingsComponent.confirmDisplay();
        }

        public void cancelDisplay() //화면 설정 원복 요청
        {
            settingsComponent.cancelDisplay();
        }

        public void saveSettings() //설정창을 닫을 때 변경한 음량 저장
        {
            settingsComponent.save();
        }

        public float getPlayerVoiceVolume(int playerId) //특정 참가자의 수신 음량 조회
        {
            return voiceComponent.getPlayerVolume(playerId);
        }

        public void setPlayerVoiceVolume(int playerId, float value) //특정 참가자의 수신 음량 변경
        {
            voiceComponent.setPlayerVolume(playerId, value);
        }

        public string getPlayerVoiceStatus(int playerId) //특정 참가자의 수신 연결 상태 조회
        {
            return voiceComponent.getPlayerStatus(playerId);
        }

        public void playMusic(AudioClip clip) //외부 요청의 음악 재생 위임
        {
            audioComponent.playMusic(clip);
        }

        public void playEffect(AudioClip clip) //외부 요청의 효과음 재생 위임
        {
            audioComponent.playEffect(clip);
        }

        public void stopVoice() //퇴장 시 마이크와 음성 연결 정리
        {
            if (voiceComponent != null)
                voiceComponent.detach();
        }

        public void toggleMicrophone() //마이크 송신 전환 요청
        {
            string error = voiceComponent.toggleMicrophone(); //장치 준비 실패 안내
            if (error != null)
                uiComponent.showMessage(error);
        }

        public void toggleMicrophoneTest() //대기실에서 송신 없이 입력 레벨 검사
        {
            string error = voiceComponent.toggleTest(); //장치 준비 실패 안내
            if (error != null)
                uiComponent.showMessage(error);
        }

        public void requestReady() //준비 상태 변경 요청
        {
            if (localState?.localSlot >= 0)
                sendAction(AuctionState.Action.Ready, 0, !localState.players[localState.localSlot].ready);
        }

        public void selectColor(int color) //캐릭터 색상 변경 요청
        {
            avatarColor = Mathf.Clamp(color, 0, 3);
            sendAction(AuctionState.Action.Profile, avatarColor);
        }

        public void requestStart() //방장의 게임 시작 요청
        {
            sendAction(AuctionState.Action.Start);
        }

        public void requestEndNightTurn() //본인의 밤 행동 종료 요청
        {
            sendAction(AuctionState.Action.EndNightTurn);
        }

        public void requestBid(int amount) //원하는 금액의 입찰 요청
        {
            sendAction(AuctionState.Action.Bid, amount);
        }

        public void requestStoreGamble(int amount) //공금 도박 요청
        {
            sendAction(AuctionState.Action.Gamble, amount);
        }

        public void requestStorePlay() //무료 놀이 요청
        {
            sendAction(AuctionState.Action.Play);
        }

        public Player getPlayer(int slot) //공동 월드에서 동료 캐릭터 조회
        {
            return slot >= 0 && slot < pawns.Length ? pawns[slot] : null;
        }

        public void requestLobby() //게임 종료 후 방장의 대기실 복귀 요청
        {
            sendAction(AuctionState.Action.ReturnToLobby);
        }

        private void sendAction(AuctionState.Action action, int value = 0, bool ready = false) //UI 행동을 네트워크 요청으로 변환
        {
            networkComponent.sendCommand(new AuctionState.Command
            {
                sequence = ++commandSequence, action = action, value = value, ready = ready,
                name = nickname, match = localState?.match ?? match, round = localState?.round ?? 0, nightTurn = localState?.nightTurn ?? -1,
                phase = localState?.phase ?? currentPhase
            });
        }

        public bool canAcceptPlayers() //현재 방의 참가 허용 여부 반환
        {
            return roundComponent.phase == AuctionState.Phase.Lobby && players.Any(player => player == null);
        }

        public void playerJoined(int playerId) //호스트가 빈 좌석에 참가자 등록
        {
            if (!networkComponent.isHost || findSlot(playerId) >= 0 || !canAcceptPlayers())
                return;
            int slot = Array.FindIndex(players, player => player == null); //새 참가자의 좌석
            players[slot] = new AuctionState.Player { id = playerId, name = "플레이어 " + (slot + 1), color = slot };
            receivedCommands[slot] = 0;
            commandTimes[slot] = 0;
            publishState();
        }

        public void playerLeft(int playerId) //대기실 이탈 또는 진행 중 게임 중단
        {
            int slot = findSlot(playerId); //이탈한 참가자의 좌석
            if (slot < 0)
                return;
            players[slot] = null;
            if (roundComponent.phase != AuctionState.Phase.Lobby)
                abortMatch("참가자가 나가 이번 게임을 중단했습니다. 대기실에서 다시 모여 주세요.");
            else
                publishState();
        }

        public void receiveCommand(int playerId, AuctionState.Command command) //실제 송신자와 단계에 따라 요청 검증 및 위임
        {
            int slot = findSlot(playerId); //네트워크 송신자로 확인한 좌석
            double now = Time.realtimeSinceStartupAsDouble; //요청을 받은 호스트 시각
            if (!networkComponent.isHost || command == null || command.version != 3 || slot < 0 ||
                command.sequence <= receivedCommands[slot] || now - commandTimes[slot] < 0.075)
                return;
            receivedCommands[slot] = command.sequence;
            commandTimes[slot] = now;
            if (storeComponent != null && storeComponent.running)
                storeComponent.tick(now, 0);
            else if (roundComponent.hasExpired(now))
                advancePhase(now);
            bool lobby = currentPhase == AuctionState.Phase.Lobby; //대기실 단계 여부
            int activeRound = storeComponent != null && storeComponent.running ? storeComponent.rounds.day - 1 : roundComponent.round; //활성 게임의 날짜 또는 판매 순번
            bool sameRound = command.match == match && command.round == activeRound; //지연 요청의 게임과 라운드 검증
            switch (command.action)
            {
                case AuctionState.Action.Profile when lobby:
                    players[slot].name = cleanName(command.name);
                    players[slot].color = Mathf.Clamp(command.value, 0, 3);
                    break;
                case AuctionState.Action.Ready when lobby:
                    players[slot].ready = command.ready;
                    break;
                case AuctionState.Action.Start when lobby && playerId == networkComponent.localId:
                    if (players.Any(player => player == null || !player.ready))
                    {
                        reject(slot, "4명 모두 준비해야 시작할 수 있습니다.");
                        return;
                    }
                    match++;
                    Array.Clear(loaded, 0, loaded.Length);
                    itemComponent.resetMatch();
                    inventoryComponent.resetMatch();
                    objectiveComponent.resetMatch();
                    nightComponent.resetMatch();
                    economyComponent.resetMatch();
                    bidComponent.resetBids();
                    storeComponent?.resetMatch();
                    roundComponent.enterPhase(AuctionState.Phase.Loading, now);
                    notice = "모든 참가자가 게임 씬에 입장하기를 기다립니다.";
                    networkComponent.setRoomOpen(false);
                    publishState();
                    networkComponent.loadGameScene(true);
                    return;
                case AuctionState.Action.Loaded when sameRound && roundComponent.phase == AuctionState.Phase.Loading:
                    loaded[slot] = true;
                    if (loaded.All(value => value))
                    {
                        networkComponent.spawnPlayers(players);
                        if (storeComponent != null)
                        {
                            storeComponent.beginMatch(now);
                            roundComponent.enterPhase(AuctionState.Phase.Day, now);
                            notice = "공동 가구점 영업 시작! 모두 같은 공금을 사용합니다.";
                        }
                        else
                            beginRound(0, now);
                    }
                    break;


                case AuctionState.Action.Bid when sameRound && roundComponent.phase == AuctionState.Phase.Bidding:
                    if (!bidComponent.tryBid(slot, roundComponent.sellerSlot, economyComponent.getBalance(slot), command.value))
                    {
                        reject(slot, "입찰 금액, 잔액 또는 판매자 여부를 확인해 주세요.");
                        return;
                    }
                    break;
                case AuctionState.Action.EndNightTurn when sameRound && roundComponent.phase == AuctionState.Phase.Night &&
                    slot == nightComponent.activeSlot && command.nightTurn == nightComponent.turn:
                    finishNightTurn(now);
                    return;
                case AuctionState.Action.Gamble:
                case AuctionState.Action.Play:
                    if (storeComponent == null || !sameRound || command.phase != currentPhase ||
                        !storeComponent.command(slot, command.action, command.value, now, out string activityMessage))
                    {
                        reject(slot, "낮에 해당 기계 가까이에서 공금과 금액을 확인하세요.");
                        return;
                    }
                    notice = activityMessage;
                    break;
                case AuctionState.Action.ReturnToLobby when playerId == networkComponent.localId &&
                    (currentPhase == AuctionState.Phase.Results || currentPhase == AuctionState.Phase.Aborted):
                    networkComponent.clearPlayers();
                    inventoryComponent.resetMatch();
                    nightComponent.resetMatch();
                    roundComponent.resetLobby();
                    economyComponent.resetMatch();
                    bidComponent.resetBids();
                    storeComponent?.resetMatch();
                    foreach (AuctionState.Player player in players) //남은 참가자의 준비 상태 초기화
                        if (player != null)
                            player.ready = false;
                    notice = "4명이 모여 준비하면 시작할 수 있습니다.";
                    networkComponent.setRoomOpen(true);
                    publishState();
                    networkComponent.loadGameScene(false);
                    return;
                default:
                    reject(slot, "현재 단계에서는 사용할 수 없는 행동입니다.");
                    return;
            }
            publishState();
        }

        private void beginRound(int index, double now) //판매 턴마다 상품 하나를 발급하고 설명 시작
        {
            roundComponent.beginRound(index, now);
            inventoryComponent.addStock(itemComponent.createItem(index + 1, roundComponent.sellerSlot));
            bidComponent.resetBids();
            for (int slot = 0; slot < pawns.Length; slot++) //낮의 경매 테이블 좌석
                if (pawns[slot] != null)
                    pawns[slot].resetPlayer(viewComponent.getSeatPosition(slot));
            notice = "판매자가 상품을 소개합니다. 원가와 상태는 판매자만 알고 있습니다.";
        }

        private void advancePhase(double now) //낮 경매와 밤 턴의 전환 조율
        {
            switch (roundComponent.phase)
            {
                case AuctionState.Phase.Loading:
                    abortMatch("입장이 지연되어 게임을 시작하지 못했습니다.");
                    return;
                case AuctionState.Phase.Pitch:
                    roundComponent.enterPhase(AuctionState.Phase.Bidding, now);
                    notice = "목표에 필요한 물건에 입찰하세요. 낙찰자에게만 원가와 상태를 공개합니다.";
                    break;
                case AuctionState.Phase.Bidding:
                    AuctionState.Item lot = inventoryComponent.getItem(roundComponent.round + 1); //정산할 판매품
                    if (lot == null || lot.status != AuctionState.ItemStatus.Stock ||
                        !economyComponent.canSettle(roundComponent.round, roundComponent.sellerSlot, bidComponent.bidderSlot, bidComponent.highestBid))
                    {
                        abortMatch("정산 상태를 확인하지 못해 게임을 중단했습니다.");
                        return;
                    }
                    inventoryComponent.settleLot(lot.id, bidComponent.bidderSlot);
                    economyComponent.settleRound(roundComponent.round, roundComponent.sellerSlot, bidComponent.bidderSlot, bidComponent.highestBid);
                    if (roundComponent.sellerSlot < 3)
                        beginRound(roundComponent.round + 1, now);
                    else
                        beginNight(now);
                    break;
                case AuctionState.Phase.Night:
                    finishNightTurn(now);
                    return;
            }
            publishState();
        }

        private void beginNight(double now) //네 판매가 끝나면 집과 무작위 행동 순서 준비
        {
            nightComponent.beginNight();
            beginNightTurn(now);
        }

        private void beginNightTurn(double now) //방어자는 집 안에서 움직이고 침입자만 외출 허용
        {
            for (int slot = 0; slot < 4; slot++) //모든 캐릭터의 안전한 집 위치
                if (pawns[slot] != null)
                    pawns[slot].resetPlayer(viewComponent.getHouse(slot).spawnPosition);
            roundComponent.enterPhase(AuctionState.Phase.Night, now);
            notice = "밤에는 모두 같은 실루엣입니다. 침입자는 물건 하나를 자기 집까지 운반하세요.";
        }

        private void finishNightTurn(double now) //운반 실패를 복구한 뒤 다음 행동 또는 회차 시작
        {
            int active = nightComponent.activeSlot; //종료할 침입자
            inventoryComponent.finishCarry(active, false);
            if (active >= 0 && pawns[active] != null)
                pawns[active].carriedId = 0;
            if (nightComponent.nextTurn())
                beginNightTurn(now);
            else if (roundComponent.round + 1 >= roundComponent.cycles * 4)
            {
                roundComponent.enterPhase(AuctionState.Phase.Results, now);
                notice = "집들이 경매 종료! 개인 목표 달성 점수가 가장 높은 사람이 승리합니다. 동점은 공동 우승입니다.";
            }
            else
                beginRound(roundComponent.round + 1, now);
            publishState();
        }

        public void abortMatch(string reason) //중단 시 운반품 복구와 입력 차단
        {
            if (!networkComponent.isHost)
                return;
            storeComponent?.abort();
            for (int slot = 0; slot < 4; slot++) //운반 중인 상품 복구
            {
                inventoryComponent.finishCarry(slot, false);
                if (pawns[slot] != null)
                    pawns[slot].carriedId = 0;
            }
            roundComponent.enterPhase(AuctionState.Phase.Aborted, 0);
            notice = reason;
            publishState();
        }

        public void registerPlayer(Player player) //Fusion이 생성한 캐릭터 등록
        {
            if (player.slot >= 0 && player.slot < 4)
                pawns[player.slot] = player;
        }

        public void unregisterPlayer(Player player) //소멸한 캐릭터의 참조 해제
        {
            if (player.slot >= 0 && player.slot < 4 && pawns[player.slot] == player)
                pawns[player.slot] = null;
        }

        public PlayerInput readPlayerInput() //설정창과 입력창에서는 월드 입력 정지
        {
            bool moving = localState?.phase == AuctionState.Phase.Night || (storeComponent != null && localState?.phase == AuctionState.Phase.Day); //월드 활동 가능 여부
            return localPlayer != null ? localPlayer.readInput(!moving || uiComponent.worldInputBlocked || (storeComponent != null && storeComponent.inputBlocked)) : default;
        }

        public void simulatePlayer(Player player, Vector2 direction, bool interact, bool lockDoor, bool attack) //호스트가 이동과 밤 행동을 순서대로 위임
        {
            if (!networkComponent.isHost || player.health <= 0)
                return;
            int slot = player.slot; //권한으로 등록한 캐릭터 좌석
            if (slot < 0 || slot >= 4 || pawns[slot] != player)
                return;
            double now = Time.realtimeSinceStartupAsDouble; //판정할 호스트 시각
            if (storeComponent != null)
            {
                storeComponent.simulatePlayer(player, direction, interact, lockDoor, now);
                return;
            }
            if (roundComponent.phase != AuctionState.Phase.Night)
                return;
            if (nightComponent.simulatePlayer(player, direction, interact, lockDoor, attack, now))
                finishNightTurn(now);
        }

        private int findSlot(int playerId) //실제 접속 식별자로 좌석 찾기
        {
            return Array.FindIndex(players, player => player != null && player.id == playerId);
        }

        private void publishState() //개별 권한을 적용한 상태를 참가자마다 전송
        {
            stateSequence++;
            for (int slot = 0; slot < players.Length; slot++) //전송 대상 좌석
                if (players[slot] != null)
                    networkComponent.sendState(players[slot].id, createState(slot, notice));
        }

        private void reject(int slot, string reason) //거절 이유를 요청자에게만 통지
        {
            stateSequence++;
            networkComponent.sendState(players[slot].id, createState(slot, reason));
        }

        private AuctionState createState(int slot, string message) //개인 목표와 상품 비밀을 수신자별로 가린 상태
        {
            if (storeComponent != null)
                return createStoreState(slot, message);
            bool night = roundComponent.phase == AuctionState.Phase.Night; //닉네임과 스킨을 숨길 단계
            bool result = roundComponent.phase == AuctionState.Phase.Results; //목표와 순위를 공개할 단계
            bool playing = roundComponent.phase != AuctionState.Phase.Lobby && roundComponent.phase != AuctionState.Phase.Loading; //목표가 배정된 게임
            AuctionState.Item[] items = inventoryComponent.createSnapshot(slot); //권한을 적용한 상품 복사본
            AuctionState state = new AuctionState
            {
                sequence = stateSequence, match = match, phase = roundComponent.phase, round = roundComponent.round, cycles = roundComponent.cycles,
                localSlot = slot, hostSlot = findSlot(networkComponent.localId), sellerSlot = roundComponent.sellerSlot,
                bidderSlot = bidComponent.bidderSlot, highestBid = bidComponent.highestBid, minimumRaise = bidComponent.raiseAmount,
                secondsRemaining = roundComponent.getRemaining(Time.realtimeSinceStartupAsDouble), phaseDuration = roundComponent.duration,
                lotId = roundComponent.round + 1, nightTurn = night ? nightComponent.turn : -1,
                activeIntruder = night ? nightComponent.activeSlot : -1, nightOrder = (int[])nightComponent.order.Clone(),
                goalTitle = playing ? objectiveComponent.getGoal(slot).title : "", goalDescription = playing ? objectiveComponent.describeGoal(slot) : "",
                goalScore = playing ? objectiveComponent.calculateScore(slot, items.Where(item => item.known)) : 0,
                unknownItems = items.Count(item => item.owner == slot && item.status == AuctionState.ItemStatus.Stored && !item.known),
                notice = message, items = items
            };
            for (int index = 0; index < players.Length; index++) //공개 참가자 복사
                if (players[index] != null)
                    state.players[index] = new AuctionState.Player
                    {
                        id = players[index].id, name = night ? (index == slot ? "나" : "익명의 이웃") : players[index].name,
                        color = night ? 0 : players[index].color, ready = players[index].ready, cash = economyComponent.getBalance(index),
                        score = result ? objectiveComponent.calculateScore(index, inventoryComponent.allItems) : -1,
                        objective = result ? objectiveComponent.getGoal(index).title : ""
                    };
            nightComponent.copyDoorsTo(state);
            return state;
        }

        public void receiveState(AuctionState state) //이전 순번을 버리고 로컬 화면 갱신
        {
            if (state == null || state.version != 3 || state.sequence <= lastStateSequence || state.players == null ||
                state.items == null || state.items.Length > 32 || state.doorOpen?.Length != 4 || state.doorStrength?.Length != 4 || state.players.Length != 4 || state.localSlot < 0 || state.localSlot >= 4)
                return;
            if (state.store != null && (state.store.furniture == null || state.store.furniture.Length > 128 ||
                state.store.escaped?.Length != 4 || state.store.playScores?.Length != 4 || state.store.ledger == null ||
                state.store.furniture.Any(item => item == null || item.kind < 0 || item.kind >= FurnitureInventoryComponent.names.Length || item.carrier < -1 || item.carrier > 3)))
                return;
            for (int slot = 0; slot < state.players.Length; slot++) //Unity JSON의 빈 클래스 표현을 빈 좌석으로 복원
                if (state.players[slot] != null && state.players[slot].id == 0)
                    state.players[slot] = null;
            if (state.players[state.localSlot] == null)
                return;
            lastStateSequence = state.sequence;
            localState = state;
            displayState();
        }

        private void displayState() //UI와 상품 연출에 수신자용 상태 전달
        {
            voiceComponent.updateParticipants(localState);
            uiComponent.showState(localState, networkComponent.roomTitle + "  ·  " + networkComponent.roomName);
            viewComponent.showState(localState);
            if (localState.store != null)
                storeComponent?.showState(localState);
        }

        private AuctionState createStoreState(int slot, string message) //비공개 목표 없는 공동 가구점 상태 전송
        {
            double now = Time.realtimeSinceStartupAsDouble; //스냅샷의 호스트 시각
            bool playing = storeComponent.running; //공동 게임 진행 여부
            AuctionState state = new AuctionState
            {
                sequence = stateSequence, match = match, phase = currentPhase, localSlot = slot, hostSlot = findSlot(networkComponent.localId),
                round = playing ? storeComponent.rounds.day - 1 : 0, cycles = storeComponent.rounds.days,
                secondsRemaining = playing ? storeComponent.rounds.remaining(now) : roundComponent.getRemaining(now),
                phaseDuration = playing ? storeComponent.rounds.duration : roundComponent.duration,
                notice = message, store = storeComponent.snapshot(now)
            };
            for (int index = 0; index < players.Length; index++) //공동 협력 중 동료 식별 정보
                if (players[index] != null)
                    state.players[index] = new AuctionState.Player { id = players[index].id, name = players[index].name, color = players[index].color, ready = players[index].ready };
            return state;
        }

        public void leaveRoom() //사용자의 방 나가기 요청
        {
            connectionLost("방에서 나왔습니다.");
        }

        public async void connectionLost(string message) //접속을 정리하고 Home으로 복귀
        {
            if (returningHome || this == null)
                return;
            returningHome = true;
            try
            {
                await networkComponent.disconnect();
                if (this == null)
                    return;
                localState = null;
                lastStateSequence = -1;
                roundComponent.resetLobby();
                storeComponent?.resetMatch();
                AsyncOperation load = SceneManager.LoadSceneAsync("Home"); //Home 복귀 작업
                while (load != null && !load.isDone)
                    await System.Threading.Tasks.Task.Yield();
                if (this != null)
                {
                    uiComponent.setBusy(false);
                    uiComponent.showMessage(message);
                }
            }
            finally
            {
                returningHome = false;
                if (this != null && !Application.isBatchMode)
                    refreshRooms();
            }
        }

        private string cleanName(string value) //빈 이름과 표시 제어 문자를 정리
        {
            string result = new string((value ?? "").Where(character => !char.IsControl(character) && character != '<' && character != '>').ToArray()).Trim(); //정리한 표시 이름
            return result.Length == 0 ? "플레이어" : result.Substring(0, Math.Min(12, result.Length));
        }
    }
}
