using Fusion;
using UnityEngine;
using UnityEngine.InputSystem;

namespace CantResell
{
    public enum PlayerButton { Interact, Lock, Attack }
    public struct PlayerInput : INetworkInput
    {
        public Vector2 direction; //평면 이동 입력
        public NetworkButtons buttons; //상호작용과 방어 입력
    }

    public sealed class PlayerInputComponent : MonoBehaviour
    {
#if CANTRESELL_CONNECTION_SMOKE
        public static System.Func<PlayerInput> smokeInput; //검증 빌드에서만 주입할 실제 Fusion 입력
#endif
        public PlayerInput readInput(bool blocked) //새 Input System에서 현재 입력 수집
        {
#if CANTRESELL_CONNECTION_SMOKE
            if (smokeInput != null)
                return smokeInput();
#endif
            PlayerInput input = default; //이번 틱의 입력
            Keyboard keyboard = Keyboard.current; //현재 키보드
            if (blocked || keyboard == null)
                return input;
            input.direction = new Vector2((keyboard.dKey.isPressed ? 1 : 0) - (keyboard.aKey.isPressed ? 1 : 0),
                (keyboard.wKey.isPressed ? 1 : 0) - (keyboard.sKey.isPressed ? 1 : 0));
            input.buttons.Set(PlayerButton.Interact, keyboard.eKey.isPressed);
            input.buttons.Set(PlayerButton.Lock, keyboard.qKey.isPressed);
            input.buttons.Set(PlayerButton.Attack, keyboard.spaceKey.isPressed);
            return input;
        }
    }
}
