using System.Linq;
using UnityEngine;

namespace CantResell
{
    public sealed class FurnitureShopComponent : MonoBehaviour
    {
        [SerializeField, Min(1)] private float patienceSeconds = 40; //손님의 주문 대기 시간
        [SerializeField, Min(0)] private float arrivalSeconds = 4; //손님 사이의 입장 간격
        private double deadline; //주문 만료 또는 다음 입장 시각
        public int orderKind { get; private set; } = -1; //현재 주문 가구 종류
        public static Vector3 counter => new Vector3(0, 0, -3); //판매를 완료할 계산대

        public void beginDay(double now) //새 영업의 첫 손님 준비
        {
            orderKind = -1;
            deadline = now;
        }
        public void tick(double now, FurnitureInventoryComponent inventory) //재고에 맞는 손님 입장과 주문 만료
        {
            if (now < deadline)
                return;
            if (orderKind >= 0)
            {
                orderKind = -1;
                deadline = now + Mathf.Max(0, arrivalSeconds);
                return;
            }
            int[] available = inventory.all.Where(item => item.location == FurnitureState.Location.Shop || item.location == FurnitureState.Location.Carried)
                .Select(item => item.kind).Distinct().ToArray(); //판매 가능한 주문 종류
            if (available.Length > 0)
                orderKind = available[Random.Range(0, available.Length)];
            deadline = now + (orderKind >= 0 ? Mathf.Max(1, patienceSeconds) : 1);
        }
        public bool deliver(int slot, double now, string name, FurnitureInventoryComponent inventory, FurnitureEconomyComponent economy) //운반한 주문 가구를 한 번만 판매
        {
            FurnitureState.Furniture item = inventory.carried(slot); //손님에게 건네줄 상품
            if (item == null || orderKind < 0 || item.kind != orderKind || now >= deadline || !economy.settleSale(item.id, item.price, name))
                return false;
            item.location = FurnitureState.Location.Sold;
            item.carrier = -1;
            orderKind = -1;
            deadline = now + Mathf.Max(0, arrivalSeconds);
            return true;
        }
        public float remaining(double now) //손님의 남은 인내 시간
        {
            return orderKind >= 0 ? (float)System.Math.Max(0, deadline - now) : 0;
        }
    }
}
