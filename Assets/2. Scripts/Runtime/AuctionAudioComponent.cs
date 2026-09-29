using UnityEngine;

namespace CantResell
{
    public sealed class AuctionAudioComponent : MonoBehaviour
    {
        [SerializeField] private AudioSource musicSource; //음악을 재생할 전용 출력
        [SerializeField] private AudioSource effectsSource; //효과음을 재생할 전용 출력

        public void initialize() //Inspector에 연결한 음악 재생 준비
        {
            if (musicSource != null)
            {
                musicSource.loop = true;
                if (musicSource.clip != null)
                    musicSource.Play();
            }
        }

        public void applyVolumes(float master, float music, float effects) //마스터와 각 재생 경로의 음량 적용
        {
            AudioListener.volume = AuctionSettingsComponent.normalizeVolume(master);
            if (musicSource != null)
                musicSource.volume = AuctionSettingsComponent.normalizeVolume(music);
            if (effectsSource != null)
                effectsSource.volume = AuctionSettingsComponent.normalizeVolume(effects);
        }

        public void playMusic(AudioClip clip) //음악 전용 출력에서 반복 재생
        {
            if (musicSource == null)
                return;
            musicSource.Stop();
            musicSource.clip = clip;
            if (clip != null)
                musicSource.Play();
        }

        public void playEffect(AudioClip clip) //효과음 전용 출력에서 일회 재생
        {
            if (effectsSource != null && clip != null)
                effectsSource.PlayOneShot(clip);
        }
    }
}
