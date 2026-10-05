using System;
using UnityEngine;

namespace CantResell
{
    [Serializable]
    public sealed class FurnitureState
    {
        public enum Location { Shop, House, Carried, Truck, Sold }
        public enum Alarm { Quiet, Reporting, Pursuit }
        [Serializable]
        public sealed class Furniture
        {
            public int id; //가구 식별자
            public int kind; //가구 종류
            public int price; //판매 가격
            public Location location; //가구 보관 상태
            public int carrier = -1; //운반 중인 참가자
            public Vector3 position; //바닥의 가구 위치
        }
        public int day; //현재 영업일
        public int days; //전체 영업일
        public int cash; //모두가 사용하는 공금
        public int targetCash; //마지막 마감의 목표 공금
        public int orderKind = -1; //손님이 주문한 가구 종류
        public float orderSeconds; //현재 손님이 기다릴 시간
        public Alarm alarm; //집주인 신고와 경찰 진행
        public float policeSeconds; //경찰 도착까지 남은 시간
        public bool sensorActive; //레이저 센서의 작동 상태
        public bool doorOpen; //집 출입문 열림
        public float leisurePhase; //무료 놀이의 현재 막대 위치
        public float leisureClock; //무료 놀이의 왕복 진행값
        public float leisureCycle = 2; //무료 놀이의 편도 이동 시간
        public Vector3 ownerPosition; //집주인의 위치
        public Vector3 policePosition; //경찰의 위치
        public bool[] escaped = new bool[4]; //차량으로 철수한 참가자
        public int[] playScores = new int[4]; //무료 놀이의 최고 기록
        public string[] ledger = Array.Empty<string>(); //최근 공동 자금 거래
        public Furniture[] furniture = Array.Empty<Furniture>(); //공개 가구 재고
        public bool success; //최종 공동 목표 달성 여부
        public string outcome; //밤 종료 또는 결과 설명
    }
}
