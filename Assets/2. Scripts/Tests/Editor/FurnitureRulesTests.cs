using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;

namespace CantResell.Tests
{
    public sealed class FurnitureRulesTests
    {
        private GameObject root; //독립 규칙 검증 오브젝트
        private FurnitureEconomyComponent economy; //공금 정산 검증 대상
        private FurnitureInventoryComponent inventory; //공동 재고 검증 대상

        [SetUp]
        public void setup() //호스트 규칙을 독립 구성
        {
            root = new GameObject("FurnitureRules");
            economy = root.AddComponent<FurnitureEconomyComponent>();
            inventory = root.AddComponent<FurnitureInventoryComponent>();
            economy.resetMatch();
            inventory.resetMatch();
        }
        [TearDown]
        public void cleanup() //검증 오브젝트 해제
        {
            Object.DestroyImmediate(root);
        }
        [Test]
        public void defaultStockRequiresAdditionalIncomeToReachGoal() //시작 재고만 팔고 침입을 건너뛰는 목표 우회 방지
        {
            Assert.Less(economy.cash + inventory.all.Sum(item => item.price), economy.target);
        }
        [Test]
        public void gamblingCanLoseEverythingAndSellingRestoresSharedMoney() //공금 전부 손실과 영업을 통한 복구
        {
            setField(economy, "winChance", 0f);
            Assert.IsTrue(economy.placeBet(1, 300, 10, "도박꾼", out _));
            Assert.AreEqual(0, economy.cash);
            Assert.IsFalse(economy.placeBet(2, 1, 10, "다른 도박꾼", out _));
            Assert.IsTrue(economy.settleSale(1, 50, "점원"));
            Assert.IsFalse(economy.settleSale(1, 50, "중복 점원"));
            Assert.AreEqual(50, economy.cash);
            Assert.IsTrue(economy.placeBet(1, 50, 12, "도박꾼", out _));
            Assert.AreEqual(0, economy.cash);
        }
        [Test]
        public void sharedWagersCannotOverspendOrOverflow() //동시 요청의 잔액 경계와 올림 벌금
        {
            setField(economy, "winChance", 0f);
            Assert.IsTrue(economy.placeBet(0, 200, 1, "A", out _));
            Assert.IsFalse(economy.placeBet(1, 200, 1, "B", out _));
            Assert.IsFalse(economy.placeBet(0, 10, 1.1, "A", out _));
            Assert.IsFalse(economy.placeBet(-1, 10, 4, "A", out _));
            Assert.IsFalse(economy.placeBet(1, -1, 4, "B", out _));
            Assert.IsFalse(economy.placeBet(1, 1, double.NaN, "B", out _));
            Assert.AreEqual(25, economy.chargeFine(1));
            Assert.AreEqual(0, economy.chargeFine(1));
            Assert.AreEqual(75, economy.cash);
            Assert.IsFalse(economy.settleSale(2, int.MaxValue, "점원"));
        }
        [Test]
        public void winPaysNetStakeAndFineRoundsUpOnce() //승리 지급과 벌금 정산 공식
        {
            setField(economy, "winChance", 1f);
            Assert.IsTrue(economy.placeBet(3, 50, 10, "A", out _));
            Assert.AreEqual(350, economy.cash);
            Assert.AreEqual(88, economy.chargeFine(1));
            Assert.AreEqual(262, economy.cash);
        }
        [Test]
        public void aFurnitureAndCarrierCanOnlyBeClaimedOnce() //동일 가구 쟁탈과 한 명 한 가구 제한
        {
            int id = inventory.all.First().id; //동시에 요청할 가구
            Assert.IsTrue(inventory.pickUp(id, 0, false));
            Assert.IsFalse(inventory.pickUp(id, 1, false));
            Assert.IsFalse(inventory.pickUp(id + 1, 0, false));
            Assert.IsTrue(inventory.drop(0, false, Vector3.one));
            Assert.IsTrue(inventory.pickUp(id, 1, false));
        }
        [TestCase(false, 7)]
        [TestCase(true, 6)]
        public void raidSettlementKeepsOldStockAndOnlyBanksSuccessfulTruckLoot(bool caught, int count) //체포 때 적재품도 몰수하고 기존 재고 유지
        {
            int[] oldIds = inventory.all.Select(item => item.id).ToArray(); //이전 영업 재고
            inventory.beginNight();
            int stolen = inventory.all.First(item => item.location == FurnitureState.Location.House).id; //적재할 가구
            Assert.IsTrue(inventory.pickUp(stolen, 0, true));
            Assert.IsTrue(inventory.loadTruck(0));
            inventory.finishNight(caught);
            Assert.AreEqual(count, inventory.all.Count());
            Assert.IsTrue(oldIds.All(id => inventory.get(id) != null));
            Assert.IsTrue(inventory.all.All(item => item.location == FurnitureState.Location.Shop));
            Assert.AreEqual(!caught, inventory.get(stolen) != null);
        }
        [Test]
        public void customerUsesAvailableStockAndDeliveryIsOnceOnly() //실제 주문과 운반 판매의 연결
        {
            FurnitureShopComponent shop = root.AddComponent<FurnitureShopComponent>(); //손님 검증 대상
            shop.beginDay(10);
            shop.tick(10, inventory);
            FurnitureState.Furniture item = inventory.all.First(value => value.kind == shop.orderKind); //주문과 일치하는 재고
            Assert.IsTrue(inventory.pickUp(item.id, 2, false));
            Assert.IsTrue(shop.deliver(2, 11, "점원", inventory, economy));
            Assert.IsFalse(shop.deliver(2, 11, "점원", inventory, economy));
            Assert.AreEqual(300 + item.price, economy.cash);
        }
        [Test]
        public void alarmHasOneDeadlineAndSwitchTemporarilyDisablesSensors() //경보 재접촉으로 경찰 도착을 미룰 수 없음
        {
            FurnitureNightComponent night = root.AddComponent<FurnitureNightComponent>(); //집 센서 검증 대상
            night.beginNight(100, new Player[4]);
            Assert.IsTrue(night.sensorActive(100));
            night.disableSensors(100);
            Assert.IsFalse(night.sensorActive(101));
            Assert.IsTrue(night.sensorActive(112));
            Assert.IsTrue(night.triggerAlarm(101));
            Assert.IsFalse(night.triggerAlarm(110));
            Assert.AreEqual(11, night.policeRemaining(110));
            night.tick(121, 0, new Player[4]);
            Assert.AreEqual(FurnitureState.Alarm.Pursuit, night.alarm);
        }
        [TestCase(-5, 3, 8, true)]
        [TestCase(0, 3, 8, false)]
        [TestCase(0, 9, 12, true)]
        [TestCase(9, 9, 12, false)]
        public void sensorDetectsCrossingBetweenTicks(float x, float from, float to, bool expected) //센서가 틱 사이 이동을 놓치지 않음
        {
            Assert.AreEqual(expected, FurnitureNightComponent.crossesSensor(new Vector3(x, 0, from), new Vector3(x, 0, to)));
        }
        private static void setField(object component, string name, object value) //테스트에서만 Inspector 설정 변경
        {
            component.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic).SetValue(component, value);
        }
    }
}
