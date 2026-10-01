using System;
using System.Collections.Generic;
using UnityEngine;

namespace CantResell
{
    public sealed class AuctionObjectiveComponent : MonoBehaviour
    {
        [Serializable]
        public sealed class Goal
        {
            public string title; //개인 목표 이름
            public AuctionState.ItemKind core; //50점 핵심 상품
            public AuctionState.ItemKind support; //30점 보조 상품
            public AuctionState.ItemKind decoration; //20점 장식 상품
        }

        [SerializeField] private Goal[] goals =
        {
            new Goal { title = "홈 카페", core = AuctionState.ItemKind.CoffeeMachine, support = AuctionState.ItemKind.Table, decoration = AuctionState.ItemKind.Lamp },
            new Goal { title = "음악방", core = AuctionState.ItemKind.Turntable, support = AuctionState.ItemKind.Speaker, decoration = AuctionState.ItemKind.Lamp },
            new Goal { title = "작업실", core = AuctionState.ItemKind.Tools, support = AuctionState.ItemKind.Workbench, decoration = AuctionState.ItemKind.Cabinet },
            new Goal { title = "편안한 방", core = AuctionState.ItemKind.Bed, support = AuctionState.ItemKind.Heater, decoration = AuctionState.ItemKind.Lamp }
        }; //동일한 100점 만점의 목표 도감
        private readonly int[] assignments = { 0, 1, 2, 3 }; //좌석별 비공개 목표

        public void resetMatch() //서로 다른 목표를 무작위 배정
        {
            for (int index = 0; index < assignments.Length; index++) //기본 목표 순서
                assignments[index] = index;
            for (int index = assignments.Length - 1; index > 0; index--) //목표 순서 섞기
            {
                int other = UnityEngine.Random.Range(0, index + 1); //교환할 목표
                (assignments[index], assignments[other]) = (assignments[other], assignments[index]);
            }
        }

        public Goal getGoal(int slot) //본인 또는 결과용 목표 조회
        {
            return goals[assignments[slot]];
        }

        public string describeGoal(int slot) //상품별 배점과 품질 반영 안내
        {
            Goal goal = getGoal(slot); //좌석의 목표
            return AuctionItemComponent.itemName(goal.core) + " 50점\n" + AuctionItemComponent.itemName(goal.support) +
                " 30점\n" + AuctionItemComponent.itemName(goal.decoration) + " 20점\n성능 비율만큼 점수 · 중복은 최고 성능만";
        }

        public int calculateScore(int slot, IEnumerable<AuctionState.Item> inventory) //상품별 최고 성능으로 목표 달성도 계산
        {
            Goal goal = getGoal(slot); //점수 계산할 목표
            int core = 0, support = 0, decoration = 0; //각 종류의 최고 성능
            foreach (AuctionState.Item item in inventory) //플레이어의 보관품
            {
                if (item.owner != slot || item.status != AuctionState.ItemStatus.Stored)
                    continue;
                int quality = Mathf.Clamp(item.condition, 0, 100); //유효 성능
                if (item.kind == goal.core) core = Mathf.Max(core, quality);
                if (item.kind == goal.support) support = Mathf.Max(support, quality);
                if (item.kind == goal.decoration) decoration = Mathf.Max(decoration, quality);
            }
            return core * 50 / 100 + support * 30 / 100 + decoration * 20 / 100;
        }
    }
}