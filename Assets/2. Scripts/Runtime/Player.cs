using Fusion;
using UnityEngine;

namespace CantResell
{
    [RequireComponent(typeof(PlayerInputComponent), typeof(PlayerMovementComponent), typeof(PlayerInteractionComponent))]
    [RequireComponent(typeof(PlayerCombatComponent))]
    public sealed class Player : NetworkBehaviour
    {
        [SerializeField] private Renderer body; //낮의 스킨과 밤의 공통 실루엣
        [SerializeField] private GameObject carriedVisual; //운반 중인 상품 표시
        [Networked] public int slot { get; set; } //호스트가 배정한 좌석
        [Networked] public int health { get; set; } //이번 밤 행동의 체력
        [Networked] public int carriedId { get; set; } //운반 중인 상품 번호
        [Networked] private NetworkButtons previousButtons { get; set; } //버튼 첫 입력 확인
        private PlayerMovementComponent movement; //이동 처리 담당
        private PlayerInputComponent inputComponent; //로컬 입력 수집 담당
        private PlayerInteractionComponent interaction; //거리와 상호작용 담당
        private PlayerCombatComponent combat; //방어 공격 담당
        private MaterialPropertyBlock colorBlock; //공유 머티리얼을 보존할 색상 설정

        public override void Spawned() //파사드 구성과 게임에 실제 캐릭터 등록
        {
            movement = GetComponent<PlayerMovementComponent>();
            inputComponent = GetComponent<PlayerInputComponent>();
            interaction = GetComponent<PlayerInteractionComponent>();
            combat = GetComponent<PlayerCombatComponent>();
            colorBlock = new MaterialPropertyBlock();
            AuctionGame.current?.registerPlayer(this);
        }

        public override void Despawned(NetworkRunner runner, bool hasState) //게임의 캐릭터 참조 해제
        {
            AuctionGame.current?.unregisterPlayer(this);
        }

        public override void FixedUpdateNetwork() //호스트만 실제 이동과 상호작용 판정
        {
            if (!HasStateAuthority)
                return;
            PlayerInput input = default; //누락된 입력은 정지 처리
            GetInput(out input);
            bool interact = input.buttons.WasPressed(previousButtons, PlayerButton.Interact); //상호작용 첫 입력
            bool lockDoor = input.buttons.WasPressed(previousButtons, PlayerButton.Lock); //잠금 첫 입력
            previousButtons = input.buttons;
            AuctionGame.current?.simulatePlayer(this, input.direction, interact, lockDoor, input.buttons.IsSet(PlayerButton.Attack));
        }

        public override void Render() //밤에는 스킨 색상을 공통 실루엣으로 교체
        {
            AuctionState state = AuctionGame.current?.displayStateValue; //수신자의 게임 상태
            if (body == null || state == null)
                return;
            Color[] colors = { new Color(1, 0.36f, 0.33f), new Color(0.24f, 0.83f, 0.70f), new Color(1, 0.77f, 0.25f), new Color(0.64f, 0.45f, 0.96f) }; //낮의 스킨 색상
            bool night = state.store == null && state.phase == AuctionState.Phase.Night; //이전 경쟁 모드에서만 정체 숨김
            Color color = night ? new Color(0.12f, 0.13f, 0.16f) : colors[Mathf.Clamp(state.players[slot]?.color ?? 0, 0, 3)]; //공통 밤 색상
            colorBlock.SetColor("_BaseColor", color);
            colorBlock.SetColor("_Color", color);
            body.SetPropertyBlock(colorBlock);
            if (carriedVisual != null)
                carriedVisual.SetActive(carriedId > 0 && state.store == null);
        }

        public PlayerInput readInput(bool blocked) //입력 담당 구성 요소를 호출하는 진입점
        {
            return inputComponent.readInput(blocked);
        }

        public void movePlayer(Vector2 direction, PlayerHouse boundary) //이동 컴포넌트 호출
        {
            movement.movePlayer(direction, boundary);
        }

        public void resetPlayer(Vector3 position) //단계 시작 위치와 체력 초기화
        {
            movement.teleportPlayer(position);
            health = 100;
            carriedId = 0;
            previousButtons = default;
        }

        public bool canInteract(Vector3 position, double now) //거리 검증 컴포넌트 호출
        {
            return interaction.canInteract(position, now);
        }

        public bool tryAttack(Player target, double now) //공격 컴포넌트 호출
        {
            return combat.tryAttack(target.transform.position, now);
        }

        public void applyDamage(int damage) //호스트가 확인한 피해 적용
        {
            health = combat.calculateRemainingHealth(health, damage);
        }
    }
}
