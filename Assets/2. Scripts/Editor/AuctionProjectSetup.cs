using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEditor.TestTools.TestRunner.Api;
using UnityEngine;
using UnityEngine.SceneManagement;
using Fusion;
using Photon.Voice.Fusion;
using Photon.Voice.Unity;

namespace CantResell.Editor
{
    public static class AuctionProjectSetup
    {
        public const string prefabPath = "Assets/3. Prefabs/AuctionGame.prefab"; //공통 진입점 프리팹 경로
        private const string materialPath = "Assets/6. Data/AuctionSurface.mat"; //시제품 URP 머티리얼 경로

        [MenuItem("CantResell/Set Up Prototype")]
        public static void setup() //기본 프리팹을 생성하고 세 씬에 연결
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("실행을 종료한 뒤 씬 구성을 진행해 주세요.");
            Material surface = AssetDatabase.LoadAssetAtPath<Material>(materialPath); //시제품에 사용할 공유 머티리얼
            if (surface == null)
            {
                Material template = AssetDatabase.LoadAssetAtPath<Material>("Packages/com.unity.render-pipelines.universal/Runtime/Materials/Lit.mat"); //설치된 URP의 기본 머티리얼
                if (template == null)
                    throw new InvalidOperationException("URP Lit 머티리얼을 찾지 못했습니다.");
                surface = new Material(template) { name = "AuctionSurface" };
                AssetDatabase.CreateAsset(surface, materialPath);
            }
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath); //기존 설정을 보존할 진입점 프리팹
            if (prefab == null)
            {
                GameObject root = new GameObject("AuctionGame"); //진입점과 구성 요소를 조립할 오브젝트
                try
                {
                    AuctionRoundComponent round = root.AddComponent<AuctionRoundComponent>(); //단계 담당 구성 요소
                    AuctionItemComponent item = root.AddComponent<AuctionItemComponent>(); //상품 담당 구성 요소
                    AuctionBidComponent bid = root.AddComponent<AuctionBidComponent>(); //입찰 담당 구성 요소
                    AuctionEconomyComponent economy = root.AddComponent<AuctionEconomyComponent>(); //경제 담당 구성 요소
                    AuctionNetworkComponent network = root.AddComponent<AuctionNetworkComponent>(); //접속 담당 구성 요소
                    AuctionUIComponent ui = root.AddComponent<AuctionUIComponent>(); //화면 담당 구성 요소
                    AuctionItemViewComponent view = root.AddComponent<AuctionItemViewComponent>(); //시연 담당 구성 요소
                    AuctionGame game = root.AddComponent<AuctionGame>(); //기능별 호출 진입점
                    SerializedObject gameData = new SerializedObject(game); //Inspector 참조를 저장할 직렬화 정보
                    gameData.FindProperty("roundComponent").objectReferenceValue = round;
                    gameData.FindProperty("itemComponent").objectReferenceValue = item;
                    gameData.FindProperty("bidComponent").objectReferenceValue = bid;
                    gameData.FindProperty("economyComponent").objectReferenceValue = economy;
                    gameData.FindProperty("networkComponent").objectReferenceValue = network;
                    gameData.FindProperty("uiComponent").objectReferenceValue = ui;
                    gameData.FindProperty("viewComponent").objectReferenceValue = view;
                    gameData.ApplyModifiedPropertiesWithoutUndo();
                    SerializedObject viewData = new SerializedObject(view); //URP 머티리얼 참조 저장
                    viewData.FindProperty("surfaceMaterial").objectReferenceValue = surface;
                    viewData.ApplyModifiedPropertiesWithoutUndo();
                    prefab = PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
                }
                finally
                {
                    UnityEngine.Object.DestroyImmediate(root);
                }
            }
            configureVoicePrefabs();
            prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
            foreach (string name in new[] { "Home", "StandBy", "Play" }) //연결할 게임 씬 이름
            {
                string path = "Assets/1. Scenes/" + name + ".unity"; //대상 씬 경로
                Scene scene = SceneManager.GetSceneByPath(path); //이미 열린 씬 조회
                bool wasLoaded = scene.IsValid() && scene.isLoaded; //기존 씬 열림 상태
                if (wasLoaded && scene.isDirty)
                    throw new InvalidOperationException(name + " 씬에 저장하지 않은 변경이 있습니다. 저장 후 다시 실행해 주세요.");
                if (!wasLoaded)
                    scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Additive);
                try
                {
                    if (!scene.GetRootGameObjects().Any(root => root.GetComponent<AuctionGame>() != null))
                    {
                        PrefabUtility.InstantiatePrefab(prefab, scene);
                        EditorSceneManager.SaveScene(scene);
                    }
                }
                finally
                {
                    if (!wasLoaded)
                        EditorSceneManager.CloseScene(scene, true);
                }
            }
            AssetDatabase.SaveAssets();
            Directory.CreateDirectory("Logs");
            File.WriteAllText("Logs/AuctionSetup.txt", "PASS: AuctionGame prefab and Home, StandBy, Play scene references.\n");
        }

        private static void configureVoicePrefabs() //기존 게임 설정을 보존하면서 음성 프리팹과 참조 추가
        {
            const string speakerPath = "Assets/3. Prefabs/AuctionSpeaker.prefab"; //수신 음성 재생 프리팹 경로
            const string runnerPath = "Assets/3. Prefabs/AuctionRunner.prefab"; //입장과 음성 연결 프리팹 경로
            GameObject speaker = AssetDatabase.LoadAssetAtPath<GameObject>(speakerPath); //기존 재생 설정 프리팹
            if (speaker == null)
            {
                GameObject root = new GameObject("AuctionSpeaker"); //자동 생성할 음성 출력
                try
                {
                    AudioSource audio = root.AddComponent<AudioSource>(); //방 전체에 들리는 2D 음성 출력
                    audio.playOnAwake = false;
                    audio.spatialBlend = 0;
                    root.AddComponent<Speaker>();
                    speaker = PrefabUtility.SaveAsPrefabAsset(root, speakerPath);
                }
                finally { UnityEngine.Object.DestroyImmediate(root); }
            }
            GameObject runner = AssetDatabase.LoadAssetAtPath<GameObject>(runnerPath); //기존 입장 러너 프리팹
            if (runner == null)
            {
                GameObject root = new GameObject("AuctionRunner"); //음성 통합 러너 구성
                try
                {
                    root.AddComponent<NetworkRunner>();
                    Recorder recorder = root.AddComponent<Recorder>(); //마이크 기본 비활성 설정
                    SerializedObject recorderData = new SerializedObject(recorder); //SDK의 직렬화 설정
                    recorderData.FindProperty("recordingEnabled").boolValue = false;
                    recorderData.FindProperty("transmitEnabled").boolValue = false;
                    recorderData.FindProperty("recordWhenJoined").boolValue = false;
                    recorderData.FindProperty("voiceDetection").boolValue = true;
                    recorderData.FindProperty("encrypt").boolValue = true;
                    recorderData.ApplyModifiedPropertiesWithoutUndo();
                    FusionVoiceClient client = root.AddComponent<FusionVoiceClient>(); //Fusion 방 입퇴장을 따라갈 음성 클라이언트
                    client.UseFusionAppSettings = true;
                    client.UseFusionAuthValues = true;
                    client.PrimaryRecorder = recorder;
                    client.SpeakerPrefab = speaker;
                    SerializedObject clientData = new SerializedObject(client); //Primary Recorder 사용 설정
                    clientData.FindProperty("usePrimaryRecorder").boolValue = true;
                    clientData.ApplyModifiedPropertiesWithoutUndo();
                    runner = PrefabUtility.SaveAsPrefabAsset(root, runnerPath);
                }
                finally { UnityEngine.Object.DestroyImmediate(root); }
            }
            GameObject contents = PrefabUtility.LoadPrefabContents(prefabPath); //기존 사용자 설정을 유지할 게임 프리팹 내용
            try
            {
                AuctionVoiceComponent voice = contents.GetComponent<AuctionVoiceComponent>(); //음성 담당 구성 요소
                if (voice == null)
                    voice = contents.AddComponent<AuctionVoiceComponent>();
                SerializedObject gameData = new SerializedObject(contents.GetComponent<AuctionGame>()); //파사드의 음성 참조 연결
                gameData.FindProperty("voiceComponent").objectReferenceValue = voice;
                gameData.ApplyModifiedPropertiesWithoutUndo();
                SerializedObject networkData = new SerializedObject(contents.GetComponent<AuctionNetworkComponent>()); //네트워크 러너 프리팹 연결
                networkData.FindProperty("runnerPrefab").objectReferenceValue = runner;
                networkData.ApplyModifiedPropertiesWithoutUndo();
                SerializedObject uiData = new SerializedObject(contents.GetComponent<AuctionUIComponent>()); //공간에 맞는 따뜻한 패널 색상
                uiData.FindProperty("panelColor").colorValue = new Color(0.07f, 0.047f, 0.031f, 0.94f);
                uiData.ApplyModifiedPropertiesWithoutUndo();
                PrefabUtility.SaveAsPrefabAsset(contents, prefabPath);
            }
            finally { PrefabUtility.UnloadPrefabContents(contents); }
        }

        public static void buildConnectionSmoke() //마이크를 사용하지 않는 별도 Photon 접속 검증 빌드
        {
            buildWindowsPlayer("Builds/ConnectionSmoke/CantResellSmoke.exe", new[] { "CANTRESELL_CONNECTION_SMOKE" });
        }

        public static void buildWindows() //검증 실행기를 포함하지 않는 일반 Windows 빌드
        {
            buildWindowsPlayer("Builds/Windows/CantResell.exe", Array.Empty<string>());
        }

        private static void buildWindowsPlayer(string path, string[] defines) //동일한 씬 목록으로 지정한 Windows 빌드 생성
        {
            UnityEditor.Build.Reporting.BuildReport report = BuildPipeline.BuildPlayer(new BuildPlayerOptions //현재 세 게임 씬을 포함할 빌드 설정
            {
                scenes = EditorBuildSettings.scenes.Where(scene => scene.enabled).Select(scene => scene.path).ToArray(),
                locationPathName = path, target = BuildTarget.StandaloneWindows64, extraScriptingDefines = defines
            });
            if (report.summary.result != UnityEditor.Build.Reporting.BuildResult.Succeeded)
                throw new InvalidOperationException("Windows build failed: " + report.summary.result);
        }

        public static void setupAndValidate() //자동 실행에서 씬 구성과 EditMode 검증 수행
        {
            try
            {
                setup();
                TestRunnerApi api = ScriptableObject.CreateInstance<TestRunnerApi>(); //Unity 테스트 실행기
                ValidationCallbacks callbacks = new ValidationCallbacks(); //결과 파일 저장 담당
                api.RegisterCallbacks(callbacks);
                api.Execute(new ExecutionSettings(new Filter { testMode = TestMode.EditMode, assemblyNames = new[] { "CantResell.Tests" } }) { runSynchronously = true });
                UnityEngine.Object.DestroyImmediate(api);
                if (Application.isBatchMode)
                    EditorApplication.Exit(callbacks.passed ? 0 : 1);
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
                Directory.CreateDirectory("Logs");
                File.WriteAllText("Logs/AuctionSetupFailure.txt", exception.ToString());
                if (Application.isBatchMode)
                    EditorApplication.Exit(1);
            }
        }

        private sealed class ValidationCallbacks : ICallbacks
        {
            public bool passed; //검증 성공 여부

            public void RunFinished(ITestResultAdaptor result) //검증 결과를 프로젝트 로그에 저장
            {
                passed = result.FailCount == 0 && result.PassCount > 0;
                TestRunnerApi.SaveResultToFile(result, "Logs/AuctionTests.xml");
                File.WriteAllText("Logs/AuctionTests.txt", "Passed: " + result.PassCount + ", Failed: " + result.FailCount + ", Skipped: " + result.SkipCount);
            }

            public void RunStarted(ITestAdaptor tests) //전체 검증 시작 콜백
            {
            }

            public void TestStarted(ITestAdaptor test) //개별 검증 시작 콜백
            {
            }

            public void TestFinished(ITestResultAdaptor result) //개별 검증 종료 콜백
            {
            }
        }
    }
}
