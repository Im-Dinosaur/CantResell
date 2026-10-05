using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using Fusion;
using Fusion.Sockets;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace CantResell
{
    public sealed class AuctionNetworkComponent : MonoBehaviour, INetworkRunnerCallbacks
    {
        [SerializeField] private string lobbyScene = "StandBy"; //대기실 씬 이름
        [SerializeField] private string playScene = "Play"; //게임 씬 이름
        [SerializeField] private GameObject runnerPrefab; //음성 송수신 설정을 포함한 러너 프리팹
        [SerializeField] private NetworkObject playerPrefab; //실제 이동하는 Fusion 캐릭터 프리팹
        private readonly List<NetworkObject> spawnedPlayers = new List<NetworkObject>(); //호스트가 생성한 캐릭터
        private byte[] admissionToken; //방장이 입장 시 비교할 방별 인증 값
        private bool browsing; //방 목록 수신 중 여부
        public string roomTitle { get; private set; } = ""; //화면에 표시할 방 제목
        public string region => runner != null ? runner.LobbyInfo.Region : ""; //현재 목록의 Photon 지역
        public sealed class Room //비밀번호를 포함하지 않는 방 목록 정보
        {
            public string code; //자동 생성된 내부 방 식별자
            public string title; //사용자가 정한 방 제목
            public int players; //현재 참가 인원
            public int capacity; //최대 참가 인원
            public bool locked; //비밀번호 필요 여부
            public bool open; //신규 참가 허용 여부
        }
        private const int protocolKey = 1128353364; //다른 데이터 스트림과 구분할 식별자
        private AuctionGame game; //네트워크 요청을 조율할 진입점
        private NetworkRunner runner; //현재 접속을 담당할 Fusion 러너
        private readonly Dictionary<int, PlayerRef> peers = new Dictionary<int, PlayerRef>(); //연결된 플레이어 참조
        private int transferSequence; //전송 데이터의 고유 순번
        private bool closing; //의도적인 종료 여부
        public bool isConnecting { get; private set; } //접속 처리 중 여부
        public bool isConnected => runner != null && runner.IsRunning; //접속 완료 여부
        public bool isHost => isConnected && runner.IsServer; //호스트 권한 여부
        public int localId => isConnected ? runner.LocalPlayer.RawEncoded : 0; //로컬 플레이어 식별자
        public string roomName { get; private set; } = ""; //참가한 방 이름

        public void initialize(AuctionGame owner) //진입점 연결
        {
            game = owner;
        }

        public static bool isValidPassword(string password) //문자와 숫자로만 구성된 1~8자 비밀번호 검증
        {
            return !string.IsNullOrEmpty(password) && password.Length <= 8 && password.All(char.IsLetterOrDigit);
        }

        public static string cleanTitle(string value) //표시 제어 문자를 제거하고 제목 길이 제한
        {
            string title = new string((value ?? "").Where(character => !char.IsControl(character) && character != '<' && character != '>').ToArray()).Trim(); //정리한 제목
            return title.Substring(0, Math.Min(title.Length, 30));
        }

        public static byte[] createAdmissionToken(string code, string password) //방마다 다른 검증 값 생성
        {
            using SHA256 hash = SHA256.Create(); //평문 비밀번호 대신 전달할 해시
            byte[] digest = hash.ComputeHash(Encoding.UTF8.GetBytes(code + ":" + (password ?? ""))); //방 식별자를 포함한 검증 값
            return new byte[] { 4 }.Concat(digest).ToArray();
        }

        public static bool acceptsToken(byte[] expected, byte[] received) //누락과 변조된 입장 요청 차단
        {
            if (expected == null || received == null || expected.Length != 33 || received.Length != 33)
                return false;
            int difference = 0; //모든 바이트를 비교한 차이
            for (int index = 0; index < expected.Length; index++) //비교할 인증 바이트
                difference |= expected[index] ^ received[index];
            return difference == 0;
        }

        private void createRunner() //목록과 방 접속에 사용할 새 러너 구성
        {
            GameObject runnerObject = Instantiate(runnerPrefab); //음성 기본 녹음이 꺼진 프리팹 인스턴스
            runnerObject.name = "AuctionRunner";
            DontDestroyOnLoad(runnerObject);
            runner = runnerObject.GetComponent<NetworkRunner>();
            runner.ProvideInput = true;
            runner.AddCallbacks(this);
        }

        public async Task<string> refreshRooms() //Photon 로비에 접속하여 전체 방 목록 구독
        {
            if (isConnecting || isConnected || closing)
                return null;
            isConnecting = true;
            try
            {
                await disconnect();
                if (this == null)
                    return null;
                createRunner();
                browsing = true;
                StartGameResult result = await runner.JoinSessionLobby(SessionLobby.ClientServer); //호스트 모드 방 목록 연결 결과
                if (result.Ok)
                    return null;
                await disconnect();
                return "방 목록 연결 실패: " + result.ShutdownReason;
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
                await disconnect();
                return "방 목록을 불러오지 못했습니다. 새로고침으로 다시 시도해 주세요.";
            }
            finally { isConnecting = false; }
        }

        public async Task<string> connect(bool host, string room, string title, bool locked, string password) //방 생성 또는 선택한 방 참가
        {
            if (isConnecting || isConnected || closing)
                return "이미 접속을 처리하고 있습니다.";
            int sceneIndex = findScene(lobbyScene); //빌드 목록의 대기실 번호
            if (sceneIndex < 0)
                return "StandBy 씬을 빌드 목록에 등록해 주세요.";
            isConnecting = true;
            roomName = room;
            roomTitle = title;
            admissionToken = createAdmissionToken(room, locked ? password : "");
            peers.Clear();
            transferSequence = 0;
            try
            {
                if (runner == null)
                    createRunner();
                browsing = false;
                game.prepareVoice(runner);
                NetworkSceneManagerDefault sceneManager = runner.gameObject.AddComponent<NetworkSceneManagerDefault>(); //호스트 씬 전환 동기화
                NetworkSceneInfo sceneInfo = new NetworkSceneInfo(); //처음 입장할 네트워크 씬 정보
                sceneInfo.AddSceneRef(SceneRef.FromIndex(sceneIndex), LoadSceneMode.Single);
                StartGameResult result = await runner.StartGame(new StartGameArgs //접속 모드와 방 제한 설정
                {
                    GameMode = host ? GameMode.Host : GameMode.Client,
                    SessionName = room,
                    PlayerCount = 4,
                    IsVisible = true,
                    IsOpen = true,
                    SessionProperties = host ? new Dictionary<string, SessionProperty> { { "v", 5 }, { "t", title }, { "p", locked ? 1 : 0 } } : null,
                    ConnectionToken = admissionToken,
                    EnableClientSessionCreation = false,
                    Scene = sceneInfo,
                    SceneManager = sceneManager
                });
                if (!result.Ok)
                {
                    await disconnect();
                    return "입장하지 못했습니다: " + result.ShutdownReason + ". 비밀번호 또는 방의 인원·진행 상태를 확인해 주세요.";
                }
                foreach (PlayerRef player in runner.ActivePlayers) //시작 도중 도착한 참가자 콜백 보완
                    OnPlayerJoined(runner, player);
                return null;
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
                await disconnect();
                return "방 연결 중 오류가 발생했습니다. Photon 설정과 인터넷 연결을 확인해 주세요.";
            }
            finally
            {
                isConnecting = false;
            }
        }

        public async Task disconnect() //접속 종료와 러너 정리
        {
            if (closing)
                return;
            closing = true;
            browsing = false;
            if (game != null)
                game.stopVoice();
            NetworkRunner oldRunner = runner; //정리할 러너 참조
            try
            {
                if (oldRunner != null)
                {
                    oldRunner.RemoveCallbacks(this);
                    await oldRunner.Shutdown();
                }
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
            }
            finally
            {
                if (oldRunner != null)
                    Destroy(oldRunner.gameObject);
                runner = null;
                peers.Clear();
                spawnedPlayers.Clear();
                roomName = "";
                roomTitle = "";
                admissionToken = null;
                closing = false;
            }
        }

        public void sendCommand(AuctionState.Command command) //로컬 행동을 호스트로 전달
        {
            if (!isConnected)
                return;
            if (isHost)
                game.receiveCommand(localId, command);
            else
                runner.SendReliableDataToServer(nextKey(), Encoding.UTF8.GetBytes(JsonUtility.ToJson(command)));
        }

        public void sendState(int playerId, AuctionState state) //수신자 권한에 맞춘 상태를 개별 전송
        {
            if (!isHost || !peers.TryGetValue(playerId, out PlayerRef player)) //전송 대상 플레이어 참조
                return;
            if (playerId == localId)
                game.receiveState(state);
            else
                runner.SendReliableDataToPlayer(player, nextKey(), Encoding.UTF8.GetBytes(JsonUtility.ToJson(state)));
        }

        public void setRoomOpen(bool open) //플레이 도중 신규 참가 차단
        {
            if (isHost)
                runner.SessionInfo.IsOpen = open;
        }

        public void loadGameScene(bool play) //호스트가 전체 참가자의 씬 이동 시작
        {
            if (!isHost)
                return;
            int sceneIndex = findScene(play ? playScene : lobbyScene); //이동할 씬의 빌드 번호
            if (sceneIndex < 0)
            {
                game.abortMatch("필요한 씬이 빌드 목록에 없습니다.");
                return;
            }
            runner.LoadScene(SceneRef.FromIndex(sceneIndex), LoadSceneMode.Single);
        }

        public void spawnPlayers(AuctionState.Player[] participants) //게임 씬 준비 후 입력 권한을 가진 캐릭터 생성
        {
            if (!isHost || playerPrefab == null || spawnedPlayers.Count > 0)
                return;
            for (int slot = 0; slot < participants.Length; slot++) //각 참가자의 실제 캐릭터
            {
                if (participants[slot] == null || !peers.TryGetValue(participants[slot].id, out PlayerRef peer))
                    continue;
                int assignedSlot = slot; //생성 콜백에 전달할 좌석
                NetworkObject pawn = runner.Spawn(playerPrefab, new Vector3(slot * 2 - 3, 0, -3), Quaternion.identity, peer,
                    (source, instance) =>
                    {
                        Player player = instance.GetComponent<Player>(); //캐릭터 기능 진입점
                        player.slot = assignedSlot;
                        player.health = 100;
                    });
                runner.SetPlayerObject(peer, pawn);
                spawnedPlayers.Add(pawn);
            }
        }

        public void clearPlayers() //대기실 복귀 전에 네트워크 캐릭터 정리
        {
            if (isHost)
                foreach (NetworkObject pawn in spawnedPlayers) //게임에 생성한 캐릭터
                    if (pawn != null && pawn.IsValid)
                        runner.Despawn(pawn);
            spawnedPlayers.Clear();
        }

        private ReliableKey nextKey() //중복되지 않는 데이터 스트림 키 생성
        {
            return ReliableKey.FromInts(protocolKey, 3, unchecked(++transferSequence), 0);
        }

        private int findScene(string sceneName) //씬 이름으로 빌드 인덱스 확인
        {
            for (int index = 0; index < SceneManager.sceneCountInBuildSettings; index++) //확인할 빌드 인덱스
                if (System.IO.Path.GetFileNameWithoutExtension(SceneUtility.GetScenePathByBuildIndex(index)) == sceneName)
                    return index;
            return -1;
        }

        public void OnReliableDataReceived(NetworkRunner source, PlayerRef player, ReliableKey key, ReadOnlySpan<byte> data) //Fusion 2.1의 신뢰성 보장 메시지 수신
        {
            if (source != runner || !isConnected || data.Length == 0 || data.Length > 32768)
                return;
            key.GetInts(out int tag, out int version, out _, out _); //스트림의 프로토콜 식별 정보
            if (tag != protocolKey || version != 3)
                return;
            try
            {
                string json = Encoding.UTF8.GetString(data.ToArray()); //수신한 JSON 데이터
                if (isHost)
                {
                    if (data.Length <= 512 && peers.ContainsKey(player.RawEncoded))
                        game.receiveCommand(player.RawEncoded, JsonUtility.FromJson<AuctionState.Command>(json));
                }
                else
                {
                    //Host 모드에서는 서버만 SendReliableDataToPlayer를 사용한다.
                    game.receiveState(JsonUtility.FromJson<AuctionState>(json));
                }
            }
            catch (ArgumentException)
            {
                Debug.LogWarning("잘못된 경매 메시지를 무시했습니다.");
            }
        }

        public void OnPlayerJoined(NetworkRunner source, PlayerRef player) //접속 참가자를 진입점에 통지
        {
            if (source != runner)
                return;
            peers[player.RawEncoded] = player;
            if (isHost)
                game.playerJoined(player.RawEncoded);
        }

        public void OnPlayerLeft(NetworkRunner source, PlayerRef player) //이탈한 참가자를 진입점에 통지
        {
            if (source != runner)
                return;
            peers.Remove(player.RawEncoded);
            if (isHost)
                game.playerLeft(player.RawEncoded);
        }

        public void OnConnectRequest(NetworkRunner source, NetworkRunnerCallbackArgs.ConnectRequest request, byte[] token) //대기실에만 새 참가자 허용
        {
            if (source == runner && game.canAcceptPlayers() && acceptsToken(admissionToken, token))
                request.Accept();
            else
                request.Refuse();
        }

        public void OnShutdown(NetworkRunner source, ShutdownReason reason) //예기치 않은 접속 종료 통지
        {
            if (source == runner && !closing && !isConnecting)
            {
                if (browsing)
                    game.roomListUnavailable("목록 연결이 종료되었습니다. 새로고침해 주세요.");
                else
                    game.connectionLost("방 연결이 종료되었습니다: " + reason);
            }
        }

        public void OnDisconnectedFromServer(NetworkRunner source, NetDisconnectReason reason) //서버 연결 끊김 통지
        {
            if (source == runner && !closing && !isConnecting)
            {
                if (browsing)
                    game.roomListUnavailable("목록 연결이 끊어졌습니다. 새로고침해 주세요.");
                else
                    game.connectionLost("방장과의 연결이 끊어졌습니다.");
            }
        }

        public void OnHostMigration(NetworkRunner source, HostMigrationToken token) //시제품의 방장 이탈 처리
        {
            game.connectionLost("방장이 나가 방이 종료되었습니다.");
        }

        public void OnSceneLoadDone(NetworkRunner source) //씬 준비는 진입점에서 확인
        {
        }
        public void OnSceneLoadStart(NetworkRunner source) //로딩 단계는 호스트 상태로 표시
        {
        }
        public void OnConnectedToServer(NetworkRunner source) //접속 결과는 connect에서 처리
        {
        }
        public void OnConnectFailed(NetworkRunner source, NetAddress address, NetConnectFailedReason reason) //접속 실패 결과는 connect에서 처리
        {
        }
        public void OnInput(NetworkRunner source, NetworkInput input) //실제 로컬 입력을 Fusion 틱으로 전송
        {
            input.Set(game.readPlayerInput());
        }
        public void OnInputMissing(NetworkRunner source, PlayerRef player, NetworkInput input) //누락 입력은 캐릭터에서 정지 처리
        {
        }
#pragma warning disable CS0618 //Fusion에서 유지하는 미사용 인터페이스 멤버
        public void OnUserSimulationMessage(NetworkRunner source, SimulationMessagePtr message) //사용자 시뮬레이션 메시지 미사용
        {
        }
#pragma warning restore CS0618
        public void OnSessionListUpdated(NetworkRunner source, List<SessionInfo> sessions) //호환되는 방의 공개 정보만 목록에 전달
        {
            if (source != runner || !browsing)
                return;
            List<Room> rooms = new List<Room>(); //이번 목록의 표시 항목
            foreach (SessionInfo session in sessions) //Photon에서 전달한 전체 방 목록
            {
                if (!session.Properties.TryGetValue("v", out SessionProperty version) || !version.IsInt || (int)version != 5 ||
                    !session.Properties.TryGetValue("t", out SessionProperty title) || !title.IsString ||
                    !session.Properties.TryGetValue("p", out SessionProperty password) || !password.IsInt)
                    continue;
                rooms.Add(new Room { code = session.Name, title = cleanTitle((string)title), players = session.PlayerCount,
                    capacity = session.MaxPlayers, locked = (int)password != 0, open = session.IsOpen });
            }
            game.receiveRooms(rooms.OrderByDescending(room => room.open && room.players < room.capacity).ThenBy(room => room.title).ToArray());
        }
        public void OnCustomAuthenticationResponse(NetworkRunner source, Dictionary<string, object> data) //기본 Photon 인증 사용
        {
        }
        public void OnReliableDataProgress(NetworkRunner source, PlayerRef player, ReliableKey key, float progress) //작은 상태 메시지의 진행률 미표시
        {
        }
        public void OnObjectExitAOI(NetworkRunner source, NetworkObject networkObject, PlayerRef player) //관심 영역 미사용
        {
        }
        public void OnObjectEnterAOI(NetworkRunner source, NetworkObject networkObject, PlayerRef player) //관심 영역 미사용
        {
        }
    }
}
