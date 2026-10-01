using UnityEngine;

namespace CantResell
{
    public sealed class PlayerCombatComponent : MonoBehaviour
    {
        [SerializeField, Min(0.1f)] private float attackCooldown = 0.7f; //공격 사이의 대기 시간
        [SerializeField, Min(0.5f)] private float attackDistance = 1.7f; //근접 공격 거리
        private double nextAttack; //다음 공격 가능 시각

        public int calculateRemainingHealth(int health, int damage) //음수 피해와 체력 하한을 검증한 결과
        {
            return Mathf.Max(0, health - Mathf.Max(0, damage));
        }

        public bool tryAttack(Vector3 target, double now) //호스트의 근접 공격과 연타 검증
        {
            if (now < nextAttack || Vector3.Distance(transform.position, target) > attackDistance)
                return false;
            nextAttack = now + attackCooldown;
            return true;
        }
    }
}
