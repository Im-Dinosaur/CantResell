#if CANTRESELL_CONNECTION_SMOKE
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using Fusion;
using UnityEngine;

namespace CantResell
{
    public sealed class AuctionGameplaySmokeComponent : MonoBehaviour
    {
        private AuctionGame game; //실제 게임 진입점
        private string room; //검증 방 이름
        private string directory; //검증 기록 폴더
        private int peer; //실행 프로세스 번호
        private int observedTurn = -1; //마지막 밤 행동 순번
        private int routeStep; //물리 이동 경로 진행
        private int targetOwner; //이번 검증의 상대 집
        private int targetItem; //운반할 상품
        private int previousHealth = 100; //실제 피해 확인 기준
        private bool scheduled; //호스트의 검증 순서 설정 여부
        private bool sentPass; //마지막 턴 종료 요청 여부
        private float nextTrace; //실제 캐릭터 이동 기록 시각
        private readonly List<Vector3> route = new List<Vector3>(); //문을 통과하는 이동 경로
        private readonly HashSet<string> stages = new HashSet<string>(); //중복 없는 성공 기록

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void startSmoke() //별도 검증 빌드와 명령행에서만 자동 입력 실행
        {
            if (!Environment.GetCommandLineArgs().Contains("-gameplayPeer"))
                return;
            GameObject root = new GameObject("GameplaySmoke"); //검증 실행기
            DontDestroyOnLoad(root);
            root.AddComponent<AuctionGameplaySmokeComponent>();
        }

        private async void Start() //실제 Photon 네 프로세스 게임을 끝까지 검증
        {
            try
            {
                peer = int.Parse(argument("-gameplayPeer"));
                room = argument("-gameplayRoom");
                directory = argument("-gameplayDirectory");
                Directory.CreateDirectory(directory);
                await waitFor(() => AuctionGame.current != null, "GAME");
                game = AuctionGame.current;
                game.GetComponent<AuctionSettingsComponent>().initialize("CantResell.Test.Gameplay." + peer + ".", false);
                AudioListener.volume = 0;
                if (peer == 0)
                {
                    setField(game.GetComponent<AuctionRoundComponent>(), "cyclesPerMatch", 1);
                    setField(game.GetComponent<AuctionRoundComponent>(), "pitchSeconds", 2f);
                    setField(game.GetComponent<AuctionRoundComponent>(), "biddingSeconds", 3f);
                    setField(game.GetComponent<AuctionRoundComponent>(), "nightSeconds", 40f);
                    setField(game.GetComponent<AuctionItemComponent>(), "normalChance", 1f);
                    game.createRoom("SmokeAlias0", room, false, "");
                }
                else
                {
                    await waitFor(() => File.Exists(Path.Combine(directory, "0_ROOM.txt")), "ROOM");
                    if (peer > 1)
                        await waitFor(() => File.Exists(Path.Combine(directory, (peer - 1) + "_JOINED.txt")), "PREVIOUS_JOIN");
                    string code = File.ReadAllText(Path.Combine(directory, "RoomCode.txt")); //실제 자동 생성된 내부 방 코드
                    game.joinRoom("SmokeAlias" + peer, new AuctionNetworkComponent.Room { code = code, title = room, open = true, capacity = 4 }, "");
                }
                await waitFor(() => game.displayStateValue?.phase == AuctionState.Phase.Lobby, "LOBBY");
                if (game.displayStateValue.localSlot != peer)
                    throw new Exception("Unexpected input authority seat.");
                if (peer == 0)
                    File.WriteAllText(Path.Combine(directory, "RoomCode.txt"), game.GetComponent<AuctionNetworkComponent>().roomName);
                mark(peer == 0 ? "ROOM" : "JOINED");
                await Task.Delay(400);
                game.requestReady();
                if (peer == 0)
                {
                    await waitFor(() => game.displayStateValue.players.All(player => player != null && player.ready), "READY");
                    await Task.Delay(200);
                    game.requestStart();
                    Queue<AuctionState.ItemKind> bag = (Queue<AuctionState.ItemKind>)typeof(AuctionItemComponent).GetField("bag", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(game.GetComponent<AuctionItemComponent>()); //재현 가능한 판매 순서
                    bag.Enqueue(AuctionState.ItemKind.CoffeeMachine);
                    bag.Enqueue(AuctionState.ItemKind.Lock);
                    bag.Enqueue(AuctionState.ItemKind.Crowbar);
                    bag.Enqueue(AuctionState.ItemKind.Hammer);
                }
                PlayerInputComponent.smokeInput = readInput;
                await waitFor(() => game.displayStateValue?.phase == AuctionState.Phase.Results, "RESULTS", 240);
                AuctionState result = game.displayStateValue; //결과의 개인 상태
                if (result.players.Sum(player => player.cash) != 1200 || result.players.Any(player => player.score < 0 || string.IsNullOrEmpty(player.objective)))
                    throw new Exception("Money conservation or objective publication failed.");
                if (result.items.Single(item => item.id == 2).owner != 0 || result.items.Single(item => item.id == 1).owner != 1 ||
                    result.items.Single(item => item.id == 3).owner != 0)
                    throw new Exception("Successful theft or knockout/timeout rollback failed.");
                mark("RESULTS");
                if (peer == 0)
                {
                    await waitFor(() => Enumerable.Range(1, 3).All(index => File.Exists(Path.Combine(directory, index + "_RESULTS.txt"))), "PEER_RESULTS");
                    if (!File.Exists(Path.Combine(directory, "3_DAMAGE.txt")) || !File.Exists(Path.Combine(directory, "2_CARRIED_TURN_2.txt")))
                        throw new Exception("Defense damage or carried-item timeout was not exercised.");
                    await Task.Delay(300);
                    game.requestLobby();
                }
                await waitFor(() => game.displayStateValue?.phase == AuctionState.Phase.Lobby, "RETURN");
                mark("RETURN");
                if (peer == 0)
                {
                    await waitFor(() => Enumerable.Range(1, 3).All(index => File.Exists(Path.Combine(directory, index + "_RETURN.txt"))), "PEER_RETURN");
                    game.leaveRoom();
                }
                await waitFor(() => !game.GetComponent<AuctionNetworkComponent>().isConnected, "DISCONNECT");
                mark("COMPLETE");
                Application.Quit(0);
            }
            catch (Exception exception)
            {
                if (!string.IsNullOrEmpty(directory))
                    File.WriteAllText(Path.Combine(directory, peer + "_FAIL.txt"), exception.ToString());
                Debug.LogException(exception);
                Application.Quit(1);
            }
        }

        private void Update() //실제 UI 요청과 호스트 상태 확인
        {
            if (game == null)
                return;
            AuctionState state = game.displayStateValue; //수신한 권한별 상태
            if (state == null)
                return;
            if (state.phase == AuctionState.Phase.Pitch || state.phase == AuctionState.Phase.Bidding)
            {
                int[] buyers = { 1, 2, 0, 1 }; //자신에게 판매하지 않는 낙찰 좌석
                AuctionState.Item lot = state.items.Single(item => item.id == state.lotId); //현재 판매품
                if (lot.known != (state.localSlot == state.sellerSlot))
                    fail("Private lot information leaked before settlement.");
                if (state.phase == AuctionState.Phase.Bidding && buyers[state.round] == peer && state.bidderSlot != peer)
                    game.requestBid(10);
            }
            if (state.phase != AuctionState.Phase.Night)
                return;
            if (Time.realtimeSinceStartup >= nextTrace && game.localPlayer != null)
            {
                nextTrace = Time.realtimeSinceStartup + 1;
                GameObject floor = GameObject.Find("Floor"); //런타임 바닥 충돌의 실제 존재 확인
                File.AppendAllText(Path.Combine(directory, peer + "_Movement.txt"),
                    "turn=" + state.nightTurn + " active=" + state.activeIntruder + " step=" + routeStep +
                    " position=" + game.localPlayer.transform.position + " carried=" + game.localPlayer.carriedId +
                    " health=" + game.localPlayer.health + " floor=" + (floor?.GetComponent<BoxCollider>() != null) +
                    " doors=" + string.Join(",", state.doorOpen) + "\n");
            }
            if (peer == 0 && !scheduled)
            {
                scheduled = true;
                AuctionNightComponent night = game.GetComponent<AuctionNightComponent>(); //검증할 고정 순서
                typeof(AuctionNightComponent).GetProperty("order").SetValue(night, new[] { 0, 3, 2, 1 });
                typeof(AuctionNightComponent).GetProperty("turn").SetValue(night, 0);
                typeof(AuctionGame).GetMethod("beginNightTurn", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(game, new object[] { Time.realtimeSinceStartupAsDouble });
                typeof(AuctionGame).GetMethod("publishState", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(game, null);
                state = game.displayStateValue;
            }
            if (state.players.Any(player => player.name.Contains("SmokeAlias") || player.color != 0))
                fail("Night nickname or skin leaked.");
            Player[] pawns = FindObjectsByType<Player>(FindObjectsSortMode.None); //실제 Fusion 캐릭터
            if (pawns.Length == 4 && game.localPlayer != null)
                mark("FOUR_PAWNS");
            if (observedTurn != state.nightTurn)
            {
                observedTurn = state.nightTurn;
                routeStep = 0;
                previousHealth = 100;
                targetOwner = state.nightTurn == 0 ? 2 : state.nightTurn == 1 ? 1 : 0;
                targetItem = state.nightTurn == 0 ? 2 : state.nightTurn == 1 ? 1 : 3;
                route.Clear();
                mark("TURN_" + observedTurn);
                if (peer == 3 && observedTurn == 1)
                    game.GetComponent<AuctionNetworkComponent>().sendCommand(new AuctionState.Command
                    {
                        sequence = 10000, match = state.match, round = state.round, nightTurn = 0,
                        action = AuctionState.Action.EndNightTurn
                    });
            }
            if (state.nightTurn == 0 && state.doorStrength[2] == 0 && state.doorOpen[2])
                mark("LOCK_BROKEN");
            if (game.localPlayer != null && game.localPlayer.health < previousHealth)
                mark("DAMAGE");
            if (game.localPlayer != null)
                previousHealth = game.localPlayer.health;
            if (state.nightTurn == 3 && peer == 1 && !sentPass)
            {
                sentPass = true;
                game.requestEndNightTurn();
            }
        }

        private PlayerInput readInput() //실제 Fusion 입력으로 벽과 문 통과
        {
            PlayerInput input = default; //검증용 현재 입력
            AuctionState state = game?.displayStateValue; //본인에게 허용된 상태
            Player pawn = game?.localPlayer; //입력 권한을 가진 캐릭터
            if (state?.phase != AuctionState.Phase.Night || pawn == null)
                return input;
            if (peer == 1 && state.nightTurn == 1)
            {
                Player intruder = FindObjectsByType<Player>(FindObjectsSortMode.None).FirstOrDefault(player => player.slot == 3); //방어할 대상
                PlayerHouse defendingHouse = game.GetComponent<AuctionItemViewComponent>().getHouse(peer); //문 뒤를 막지 않는 방어 검증
                if (intruder != null && defendingHouse.contains(intruder.transform.position, 2.3f))
                {
                    Vector3 delta = intruder.transform.position - pawn.transform.position; //공격 대상까지 이동
                    input.direction = delta.magnitude > 1.1f ? new Vector2(delta.x, delta.z).normalized : Vector2.zero;
                    input.buttons.Set(PlayerButton.Attack, true);
                }
                return input;
            }
            if (state.activeIntruder != peer || state.nightTurn == 3)
                return input;
            AuctionItemViewComponent view = game.GetComponent<AuctionItemViewComponent>(); //실제 집과 보관품 위치
            PlayerHouse home = view.getHouse(peer); //출발하는 집
            PlayerHouse target = view.getHouse(targetOwner); //침입하는 집
            AuctionState.Item item = state.items.FirstOrDefault(value => value.id == targetItem); //대상 보관품
            if (item == null)
                return input;
            if (route.Count == 0)
            {
                Vector3 inward = home.transform.position.z > 0 ? Vector3.forward : Vector3.back; //자기 집 안쪽 방향
                Vector3 targetInward = target.transform.position.z > 0 ? Vector3.forward : Vector3.back; //상대 집 안쪽 방향
                route.Add(home.doorPosition + inward * 1.1f);
                route.Add(home.doorPosition - inward * 1.2f);
                route.Add(new Vector3(home.transform.position.x, 0, 4.5f));
                route.Add(new Vector3(target.transform.position.x, 0, 4.5f));
                route.Add(target.doorPosition - targetInward * 1.1f);
                route.Add(target.doorPosition + targetInward * 1.2f);
                route.Add(view.getItemPosition(item, state.items));
                route.Add(target.doorPosition + targetInward * 1.1f);
                route.Add(target.doorPosition - targetInward * 1.2f);
                route.Add(new Vector3(target.transform.position.x, 0, 4.5f));
                route.Add(new Vector3(home.transform.position.x, 0, 4.5f));
                route.Add(home.doorPosition - inward * 1.1f);
                route.Add(home.spawnPosition);
            }
            bool pulse = Mathf.FloorToInt(Time.realtimeSinceStartup * 4) % 2 == 0; //반복하는 버튼 입력
            if (routeStep == 0 && !state.doorOpen[peer])
            {
                if (flatDistance(pawn.transform.position, route[0]) < 0.25f)
                {
                    input.buttons.Set(PlayerButton.Interact, pulse);
                    return input;
                }
            }
            if (routeStep == 4 && !state.doorOpen[targetOwner])
            {
                if (flatDistance(pawn.transform.position, route[4]) < 0.25f)
                {
                    input.buttons.Set(PlayerButton.Interact, pulse);
                    return input;
                }
            }
            if (routeStep == 6 && pawn.carriedId == 0)
            {
                if (flatDistance(pawn.transform.position, route[6]) < 1.1f)
                {
                    input.buttons.Set(PlayerButton.Interact, pulse);
                    return input;
                }
            }
            if (pawn.carriedId > 0)
            {
                mark("CARRIED_TURN_" + state.nightTurn);
                if (state.nightTurn == 0 && item.known)
                    fail("Stolen item truth disclosed to thief.");
                if (state.nightTurn == 2)
                    return input;
            }
            if (routeStep < route.Count && flatDistance(pawn.transform.position, route[routeStep]) < 0.25f)
                routeStep++;
            if (routeStep < route.Count)
            {
                Vector3 delta = route[routeStep] - pawn.transform.position; //다음 경로 위치
                input.direction = new Vector2(delta.x, delta.z).normalized;
                if (input.direction != Vector2.zero)
                    mark("INPUT_MOVEMENT");
            }
            return input;
        }

        private static float flatDistance(Vector3 first, Vector3 second) //지면을 따라 측정한 거리
        {
            return Vector2.Distance(new Vector2(first.x, first.z), new Vector2(second.x, second.z));
        }

        private void mark(string stage) //민감한 설정을 포함하지 않는 성공 기록
        {
            if (stages.Add(stage))
                File.WriteAllText(Path.Combine(directory, peer + "_" + stage + ".txt"), "PASS");
        }

        private void fail(string message) //실패를 기록하고 종료
        {
            File.WriteAllText(Path.Combine(directory, peer + "_FAIL.txt"), message);
            Application.Quit(1);
        }

        private static void setField(object target, string name, object value) //검증 빌드만 사용하는 짧은 시간
        {
            target.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic).SetValue(target, value);
        }

        private static async Task waitFor(Func<bool> predicate, string label, int seconds = 90) //조건 대기와 시간 초과 기록
        {
            DateTime deadline = DateTime.UtcNow.AddSeconds(seconds); //검증 종료 시각
            while (!predicate())
            {
                if (DateTime.UtcNow > deadline)
                    throw new TimeoutException(label);
                await Task.Delay(100);
            }
        }

        private static string argument(string name) //명시한 검증 인수 조회
        {
            string[] args = Environment.GetCommandLineArgs(); //실행 인수
            int index = Array.IndexOf(args, name); //대상 인수 위치
            return index >= 0 && index + 1 < args.Length ? args[index + 1] : "";
        }
    }
}
#endif
