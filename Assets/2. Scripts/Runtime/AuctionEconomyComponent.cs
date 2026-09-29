using System.Collections.Generic;
using UnityEngine;

namespace CantResell
{
    public sealed class AuctionEconomyComponent : MonoBehaviour
    {
        [SerializeField, Range(1, 100000)] private int startingCash = 100; //시작 자금
        [SerializeField, Range(0, 100000)] private int normalReward = 120; //정상 상품 구매 보상
        private readonly int[] balances = new int[4]; //호스트가 관리하는 소지금
        private readonly HashSet<int> settledRounds = new HashSet<int>(); //중복 정산 방지 기록
        public int reward => Mathf.Clamp(normalReward, 0, 100000); //검증된 정상 상품 보상

        public void resetMatch() //자금과 정산 기록 초기화
        {
            settledRounds.Clear();
            for (int slot = 0; slot < balances.Length; slot++) //초기화할 좌석 번호
                balances[slot] = Mathf.Clamp(startingCash, 1, 100000);
        }

        public int getBalance(int slot) //좌석별 소지금 반환
        {
            return slot >= 0 && slot < 4 ? balances[slot] : 0;
        }

        public bool settleRound(int round, int seller, int buyer, int price, bool good) //거래와 보상을 라운드당 한 번만 정산
        {
            if (round < 0 || round >= 4 || seller < 0 || seller >= 4 || settledRounds.Contains(round))
                return false;
            if (buyer == -1 && price == 0)
                return settledRounds.Add(round);
            if (buyer < 0 || buyer >= 4 || buyer == seller || price <= 0 || price > balances[buyer])
                return false;
            balances[buyer] -= price;
            balances[seller] += price;
            if (good)
                balances[buyer] += reward;
            settledRounds.Add(round);
            return true;
        }
    }
}
