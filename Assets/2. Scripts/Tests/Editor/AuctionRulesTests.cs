using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace CantResell.Tests
{
    public sealed class AuctionRulesTests
    {
        private GameObject root; //검증용 임시 구성 요소 루트
        private AuctionBidComponent bid; //입찰 검증 대상
        private AuctionEconomyComponent economy; //정산 검증 대상
        private AuctionItemComponent item; //상품 검사 검증 대상

        [TestCase("a1", true)]
        [TestCase("한글123", true)]
        [TestCase("12345678", true)]
        [TestCase("abcdefgh", true)]
        [TestCase("123456789", false)]
        [TestCase("", false)]
        [TestCase(null, false)]
        [TestCase("a b", false)]
        [TestCase("a!1", false)]
        public void validateRoomPasswordBoundary(string password, bool valid) //문자 종류와 비밀번호 최대 길이 경계 검증
        {
            Assert.AreEqual(valid, AuctionNetworkComponent.isValidPassword(password));
        }

        [Test]
        public void rejectWrongPasswordRoomAndMalformedToken() //비밀번호 및 대상 방이 다른 요청을 호스트에서 거부
        {
            byte[] token = AuctionNetworkComponent.createAdmissionToken("room-a", "비밀12"); //호스트가 보관한 인증 값
            Assert.IsTrue(AuctionNetworkComponent.acceptsToken(token, AuctionNetworkComponent.createAdmissionToken("room-a", "비밀12")));
            Assert.IsFalse(AuctionNetworkComponent.acceptsToken(token, AuctionNetworkComponent.createAdmissionToken("room-a", "틀림12")));
            Assert.IsFalse(AuctionNetworkComponent.acceptsToken(token, AuctionNetworkComponent.createAdmissionToken("room-b", "비밀12")));
            Assert.IsFalse(AuctionNetworkComponent.acceptsToken(token, null));
            Assert.IsFalse(AuctionNetworkComponent.acceptsToken(token, new byte[1]));
            byte[] altered = (byte[])token.Clone(); //변조된 인증 요청
            altered[0] = 1;
            Assert.IsFalse(AuctionNetworkComponent.acceptsToken(token, altered));
        }

        [Test]
        public void preserveRoomTitleWithoutControlCharacters() //제목 표시를 깨뜨리는 제어 문자 제거 검증
        {
            Assert.AreEqual("즐거운 경매장", AuctionNetworkComponent.cleanTitle("  즐거운 경매장\n  "));
            Assert.AreEqual(30, AuctionNetworkComponent.cleanTitle(new string('가', 40)).Length);
        }

        [Test]
        public void voicePrefabStartsWithoutMicrophoneCapture() //음성 프리팹 참조와 명시적인 마이크 활성화 기본값 검증
        {
            GameObject runner = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/3. Prefabs/AuctionRunner.prefab"); //실제 게임에서 사용할 러너
            Assert.IsNotNull(runner);
            Component recorder = runner.GetComponents<Component>().Single(component => component.GetType().Name == "Recorder"); //SDK 직접 참조 없이 직렬화된 녹음 설정 검사
            SerializedObject data = new SerializedObject(recorder); //기본 녹음 설정
            Assert.IsFalse(data.FindProperty("recordingEnabled").boolValue);
            Assert.IsFalse(data.FindProperty("transmitEnabled").boolValue);
            Assert.IsFalse(data.FindProperty("recordWhenJoined").boolValue);
            Component client = runner.GetComponents<Component>().Single(component => component.GetType().Name == "AuctionVoiceClientComponent"); //음성 연결 구성
            data = new SerializedObject(client);
            Assert.IsTrue(data.FindProperty("usePrimaryRecorder").boolValue);
            Assert.IsNotNull(data.FindProperty("speakerPrefab").objectReferenceValue);
            Assert.AreSame(recorder, data.FindProperty("primaryRecorder").objectReferenceValue);
        }

        [SetUp]
        public void setUp() //독립적인 검증 상태 준비
        {
            root = new GameObject("AuctionRulesTest") { hideFlags = HideFlags.HideAndDontSave };
            bid = root.AddComponent<AuctionBidComponent>();
            economy = root.AddComponent<AuctionEconomyComponent>();
            item = root.AddComponent<AuctionItemComponent>();
            economy.resetMatch();
            item.resetMatch();

        }

        [TearDown]
        public void tearDown() //검증용 오브젝트 정리
        {
            Object.DestroyImmediate(root);
        }

        [Test]
        public void rejectSellerAndOverBudgetBids() //판매자와 잔액 초과 요청이 최고가를 바꾸지 않는지 검증
        {
            Assert.IsFalse(bid.tryBid(0, 0, 100, 20));
            Assert.IsFalse(bid.tryBid(1, 0, 100, 101));
            Assert.AreEqual(-1, bid.bidderSlot);
            Assert.AreEqual(0, bid.highestBid);
            Assert.IsTrue(bid.tryBid(1, 0, 100, 100));
        }

        [Test]
        public void requireIncreasingBidsWithoutOverflow() //동일가와 역순 및 정수 경계 입찰 검증
        {
            Assert.IsTrue(bid.tryBid(1, 0, int.MaxValue, int.MaxValue - 5));
            Assert.IsFalse(bid.tryBid(2, 0, int.MaxValue, int.MaxValue));
            Assert.IsFalse(bid.tryBid(2, 0, 100, 10));
            Assert.AreEqual(1, bid.bidderSlot);
        }

        [Test]
        public void purchaseConservesMoneyAndRejectsDuplicateSettlement() //낙찰 보상 제거와 중복 정산 차단
        {
            Assert.IsTrue(economy.settleRound(0, 0, 1, 40));
            Assert.AreEqual(340, economy.getBalance(0));
            Assert.AreEqual(260, economy.getBalance(1));
            Assert.AreEqual(1200, Enumerable.Range(0, 4).Sum(economy.getBalance));
            Assert.IsFalse(economy.settleRound(0, 0, 1, 40));
            Assert.IsTrue(economy.settleRound(12, 0, 1, 40));
        }

        [Test]
        public void unsoldAndInvalidTradeCannotCreateMoney() //유찰과 잘못된 거래 처리
        {
            Assert.IsTrue(economy.settleRound(0, 0, -1, 0));
            Assert.IsFalse(economy.settleRound(1, 0, 0, 40));
            Assert.IsFalse(economy.settleRound(1, 0, 1, 301));
            Assert.IsTrue(economy.settleRound(1, 0, 1, 30));
            Assert.AreEqual(1200, Enumerable.Range(0, 4).Sum(economy.getBalance));
        }

        [Test]
        public void catalogDrawsEveryKindBeforeRepeating() //도구를 포함한 도감의 무작위 순환
        {
            AuctionState.Item[] draws = Enumerable.Range(1, 13).Select(id => item.createItem(id, 0)).ToArray(); //한 주머니의 추첨
            Assert.AreEqual(13, draws.Select(value => value.kind).Distinct().Count());
            Assert.IsTrue(draws.All(value => value.cost >= 20 && value.cost <= 120));
            Assert.IsTrue(draws.All(value => value.condition == 0 || value.condition == 50 || value.condition == 100));
        }

        [Test]
        public void onlySellerAndWinnerKnowTruthEvenAfterTheft() //낙찰자 이외의 추가 공개와 점수 누설 방지
        {
            AuctionInventoryComponent inventory = root.AddComponent<AuctionInventoryComponent>(); //호스트 보유품
            Assert.IsTrue(inventory.addStock(new AuctionState.Item { id = 1, owner = 0, kind = AuctionState.ItemKind.Lamp, cost = 73, condition = 50 }));
            Assert.IsFalse(inventory.addStock(new AuctionState.Item { id = 1, owner = 2 }));
            Assert.IsTrue(inventory.createSnapshot(0)[0].known);
            Assert.AreEqual(0, inventory.createSnapshot(1)[0].cost);
            Assert.IsFalse(inventory.settleLot(1, 0));
            Assert.IsTrue(inventory.settleLot(1, 1));
            Assert.AreEqual(73, inventory.createSnapshot(1)[0].cost);
            Assert.AreEqual(50, inventory.createSnapshot(1)[0].condition);
            Assert.IsFalse(inventory.createSnapshot(2)[0].known);
            Assert.IsTrue(inventory.beginCarry(1, 2));
            Assert.AreEqual(1, inventory.getItem(1).owner);
            Assert.IsTrue(inventory.finishCarry(2, true));
            Assert.AreEqual(2, inventory.getItem(1).owner);
            Assert.IsFalse(inventory.createSnapshot(2)[0].known);
            Assert.AreEqual(0, inventory.createSnapshot(2)[0].condition);
            Assert.IsTrue(inventory.createSnapshot(0)[0].known);
            Assert.IsTrue(inventory.createSnapshot(1)[0].known);
        }

        [Test]
        public void failedCarryReturnsItemWithoutDuplicateOrSelfTheft() //시간 초과와 제압 시 소유권 복구
        {
            AuctionInventoryComponent inventory = root.AddComponent<AuctionInventoryComponent>(); //보유품 처리
            inventory.addStock(new AuctionState.Item { id = 1, owner = 0 });
            inventory.settleLot(1, 1);
            inventory.addStock(new AuctionState.Item { id = 2, owner = 0 });
            inventory.settleLot(2, 1);
            Assert.IsFalse(inventory.beginCarry(1, 1));
            Assert.IsTrue(inventory.beginCarry(1, 2));
            Assert.IsFalse(inventory.beginCarry(2, 2));
            Assert.IsFalse(inventory.beginCarry(1, 3));
            Assert.IsTrue(inventory.finishCarry(2, false));
            Assert.AreEqual(1, inventory.getItem(1).owner);
            Assert.AreEqual(AuctionState.ItemStatus.Stored, inventory.getItem(1).status);
            Assert.IsFalse(inventory.finishCarry(2, true));
        }

        [Test]
        public void objectivesShareMaximumAndDoNotStackDuplicates() //돈과 무관한 동일 만점 및 최고 성능 집계
        {
            AuctionObjectiveComponent objective = root.AddComponent<AuctionObjectiveComponent>(); //목표 점수
            objective.resetMatch();
            Assert.AreEqual(4, Enumerable.Range(0, 4).Select(slot => objective.getGoal(slot).title).Distinct().Count());
            for (int slot = 0; slot < 4; slot++) //각 목표의 동일 만점
            {
                AuctionObjectiveComponent.Goal goal = objective.getGoal(slot); //해당 목표
                AuctionState.Item[] owned =
                {
                    new AuctionState.Item { owner = slot, kind = goal.core, condition = 100, status = AuctionState.ItemStatus.Stored },
                    new AuctionState.Item { owner = slot, kind = goal.core, condition = 50, status = AuctionState.ItemStatus.Stored },
                    new AuctionState.Item { owner = slot, kind = goal.support, condition = 100, status = AuctionState.ItemStatus.Stored },
                    new AuctionState.Item { owner = slot, kind = goal.decoration, condition = 100, status = AuctionState.ItemStatus.Stored }
                }; //중복 핵심 상품을 가진 집
                Assert.AreEqual(100, objective.calculateScore(slot, owned));
                owned[0].condition = 0;
                Assert.AreEqual(75, objective.calculateScore(slot, owned));
                owned[2].status = AuctionState.ItemStatus.Carried;
                Assert.AreEqual(45, objective.calculateScore(slot, owned));
            }
        }

        [Test]
        public void eachNightGivesExactlyOneRandomTurnPerPlayer() //무작위 순서의 중복과 행동권 경계
        {
            AuctionNightComponent night = root.AddComponent<AuctionNightComponent>(); //밤 진행
            night.beginNight();
            Assert.AreEqual(4, night.order.Distinct().Count());
            for (int turn = 0; turn < 4; turn++) //네 참가자의 한 번씩 행동
            {
                Assert.AreEqual(turn, night.turn);
                Assert.IsTrue(night.canLeaveHouse(night.order[turn]));
                Assert.IsFalse(night.canLeaveHouse((night.order[turn] + 1) % 4));
                Assert.AreEqual(turn < 3, night.nextTurn());
            }
            Assert.AreEqual(-1, night.activeSlot);
            Assert.IsFalse(night.nextTurn());
        }

        [Test]
        public void hostClockSupportsTwoAuctionPhasesAndRepeatedCycles() //판매자 회전과 설명 입찰 밤 시간 경계
        {
            AuctionRoundComponent round = root.AddComponent<AuctionRoundComponent>(); //게임 단계
            round.beginRound(4, 100);
            Assert.AreEqual(0, round.sellerSlot);
            Assert.IsFalse(round.hasExpired(119.99));
            Assert.IsTrue(round.hasExpired(120));
            round.enterPhase(AuctionState.Phase.Bidding, 120);
            Assert.AreEqual(25, round.getRemaining(120));
            round.enterPhase(AuctionState.Phase.Night, 145);
            Assert.AreEqual(45, round.getRemaining(145));
            round.enterPhase(AuctionState.Phase.Results, 200);
            Assert.IsFalse(round.hasExpired(999));
        }

        [Test]
        public void realSnapshotSharesMoneyAndKeepsTeammateNames() //실제 전송 경로의 공금과 동료 식별 확인
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(CantResell.Editor.AuctionProjectSetup.prefabPath); //실제 게임 프리팹
            GameObject testGame = Object.Instantiate(prefab); //전송 검증 대상
            try
            {
                AuctionGame game = testGame.GetComponent<AuctionGame>(); //세션 진입점
                AuctionState.Player[] participants = (AuctionState.Player[])typeof(AuctionGame).GetField("players", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(game); //호스트 참가자
                for (int slot = 0; slot < 4; slot++) //네 참가자 등록
                    participants[slot] = new AuctionState.Player { id = slot + 1, name = "동료" + slot, color = slot };
                FurnitureStore store = testGame.GetComponent<FurnitureStore>(); //공동 게임 파사드
                store.initialize(new Player[4], () => { }, slot => "동료" + slot);
                store.beginMatch(100);
                store.tick(280, 0);
                MethodInfo createState = typeof(AuctionGame).GetMethod("createState", BindingFlags.Instance | BindingFlags.NonPublic); //실제 전송 복사
                AuctionState night = JsonUtility.FromJson<AuctionState>(JsonUtility.ToJson(createState.Invoke(game, new object[] { 1, "" }))); //JSON 전송 경계
                Assert.AreEqual(AuctionState.Phase.Night, night.phase);
                Assert.AreEqual(300, night.store.cash);
                Assert.IsTrue(night.players.All(value => value.name.StartsWith("동료") && string.IsNullOrEmpty(value.objective)));
                Assert.Greater(night.store.furniture.Length, 6);
                night.store.furniture[0].price = 0;
                Assert.Greater(store.snapshot(280).furniture[0].price, 0);
            }
            finally { Object.DestroyImmediate(testGame); }
        }

        [Test]
        public void completeFourDaysAndThreeRaidsThroughTheRealCoordinator() //마지막 밤의 가구를 팔 수 있는 최종 영업 확인
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(CantResell.Editor.AuctionProjectSetup.prefabPath); //실제 연결 구성
            GameObject testGame = Object.Instantiate(prefab); //진행 검증 대상
            try
            {
                FurnitureStore store = testGame.GetComponent<FurnitureStore>(); //담당 구성 요소를 조율할 파사드
                store.initialize(new Player[4], () => { }, slot => "동료" + slot);
                store.beginMatch(100);
                double now = 100; //호스트 검증 시각
                for (int day = 1; day <= 4; day++) //네 번의 영업과 세 번의 침입
                {
                    Assert.AreEqual(day, store.rounds.day);
                    Assert.AreEqual(AuctionState.Phase.Day, store.rounds.phase);
                    store.tick(now += 180, 0);
                    if (day < 4)
                    {
                        Assert.AreEqual(AuctionState.Phase.Night, store.rounds.phase);
                        store.tick(now += 180, 0);
                    }
                }
                Assert.AreEqual(AuctionState.Phase.Results, store.rounds.phase);
                Assert.AreEqual(300, store.snapshot(now).cash);
                Assert.IsFalse(store.snapshot(now).success);
                store.resetMatch();
                Assert.AreEqual(AuctionState.Phase.Lobby, store.rounds.phase);
                Assert.IsFalse(store.running);
            }
            finally { Object.DestroyImmediate(testGame); }
        }
        [TestCase("Home")]
        [TestCase("StandBy")]
        [TestCase("Play")]
        public void sceneContainsOneConnectedFacade(string sceneName) //실제 씬의 진입점과 필수 참조 검증
        {
            Scene scene = EditorSceneManager.OpenScene("Assets/1. Scenes/" + sceneName + ".unity", OpenSceneMode.Additive); //검증할 실제 씬
            try
            {
                Assert.IsFalse(scene.GetRootGameObjects().SelectMany(value => value.GetComponentsInChildren<Component>(true)).Any(component => component == null), sceneName + " 씬에 누락된 스크립트가 있습니다.");
                AuctionGame[] facades = scene.GetRootGameObjects().SelectMany(value => value.GetComponentsInChildren<AuctionGame>()).ToArray(); //씬에 연결된 진입점 목록
                Assert.AreEqual(1, facades.Length);
                SerializedObject settings = new SerializedObject(facades[0]); //저장된 구성 요소 참조
                foreach (string field in new[] { "roundComponent", "itemComponent", "bidComponent", "economyComponent", "networkComponent", "uiComponent", "viewComponent", "voiceComponent", "settingsComponent", "audioComponent", "inventoryComponent", "objectiveComponent", "nightComponent", "storeComponent" }) //필수 연결 필드
                    Assert.IsNotNull(settings.FindProperty(field).objectReferenceValue, field);
                Assert.AreEqual(1, scene.GetRootGameObjects().SelectMany(value => value.GetComponentsInChildren<Camera>()).Count());
            }
            finally
            {
                EditorSceneManager.CloseScene(scene, true);
            }
        }

        [Test]
        public void renderPipelineSettingsHaveNoMissingTypes() //버전 전환 뒤 URP 리소스 형식이 모두 해석되는지 검증
        {
            Object settings = AssetDatabase.LoadMainAssetAtPath("Assets/Settings/UniversalRenderPipelineGlobalSettings.asset"); //실제 URP 전역 설정
            Assert.IsNotNull(settings);
            Assert.IsFalse(SerializationUtility.HasManagedReferencesWithMissingTypes(settings));
        }

        [Test]
        public void emptySeatsRemainEmptyAfterJsonTransfer() //JSON을 거친 대기실의 빈 좌석 표시 검증
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(CantResell.Editor.AuctionProjectSetup.prefabPath); //실제 진입점 프리팹
            GameObject testGame = Object.Instantiate(prefab); //상태 수신을 검증할 인스턴스
            try
            {
                AuctionState state = new AuctionState { sequence = 1, localSlot = 0 }; //한 명만 참가한 대기실 상태
                state.players[0] = new AuctionState.Player { id = 1, name = "방장" };
                string json = JsonUtility.ToJson(state); //실제 네트워크에 전달할 JSON
                testGame.GetComponent<AuctionGame>().receiveState(JsonUtility.FromJson<AuctionState>(json));
                AuctionState received = (AuctionState)typeof(AuctionGame).GetField("localState", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(testGame.GetComponent<AuctionGame>()); //수신 처리 후 화면 상태
                Assert.IsNotNull(received.players[0]);
                Assert.IsNull(received.players[1]);
                Assert.IsNull(received.players[2]);
                Assert.IsNull(received.players[3]);
            }
            finally
            {
                Object.DestroyImmediate(testGame);
            }
        }
    }
}
