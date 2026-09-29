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
            Component client = runner.GetComponents<Component>().Single(component => component.GetType().Name == "FusionVoiceClient"); //음성 연결 구성
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
            item.prepareItem();
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

        [TestCase(true, 180)]
        [TestCase(false, 60)]
        public void settlePurchaseExactlyOnce(bool good, int buyerBalance) //정상 보상과 불량 손실 및 중복 정산 차단 검증
        {
            Assert.IsTrue(economy.settleRound(0, 0, 1, 40, good));
            Assert.AreEqual(140, economy.getBalance(0));
            Assert.AreEqual(buyerBalance, economy.getBalance(1));
            Assert.IsFalse(economy.settleRound(0, 0, 1, 40, good));
            Assert.AreEqual(buyerBalance, economy.getBalance(1));
        }

        [Test]
        public void unsoldItemDoesNotCreateMoney() //유찰 시 자금 변화가 없는지 검증
        {
            Assert.IsTrue(economy.settleRound(0, 0, -1, 0, true));
            Assert.AreEqual(400, Enumerable.Range(0, 4).Sum(economy.getBalance));
        }

        [Test]
        public void invalidSettlementDoesNotConsumeRound() //잘못된 거래가 라운드를 잠그지 않는지 검증
        {
            Assert.IsFalse(economy.settleRound(0, 0, 0, 40, true));
            Assert.IsFalse(economy.settleRound(0, 0, 1, 101, true));
            Assert.IsTrue(economy.settleRound(0, 0, 1, 30, false));
        }

        [Test]
        public void inspectionIsPrivateAndLimitedAcrossRounds() //검사권 소모와 중복 검사 및 비밀 열람 권한 검증
        {
            Assert.IsTrue(item.knowsCondition(0, 0, false));
            Assert.IsFalse(item.knowsCondition(1, 0, false));
            Assert.IsFalse(item.tryInspect(0, 0));
            Assert.IsTrue(item.tryInspect(1, 0));
            Assert.IsFalse(item.tryInspect(1, 0));
            Assert.AreEqual(1, item.getTickets(1));
            Assert.IsTrue(item.knowsCondition(1, 0, false));
            Assert.IsFalse(item.knowsCondition(2, 0, false));
            item.prepareItem();
            Assert.IsFalse(item.knowsCondition(1, 0, false));
            Assert.IsTrue(item.tryInspect(1, 0));
            item.prepareItem();
            Assert.IsFalse(item.tryInspect(1, 0));
            Assert.IsTrue(item.knowsCondition(2, 0, true));
        }

        [Test]
        public void phaseDeadlineUsesHostClock() //시간 배율과 무관한 단계 경계 검증
        {
            AuctionRoundComponent round = root.AddComponent<AuctionRoundComponent>(); //단계 검증 대상
            round.beginRound(0, 100);
            Assert.IsFalse(round.hasExpired(119.99));
            Assert.IsTrue(round.hasExpired(120));
            round.enterPhase(AuctionState.Phase.Inspection, 120);
            Assert.IsFalse(round.hasExpired(120));
            Assert.AreEqual(15, round.getRemaining(120));
            round.enterPhase(AuctionState.Phase.Results, 200);
            Assert.IsFalse(round.hasExpired(999));
        }

        [Test]
        public void snapshotDoesNotSerializeUnrevealedConditionForOtherPlayers() //전송되는 상태에도 비밀 정보가 빠지는지 검증
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(CantResell.Editor.AuctionProjectSetup.prefabPath); //실제 진입점 프리팹
            GameObject testGame = Object.Instantiate(prefab); //연결된 구성 요소를 가진 검증 인스턴스
            try
            {
                AuctionItemComponent targetItem = testGame.GetComponent<AuctionItemComponent>(); //호스트 상품 상태
                SerializedObject settings = new SerializedObject(targetItem); //정상 상품을 강제할 검증 설정
                settings.FindProperty("normalChance").floatValue = 1;
                settings.ApplyModifiedPropertiesWithoutUndo();
                targetItem.resetMatch();
                targetItem.prepareItem();
                testGame.GetComponent<AuctionRoundComponent>().beginRound(0, 0);
                MethodInfo createState = typeof(AuctionGame).GetMethod("createState", BindingFlags.Instance | BindingFlags.NonPublic); //실제 직렬화 직전 상태 생성 함수
                AuctionState buyer = (AuctionState)createState.Invoke(testGame.GetComponent<AuctionGame>(), new object[] { 1, "" }); //검사하지 않은 구매자의 전송 상태
                Assert.IsFalse(buyer.knowsCondition);
                Assert.IsFalse(buyer.goodCondition);
                AuctionState seller = (AuctionState)createState.Invoke(testGame.GetComponent<AuctionGame>(), new object[] { 0, "" }); //판매자의 전송 상태
                Assert.IsTrue(seller.knowsCondition);
                Assert.IsTrue(seller.goodCondition);
                Assert.IsTrue(targetItem.tryInspect(1, 0));
                buyer = (AuctionState)createState.Invoke(testGame.GetComponent<AuctionGame>(), new object[] { 1, "" });
                Assert.IsTrue(buyer.goodCondition);
            }
            finally
            {
                Object.DestroyImmediate(testGame);
            }
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
                foreach (string field in new[] { "roundComponent", "itemComponent", "bidComponent", "economyComponent", "networkComponent", "uiComponent", "viewComponent" }) //필수 연결 필드
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
