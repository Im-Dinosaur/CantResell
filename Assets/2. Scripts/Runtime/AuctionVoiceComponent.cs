using Fusion;
using Photon.Voice.Fusion;
using Photon.Voice.Unity;
using UnityEngine;
using System.Collections.Generic;
using System.Linq;

namespace CantResell
{
    public sealed class AuctionVoiceComponent : MonoBehaviour
    {
        [SerializeField, Range(0.001f, 0.1f)] private float detectionThreshold = 0.01f; //주변 잡음을 제외할 송신 임계값
        private FusionVoiceClient client; //현재 Fusion 방을 따라가는 음성 클라이언트
        private Recorder recorder; //로컬 마이크 입력 처리
        private NetworkRunner runner; //송신자의 게임 참가자 ID 확인
        private readonly Dictionary<int, float> playerVolumes = new Dictionary<int, float>(); //현재 방의 참가자별 수신 음량
        private readonly Dictionary<Speaker, int> speakers = new Dictionary<Speaker, int>(); //음성 출력과 게임 참가자의 연결
        private readonly HashSet<int> participants = new HashSet<int>(); //최신 게임 상태에 포함된 상대 참가자
        public bool microphoneEnabled { get; private set; } //사용자가 켠 마이크 상태
        public bool testing { get; private set; } //송신 없이 마이크 입력을 검사하는 상태
        public bool connected => client != null && client.Client.InRoom; //음성 방 연결 완료 여부
        public bool transmitting => recorder != null && recorder.IsCurrentlyTransmitting; //현재 음성 송신 여부
        public float inputLevel => recorder?.LevelMeter == null ? 0 : Mathf.Clamp01(recorder.LevelMeter.CurrentPeakAmp * 8); //화면에 표시할 입력 크기
        public string status => client == null ? "방 입장 후 음성 연결" : connected ?
            (testing ? "마이크 테스트 · 상대에게 전송하지 않음" : microphoneEnabled ? (transmitting ? "말하는 중" : "음성 연결됨 · 마이크 켜짐") : "음성 연결됨 · 마이크 꺼짐") :
            "음성 연결: " + client.ClientState; //현재 음성 연결 안내

        public void attach(NetworkRunner source) //입장 러너의 음성 구성 요소 연결
        {
            detach();
            runner = source;
            client = runner.GetComponent<FusionVoiceClient>();
            recorder = runner.GetComponent<Recorder>();
            if (client == null || recorder == null)
            {
                Debug.LogError("AuctionRunner 프리팹에 FusionVoiceClient와 Recorder를 연결해 주세요.");
                return;
            }
            recorder.RecordWhenJoined = false;
            recorder.RecordingEnabled = false;
            recorder.TransmitEnabled = false;
            recorder.VoiceDetection = true;
            recorder.VoiceDetectionThreshold = detectionThreshold;
            recorder.Encrypt = true;
            client.SpeakerLinked += onSpeakerLinked;
            client.AutoConnectAndJoin = true;
        }

        public void updateParticipants(AuctionState state) //떠난 참가자의 음량 제거와 현재 참가자 연결 갱신
        {
            participants.Clear();
            if (state?.players != null)
                for (int slot = 0; slot < state.players.Length; slot++) //현재 상대 좌석 정보
                    if (slot != state.localSlot && state.players[slot] != null)
                        participants.Add(state.players[slot].id);
            foreach (int playerId in playerVolumes.Keys.Where(id => !participants.Contains(id)).ToArray()) //나간 참가자의 저장 음량
                playerVolumes.Remove(playerId);
            applyPlayerVolumes();
        }

        public float getPlayerVolume(int playerId) //발화 전에도 조절 가능한 참가자별 수신 음량
        {
            return playerVolumes.TryGetValue(playerId, out float volume) ? volume : 1;
        }

        public void setPlayerVolume(int playerId, float volume) //내 컴퓨터에서만 해당 참가자 음량 변경
        {
            if (!participants.Contains(playerId))
                return;
            playerVolumes[playerId] = AuctionSettingsComponent.normalizeVolume(volume);
            applyPlayerVolumes();
        }

        public string getPlayerStatus(int playerId) //설정창의 수신 음성 상태 표시
        {
            if (getPlayerVolume(playerId) <= 0)
                return "음소거";
            return speakers.Any(pair => pair.Key != null && pair.Value == playerId) ? "음성 연결됨" : "음성 수신 대기";
        }

        private void onSpeakerLinked(Speaker speaker) //새 음성 스트림에 기존 개인 음량을 즉시 적용
        {
            if (!(speaker.RemoteVoice?.VoiceInfo.UserData is int playerId) || playerId <= 0)
                return;
            speakers[speaker] = playerId;
            speaker.OnRemoteVoiceRemoveAction += onSpeakerRemoved;
            applyPlayerVolumes();
        }

        private void onSpeakerRemoved(Speaker speaker) //마이크 재시작과 퇴장 시 사라진 출력 정리
        {
            speakers.Remove(speaker);
            speaker.OnRemoteVoiceRemoveAction -= onSpeakerRemoved;
        }

        private void applyPlayerVolumes() //유효한 참가자의 모든 음성 출력에 수신 음량 적용
        {
            foreach (KeyValuePair<Speaker, int> pair in speakers) //참가자에게 연결된 음성 스트림
                if (pair.Key != null)
                    pair.Key.GetComponent<AudioSource>().volume = participants.Contains(pair.Value) ? getPlayerVolume(pair.Value) : 0;
        }

        public string toggleMicrophone() //명시적인 버튼 입력으로 마이크 녹음과 송신 전환
        {
            if (!microphoneEnabled)
            {
                string error = checkMicrophone(); //녹음 시작 전 장치 확인
                if (error != null)
                    return error;
            }
            testing = false;
            microphoneEnabled = !microphoneEnabled;
            applyRecording();
            return null;
        }

        public string toggleTest() //마이크 테스트 동안 상대방 송신 차단
        {
            if (!testing)
            {
                string error = checkMicrophone(); //테스트 시작 전 장치 확인
                if (error != null)
                    return error;
            }
            testing = !testing;
            applyRecording();
            return null;
        }

        public void endTest() //씬 전환 시 테스트를 끝내고 사용자의 마이크 상태 복원
        {
            testing = false;
            applyRecording();
        }

        private string checkMicrophone() //음성 방과 기본 마이크 이용 가능 여부 확인
        {
            if (!connected || recorder == null)
                return "음성 연결을 기다려 주세요. 연결되지 않으면 Photon Voice 설정을 확인해 주세요.";
            if (Microphone.devices.Length == 0)
                return "마이크를 찾을 수 없습니다. Windows 입력 장치와 마이크 권한을 확인해 주세요.";
            return null;
        }

        private void applyRecording() //사용자의 마이크 선택과 테스트 상태 반영
        {
            if (recorder == null)
                return;
            recorder.TransmitEnabled = microphoneEnabled && !testing && connected;
            recorder.RecordingEnabled = (microphoneEnabled || testing) && connected;
        }

        private void Update() //음성 연결 복구 또는 단절 시 녹음 상태 동기화
        {
            if (runner != null && runner.IsRunning && recorder != null && runner.LocalPlayer.RawEncoded > 0 &&
                (!(recorder.UserData is int ownerId) || ownerId != runner.LocalPlayer.RawEncoded))
                recorder.UserData = runner.LocalPlayer.RawEncoded;
            applyRecording();
        }

        public void detach() //퇴장과 종료 시 마이크를 멈추고 참조 해제
        {
            microphoneEnabled = testing = false;
            applyRecording();
            if (client != null)
            {
                client.SpeakerLinked -= onSpeakerLinked;
                client.AutoConnectAndJoin = false;
                if (client.Client.IsConnected)
                    client.Disconnect();
            }
            client = null;
            recorder = null;
            runner = null;
            foreach (Speaker speaker in speakers.Keys.ToArray()) //이벤트가 남지 않도록 음성 출력 해제
                if (speaker != null)
                    speaker.OnRemoteVoiceRemoveAction -= onSpeakerRemoved;
            speakers.Clear();
            participants.Clear();
            playerVolumes.Clear();
        }

        private void OnDestroy() //오브젝트 종료 시 입력 장치 해제
        {
            detach();
        }
    }
}
