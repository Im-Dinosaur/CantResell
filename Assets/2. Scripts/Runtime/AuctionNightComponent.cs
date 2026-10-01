using System;
using System.Linq;
using UnityEngine;

namespace CantResell
{
    public sealed class AuctionNightComponent : MonoBehaviour
    {
        public int[] order { get; private set; } = Array.Empty<int>(); //이번 밤의 무작위 순서
        public int turn { get; private set; } = -1; //밤 행동 순번
        public int activeSlot => turn >= 0 && turn < order.Length ? order[turn] : -1; //집을 나갈 수 있는 침입자

        private readonly int[] doorStrength = new int[4]; //호스트의 자물쇠 내구도
        private readonly bool[] doorOpen = new bool[4]; //호스트의 출입문 상태
        private readonly bool[] brokenDoors = new bool[4]; //이번 밤에 파괴한 자물쇠
        private AuctionInventoryComponent inventoryComponent; //소유권과 도구 조회 담당
        private AuctionItemViewComponent viewComponent; //실제 집과 보관품 위치 담당
        private Player[] pawns; //캐릭터 파사드 참조
        private Action publishState; //문과 운반 변경의 상태 전송 호출

        public void initialize(AuctionInventoryComponent inventory, AuctionItemViewComponent view, Player[] players, Action changed) //진입점에서 밤 행동의 의존 관계 연결
        {
            inventoryComponent = inventory;
            viewComponent = view;
            pawns = players;
            publishState = changed;
        }

        public void copyDoorsTo(AuctionState state) //전송 데이터에 문 상태 복사
        {
            state.doorStrength = (int[])doorStrength.Clone();
            state.doorOpen = (bool[])doorOpen.Clone();
        }

        public bool simulatePlayer(Player player, Vector2 direction, bool interact, bool lockDoor, bool attack, double now) //밤의 이동·문·운반·방어를 처리하고 턴 종료 여부 반환
        {
            int slot = player.slot; //검증된 캐릭터 좌석
            PlayerHouse home = viewComponent.getHouse(slot); //자기 집 경계
            if (home == null)
                return false;
            player.movePlayer(direction, canLeaveHouse(slot) ? null : home);
            if (lockDoor)
                toggleHouseLock(player, home, now);
            if (interact)
                interactAtNight(player, now);
            if (attack && attackAtNight(player, now))
                return true;
            AuctionState.Item carried = inventoryComponent.getCarried(slot); //현재 운반품
            player.carriedId = carried?.id ?? 0;
            if (slot == activeSlot && carried != null && home.contains(player.transform.position))
            {
                inventoryComponent.finishCarry(slot, true);
                player.carriedId = 0;
                return true;
            }
            return false;
        }

        public void resetMatch() //밤 진행 초기화
        {
            order = Array.Empty<int>();
            turn = -1;
            Array.Clear(doorStrength, 0, doorStrength.Length);
            Array.Clear(doorOpen, 0, doorOpen.Length);
            Array.Clear(brokenDoors, 0, brokenDoors.Length);
        }

        public void beginNight() //각 플레이어에게 한 번씩 무작위 행동 기회
        {
            order = new[] { 0, 1, 2, 3 };
            for (int index = 3; index > 0; index--) //좌석 순서 섞기
            {
                int other = UnityEngine.Random.Range(0, index + 1); //교환할 좌석
                (order[index], order[other]) = (order[other], order[index]);
            }
            turn = 0;
            for (int slot = 0; slot < 4; slot++) //이번 밤에 보유한 자물쇠 준비
            {
                doorStrength[slot] = inventoryComponent != null ? inventoryComponent.getToolStrength(slot, AuctionState.ItemKind.Lock) : 0;
                doorOpen[slot] = false;
                brokenDoors[slot] = false;
            }
        }

        public bool nextTurn() //현재 턴 종료 후 다음 참가자 선택
        {
            if (turn < 0 || turn >= order.Length)
                return false;
            return ++turn < order.Length;
        }

        private void toggleHouseLock(Player player, PlayerHouse home, double now) //집 주인이 가진 자물쇠로 출입문 잠금
        {
            if (!player.canInteract(home.doorPosition, now) || brokenDoors[player.slot])
                return;
            int strength = inventoryComponent.getToolStrength(player.slot, AuctionState.ItemKind.Lock); //보유한 자물쇠 성능
            if (strength <= 0)
                return;
            if (doorStrength[player.slot] > 0)
                doorStrength[player.slot] = 0;
            else
            {
                if (pawns.Any(other => other != null && Vector3.Distance(other.transform.position, home.doorPosition) < 0.75f))
                    return;
                doorStrength[player.slot] = strength;
                doorOpen[player.slot] = false;
            }
            publishState();
        }

        private void interactAtNight(Player player, double now) //근처 문 또는 보관품과 실제 거리로 상호작용
        {
            PlayerHouse nearest = Enumerable.Range(0, 4).Select(viewComponent.getHouse)
                .OrderBy(home => Vector3.Distance(home.doorPosition, player.transform.position)).First(); //가장 가까운 문
            AuctionState.Item closestItem = player.slot == activeSlot && inventoryComponent.getCarried(player.slot) == null ?
                inventoryComponent.allItems.Where(item => item.status == AuctionState.ItemStatus.Stored && item.owner != player.slot &&
                    viewComponent.getHouse(item.owner).contains(player.transform.position))
                    .OrderBy(item => Vector3.Distance(player.transform.position, viewComponent.getItemPosition(item, inventoryComponent.allItems))).FirstOrDefault() : null; //집 안에서 가장 가까운 상대 상품
            float itemDistance = closestItem != null ? Vector3.Distance(player.transform.position, viewComponent.getItemPosition(closestItem, inventoryComponent.allItems)) : float.PositiveInfinity; //문과 상품의 거리 비교
            if (Vector3.Distance(nearest.doorPosition, player.transform.position) <= itemDistance && player.canInteract(nearest.doorPosition, now))
            {
                int owner = nearest.owner; //문 주인
                if (owner != player.slot && player.slot != activeSlot)
                    return;
                if (doorStrength[owner] > 0)
                {
                    if (owner == player.slot)
                        return;
                    int power = inventoryComponent.getToolStrength(player.slot, AuctionState.ItemKind.Crowbar); //지렛대 성능
                    if (power <= 0)
                        return;
                    doorStrength[owner] = Mathf.Max(0, doorStrength[owner] - power / 2);
                    if (doorStrength[owner] == 0)
                    {
                        brokenDoors[owner] = true;
                        doorOpen[owner] = true;
                    }
                }
                else if (!doorOpen[owner] || !pawns.Any(other => other != null && Vector3.Distance(other.transform.position, nearest.doorPosition) < 0.75f))
                    doorOpen[owner] = !doorOpen[owner];
                publishState();
                return;
            }
            if (player.slot != activeSlot || inventoryComponent.getCarried(player.slot) != null)
                return;
            if (closestItem != null) //가까운 보관품 하나만 조작
            {
                Vector3 position = viewComponent.getItemPosition(closestItem, inventoryComponent.allItems); //실제 보관품 위치
                if (player.canInteract(position, now) && inventoryComponent.beginCarry(closestItem.id, player.slot))
                {
                    player.carriedId = closestItem.id;
                    publishState();
                    return;
                }
            }
        }

        private bool attackAtNight(Player player, double now) //자기 집 안에서 망치로 침입자 방어
        {
            int active = activeSlot; //이번 턴 침입자
            if (active < 0 || player.slot == active || pawns[active] == null)
                return false;
            Player target = pawns[active]; //현재 침입자 캐릭터
            PlayerHouse house = viewComponent.getHouse(player.slot); //방어자의 집
            int strength = inventoryComponent.getToolStrength(player.slot, AuctionState.ItemKind.Hammer); //보관한 방어 무기 성능
            if (strength <= 0 || !house.contains(target.transform.position, 3) || !player.tryAttack(target, now))
                return false;
            target.applyDamage(Mathf.Max(1, strength * 40 / 100));
            return target.health == 0;
        }

        public bool canLeaveHouse(int slot) //현재 행동권 소유 여부
        {
            return slot >= 0 && slot == activeSlot;
        }
    }
}
