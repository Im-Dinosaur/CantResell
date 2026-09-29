using Photon.Voice.Fusion;
using Photon.Voice.Unity;

namespace CantResell
{
    public sealed class AuctionVoiceClientComponent : FusionVoiceClient
    {
        protected override Speaker InstantiateSpeakerForRemoteVoice(int playerId, byte voiceId, object userData) //Fusion 참가자 ID를 가진 방 음성 출력 생성
        {
            if (userData is int ownerId && ownerId > 0)
            {
                Speaker speaker = InstantiateSpeakerPrefab(gameObject, true); //개별 볼륨을 적용할 원격 음성 출력
                if (speaker != null)
                    speaker.GetComponent<UnityEngine.AudioSource>().volume = 0;
                return speaker;
            }
            return base.InstantiateSpeakerForRemoteVoice(playerId, voiceId, userData);
        }
    }
}
