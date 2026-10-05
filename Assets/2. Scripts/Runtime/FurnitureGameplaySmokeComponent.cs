#if CANTRESELL_CONNECTION_SMOKE
using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using UnityEngine;

namespace CantResell
{
    public sealed class FurnitureGameplaySmokeComponent : MonoBehaviour
    {
        private AuctionGame game; //검증할 실제 세션
        private string directory; //프로세스 사이의 검증 기록 폴더
        private int peer; //현재 프로세스 좌석
        private Vector3? destination; //실제 입력으로 이동할 목적지
        private bool pressing; //E 키 입력 상태
        private double nextTrace; //실제 이동 상태 기록 시각

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void startSmoke() //별도 검증 빌드의 명령행에서만 실행
        {
            if (!Environment.GetCommandLineArgs().Contains("-furniturePeer"))
                return;
            GameObject runner = new GameObject("FurnitureGameplaySmoke"); //자동 검증 실행기
            DontDestroyOnLoad(runner);
            runner.AddComponent<FurnitureGameplaySmokeComponent>();
        }
        private async void Start() //네 프로세스에서 정상 입력과 호스트 규칙의 연결 검증
        {
            try
            {
                peer = int.Parse(argument("-furniturePeer"));
                directory = argument("-furnitureDirectory");
                Directory.CreateDirectory(directory);
                await waitFor(() => AuctionGame.current != null, "GAME");
                game = AuctionGame.current;
                game.GetComponent<AuctionSettingsComponent>().initialize("CantResell.Test.Furniture." + peer + ".", false);
                AudioListener.volume = 0;
                if (peer == 0)
                {
                    setField(game.store.rounds, "tradingDays", 3);
                    setField(game.store.rounds, "daySeconds", 120f);
                    setField(game.store.rounds, "nightSeconds", 90f);
                    setField(game.GetComponent<FurnitureEconomyComponent>(), "winChance", 0f);
                    setField(game.GetComponent<FurnitureShopComponent>(), "patienceSeconds", 110f);
                    game.createRoom("가구검증0", argument("-furnitureRoom"), false, "");
                }
                else
                {
                    await waitFor(() => exists(0, "ROOM"), "ROOM");
                    if (peer > 1)
                        await waitFor(() => exists(peer - 1, "JOINED"), "PREVIOUS_JOIN");
                    game.joinRoom("가구검증" + peer, new AuctionNetworkComponent.Room
                    {
                        code = File.ReadAllText(Path.Combine(directory, "RoomCode.txt")), open = true, capacity = 4
                    }, "");
                }
                await waitFor(() => state?.phase == AuctionState.Phase.Lobby, "LOBBY");
                require(state.localSlot == peer, "Wrong input authority seat");
                if (peer == 0)
                    File.WriteAllText(Path.Combine(directory, "RoomCode.txt"), game.GetComponent<AuctionNetworkComponent>().roomName);
                mark(peer == 0 ? "ROOM" : "JOINED");
                await Task.Delay(500);
                game.requestReady();
                if (peer == 0)
                {
                    await waitFor(() => state.players.All(player => player != null && player.ready), "READY");
                    game.requestStart();
                }
                PlayerInputComponent.smokeInput = readInput;
                await waitFor(() => state?.phase == AuctionState.Phase.Day && game.localPlayer != null, "DAY1");
                await waitFor(() => FindObjectsByType<Player>(FindObjectsSortMode.None).Length == 4, "FOUR_PAWNS");
                int price = 0; //실제 주문 판매 가격
                if (peer == 0)
                {
                    await waitFor(() => state.store.orderKind >= 0, "ORDER");
                    FurnitureState.Furniture item = state.store.furniture.First(value => value.kind == state.store.orderKind); //실제 손님 주문
                    price = item.price;
                    await moveTo(item.position);
                    await press();
                    await waitFor(() => state.store.furniture.Any(value => value.id == item.id && value.carrier == peer), "DAY_PICKUP");
                    await moveTo(FurnitureShopComponent.counter);
                    await press();
                    await waitFor(() => !state.store.furniture.Any(value => value.id == item.id), "SALE");
                    File.WriteAllText(Path.Combine(directory, "ExpectedCash.txt"), (250 + price).ToString());
                }
                else if (peer == 2)
                {
                    await moveTo(FurnitureStore.gamblingTable);
                    game.requestStoreGamble(50);
                    await waitFor(() => state.store.ledger.Any(entry => entry.Contains("도박") && entry.Contains("50")), "GAMBLE");
                }
                else if (peer == 3)
                {
                    await moveTo(FurnitureLeisureComponent.machine);
                    game.requestStorePlay();
                    await waitFor(() => state.notice.Contains("놀이 기록"), "PLAY");
                }
                else
                    await moveTo(new Vector3(-5, 0, -2));
                await waitFor(() => File.Exists(Path.Combine(directory, "ExpectedCash.txt")), "SHARED_EXPECTATION");
                int expected = int.Parse(File.ReadAllText(Path.Combine(directory, "ExpectedCash.txt"))); //전원이 확인할 공금
                await waitFor(() => state.store.cash == expected, "SHARED_MONEY");
                mark("DAY_ACTIVITIES");
                if (peer == 0)
                {
                    await allMarked("DAY_ACTIVITIES");
                    expirePhase();
                }
                await waitFor(() => state?.phase == AuctionState.Phase.Night && state.store.day == 1, "NIGHT1");
                await moveTo(new Vector3(0, 0, -1));
                if (peer == 0)
                    await press();
                await waitFor(() => state.store.doorOpen, "DOOR");
                await moveTo(new Vector3(-1.5f + peer, 0, 2));
                mark("ENTERED_TOGETHER");
                await allMarked("ENTERED_TOGETHER");
                if (peer < 2)
                {
                    FurnitureState.Furniture item = state.store.furniture.First(value => value.location == FurnitureState.Location.House && value.position == FurnitureInventoryComponent.housePosition(peer)); //센서 앞쪽의 가구
                    await moveTo(new Vector3(peer == 0 ? -2 : 2, 0, 4));
                    await moveTo(item.position);
                    await press();
                    await waitFor(() => state.store.furniture.Any(value => value.id == item.id && value.carrier == peer), "RAID_PICKUP");
                    await moveTo(new Vector3(peer == 0 ? -0.5f : 0.5f, 0, 1.5f));
                    await moveTo(new Vector3(peer == 0 ? -0.5f : 0.5f, 0, -2));
                    await moveTo(truckPosition());
                    await press();
                    await waitFor(() => state.store.furniture.Any(value => value.id == item.id && value.location == FurnitureState.Location.Truck), "LOAD");
                    mark("TRUCK_LOOT");
                }
                else
                {
                    await moveTo(new Vector3(0.5f, 0, -2));
                    await moveTo(truckPosition());
                }
                await Task.Delay(700);
                await press();
                await waitFor(() => state.store.escaped[peer] || state.store.day == 2, "ESCAPE");
                await waitFor(() => state.phase == AuctionState.Phase.Day && state.store.day == 2, "DAY2");
                require(state.store.furniture.Length == 7, "Two loaded pieces were not banked into remaining five stock");
                require(state.store.cash == expected, "Safe raid changed cash");
                mark("BANKED");
                if (peer == 0)
                {
                    await allMarked("BANKED");
                    setField(game.GetComponent<FurnitureNightComponent>(), "policeArrivalSeconds", 2f);
                    expirePhase();
                }
                await waitFor(() => state.phase == AuctionState.Phase.Night && state.store.day == 2, "NIGHT2");
                await moveTo(new Vector3(0, 0, -1));
                if (peer == 0)
                    await press();
                await waitFor(() => state.store.doorOpen, "DOOR2");
                await moveTo(new Vector3(-1.5f + peer, 0, 2));
                mark("CAPTURE_READY");
                if (peer == 0)
                {
                    await allMarked("CAPTURE_READY");
                    await moveTo(new Vector3(-5, 0, 3));
                    await moveTo(new Vector3(-5, 0, 7));
                    if (state.store.alarm == FurnitureState.Alarm.Quiet)
                    {
                        await moveTo(new Vector3(-5, 0, 4));
                        await moveTo(new Vector3(-5, 0, 7));
                    }
                }
                await waitFor(() => state.phase == AuctionState.Phase.Day && state.store.day == 3, "CAUGHT", 90);
                require(state.store.cash == expected - Mathf.CeilToInt(expected * 0.25f), "Shared fine was incorrect");
                require(state.store.furniture.Length == 7 && state.store.outcome.Contains("체포"), "Old stock or capture settlement failed");
                mark("CAPTURE_FINE");
                if (peer == 0)
                {
                    await allMarked("CAPTURE_FINE");
                    expirePhase();
                }
                await waitFor(() => state.phase == AuctionState.Phase.Results, "RESULTS");
                require(state.players.All(player => player.name.StartsWith("가구검증")), "Teammate identity lost");
                mark("RESULTS");
                if (peer == 0)
                {
                    await allMarked("RESULTS");
                    game.requestLobby();
                }
                await waitFor(() => state.phase == AuctionState.Phase.Lobby, "RETURN");
                mark("RETURN");
                if (peer == 0)
                {
                    await allMarked("RETURN");
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
        private AuctionState state => game?.displayStateValue; //최신 공동 상태
        private void Update() //별도 검증 빌드의 이동과 단계 기록
        {
            if (game?.localPlayer == null || string.IsNullOrEmpty(directory) || Time.realtimeSinceStartupAsDouble < nextTrace)
                return;
            nextTrace = Time.realtimeSinceStartupAsDouble + 1;
            File.AppendAllText(Path.Combine(directory, peer + "_Movement.txt"), "day=" + state?.store?.day + " phase=" + state?.phase +
                " position=" + game.localPlayer.transform.position + " destination=" + destination + " carried=" + game.localPlayer.carriedId +
                " alarm=" + state?.store?.alarm + " police=" + state?.store?.policePosition + "\n");
        }
        private Vector3 truckPosition() //정지한 동료와 겹치지 않는 차량 조작 위치
        {
            return FurnitureNightComponent.truck + new Vector3(peer % 2 == 0 ? -0.8f : 0.8f, 0, peer < 2 ? 0.3f : -0.6f);
        }
        private PlayerInput readInput() //실제 Fusion 입력으로 캐릭터 이동
        {
            PlayerInput input = default; //이번 틱 입력
            if (destination.HasValue && game?.localPlayer != null)
            {
                Vector3 difference = destination.Value - game.localPlayer.transform.position; //목적지의 평면 방향
                input.direction = new Vector2(difference.x, difference.z).normalized;
            }
            input.buttons.Set(PlayerButton.Interact, pressing);
            return input;
        }
        private async Task moveTo(Vector3 position) //실제 이동의 목적지 도착 대기
        {
            destination = position;
            await waitFor(() => game.localPlayer != null && Vector2.Distance(new Vector2(game.localPlayer.transform.position.x, game.localPlayer.transform.position.z), new Vector2(position.x, position.z)) < 0.7f, "MOVE_" + position, 30);
            destination = null;
            await Task.Delay(300);
        }
        private async Task press() //키 누름과 해제를 별도 틱으로 전송
        {
            pressing = true;
            await Task.Delay(180);
            pressing = false;
            await Task.Delay(700);
        }
        private void expirePhase() //검증 시간만 줄이고 정상 만료 경로 사용
        {
            setField(game.store.rounds, "deadline", Time.realtimeSinceStartupAsDouble);
        }
        private Task allMarked(string stage) //모든 참가자의 상태 확인 대기
        {
            return waitFor(() => Enumerable.Range(0, 4).All(index => exists(index, stage)), "ALL_" + stage);
        }
        private bool exists(int index, string stage) //검증 완료 단계 확인
        {
            return File.Exists(Path.Combine(directory, index + "_" + stage + ".txt"));
        }
        private void mark(string stage) //확인한 공동 상태 기록
        {
            File.WriteAllText(Path.Combine(directory, peer + "_" + stage + ".txt"), JsonUtility.ToJson(state));
        }
        private static async Task waitFor(Func<bool> predicate, string stage, int seconds = 60) //비동기 조건 대기와 시간 제한
        {
            DateTime deadline = DateTime.UtcNow.AddSeconds(seconds); //검증의 대기 상한
            while (!predicate())
            {
                if (DateTime.UtcNow > deadline)
                    throw new Exception("Timeout: " + stage);
                await Task.Delay(50);
            }
        }
        private static void require(bool condition, string message) //실제 상태 검증
        {
            if (!condition)
                throw new Exception(message);
        }
        private static void setField(object component, string name, object value) //검증 빌드에서만 Inspector 설정 변경
        {
            component.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic).SetValue(component, value);
        }
        private static string argument(string name) //검증 실행 인자 값 조회
        {
            string[] args = Environment.GetCommandLineArgs(); //실행 인자
            return args[Array.IndexOf(args, name) + 1];
        }
    }
}
#endif
