#if CANTRESELL_CONNECTION_SMOKE
using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Photon.Voice.Unity;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace CantResell
{
    //별도 검증 빌드에서만 포함되며 일반 게임 빌드에는 들어가지 않는다.
    public sealed class AuctionConnectionSmokeComponent : MonoBehaviour
    {
        private AuctionGame game; //실제 게임 진입점
        private string role; //이번 프로세스의 검증 역할
        private string title; //검증 방의 고유 제목
        private string directory; //프로세스별 검증 결과 경로

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void startSmoke() //명령행으로 요청한 별도 검증만 실행
        {
            if (!Environment.GetCommandLineArgs().Contains("-auctionSmokeRole"))
                return;
            GameObject root = new GameObject("ConnectionSmoke"); //검증 실행기
            DontDestroyOnLoad(root);
            root.AddComponent<AuctionConnectionSmokeComponent>();
        }

        private async void Start() //실제 UI와 네트워크를 통과하는 접속 시나리오
        {
            role = argument("-auctionSmokeRole");
            title = argument("-auctionSmokeTitle");
            directory = argument("-auctionSmokeDirectory");
            Directory.CreateDirectory(directory);
            try
            {
                await waitFor(() => FindAnyObjectByType<AuctionGame>() != null, "game");
                game = FindAnyObjectByType<AuctionGame>();
                AudioListener.volume = 0;
                if (role == "settings")
                    await displayScenario();
                else if (role == "host")
                    await hostScenario();
                else
                    await clientScenario();
                mark("COMPLETE");
                Application.Quit(0);
            }
            catch (Exception exception)
            {
                File.WriteAllText(Path.Combine(directory, role + "_FAIL.txt"), exception.ToString());
                Debug.LogException(exception);
                if (game != null)
                    game.leaveRoom();
                await Task.Delay(1000);
                Application.Quit(1);
            }
        }

        private async Task hostScenario() //비공개 생성과 4인 이동 및 공개 방 재생성 검증
        {
            game.createRoom("검증 방장", title, true, "Smoke123");
            await waitFor(() => SceneManager.GetActiveScene().name == "StandBy" && game.voice.connected, "HOST_VOICE");
            mark("PRIVATE_CREATED");
            await verifyIndividualVoiceVolumes();
            mark("VOICE_AUDIO_RECEIVED");
            await Task.Delay(250);
            game.requestReady();
            await waitFor(() => button("게임 시작")?.interactable == true, "ALL_READY", 150);
            game.requestStart();
            await waitFor(() => SceneManager.GetActiveScene().name == "Play" && game.voice.connected, "HOST_PLAY");
            await waitFor(() => otherClientsHave("PLAY"), "CLIENTS_PLAY");
            game.abortMatch("자동 연결 검증의 대기실 복귀 확인");
            await Task.Delay(350);
            game.requestLobby();
            await waitFor(() => SceneManager.GetActiveScene().name == "StandBy" && game.voice.connected, "HOST_RETURN");
            await waitFor(() => otherClientsHave("RETURN"), "CLIENTS_RETURN");
            game.leaveRoom();
            await waitFor(() => SceneManager.GetActiveScene().name == "Home" && !game.voice.connected, "HOST_LEAVE");
            await waitFor(() => otherClientsHave("DISCONNECTED"), "CLIENTS_DISCONNECTED");
            game.createRoom("검증 방장", title + " 공개", false, "");
            await waitFor(() => SceneManager.GetActiveScene().name == "StandBy", "PUBLIC_CREATED");
            mark("PUBLIC_CREATED");
            await waitFor(() => has("client2", "PUBLIC_JOINED"), "PUBLIC_CLIENT");
            game.leaveRoom();
            await waitFor(() => SceneManager.GetActiveScene().name == "Home", "FINAL_HOME");
        }

        private async Task clientScenario() //목록과 비밀번호 오류 및 재시도 후 실제 방 참가
        {
            await waitFor(() => has("host", "PRIVATE_CREATED"), "HOST_READY");
            game.refreshRooms();
            await waitFor(() => roomButton(title) != null && roomButton(title).interactable, "ROOM_LIST");
            mark("ROOM_LIST");
            roomButton(title).onClick.Invoke();
            InputField password = game.GetComponentsInChildren<InputField>().Single(input => input.name == "RoomPassword"); //실제 비밀번호 팝업
            password.text = role == "bad" ? "wrong123" : "Smoke123";
            button("입장하기").onClick.Invoke();
            if (role == "bad")
            {
                await waitFor(() => game.GetComponentsInChildren<Text>().Any(text => text.text.Contains("입장하지 못했습니다")) && !game.GetComponent<AuctionNetworkComponent>().isConnecting, "WRONG_PASSWORD_REJECTED");
                mark("WRONG_PASSWORD_REJECTED");
                password = game.GetComponentsInChildren<InputField>().Single(input => input.name == "RoomPassword");
                password.text = "Smoke123";
                button("입장하기").onClick.Invoke();
            }
            await waitFor(() => SceneManager.GetActiveScene().name == "StandBy" && game.voice.connected, "CLIENT_VOICE");
            if (game.voice.microphoneEnabled)
                throw new InvalidOperationException("Microphone must stay off in network smoke tests.");
            mark("VOICE_CONNECTED_MIC_OFF");
            if (role == "client2" || role == "client3")
                await sendSyntheticAudio();
            await Task.Delay(500);
            game.requestReady();
            await waitFor(() => SceneManager.GetActiveScene().name == "Play" && game.voice.connected, "CLIENT_PLAY", 150);
            mark("PLAY");
            await waitFor(() => SceneManager.GetActiveScene().name == "StandBy" && game.voice.connected, "CLIENT_RETURN");
            mark("RETURN");
            await waitFor(() => SceneManager.GetActiveScene().name == "Home" && !game.voice.connected, "HOST_DISCONNECT");
            mark("DISCONNECTED");
            if (role != "client2")
                return;
            await waitFor(() => has("host", "PUBLIC_CREATED"), "PUBLIC_READY");
            game.refreshRooms();
            await waitFor(() => roomButton(title + " 공개")?.interactable == true, "PUBLIC_LIST");
            roomButton(title + " 공개").onClick.Invoke();
            await waitFor(() => SceneManager.GetActiveScene().name == "StandBy" && game.voice.connected, "PUBLIC_JOIN");
            mark("PUBLIC_JOINED");
            await waitFor(() => SceneManager.GetActiveScene().name == "Home", "PUBLIC_END");
        }

        private async Task sendSyntheticAudio() //실제 마이크 없이 생성한 PCM으로 음성 전송 경로 검증
        {
            Recorder recorder = FindAnyObjectByType<Recorder>(); //연결된 음성 송신기
            AudioClip clip = AudioClip.Create("GeneratedTestTone", 24000, 1, 24000, false); //외부 녹음이 아닌 테스트 입력
            float[] samples = new float[24000]; //1초 길이의 생성 파형
            for (int index = 0; index < samples.Length; index++) //낮은 크기의 테스트 파형
                samples[index] = 0.025f * Mathf.Sin(index * 2 * Mathf.PI * 440 / 24000);
            clip.SetData(samples, 0);
            game.voice.enabled = false;
            recorder.UserData = FindAnyObjectByType<Fusion.NetworkRunner>().LocalPlayer.RawEncoded;
            File.WriteAllText(Path.Combine(directory, role + "_PlayerId.txt"), recorder.UserData.ToString());
            recorder.SourceType = Recorder.InputSourceType.AudioClip;
            recorder.AudioClip = clip;
            recorder.VoiceDetection = false;
            recorder.TransmitEnabled = true;
            recorder.RecordingEnabled = true;
            try
            {
                await waitFor(() => has("host", "INITIAL_VOICE_GAINS"), "SYNTHETIC_AUDIO");
                if (role == "client2")
                {
                    recorder.RestartRecording();
                    mark("VOICE_RESTARTED");
                }
                await waitFor(() => has("host", "VOICE_AUDIO_RECEIVED"), "RESTART_VOLUME");
            }
            finally
            {
                recorder.TransmitEnabled = recorder.RecordingEnabled = false;
                game.voice.enabled = true;
                Destroy(clip);
            }
        }

        private async Task verifyIndividualVoiceVolumes() //서로 다른 두 송신자의 실제 재생 음량과 재연결 유지 검증
        {
            await waitFor(() => FindObjectsByType<Speaker>(FindObjectsSortMode.None).Count(speaker => speaker.IsPlaying) >= 2, "TWO_VOICES", 150);
            int firstId = int.Parse(File.ReadAllText(Path.Combine(directory, "client2_PlayerId.txt"))); //음소거할 게임 참가자
            int secondId = int.Parse(File.ReadAllText(Path.Combine(directory, "client3_PlayerId.txt"))); //별도 음량을 적용할 게임 참가자
            Speaker findSpeaker(int id) //게임 ID로 실제 원격 출력 조회
            {
                return FindObjectsByType<Speaker>(FindObjectsSortMode.None).FirstOrDefault(speaker => speaker.RemoteVoice?.VoiceInfo.UserData is int owner && owner == id);
            }
            Speaker first = findSpeaker(firstId); //재시작 전 첫 참가자의 출력
            Speaker second = findSpeaker(secondId); //두 번째 참가자의 출력
            await waitFor(() => first != null && second != null && first.GetComponent<AudioSource>().volume == 1 && second.GetComponent<AudioSource>().volume == 1, "DEFAULT_VOICE_GAINS");
            button("설정").onClick.Invoke();
            game.GetComponentsInChildren<Slider>().Single(slider => slider.name == "Voice_" + firstId).value = 0;
            if (first.GetComponent<AudioSource>().volume != 0 || second.GetComponent<AudioSource>().volume != 1)
                throw new InvalidOperationException("Muting one player affected another speaker.");
            game.GetComponentsInChildren<Slider>().Single(slider => slider.name == "Voice_" + secondId).value = 0.35f;
            if (Mathf.Abs(second.GetComponent<AudioSource>().volume - 0.35f) > 0.001f)
                throw new InvalidOperationException("Second speaker gain was not applied.");
            mark("INITIAL_VOICE_GAINS");
            await waitFor(() => has("client2", "VOICE_RESTARTED") && findSpeaker(firstId) != null && findSpeaker(firstId) != first && findSpeaker(firstId).IsPlaying, "RECREATED_SPEAKER");
            if (findSpeaker(firstId).GetComponent<AudioSource>().volume != 0 || Mathf.Abs(findSpeaker(secondId).GetComponent<AudioSource>().volume - 0.35f) > 0.001f)
                throw new InvalidOperationException("Voice restart lost individual volume settings.");
            mark("VOICE_RESTART_GAIN_PRESERVED");
            button("닫기").onClick.Invoke();
        }

        private async Task displayScenario() //실제 Windows 창의 해상도 적용과 확인 및 자동 원복 검증
        {
            string prefix = "CantResell.Test.Display." + Guid.NewGuid() + "."; //일반 설정을 건드리지 않는 저장 영역
            AuctionSettingsComponent settings = game.GetComponent<AuctionSettingsComponent>(); //표시 설정 담당
            settings.initialize(prefix, false);
            try
            {
                Vector2Int first = new Vector2Int(1280, 720); //확정해서 저장할 창 크기
                Vector2Int second = new Vector2Int(1600, 900); //취소할 임시 창 크기
                if (!settings.resolutions.Contains(first) || !settings.resolutions.Contains(second))
                    throw new InvalidOperationException("Display smoke requires a monitor supporting 1600x900.");
                game.applyDisplay(first, false);
                await waitFor(() => Screen.width == first.x && Screen.height == first.y && !Screen.fullScreen, "WINDOW_RESOLUTION");
                game.confirmDisplay();
                if (PlayerPrefs.GetInt(prefix + "Width") != first.x || PlayerPrefs.GetInt(prefix + "Height") != first.y)
                    throw new InvalidOperationException("Confirmed resolution was not persisted.");
                mark("WINDOW_SAVED");
                game.applyDisplay(second, false);
                await waitFor(() => Screen.width == second.x && Screen.height == second.y, "PREVIEW_RESOLUTION");
                game.cancelDisplay();
                await waitFor(() => Screen.width == first.x && Screen.height == first.y, "CANCEL_RESOLUTION");
                mark("CANCEL_RESTORED");
                game.applyDisplay(second, false);
                await waitFor(() => !game.displayPending && Screen.width == first.x && Screen.height == first.y, "AUTO_REVERT", 25);
                mark("TIMEOUT_RESTORED");
                game.applyDisplay(first, true);
                await waitFor(() => Screen.fullScreen, "FULLSCREEN_MODE");
                game.cancelDisplay();
                await waitFor(() => !Screen.fullScreen && Screen.width == first.x && Screen.height == first.y, "WINDOW_MODE_RESTORED");
                mark("FULLSCREEN_RESTORED");
            }
            finally
            {
                game.cancelDisplay();
                foreach (string key in new[] { "Volume", "MusicVolume", "EffectsVolume", "Width", "Height", "Fullscreen" }) //검증 저장 값 제거
                    PlayerPrefs.DeleteKey(prefix + key);
                PlayerPrefs.Save();
            }
        }

        private Button button(string name) //실제 UI의 지정 버튼 조회
        {
            return game.GetComponentsInChildren<Button>().FirstOrDefault(candidate => candidate.name == name);
        }

        private Button roomButton(string roomTitle) //목록에 표시된 제목으로 참가 버튼 탐색
        {
            return game.GetComponentsInChildren<Button>().FirstOrDefault(candidate => candidate.name.StartsWith("Room_") && candidate.GetComponentsInChildren<Text>().Any(text => text.text == roomTitle));
        }

        private bool otherClientsHave(string stage) //세 클라이언트의 단계 완료 확인
        {
            return new[] { "bad", "client2", "client3" }.All(client => has(client, stage));
        }

        private bool has(string client, string stage) //다른 프로세스의 결과 확인
        {
            return File.Exists(Path.Combine(directory, client + "_" + stage + ".pass"));
        }

        private void mark(string stage) //민감 정보 없이 성공 단계 기록
        {
            File.WriteAllText(Path.Combine(directory, role + "_" + stage + ".pass"), DateTime.UtcNow.ToString("O"));
            Debug.Log("SMOKE_PASS " + role + " " + stage);
        }

        private static async Task waitFor(Func<bool> condition, string stage, int seconds = 75) //네트워크 단계에 제한 시간을 둔 대기
        {
            DateTime deadline = DateTime.UtcNow.AddSeconds(seconds); //해당 단계의 종료 시각
            while (!condition())
            {
                if (DateTime.UtcNow > deadline)
                    throw new TimeoutException(stage);
                await Task.Delay(100);
            }
        }

        private static string argument(string key) //별도 검증 프로세스 명령행 설정 조회
        {
            string[] arguments = Environment.GetCommandLineArgs(); //현재 실행 인자
            int index = Array.IndexOf(arguments, key); //요청한 인자의 위치
            return index >= 0 && index + 1 < arguments.Length ? arguments[index + 1] : "";
        }
    }
}
#endif
