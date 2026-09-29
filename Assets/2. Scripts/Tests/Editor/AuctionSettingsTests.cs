using System;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace CantResell.Tests
{
    public sealed class AuctionSettingsTests
    {
        private GameObject root; //설정 검증용 오브젝트
        private string prefix; //실제 사용자 설정과 분리된 저장 키
        private float originalMaster; //검증 전 전체 음량

        [SetUp]
        public void setUp() //독립된 설정 저장 공간 준비
        {
            root = new GameObject("SettingsTest");
            prefix = "CantResell.Test." + Guid.NewGuid() + ".";
            originalMaster = AudioListener.volume;
        }

        [TearDown]
        public void tearDown() //테스트 설정과 전역 음량 복구
        {
            foreach (string key in new[] { "Volume", "MusicVolume", "EffectsVolume", "Width", "Height", "Fullscreen" }) //이번 검증이 사용한 설정 키
                PlayerPrefs.DeleteKey(prefix + key);
            PlayerPrefs.Save();
            AudioListener.volume = originalMaster;
            Object.DestroyImmediate(root);
        }

        [Test]
        public void preserveLegacyMasterAndPersistIndependentChannels() //이전 전체 음량 보존과 채널별 저장 검증
        {
            PlayerPrefs.SetFloat(prefix + "Volume", 0.37f);
            AuctionSettingsComponent settings = root.AddComponent<AuctionSettingsComponent>(); //설정 저장 대상
            settings.initialize(prefix, false);
            Assert.AreEqual(0.37f, settings.masterVolume);
            Assert.AreEqual(1, settings.musicVolume);
            Assert.AreEqual(1, settings.effectsVolume);
            settings.setVolume(AuctionSettingsComponent.VolumeChannel.Music, 0.2f);
            settings.setVolume(AuctionSettingsComponent.VolumeChannel.Effects, 0.6f);
            settings.save();
            settings.initialize(prefix, false);
            Assert.AreEqual(0.37f, settings.masterVolume);
            Assert.AreEqual(0.2f, settings.musicVolume);
            Assert.AreEqual(0.6f, settings.effectsVolume);
            settings.setVolume(AuctionSettingsComponent.VolumeChannel.Master, 0);
            Assert.AreEqual(0.2f, settings.musicVolume);
            Assert.AreEqual(0.6f, settings.effectsVolume);
        }

        [TestCase(-1f, 0f)]
        [TestCase(2f, 1f)]
        [TestCase(float.NaN, 1f)]
        [TestCase(float.PositiveInfinity, 1f)]
        public void rejectInvalidVolumeValues(float input, float expected) //저장 값 범위와 잘못된 수치 처리 검증
        {
            Assert.AreEqual(expected, AuctionSettingsComponent.normalizeVolume(input));
        }

        [Test]
        public void resolutionOptionsAreUniqueAndFitTheMonitor() //주사율 중복과 모니터 밖 화면 크기 제거 검증
        {
            Resolution[] modes = { new Resolution { width = 1920, height = 1080 }, new Resolution { width = 1920, height = 1080 },
                new Resolution { width = 3840, height = 2160 }, new Resolution { width = 320, height = 200 } }; //OS가 제공할 수 있는 중복 화면 크기
            Vector2Int[] sizes = AuctionSettingsComponent.buildResolutions(modes, new Vector2Int(1366, 768), new Vector2Int(1920, 1080)); //사용자 선택 목록
            Assert.AreEqual(sizes.Length, sizes.Distinct().Count());
            Assert.AreEqual(new Vector2Int(1920, 1080), sizes[0]);
            Assert.Contains(new Vector2Int(1366, 768), sizes);
            Assert.Contains(new Vector2Int(1280, 720), sizes);
            Assert.IsTrue(sizes.All(size => size.x <= 1920 && size.y <= 1080 && size.x >= 640));
            Assert.IsNotEmpty(AuctionSettingsComponent.buildResolutions(null, Vector2Int.zero, Vector2Int.zero));
        }

        [Test]
        public void playerVolumesFollowIdsAcrossRosterUpdatesAndResetOnDeparture() //좌석 교체와 퇴장 시 개인 음량 처리 검증
        {
            AuctionVoiceComponent voice = root.AddComponent<AuctionVoiceComponent>(); //개별 수신 설정 대상
            AuctionState state = new AuctionState { localSlot = 0 }; //본인과 상대 두 명의 방 상태
            state.players[0] = new AuctionState.Player { id = 42 };
            state.players[1] = new AuctionState.Player { id = 12 };
            state.players[2] = new AuctionState.Player { id = 99 };
            voice.updateParticipants(state);
            voice.setPlayerVolume(12, 0);
            voice.setPlayerVolume(99, 0.3f);
            voice.setPlayerVolume(42, 0);
            Assert.AreEqual(1, voice.getPlayerVolume(42));
            Assert.AreEqual("음소거", voice.getPlayerStatus(12));
            (state.players[1], state.players[2]) = (state.players[2], state.players[1]);
            voice.updateParticipants(state);
            Assert.AreEqual(0, voice.getPlayerVolume(12));
            Assert.AreEqual(0.3f, voice.getPlayerVolume(99));
            state.players[2] = null;
            voice.updateParticipants(state);
            Assert.AreEqual(1, voice.getPlayerVolume(12));
            Assert.AreEqual(0.3f, voice.getPlayerVolume(99));
            voice.detach();
            Assert.AreEqual(1, voice.getPlayerVolume(99));
        }

        [Test]
        public void audioChannelsDoNotOverwriteEachOtherOrVoiceGain() //실제 프리팹의 음악과 효과음 출력 분리 검증
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(CantResell.Editor.AuctionProjectSetup.prefabPath); //프로덕션 프리팹
            GameObject instance = Object.Instantiate(prefab, root.transform); //직렬화된 오디오 연결을 가진 인스턴스
            AuctionAudioComponent audio = instance.GetComponent<AuctionAudioComponent>(); //오디오 재생 담당
            SerializedObject data = new SerializedObject(audio); //Inspector 출력 참조
            AudioSource music = (AudioSource)data.FindProperty("musicSource").objectReferenceValue; //음악 출력
            AudioSource effects = (AudioSource)data.FindProperty("effectsSource").objectReferenceValue; //효과음 출력
            AudioSource voice = root.AddComponent<AudioSource>(); //별도로 조절한 음성 출력
            voice.volume = 0.4f;
            Assert.IsNotNull(music);
            Assert.IsNotNull(effects);
            Assert.AreNotSame(music, effects);
            Assert.IsTrue(music.loop);
            Assert.IsFalse(effects.loop);
            audio.applyVolumes(0.5f, 0.2f, 0.7f);
            Assert.AreEqual(0.5f, AudioListener.volume);
            Assert.AreEqual(0.2f, music.volume);
            Assert.AreEqual(0.7f, effects.volume);
            Assert.AreEqual(0.4f, voice.volume);
            audio.applyVolumes(0, 0.2f, 0.7f);
            Assert.AreEqual(0, AudioListener.volume);
            Assert.AreEqual(0.2f, music.volume);
            Assert.AreEqual(0.7f, effects.volume);
        }
    }
}
