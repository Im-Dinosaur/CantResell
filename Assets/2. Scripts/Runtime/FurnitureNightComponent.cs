using System;
using System.Linq;
using UnityEngine;

namespace CantResell
{
    public sealed class FurnitureNightComponent : MonoBehaviour
    {
        [SerializeField, Min(1)] private float policeArrivalSeconds = 20; //신고부터 경찰 도착까지의 시간
        [SerializeField, Min(0.1f)] private float policeSpeed = 4.8f; //경찰 추격 속도
        [SerializeField, Min(0.1f)] private float captureDistance = 0.85f; //경찰 체포 거리
        [SerializeField, Min(1)] private float sensorPeriod = 6; //레이저의 작동 주기
        [SerializeField, Min(1)] private float disableSeconds = 10; //스위치로 센서를 끄는 시간
        private readonly bool[] escaped = new bool[4]; //각 참가자의 철수 상태
        private readonly Vector3[] previous = new Vector3[4]; //센서 통과 판정의 이전 위치
        private double started; //이번 밤 시작 시각
        private double policeDeadline; //경찰 도착 시각
        private double disabledUntil; //센서를 끄는 종료 시각
        public FurnitureState.Alarm alarm { get; private set; } //경보 진행
        public Vector3 ownerPosition { get; private set; } //집주인 위치
        public Vector3 policePosition { get; private set; } //경찰 위치
        public bool[] escapeSnapshot => (bool[])escaped.Clone(); //독립 철수 정보
        public bool allEscaped => escaped.All(value => value); //전원 철수 완료
        public static Vector3 truck => new Vector3(0, 0, -6); //가구 적재와 탈출 지점
        public static Vector3 sensorSwitch => new Vector3(-6.8f, 0, 1.5f); //입구의 센서 스위치
        public static Vector3 door => Vector3.zero; //집 출입문 위치
        public static Vector3 spawn(int slot) => new Vector3(-2 + slot * 1.3f, 0, -7.5f); //참가자별 시작 위치

        public void beginNight(double now, Player[] players) //센서와 집주인 및 철수 상태 초기화
        {
            Array.Clear(escaped, 0, escaped.Length);
            started = now;
            policeDeadline = 0;
            disabledUntil = 0;
            alarm = FurnitureState.Alarm.Quiet;
            ownerPosition = new Vector3(5.8f, 0, 14);
            policePosition = new Vector3(0, 0, -9);
            for (int slot = 0; slot < 4; slot++) //밤 시작 위치를 이전 좌표로 준비
                previous[slot] = players[slot] != null ? players[slot].transform.position : spawn(slot);
        }
        public bool isEscaped(int slot) //철수한 참가자의 입력과 체포 제외
        {
            return slot >= 0 && slot < 4 && escaped[slot];
        }
        public bool escape(int slot) //차량으로 돌아온 참가자의 철수 등록
        {
            if (slot < 0 || slot >= 4 || escaped[slot])
                return false;
            escaped[slot] = true;
            return true;
        }
        public bool sensorActive(double now) //호스트 시각 기준 레이저 켜짐
        {
            return now >= disabledUntil && (now - started) % Mathf.Max(1, sensorPeriod) < Mathf.Max(1, sensorPeriod) * 0.5;
        }
        public void disableSensors(double now) //스위치의 일시적인 센서 차단
        {
            disabledUntil = now + Mathf.Max(1, disableSeconds);
        }
        public float policeRemaining(double now) //신고 후 경찰 도착까지의 남은 시간
        {
            return alarm == FurnitureState.Alarm.Reporting ? (float)Math.Max(0, policeDeadline - now) : 0;
        }
        public bool triggerAlarm(double now) //처음 울린 알람만 집주인 신고 시작
        {
            if (alarm != FurnitureState.Alarm.Quiet)
                return false;
            alarm = FurnitureState.Alarm.Reporting;
            policeDeadline = now + Mathf.Max(1, policeArrivalSeconds);
            return true;
        }
        public int tick(double now, float delta, Player[] players) //센서 통과와 집주인 및 경찰 이동 뒤 체포 좌석 반환
        {
            for (int slot = 0; slot < 4; slot++) //동시에 움직이는 네 참가자 검사
            {
                if (players[slot] == null || escaped[slot])
                    continue;
                Vector3 position = players[slot].transform.position; //현재 참가자 위치
                if (sensorActive(now) && crossesSensor(previous[slot], position))
                    triggerAlarm(now);
                previous[slot] = position;
            }
            if (alarm == FurnitureState.Alarm.Reporting)
            {
                ownerPosition = Vector3.MoveTowards(ownerPosition, new Vector3(3, 0, 14), Mathf.Max(0, delta) * 1.8f);
                if (now >= policeDeadline)
                    alarm = FurnitureState.Alarm.Pursuit;
            }
            if (alarm != FurnitureState.Alarm.Pursuit)
                return -1;
            Player target = players.Where(player => player != null && !escaped[player.slot])
                .OrderBy(player => Vector3.Distance(player.transform.position, policePosition)).FirstOrDefault(); //가장 가까운 철수 전 참가자
            if (target == null)
                return -1;
            Vector3 destination = nextWaypoint(target.transform.position); //출입구와 외벽을 우회하는 추격 목적지
            policePosition = Vector3.MoveTowards(policePosition, destination, Mathf.Max(0, delta) * Mathf.Max(0.1f, policeSpeed));
            Vector3 difference = target.transform.position - policePosition; //높이를 제외한 체포 거리
            difference.y = 0;
            bool blocked = Physics.Linecast(policePosition + Vector3.up, target.transform.position + Vector3.up, out RaycastHit hit, ~0, QueryTriggerInteraction.Ignore) &&
                hit.collider.GetComponentInParent<Player>() != target; //벽을 사이에 둔 체포 방지
            return difference.magnitude <= captureDistance && !blocked ? target.slot : -1;
        }
        private Vector3 nextWaypoint(Vector3 target) //작은 고정 집의 출입구와 모서리로 벽 관통 방지
        {
            target.y = 0;
            Vector3[] points = { policePosition, target, new Vector3(0, 0, -1.2f), new Vector3(0, 0, 1.2f),
                new Vector3(-9, 0, -1.2f), new Vector3(9, 0, -1.2f), new Vector3(-9, 0, 16.6f), new Vector3(9, 0, 16.6f) }; //문과 외벽 모서리 경유점
            float[] distances = Enumerable.Repeat(float.PositiveInfinity, points.Length).ToArray(); //최단 경로 누적 거리
            int[] previousPoint = Enumerable.Repeat(-1, points.Length).ToArray(); //경로 역추적 정보
            bool[] visited = new bool[points.Length]; //검사 완료 경유점
            distances[0] = 0;
            for (int step = 0; step < points.Length; step++) //소규모 경유점 그래프 탐색
            {
                int current = -1; //가장 가까운 미방문 경유점
                for (int index = 0; index < points.Length; index++) //다음 탐색 대상 선택
                    if (!visited[index] && (current < 0 || distances[index] < distances[current]))
                        current = index;
                if (current < 0 || float.IsPositiveInfinity(distances[current]) || current == 1)
                    break;
                visited[current] = true;
                for (int next = 1; next < points.Length; next++) //벽이 없는 연결만 사용
                {
                    if (visited[next] || Physics.RaycastAll(points[current] + Vector3.up, (points[next] - points[current]).normalized,
                        Vector3.Distance(points[current], points[next]), ~0, QueryTriggerInteraction.Ignore).Any(hit => hit.collider.GetComponentInParent<Player>() == null))
                        continue;
                    float candidate = distances[current] + Vector3.Distance(points[current], points[next]); //현재 경로로 이동할 거리
                    if (candidate < distances[next])
                    {
                        distances[next] = candidate;
                        previousPoint[next] = current;
                    }
                }
            }
            int first = 1; //첫 이동 지점
            if (previousPoint[first] < 0)
                return policePosition;
            while (previousPoint[first] > 0)
                first = previousPoint[first];
            return points[first];
        }
        public static bool crossesSensor(Vector3 from, Vector3 to) //얇은 레이저를 한 틱에 지나친 이동도 검출
        {
            foreach (float line in new[] { 5.5f, 10f }) //집 안의 두 레이저 높이
            {
                float distance = to.z - from.z; //이동의 깊이 변화
                if (Mathf.Abs(distance) < 0.0001f)
                    continue;
                float fraction = (line - from.z) / distance; //레이저 교차 비율
                if (fraction < 0 || fraction > 1)
                    continue;
                float x = Mathf.Lerp(from.x, to.x, fraction); //교차 지점의 가로 위치
                if (Mathf.Abs(x) < 7.7f && (line > 6 || Mathf.Abs(x) > 1.5f))
                    return true;
            }
            return false;
        }
    }
}
