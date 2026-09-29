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
                if (role == "host")
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
            await waitFor(() => FindObjectsByType<Speaker>(FindObjectsSortMode.None).Any(speaker => speaker.IsPlaying), "VOICE_AUDIO_RECEIVED", 150);
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
            if (role == "client2")
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
            recorder.SourceType = Recorder.InputSourceType.AudioClip;
            recorder.AudioClip = clip;
            recorder.VoiceDetection = false;
            recorder.TransmitEnabled = true;
            recorder.RecordingEnabled = true;
            try { await waitFor(() => has("host", "VOICE_AUDIO_RECEIVED"), "SYNTHETIC_AUDIO"); }
            finally
            {
                recorder.TransmitEnabled = recorder.RecordingEnabled = false;
                game.voice.enabled = true;
                Destroy(clip);
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
