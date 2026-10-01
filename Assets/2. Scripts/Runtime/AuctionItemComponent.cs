using System;
using System.Collections.Generic;
using UnityEngine;

namespace CantResell
{
    public sealed class AuctionItemComponent : MonoBehaviour
    {
        [Serializable]
        public sealed class Definition
        {
            public AuctionState.ItemKind kind; //상품 종류
            public string description; //공개 용도
            public int minimumCost = 30; //원가 최솟값
            public int maximumCost = 100; //원가 최댓값
        }

        [SerializeField] private Definition[] definitions = createDefaults(); //공개 상품 도감
        [SerializeField, Range(0, 1)] private float normalChance = 0.65f; //정상 상품 확률
        [SerializeField, Range(0, 1)] private float wornChance = 0.25f; //낡은 상품 확률
        private readonly Queue<AuctionState.ItemKind> bag = new Queue<AuctionState.ItemKind>(); //중복 편중을 줄이는 무작위 상품 주머니
        public Definition[] catalog => definitions; //UI에 제공할 공개 상품 종류

        public void resetMatch() //게임마다 상품 추첨 순서 초기화
        {
            bag.Clear();
        }

        public AuctionState.Item createItem(int id, int seller) //판매 턴에 상품 하나 발급
        {
            if (bag.Count == 0)
            {
                List<AuctionState.ItemKind> kinds = new List<AuctionState.ItemKind>(); //유효 상품 종류
                foreach (Definition definition in definitions) //도감의 상품
                    if (definition != null && !kinds.Contains(definition.kind))
                        kinds.Add(definition.kind);
                if (kinds.Count == 0)
                    throw new InvalidOperationException("경매 상품 도감이 비어 있습니다.");
                for (int index = kinds.Count - 1; index > 0; index--) //추첨 주머니 섞기
                {
                    int other = UnityEngine.Random.Range(0, index + 1); //교환할 위치
                    (kinds[index], kinds[other]) = (kinds[other], kinds[index]);
                }
                foreach (AuctionState.ItemKind kind in kinds) //추첨 순서 보관
                    bag.Enqueue(kind);
            }
            AuctionState.ItemKind selected = bag.Dequeue(); //이번 판매 상품
            Definition config = Array.Find(definitions, value => value != null && value.kind == selected); //상품별 원가 범위
            int minimum = Mathf.Clamp(config.minimumCost, 1, 1000000); //안전한 원가 하한
            int maximum = Mathf.Clamp(config.maximumCost, minimum, 1000000); //안전한 원가 상한
            float roll = UnityEngine.Random.value; //상태 추첨 값
            return new AuctionState.Item { id = id, kind = selected, owner = seller, status = AuctionState.ItemStatus.Stock,
                cost = UnityEngine.Random.Range(minimum, maximum + 1), condition = roll < normalChance ? 100 : roll < normalChance + wornChance ? 50 : 0, known = true };
        }

        public static string itemName(AuctionState.ItemKind kind) //상품의 공개 이름
        {
            string[] names = { "커피 머신", "테이블", "조명", "턴테이블", "스피커", "공구", "작업대", "수납장", "침대", "난방기", "자물쇠", "고무 망치", "쇠지렛대" }; //도감 이름
            return (int)kind >= 0 && (int)kind < names.Length ? names[(int)kind] : "알 수 없는 상품";
        }

        public static string conditionName(int condition) //비공개 성능 설명
        {
            return condition >= 100 ? "정상 · 성능 100%" : condition > 0 ? "낡음 · 성능 50%" : "고장 · 성능 0%";
        }

        private static Definition[] createDefaults() //집들이 경매의 전체 상품 구성
        {
            string[] descriptions = { "홈 카페의 핵심", "홈 카페의 보조", "모든 방의 장식", "음악방의 핵심", "음악방의 보조",
                "작업실의 핵심", "작업실의 보조", "작업실의 장식", "편안한 방의 핵심", "편안한 방의 보조",
                "밤에 문을 잠금", "가까운 침입자 방어", "상대의 자물쇠 파괴" }; //공개 용도
            Definition[] result = new Definition[descriptions.Length]; //전체 도감
            for (int index = 0; index < result.Length; index++) //도감의 각 상품
                result[index] = new Definition { kind = (AuctionState.ItemKind)index, description = descriptions[index],
                    minimumCost = index >= 10 ? 20 : 35, maximumCost = index >= 10 ? 65 : 120 };
            return result;
        }
    }
}