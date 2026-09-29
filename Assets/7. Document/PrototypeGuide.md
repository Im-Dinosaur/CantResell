# 반품 불가! 첫 시제품

## 실행

1. Unity 6000.3.25f1 LTS에서 프로젝트를 열고 컴파일이 끝날 때까지 기다린다.
2. Photon Fusion App ID와 Voice App ID, 지역 설정을 완료한다. 다른 참가자도 동일한 App ID, 앱 버전, 지역의 빌드를 사용해야 한다.
3. `Assets/1. Scenes/Home.unity`를 열고 Play를 누른다.
4. 이름을 입력하고 **방 만들기** 팝업에서 방 제목(최대 30자)과 공개/비공개를 선택한다. 비공개 방은 문자·숫자 1~8자로 비밀번호를 설정한다. 한글과 영문 및 숫자 혼합이 가능하며 공백·기호는 허용하지 않는다. 내부 방 코드는 자동 생성한다.
5. 다른 참가자는 오른쪽 방 목록에서 제목을 선택한다. 비공개 방도 자물쇠와 함께 표시되며 비밀번호 입력 후 입장한다. 인원이 가득 찼거나 진행 중인 방은 참가할 수 없다. 목록은 실시간 갱신하며 다섯 개씩 이전/다음 페이지로 볼 수 있다.
6. StandBy에서 캐릭터 색상을 고른다. **마이크 켜기**로 같은 방 참가자에게 음성을 보낸다. 기본은 마이크 꺼짐이며 상대 음성은 들을 수 있다. **마이크 테스트**는 입력 막대만 확인하고 음성을 상대에게 보내지 않는다. 테스트를 종료하면 이전 마이크 상태로 돌아간다. 4명 모두 **준비하기**를 누르면 방장만 **게임 시작**을 누를 수 있다.
7. 판매 설명 → 비밀 검사 → 입찰 → 시연·정산을 각자 판매자 한 번씩, 총 4라운드 진행한다.
8. 결과 화면에서 방장은 대기실로 돌아가 재시작할 수 있다. 방 나가기는 Home으로 돌아간다.

## 설정 사용

Home·StandBy·Play의 **설정** 버튼이나 **Esc**로 설정 창을 열고 닫는다. 설정 창이 열린 동안 뒤쪽 버튼은 눌리지 않지만 온라인 게임은 계속 진행된다.

- 왼쪽에서 마스터·음악·효과음 볼륨을 0~100%로 조절한다. 즉시 적용하며 창을 닫거나 게임을 종료하면 저장한다. 마스터는 음성채팅을 포함한 모든 출력에 적용한다.
- 오른쪽에서 본인을 제외한 현재 방 참가자의 수신 음량을 각각 조절한다. 0%는 그 참가자만 음소거한다. 마이크를 켜기 전에도 미리 조절할 수 있으며, 해당 참가자의 마이크 재시작과 StandBy↔Play 전환에도 유지한다. 참가자가 나가거나 내가 방을 떠나면 개인 음량은 초기화한다.
- 지원 해상도와 창 모드/전체 화면을 선택하고 **적용**을 누른다. **이 설정 유지**를 눌러야 저장한다. 15초 동안 확인하지 않거나 **되돌리기**, 설정 닫기, 씬 전환을 하면 이전 화면으로 돌아간다. 실제 화면 크기 변경은 Windows 실행 파일에서 적용되며 Unity 에디터에서는 안내만 표시한다.

음악과 효과음 파일은 아직 포함하지 않았다. `AuctionGame.prefab`의 `Music` 자식 AudioSource에 BGM을 연결하면 시작 시 반복 재생한다. 코드에서는 `AuctionGame.playMusic(clip)`과 `AuctionGame.playEffect(clip)`으로 각 출력 경로를 사용한다. 별도로 만든 AudioSource를 직접 재생하면 해당 채널의 볼륨은 적용되지 않는다.

첫 테스트는 Windows PC의 에디터 1개와 Windows 빌드 3개 또는 PC 4대를 사용한다. 에디터 하나에서 여러 러너를 사용하는 Multi-Peer 시제품은 지원하지 않는다.

6.3 LTS 전환에 사용한 백업 위치, 에디터 설치 경로와 검증 결과는 [Unity63Migration.md](Unity63Migration.md)에 기록했다.

## 현재 임시 규칙

방 목록·UI·음성 구현 이후 사용자와 경매 규칙을 다시 정할 예정이다. 아래 값은 이번 작업에서 변경하지 않은 기존 시제품의 동작이다.

| 설정 | 기본값 |
| --- | --- |
| 참가자 | 정확히 4명 |
| 시작 자금 | 100 코인 |
| 정상 상품 보상 | 120 코인 |
| 최소 입찰 증가액 | 10 코인 |
| 검사권 | 1인당 한 게임에 2회 |
| 정상 확률 | 50% |
| 단계 시간 | 설명 20초 / 검사 15초 / 입찰 25초 / 시연 8초 |

판매자는 자신의 상품을 검사하거나 입찰할 수 없다. 구매자는 검사 단계에 한 번만 검사할 수 있으며, 검사 결과는 본인에게만 전달한다. 검사 결과를 말할 때 거짓말을 해도 된다.

입찰은 호스트가 받은 순서로 검증한다. 판매자 입찰, 잔액을 넘는 입찰, 최소 증가액보다 낮은 입찰, 이전 게임·라운드 요청과 중복 요청은 처리하지 않는다. 최고 입찰액은 낙찰 시 한 번만 차감한다. 정상일 때만 구매 보상을 지급한다. 유찰이면 자금 이동 없이 상품 상태만 공개한다.

가장 돈이 많은 사람이 승리하며 동점은 공동 순위다. 준비·입찰·정산 등 공개 정보와 상품 상태·검사 기록 등 개인 정보를 수신자별로 분리한다. 호스트는 판정을 위해 전체 상태를 보유하므로 방장의 프로세스 자체를 신뢰하지 않는 부정행위 방지 기능은 포함하지 않는다.

## Inspector 연결과 조정

`Assets/3. Prefabs/AuctionGame.prefab`의 기본 참조는 세 씬에 공통 적용된다.

| 스크립트 | 역할 / Inspector 조정 항목 |
| --- | --- |
| AuctionGame | 외부 진입점, 10개 Component 참조와 호출 순서 |
| AuctionRoundComponent | 진행 단계 / 네 단계의 제한 시간 |
| AuctionItemComponent | 비밀 상태와 검사 / 정상 확률, 게임당 검사권 |
| AuctionBidComponent | 입찰과 낙찰자 / 최소 입찰 증가액 |
| AuctionEconomyComponent | 자금과 정산 / 시작 자금, 정상 보상 |
| AuctionNetworkComponent | 방 목록·제목·입장 비밀번호 확인·Fusion 접속 / 러너 프리팹, 씬 이름 |
| AuctionVoiceComponent | Fusion 방 음성 송수신과 마이크 테스트, 참가자별 수신 음량 / 음성 감지 임계값 |
| AuctionSettingsComponent | 음량 저장과 해상도 선택·미리보기·원복 / 기본 마스터 음량 |
| AuctionAudioComponent | 음악과 효과음 재생 및 볼륨 적용 / Music·Effects AudioSource 참조 |
| AuctionVoiceClientComponent | 러너에 연결되는 FusionVoiceClient 확장, 게임 참가자 ID에 맞는 음성 출력 생성 |
| AuctionUIComponent | Home·대기실·경매 UI / 패널 및 강조 색상 |
| AuctionItemViewComponent | Home 테이블·StandBy 정면 캐릭터·토스터 시연 / 머티리얼, 캐릭터 색상, 씬별 카메라 위치 |

런타임 코드는 `Assets/2. Scripts/Runtime`, Unity 편집 도구는 `Editor`, 규칙 검증은 `Tests/Editor`에 있다. `AuctionState`는 공개·개인 상태와 요청의 데이터 형식이다.

UI와 3D 임시 도형은 실행 시 생성된다. 정식 아트와 편집 가능한 UI 프리팹으로 교체하기 전, 네 명의 플레이로 규칙을 시험하기 위한 구성이다. 한글 글꼴은 PC 운영체제의 맑은 고딕 등을 사용하므로 다른 플랫폼 출시 전에는 배포 가능한 한글 폰트를 별도 연결해야 한다.

## 네트워크와 종료 동작

- 설치된 Fusion 2.1.3의 Host 모드를 사용한다. 방장이 단계·입찰·비밀 정보·정산을 판정한다.
- `NetworkRunner`와 `NetworkSceneManagerDefault`가 접속과 씬 이동을 담당한다.
- 빈도가 낮은 턴제 상태를 Fusion Reliable Data API로 참가자별 전송한다. 상태 순번으로 오래된 결과를 버린다. 비밀 상태를 포함한 공용 Networked 프로퍼티는 만들지 않는다.
- 경기 시작 뒤에는 신규 참가를 닫는다. 모든 참가자의 Play 입장을 확인한 뒤 설명 시간을 시작한다.
- 플레이어가 경기 중 나가면 경기를 중단하고 방장이 대기실로 돌아갈 수 있다. 방장이 나가면 방을 종료한다. 호스트 이관과 진행 중 재접속은 후속 범위다.
- `AuctionRunner.prefab`의 AuctionVoiceClientComponent와 Primary Recorder가 게임 방의 음성 연결을 담당하고, `AuctionSpeaker.prefab`이 상대 음성을 재생한다. Recorder.UserData의 Fusion 참가자 ID로 음성 출력을 연결한다. 별도 Voice 서버의 입장 번호를 게임 참가자 ID로 사용하지 않는다. StandBy↔Play 이동 중 같은 러너와 음성 방을 유지하고 퇴장 시 마이크와 연결을 정리한다. 방 목록 호환 버전은 3이며 참가자별 음량을 지원하지 않는 이전 빌드와 방 목록을 섞지 않는다.
- 방 목록에는 제목·인원·잠금 여부만 공개한다. 비밀번호 평문이나 검증 값은 SessionProperties에 넣지 않는다. 입장 요청에는 방 코드와 비밀번호로 만든 SHA-256 값을 전달하여 호스트가 비교한다. 별도 인증 서버를 사용하는 계정 인증 체계는 아니다.

Photon 연결 설정 파일은 사용자가 관리하며 코드에서 App ID를 저장하거나 덮어쓰지 않는다.

설치한 SDK의 `Assets/Photon/PhotonRealtime/Code/EventBetter.cs`에는 Unity 6000.6 호환 분기가 남아 있다. 현재 사용하는 6000.3 LTS에서는 원래 SDK의 `int`와 `GetInstanceID()` 분기로 컴파일되며, 6000.6 이상에서만 `EntityId`와 `GetEntityId()`를 사용한다. [Unity의 식별자 이전 안내](https://docs.unity.com/en-us/engine/6000.6/manual/scripting/fundamental-unity-types/instanceid-to-entityid-migration).

## 검증과 유지보수

Unity Test Runner의 EditMode에서 `CantResell.Tests`를 실행한다. 잔액·판매자·증가액 검증, 정상/불량 정산과 중복 방지, 유찰, 검사권과 비밀 정보 분리, 시간 경계, 실제 씬 참조를 확인한다.

`CantResell > Set Up Prototype` 메뉴는 누락된 초기 프리팹과 음성 프리팹을 생성하고 세 씬의 진입점을 연결한다. 음성 참조와 공통 패널 색상을 적용하며 기존 경매 설정을 유지한다. 저장되지 않은 씬 변경이 있으면 중단한다.

자동 검증 결과는 `Logs/AuctionTests.xml`, `Logs/AuctionTests.txt`에 기록한다. 오프라인 규칙·씬 검증만으로 실제 4인 Photon 접속과 시차가 있는 환경의 게임 진행을 검증했다고 간주하지 않는다.

PlayMode의 `CantResell.PlayTests`는 네트워크 없이 수신자용 표시 데이터를 주입하여 방 목록·페이지 이동·잠금 방 팝업·생성 입력 검증, 세 씬의 초기화, 싱글턴 유지, 설정 창, 개인 정보 표시와 공동 순위를 검증한다. 화면 캡처는 `Logs/PrototypePreview`에 저장하며 캡처의 방 목록은 테스트용 데이터다. 배치 실행에서는 Home의 자동 로비 접속을 생략하여 오프라인 검증이 외부 연결을 시작하지 않는다.

`AuctionProjectSetup.buildConnectionSmoke`는 `CANTRESELL_CONNECTION_SMOKE` 심볼을 사용하는 별도 빌드를 만든다. 일반 `buildWindows` 빌드에는 검증 실행기가 포함되지 않는다. 2026-09-30에 로컬 Windows 프로세스 네 개로 실제 Photon 목록 조회, 틀린 비밀번호 거부와 재시도, 비공개·공개 입장, 준비 및 Home→StandBy→Play→StandBy→Home, 방장 퇴장 후 정리를 통과했다. 생성한 PCM 음원을 보내 다른 프로세스 Speaker에서 재생 상태를 확인했다. 모든 실제 마이크는 꺼진 상태이며 실제 장치 녹음 품질과 서로 다른 PC의 환경은 별도 확인 대상이다. 결과는 `Logs/ConnectionSmoke/run01`에 기록했다.

설정 기능 추가 후 EditMode 34개와 PlayMode 시나리오 1개를 통과했다. 채널별 저장과 기존 마스터 설정 보존, 출력 분리, 참가자 입퇴장과 ID별 음량, 세 씬의 설정창·해상도 목록·배경 입력 차단을 검사했다. 실제 Photon 4인 검증에서는 서로 다른 두 참가자의 생성 PCM을 수신하여 UI에서 한 명 음소거·다른 한 명 35% 조절과 음성 스트림 재시작 후 음량 유지를 확인했다. 별도 Windows 실행에서는 1280×720 저장, 1600×900 미리보기 취소, 15초 자동 원복, 전체 화면 전환과 창 모드 복귀를 통과했다. 결과는 `Logs/SettingsSmoke/run01`, 설정창 이미지는 `Logs/PrototypePreview/SettingsHome.png`, `SettingsStandBy.png`에 있다.

참고: [Fusion 방 목록](https://doc.photonengine.com/fusion/v2/manual/connection-and-matchmaking/matchmaking), [Fusion Voice 연동](https://doc.photonengine.com/voice/v2/getting-started/voice-for-fusion), [Fusion 2 데이터 전송](https://doc.photonengine.com/fusion/v2/manual/data-transfer/data-streaming).
