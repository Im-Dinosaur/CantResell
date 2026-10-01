using System.Collections.Generic;
using UnityEngine;

namespace CantResell
{
    public sealed class AuctionEconomyComponent : MonoBehaviour
    {
        [SerializeField, Min(1)] private int startingCash = 300; //게임 시작 때만 지급할 자금
        private readonly int[] balances = new int[4]; //좌석별 잔액
        private readonly HashSet<int> settledRounds = new HashSet<int>(); //중복 정산 방지 번호

        public void resetMatch() //초기 자금과 거래 기록 준비
        {
            settledRounds.Clear();
            for (int index = 0; index < balances.Length; index++) //초기화할 좌석
                balances[index] = Mathf.Max(1, startingCash);
        }

        public int getBalance(int slot) //좌석의 현재 잔액 조회
        {
            return slot >= 0 && slot < 4 ? balances[slot] : 0;
        }

        public bool canSettle(int round, int seller, int buyer, int price) //거래 검증과 오버플로 방지
        {
            if (round < 0 || seller < 0 || seller >= 4 || settledRounds.Contains(round))
                return false;
            if (buyer == -1)
                return price == 0;
            return buyer >= 0 && buyer < 4 && buyer != seller && price > 0 &&
                price <= balances[buyer] && (long)balances[seller] + price <= int.MaxValue;
        }

        public bool settleRound(int round, int seller, int buyer, int price) //추가 보상 없이 낙찰액만 이동
        {
            if (!canSettle(round, seller, buyer, price))
                return false;
            settledRounds.Add(round);
            if (buyer >= 0)
            {
                balances[buyer] -= price;
                balances[seller] += price;
            }
            return true;
        }
    }
}