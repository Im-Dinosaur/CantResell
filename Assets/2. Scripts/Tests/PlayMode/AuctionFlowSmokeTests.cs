using System.Collections;
using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace CantResell.PlayTests
{
    public sealed class AuctionFlowSmokeTests
    {
        private string preferencePrefix; //사용자 설정을 보존할 검증 전용 저장 키
        private float previousMaster; //검증 전에 사용하던 전체 음량
#if UNITY_EDITOR
        private bool previousAsyncCompilation; //검증 전에 사용하던 에디터 셰이더 컴파일 설정

        [SetUp]
        public void prepareSynchronousShaders() //첫 화면도 셰이더 준비가 끝난 뒤 캡처하도록 설정
        {
            previousAsyncCompilation = UnityEditor.ShaderUtil.allowAsyncCompilation;
            UnityEditor.ShaderUtil.allowAsyncCompilation = false;
        }
#endif

        [UnityTest]
        public IEnumerator initializeThreeScenesAndRenderPrivateViews() //네트워크 접속 없이 실제 화면 초기화와 씬 유지 및 표시 검증
        {
            Directory.CreateDirectory("Logs/PrototypePreview");
            preferencePrefix = "CantResell.Test.Play." + System.Guid.NewGuid() + ".";
            previousMaster = AudioListener.volume;
            Screen.SetResolution(1600, 900, false);
            yield return null;
            yield return SceneManager.LoadSceneAsync("Home");
            yield return null;
            yield return null;
            AuctionGame game = Object.FindAnyObjectByType<AuctionGame>(); //실행 중인 진입점
            Assert.IsNotNull(game);
            game.GetComponent<AuctionSettingsComponent>().initialize(preferencePrefix, false);
            Assert.AreEqual(1, Object.FindObjectsByType<AuctionGame>(FindObjectsSortMode.None).Length);
            Assert.AreEqual(1, game.GetComponentsInChildren<InputField>().Length);
            Assert.IsFalse(game.voice.microphoneEnabled);
            AuctionNetworkComponent.Room[] rooms = Enumerable.Range(1, 6).Select(index => new AuctionNetworkComponent.Room //실제 서버 목록을 대체할 화면 검증 데이터
            {
                code = "preview-" + index, title = new[] { "처음 오신 분도 환영해요", "오늘의 마지막 경매", "친구들과 연습하는 방", "누구의 말을 믿을까", "가볍게 한 판!", "다음 경매장" }[index - 1],
                players = index % 4 + 1, capacity = 4, locked = index == 3, open = index != 5
            }).ToArray();
            rooms[2].players = 2;
            game.receiveRooms(rooms);
            yield return null;
            Assert.AreEqual(5, game.GetComponentsInChildren<Button>().Count(button => button.name.StartsWith("Room_")));
            Assert.IsFalse(game.GetComponentsInChildren<Button>().Single(button => button.name == "Room_preview-5").interactable);
            captureScreen(game, "Logs/PrototypePreview/Home.png");
            game.GetComponentsInChildren<Button>().Single(button => button.name == "다음").onClick.Invoke();
            yield return null;
            Assert.AreEqual(1, game.GetComponentsInChildren<Button>().Count(button => button.name.StartsWith("Room_")));
            game.GetComponentsInChildren<Button>().Single(button => button.name == "이전").onClick.Invoke();
            yield return null;
            game.GetComponentsInChildren<Button>().Single(button => button.name == "Room_preview-3").onClick.Invoke();
            Assert.IsFalse(game.GetComponentsInChildren<Button>().Single(button => button.name == "방 만들기").IsInteractable());
            Assert.AreEqual(InputField.ContentType.Password, game.GetComponentsInChildren<InputField>().Single(input => input.name == "RoomPassword").contentType);
            game.GetComponentsInChildren<Button>().Single(button => button.name == "취소").onClick.Invoke();
            yield return null;
            Assert.IsTrue(game.GetComponentsInChildren<Button>().Single(button => button.name == "방 만들기").IsInteractable());
            game.GetComponentsInChildren<Button>().Single(button => button.name == "방 만들기").onClick.Invoke();
            Assert.IsFalse(game.GetComponentsInChildren<InputField>().Any(input => input.name == "RoomPassword"));
            game.GetComponentsInChildren<Button>().Single(button => button.name == "비공개").onClick.Invoke();
            InputField password = game.GetComponentsInChildren<InputField>().Single(input => input.name == "RoomPassword"); //비공개 선택으로 나타난 비밀번호 입력
            Assert.AreEqual(8, password.characterLimit);
            game.GetComponentsInChildren<Button>().Single(button => button.name == "생성하기").onClick.Invoke();
            Assert.IsTrue(game.GetComponentsInChildren<Text>().Any(label => label.text == "방 제목을 입력해 주세요."));
            game.GetComponentsInChildren<InputField>().Single(input => input.name == "RoomTitle").text = "함께 즐기는 경매장";
            password.text = "room1234";
            captureScreen(game, "Logs/PrototypePreview/CreateRoom.png");
            game.GetComponentsInChildren<Button>().Single(button => button.name == "취소").onClick.Invoke();
            yield return null;
            game.GetComponentsInChildren<Button>().Single(button => button.name == "설정").onClick.Invoke();
            Assert.AreEqual(3, game.GetComponentsInChildren<Slider>().Length);
            Assert.IsFalse(game.GetComponentsInChildren<Button>().Single(button => button.name == "방 만들기").IsInteractable());
            Slider master = game.GetComponentsInChildren<Slider>().Single(slider => slider.name == "MasterVolume"); //마스터 음량 입력
            Slider music = game.GetComponentsInChildren<Slider>().Single(slider => slider.name == "MusicVolume"); //음악 음량 입력
            Slider effects = game.GetComponentsInChildren<Slider>().Single(slider => slider.name == "EffectsVolume"); //효과음 음량 입력
            master.value = 0.65f;
            music.value = 0.3f;
            effects.value = 0.8f;
            Assert.AreEqual(0.65f, AudioListener.volume);
            Assert.AreEqual(0.3f, game.transform.Find("Music").GetComponent<AudioSource>().volume);
            Assert.AreEqual(0.8f, game.transform.Find("Effects").GetComponent<AudioSource>().volume);
            Dropdown resolution = game.GetComponentInChildren<Dropdown>(); //해상도 선택 목록
            Assert.Greater(resolution.options.Count, 0);
            yield return null;
            resolution.Show();
            yield return null;
            Assert.IsNotNull(GameObject.Find("Dropdown List"));
            captureScreen(game, "Logs/PrototypePreview/SettingsResolution.png");
            resolution.Hide();
            yield return new WaitForSecondsRealtime(0.2f);
            captureScreen(game, "Logs/PrototypePreview/SettingsHome.png");
            game.GetComponentsInChildren<Button>().Single(button => button.name == "닫기").onClick.Invoke();
            yield return null;
            Assert.AreEqual(0.65f, PlayerPrefs.GetFloat(preferencePrefix + "Volume"));
            Assert.AreEqual(0.3f, PlayerPrefs.GetFloat(preferencePrefix + "MusicVolume"));
            Assert.AreEqual(0.8f, PlayerPrefs.GetFloat(preferencePrefix + "EffectsVolume"));

            yield return SceneManager.LoadSceneAsync("StandBy");
            yield return null;
            yield return null;
            Assert.AreSame(game, Object.FindAnyObjectByType<AuctionGame>());
            Assert.AreEqual(1, Object.FindObjectsByType<AuctionGame>(FindObjectsSortMode.None).Length);
            AuctionState state = createState(1, AuctionState.Phase.Lobby); //접속 대신 주입할 수신자용 표시 정보
            game.receiveState(state);
            Assert.IsNotNull(GameObject.Find("AuctionRoomView"));
            Assert.AreEqual("StandBy", GameObject.Find("AuctionRoomView").scene.name);
            Assert.IsNotNull(GameObject.Find("Player1"));
            Material tableMaterial = GameObject.Find("Table").GetComponent<Renderer>().sharedMaterial; //버전 전환 후 실제 사용하는 URP 머티리얼
            Assert.IsNotNull(tableMaterial.shader);
            Assert.IsTrue(tableMaterial.shader.isSupported);
            Assert.AreEqual("Universal Render Pipeline/Lit", tableMaterial.shader.name);
            Assert.AreEqual(new Vector3(0, 2.35f, -10), Camera.main.transform.position);
            for (int frame = 0; frame < 6; frame++) //새 씬의 렌더링 갱신 대기
                yield return null;
            Assert.IsNotNull(GameObject.Find("Player1"));
            captureScreen(game, "Logs/PrototypePreview/StandBy.png");
            yield return null;
            game.GetComponentsInChildren<Button>().Single(button => button.name == "설정").onClick.Invoke();
            Assert.AreEqual(6, game.GetComponentsInChildren<Slider>().Length);
            Slider firstVoice = game.GetComponentsInChildren<Slider>().Single(slider => slider.name == "Voice_1"); //첫 상대의 수신 음량
            Slider otherVoice = game.GetComponentsInChildren<Slider>().Single(slider => slider.name == "Voice_3"); //다른 상대의 수신 음량
            firstVoice.value = 0;
            otherVoice.value = 0.45f;
            Assert.AreEqual(0, game.getPlayerVoiceVolume(1));
            Assert.AreEqual(0.45f, game.getPlayerVoiceVolume(3));
            Assert.AreEqual(1, game.getPlayerVoiceVolume(4));
            Assert.IsFalse(game.GetComponentsInChildren<Slider>().Any(slider => slider.name == "Voice_2"));
            game.GetComponent<AuctionUIComponent>().showState(state, "화면 검증");
            Assert.AreSame(firstVoice, game.GetComponentsInChildren<Slider>().Single(slider => slider.name == "Voice_1"));
            yield return null;
            captureScreen(game, "Logs/PrototypePreview/SettingsStandBy.png");

            yield return SceneManager.LoadSceneAsync("Play");
            yield return null;
            yield return null;
            Assert.AreSame(game, Object.FindAnyObjectByType<AuctionGame>());
            Assert.IsFalse(game.GetComponentsInChildren<Slider>().Any());
            Assert.AreEqual(1, Object.FindObjectsByType<AuctionGame>(FindObjectsSortMode.None).Length);
            state = createState(2, AuctionState.Phase.Inspection);
            game.receiveState(state);
            Assert.IsTrue(game.GetComponentsInChildren<Text>().Any(label => label.text.Contains("상태: 알 수 없음")));
            game.GetComponentsInChildren<Button>().Single(button => button.name == "설정").onClick.Invoke();
            Assert.AreEqual(0, game.GetComponentsInChildren<Slider>().Single(slider => slider.name == "Voice_1").value);
            Assert.AreEqual(0.45f, game.GetComponentsInChildren<Slider>().Single(slider => slider.name == "Voice_3").value);
            Assert.AreEqual(0.3f, game.GetComponentsInChildren<Slider>().Single(slider => slider.name == "MusicVolume").value);
            game.GetComponentsInChildren<Button>().Single(button => button.name == "닫기").onClick.Invoke();
            yield return null;
            Assert.IsTrue(game.GetComponentsInChildren<Button>().Single(button => button.name == "비밀 검사").interactable);
            state = createState(3, AuctionState.Phase.Bidding);
            game.receiveState(state);
            Assert.IsTrue(game.GetComponentsInChildren<Button>().Single(button => button.name == "입찰하기").interactable);
            captureScreen(game, "Logs/PrototypePreview/Play.png");
            yield return null;

            state = createState(4, AuctionState.Phase.Reveal);
            state.knowsCondition = true;
            state.goodCondition = false;
            game.receiveState(state);
            yield return new WaitForSecondsRealtime(0.4f);
            Assert.IsNotNull(GameObject.Find("Smoke0"));
            captureScreen(game, "Logs/PrototypePreview/Reveal.png");
            yield return null;

            state = createState(5, AuctionState.Phase.Results);
            state.knowsCondition = true;
            state.players[0].cash = state.players[1].cash = 180;
            game.receiveState(state);
            Assert.IsTrue(game.GetComponentsInChildren<Text>().Any(label => label.text.Contains("1위  플레이어 1") && label.text.Contains("1위  플레이어 2")));
            captureScreen(game, "Logs/PrototypePreview/Results.png");
            yield return null;
            game.GetComponentsInChildren<Button>().Single(button => button.name == "설정").onClick.Invoke();
            state = createState(6, AuctionState.Phase.Results);
            state.players[3] = null;
            game.receiveState(state);
            Assert.AreEqual(5, game.GetComponentsInChildren<Slider>().Length);
            Assert.IsFalse(game.GetComponentsInChildren<Slider>().Any(slider => slider.name == "Voice_4"));
            yield return null;
        }

        private void captureScreen(AuctionGame game, string path) //숨겨진 검증 창에서도 UI와 3D 화면을 이미지로 저장
        {
            Camera camera = Camera.main; //현재 씬 렌더링 카메라
            Canvas canvas = game.GetComponentInChildren<Canvas>(); //저장할 화면의 UI 캔버스
            RenderTexture target = new RenderTexture(1600, 900, 24); //화면 저장용 렌더 텍스처
            RenderTexture previousTarget = camera.targetTexture; //기존 카메라 출력 대상
            RenderTexture previousActive = RenderTexture.active; //기존 활성 렌더 텍스처
            RenderMode previousMode = canvas.renderMode; //기존 캔버스 출력 방식
            Camera previousCamera = canvas.worldCamera; //기존 캔버스 카메라
            float previousDistance = canvas.planeDistance; //기존 캔버스 표시 거리
            float previousScale = canvas.scaleFactor; //실행 창 해상도에 따른 기존 UI 배율
            Texture2D image = new Texture2D(1600, 900, TextureFormat.RGB24, false); //저장할 픽셀 이미지
            try
            {
                target.Create();
                camera.targetTexture = target;
                canvas.renderMode = RenderMode.ScreenSpaceCamera;
                canvas.worldCamera = camera;
                canvas.planeDistance = 0.5f;
                canvas.scaleFactor = 1;
                foreach (Text label in canvas.GetComponentsInChildren<Text>()) //캡처 해상도에 맞는 글꼴 정점 재생성
                    label.SetAllDirty();
                Canvas.ForceUpdateCanvases();
                camera.Render();
                RenderTexture.active = target;
                image.ReadPixels(new Rect(0, 0, 1600, 900), 0, 0);
                image.Apply();
                File.WriteAllBytes(path, image.EncodeToPNG());
                Assert.Greater(new FileInfo(path).Length, 1000);
            }
            finally
            {
                camera.targetTexture = previousTarget;
                canvas.renderMode = previousMode;
                canvas.worldCamera = previousCamera;
                canvas.planeDistance = previousDistance;
                canvas.scaleFactor = previousScale;
                RenderTexture.active = previousActive;
                target.Release();
                Object.Destroy(target);
                Object.Destroy(image);
            }
        }

        private AuctionState createState(long sequence, AuctionState.Phase phase) //화면 검증용 공개 데이터 생성
        {
            AuctionState state = new AuctionState //로컬 구매자 화면의 입력 데이터
            {
                sequence = sequence, match = 1, phase = phase, round = 0,
                localSlot = 1, hostSlot = 0, sellerSlot = 0, bidderSlot = 2,
                highestBid = 20, minimumRaise = 10, normalReward = 120,
                secondsRemaining = 20, inspectionTickets = 2,
                notice = "판매자는 자유롭게 설명하고, 구매자는 서로의 말을 듣고 판단하세요."
            };
            for (int slot = 0; slot < 4; slot++) //테스트 화면의 참가자 생성 번호
                state.players[slot] = new AuctionState.Player { id = slot + 1, name = "플레이어 " + (slot + 1), color = slot, cash = 100, ready = true };
            return state;
        }

        [UnityTearDown]
        public IEnumerator tearDown() //실행 중인 시제품 오브젝트 정리
        {
#if UNITY_EDITOR
            UnityEditor.ShaderUtil.allowAsyncCompilation = previousAsyncCompilation;
#endif
            AuctionGame game = Object.FindAnyObjectByType<AuctionGame>(); //정리할 테스트 진입점
            if (game != null)
                Object.Destroy(game.gameObject);
            yield return null;
            AudioListener.volume = previousMaster;
            if (preferencePrefix != null)
                foreach (string key in new[] { "Volume", "MusicVolume", "EffectsVolume", "Width", "Height", "Fullscreen" }) //테스트 저장 값 제거
                    PlayerPrefs.DeleteKey(preferencePrefix + key);
            PlayerPrefs.Save();
        }
    }
}
