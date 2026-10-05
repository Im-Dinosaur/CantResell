using System;
using System.Collections.Generic;
using UnityEngine;

namespace CantResell
{
    public sealed class FurnitureEconomyComponent : MonoBehaviour
    {
        [SerializeField, Min(0)] private int startingCash = 300; //팀의 시작 공금
        [SerializeField, Min(1)] private int goalCash = 1000; //마지막 영업의 목표 공금
        [SerializeField, Range(0, 1)] private float fineRatio = 0.25f; //체포 때 잃는 공금 비율
        [SerializeField, Range(0, 1)] private float winChance = 0.5f; //두 배 지급 도박의 승률
        [SerializeField, Min(0.1f)] private float gambleSeconds = 2; //한 번의 도박 연출 간격
        private readonly Queue<string> history = new Queue<string>(); //최근 거래 내역
        private readonly HashSet<int> sales = new HashSet<int>(); //중복 판매 정산 방지
        private readonly HashSet<int> fines = new HashSet<int>(); //밤별 중복 벌금 방지
        private readonly double[] nextBet = new double[4]; //참가자별 다음 도박 시각
        public int cash { get; private set; } //공동 잔액
        public int target => Mathf.Max(1, goalCash); //공동 목표
        public string[] ledger => history.ToArray(); //표시할 거래 내역

        public void resetMatch() //공금과 정산 기록 초기화
        {
            cash = Mathf.Max(0, startingCash);
            history.Clear();
            sales.Clear();
            fines.Clear();
            Array.Clear(nextBet, 0, nextBet.Length);
            record("공동 가게 시작: " + cash + " 코인");
        }
        public bool settleSale(int id, int price, string playerName) //가구 하나의 판매 수입을 한 번만 지급
        {
            if (id <= 0 || price <= 0 || sales.Contains(id) || (long)cash + price > int.MaxValue)
                return false;
            sales.Add(id);
            cash += price;
            record(playerName + " 판매 +" + price + " → " + cash);
            return true;
        }
        public bool placeBet(int slot, int wager, double now, string playerName, out string message) //같은 공금으로 검증된 도박 한 번 정산
        {
            message = "공금과 베팅 금액을 확인하세요.";
            if (slot < 0 || slot >= 4 || wager <= 0 || wager > cash || now < nextBet[slot] ||
                (long)cash + wager > int.MaxValue || !double.IsFinite(now))
                return false;
            nextBet[slot] = now + Mathf.Max(0.1f, gambleSeconds);
            bool won = UnityEngine.Random.value < winChance; //호스트에서만 결정할 결과
            cash += won ? wager : -wager;
            message = playerName + " 도박 " + (won ? "+" : "−") + wager + " → 공금 " + cash;
            record(message);
            return true;
        }
        public int chargeFine(int day) //같은 체포를 중복 차감하지 않는 벌금
        {
            if (day <= 0 || !fines.Add(day))
                return 0;
            int fine = (int)Math.Min(cash, Math.Ceiling(cash * (double)Mathf.Clamp01(fineRatio))); //유효 잔액 안의 올림 벌금
            cash -= fine;
            record(day + "일 밤 체포 −" + fine + " → " + cash);
            return fine;
        }
        private void record(string entry) //표시와 네트워크 크기를 제한한 거래 기록
        {
            history.Enqueue(entry);
            while (history.Count > 8)
                history.Dequeue();
        }
    }
}
