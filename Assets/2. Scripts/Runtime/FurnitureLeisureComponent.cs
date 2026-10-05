using System;
using UnityEngine;

namespace CantResell
{
    public sealed class FurnitureLeisureComponent : MonoBehaviour
    {
        [SerializeField, Min(0.5f)] private float cycleSeconds = 2; //타이밍 막대의 왕복 주기
        private readonly int[] scores = new int[4]; //참가자의 최고 기록
        private readonly double[] nextPlay = new double[4]; //기록 연타 제한
        public static Vector3 machine => new Vector3(11, 0, -3); //무료 놀이 기계 위치
        public int[] records => (int[])scores.Clone(); //화면에 표시할 독립 기록
        public float cycle => Mathf.Max(0.5f, cycleSeconds); //막대가 한쪽 끝까지 이동할 시간
        public float clock(double now) //클라이언트 보간을 위한 왕복 진행값
        {
            return (float)(now % (cycle * 2)) / cycle;
        }
        public float phase(double now) //가운데를 맞출 타이밍 막대 위치
        {
            return Mathf.PingPong(clock(now), 1);
        }
        public void resetMatch() //최고 기록과 입력 간격 초기화
        {
            Array.Clear(scores, 0, scores.Length);
            Array.Clear(nextPlay, 0, nextPlay.Length);
        }
        public int play(int slot, double now) //돈을 사용하지 않고 타이밍 기록 갱신
        {
            if (slot < 0 || slot >= 4 || now < nextPlay[slot])
                return -1;
            nextPlay[slot] = now + 0.5;
            int score = Mathf.RoundToInt((1 - Mathf.Abs(phase(now) - 0.5f) * 2) * 100); //가운데에 가까울수록 높은 기록
            scores[slot] = Mathf.Max(scores[slot], score);
            return score;
        }
    }
}
