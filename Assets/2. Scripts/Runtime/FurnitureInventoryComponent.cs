using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace CantResell
{
    public sealed class FurnitureInventoryComponent : MonoBehaviour
    {
        [SerializeField, Range(1, 8)] private int startingStock = 6; //첫날에 판매할 가구
        [SerializeField, Range(1, 10)] private int furniturePerHouse = 8; //밤마다 집에 놓을 가구
        private readonly List<FurnitureState.Furniture> items = new List<FurnitureState.Furniture>(); //호스트의 공동 재고
        private int nextId; //다음 가구 식별자
        public IEnumerable<FurnitureState.Furniture> all => items; //게임 내부 가구
        public static readonly string[] names = { "의자", "조명", "테이블", "수납장", "소파", "침대" }; //시제품 가구 이름
        private static readonly int[] prices = { 25, 35, 50, 60, 80, 100 }; //시작 재고만으로 목표를 채울 수 없는 테스트 가격

        public void resetMatch() //처음부터 사용할 공동 재고 준비
        {
            items.Clear();
            nextId = 0;
            for (int index = 0; index < Mathf.Clamp(startingStock, 1, 8); index++) //시작 재고
                add(index % names.Length, FurnitureState.Location.Shop, shopPosition(index));
        }
        public void beginNight() //새 집의 가구를 생성하고 과거 판매품 정리
        {
            items.RemoveAll(item => item.location != FurnitureState.Location.Shop);
            for (int index = 0; index < Mathf.Clamp(furniturePerHouse, 1, 10); index++) //침입 장소의 가구
                add(UnityEngine.Random.Range(0, names.Length), FurnitureState.Location.House, housePosition(index));
        }
        private void add(int kind, FurnitureState.Location location, Vector3 position) //고유 번호와 가격을 가진 가구 등록
        {
            items.Add(new FurnitureState.Furniture { id = ++nextId, kind = kind, price = prices[kind], location = location, position = position });
        }
        public FurnitureState.Furniture get(int id) //고유 번호로 가구 조회
        {
            return items.FirstOrDefault(item => item.id == id);
        }
        public FurnitureState.Furniture carried(int slot) //참가자가 운반하는 가구 조회
        {
            return items.FirstOrDefault(item => item.location == FurnitureState.Location.Carried && item.carrier == slot);
        }
        public bool pickUp(int id, int slot, bool night) //동일 가구와 운반자의 중복 운반 방지
        {
            FurnitureState.Furniture item = get(id); //대상 가구
            if (slot < 0 || slot >= 4 || item == null || carried(slot) != null ||
                item.location != (night ? FurnitureState.Location.House : FurnitureState.Location.Shop))
                return false;
            item.location = FurnitureState.Location.Carried;
            item.carrier = slot;
            return true;
        }
        public bool drop(int slot, bool night, Vector3 position) //현재 장소에 운반품 내려놓기
        {
            FurnitureState.Furniture item = carried(slot); //운반 중인 가구
            if (item == null)
                return false;
            item.location = night ? FurnitureState.Location.House : FurnitureState.Location.Shop;
            item.carrier = -1;
            item.position = position;
            return true;
        }
        public bool loadTruck(int slot) //차량에 가구 적재
        {
            FurnitureState.Furniture item = carried(slot); //차량으로 옮길 가구
            if (item == null)
                return false;
            item.location = FurnitureState.Location.Truck;
            item.carrier = -1;
            item.position = new Vector3(-2 + items.Count(value => value.location == FurnitureState.Location.Truck) * 0.5f, 0, -6);
            return true;
        }
        public void finishNight(bool caught) //성공 적재품을 재고로 바꾸거나 이번 밤 가구 몰수
        {
            int index = items.Count(item => item.location == FurnitureState.Location.Shop); //기존 재고 다음 진열 위치
            foreach (FurnitureState.Furniture item in items) //이번 밤 정리할 가구
                if (item.location != FurnitureState.Location.Shop)
                {
                    item.location = !caught && item.location == FurnitureState.Location.Truck ? FurnitureState.Location.Shop : FurnitureState.Location.Sold;
                    item.carrier = -1;
                    if (item.location == FurnitureState.Location.Shop)
                        item.position = shopPosition(index++);
                }
            items.RemoveAll(item => item.location == FurnitureState.Location.Sold);
            arrangeShop();
        }
        public void arrangeShop() //다음 영업 전 운반품 회수와 재고 진열
        {
            int index = 0; //진열할 재고 순서
            foreach (FurnitureState.Furniture item in items.Where(item => item.location != FurnitureState.Location.Sold))
            {
                item.location = FurnitureState.Location.Shop;
                item.carrier = -1;
                item.position = shopPosition(index++);
            }
        }
        public FurnitureState.Furniture[] snapshot() //공유 상태를 독립 복사
        {
            return items.Where(item => item.location != FurnitureState.Location.Sold).Select(item => new FurnitureState.Furniture
            { id = item.id, kind = item.kind, price = item.price, location = item.location, carrier = item.carrier, position = item.position }).ToArray();
        }
        public static Vector3 shopPosition(int index) //매장 안쪽의 재고 위치
        {
            return new Vector3(-5 + index % 6 * 2, 0, 2 + index / 6 * 1.5f);
        }
        public static Vector3 housePosition(int index) //센서 양쪽에 놓일 침입 가구 위치
        {
            return new Vector3(index % 2 == 0 ? -5 : 5, 0, 3 + index / 2 * 3);
        }
    }
}
