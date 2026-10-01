using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace CantResell
{
    public sealed class AuctionInventoryComponent : MonoBehaviour
    {
        private readonly Dictionary<int, AuctionState.Item> items = new Dictionary<int, AuctionState.Item>(); //호스트만 보관하는 상품 진실
        private readonly Dictionary<int, int> knowledge = new Dictionary<int, int>(); //상품별 열람 가능한 좌석 비트
        public IEnumerable<AuctionState.Item> allItems => items.Values; //정산과 목표 계산의 내부 상품

        public void resetMatch() //상품과 비밀 열람 기록 제거
        {
            items.Clear();
            knowledge.Clear();
        }

        public bool addStock(AuctionState.Item item) //중복 없는 새 판매 상품 등록
        {
            if (item == null || item.id <= 0 || item.owner < 0 || item.owner >= 4 || items.ContainsKey(item.id))
                return false;
            items.Add(item.id, item);
            knowledge.Add(item.id, 1 << item.owner);
            return true;
        }

        public AuctionState.Item getItem(int id) //내부 상품 조회
        {
            return items.TryGetValue(id, out AuctionState.Item item) ? item : null;
        }

        public bool settleLot(int id, int buyer) //낙찰자에게만 원가와 상태 공개
        {
            AuctionState.Item item = getItem(id); //현재 판매품
            if (item == null || item.status != AuctionState.ItemStatus.Stock || buyer < -1 || buyer >= 4 || buyer == item.owner)
                return false;
            if (buyer < 0)
                item.status = AuctionState.ItemStatus.Retired;
            else
            {
                item.owner = buyer;
                item.status = AuctionState.ItemStatus.Stored;
                knowledge[id] |= 1 << buyer;
            }
            return true;
        }

        public bool knowsItem(int id, int slot) //판매자 또는 낙찰자였는지 확인
        {
            return slot >= 0 && slot < 4 && knowledge.TryGetValue(id, out int mask) && (mask & (1 << slot)) != 0;
        }

        public AuctionState.Item[] createSnapshot(int slot) //수신자의 열람 권한으로 가린 복사본
        {
            return items.Values.Where(item => item.status != AuctionState.ItemStatus.Retired).OrderBy(item => item.id).Select(item =>
            {
                bool known = knowsItem(item.id, slot); //수신자 열람 권한
                return new AuctionState.Item { id = item.id, kind = item.kind, owner = item.owner, status = item.status,
                    carrier = item.carrier, known = known, cost = known ? item.cost : 0, condition = known ? item.condition : 0 };
            }).ToArray();
        }

        public bool beginCarry(int id, int thief) //소유권을 유지하며 상품 하나 운반 시작
        {
            AuctionState.Item item = getItem(id); //훔칠 보관품
            if (thief < 0 || thief >= 4 || item == null || item.owner == thief ||
                item.status != AuctionState.ItemStatus.Stored || getCarried(thief) != null)
                return false;
            item.status = AuctionState.ItemStatus.Carried;
            item.carrier = thief;
            return true;
        }

        public AuctionState.Item getCarried(int slot) //현재 운반 중인 상품 조회
        {
            return items.Values.FirstOrDefault(item => item.status == AuctionState.ItemStatus.Carried && item.carrier == slot);
        }

        public bool finishCarry(int slot, bool escaped) //귀가 성공 때만 소유권 변경
        {
            AuctionState.Item item = getCarried(slot); //정리할 운반품
            if (item == null)
                return false;
            if (escaped)
                item.owner = slot;
            item.status = AuctionState.ItemStatus.Stored;
            item.carrier = -1;
            return true;
        }

        public int getToolStrength(int slot, AuctionState.ItemKind kind) //보관한 도구 중 최고 성능
        {
            return items.Values.Where(item => item.owner == slot && item.status == AuctionState.ItemStatus.Stored && item.kind == kind)
                .Select(item => item.condition).DefaultIfEmpty(0).Max();
        }
    }
}