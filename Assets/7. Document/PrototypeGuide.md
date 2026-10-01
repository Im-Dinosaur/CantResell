# 반품 불가! 첫 시제품

## 실행

1. Unity 6000.3.25f1 LTS에서 프로젝트를 열고 컴파일이 끝날 때까지 기다린다.
2. Photon Fusion App ID와 Voice App ID, 지역 설정을 완료한다. 다른 참가자도 동일한 App ID, 앱 버전, 지역의 빌드를 사용해야 한다.
3. `Assets/1. Scenes/Home.unity`를 열고 Play를 누른다.
4. 이름을 입력하고 **방 만들기** 팝업에서 방 제목(최대 30자)과 공개/비공개를 선택한다. 비공개 방은 문자·숫자 1~8자로 비밀번호를 설정한다. 한글과 영문 및 숫자 혼합이 가능하며 공백·기호는 허용하지 않는다. 내부 방 코드는 자동 생성한다.
5. 다른 참가자는 오른쪽 방 목록에서 제목을 선택한다. 비공개 방도 자물쇠와 함께 표시되며 비밀번호 입력 후 입장한다. 인원이 가득 찼거나 진행 중인 방은 참가할 수 없다. 목록은 실시간 갱신하며 다섯 개씩 이전/다음 페이지로 볼 수 있다.
6. StandBy에서 캐릭터 색상을 고른다. **마이크 켜기**로 같은 방 참가자에게 음성을 보낸다. 기본은 마이크 꺼짐이며 상대 음성은 들을 수 있다. **마이크 테스트**는 입력 막대만 확인하고 음성을 상대에게 보내지 않는다. 테스트를 종료하면 이전 마이크 상태로 돌아간다. 4명 모두 **준비하기**를 누르면 방장만 **게임 시작**을 누를 수 있다.
7. 개인 목표를 확인하고 상품 설명 → 입찰을 네 명이 한 번씩 판매자로 진행한다. 회차마다 밤 행동 네 번을 거친다. 기본 세 회차 뒤 목표 달성 점수로 승자를 정한다.
8. 결과 화면에서 방장은 대기실로 돌아가 재시작할 수 있다. 방 나가기는 Home으로 돌아간다.

## 설정 사용

Home·StandBy·Play의 **설정** 버튼이나 **Esc**로 설정 창을 열고 닫는다. 설정 창이 열린 동안 뒤쪽 버튼은 눌리지 않지만 온라인 게임은 계속 진행된다.

- 왼쪽에서 마스터·음악·효과음 볼륨을 0~100%로 조절한다. 즉시 적용하며 창을 닫거나 게임을 종료하면 저장한다. 마스터는 음성채팅을 포함한 모든 출력에 적용한다.
- 오른쪽에서 본인을 제외한 현재 방 참가자의 수신 음량을 각각 조절한다. 0%는 그 참가자만 음소거한다. 마이크를 켜기 전에도 미리 조절할 수 있으며, 해당 참가자의 마이크 재시작과 StandBy↔Play 전환에도 유지한다. 참가자가 나가거나 내가 방을 떠나면 개인 음량은 초기화한다.
- 지원 해상도와 창 모드/전체 화면을 선택하고 **적용**을 누른다. **이 설정 유지**를 눌러야 저장한다. 15초 동안 확인하지 않거나 **되돌리기**, 설정 닫기, 씬 전환을 하면 이전 화면으로 돌아간다. 실제 화면 크기 변경은 Windows 실행 파일에서 적용되며 Unity 에디터에서는 안내만 표시한다.

음악과 효과음 파일은 아직 포함하지 않았다. `AuctionGame.prefab`의 `Music` 자식 AudioSource에 BGM을 연결하면 시작 시 반복 재생한다. 코드에서는 `AuctionGame.playMusic(clip)`과 `AuctionGame.playEffect(clip)`으로 각 출력 경로를 사용한다. 별도로 만든 AudioSource를 직접 재생하면 해당 채널의 볼륨은 적용되지 않는다.

첫 테스트는 Windows PC의 에디터 1개와 Windows 빌드 3개 또는 PC 4대를 사용한다. 에디터 하나에서 여러 러너를 사용하는 Multi-Peer 시제품은 지원하지 않는다.

6.3 LTS 전환에 사용한 백업 위치, 에디터 설치 경로와 검증 결과는 [Unity63Migration.md](Unity63Migration.md)에 기록했다.

## 집들이 경매 규칙

주제는 **수상한 이웃들의 집들이 경매**다. 시작 때 서로 다른 비공개 목표를 배정하고, 필요한 물건을 경매와 도둑질로 모은다.

| 목표 | 핵심 50점 | 보조 30점 | 장식 20점 |
| --- | --- | --- | --- |
| 홈 카페 | 커피 머신 | 테이블 | 조명 |
| 음악방 | 턴테이블 | 스피커 | 조명 |
| 작업실 | 공구 | 작업대 | 수납장 |
| 편안한 방 | 침대 | 난방기 | 조명 |

정상 상품은 배점의 100%, 낡은 상품은 50%, 고장 상품은 0%를 얻는다. 같은 종류를 여러 개 보유해도 최고 성능 하나만 계산한다. 보관 중인 상품만 점수에 포함하고 운반 중에는 원래 소유자의 점수에서 제외한다. 목표 점수가 가장 높은 사람이 이기며 동점은 공동 우승이다. 돈은 입찰 자원으로 사용하고 승리 점수에는 더하지 않는다.

| 설정 | 기본값 |
| --- | --- |
| 참가자 | 정확히 4명 |
| 시작 자금 | 300 코인, 시작 때 한 번 |
| 자금 수급 | 본인 상품 낙찰액만 |
| 최소 입찰 증가액 | 10 코인 |
| 반복 회차 | 3회, Inspector에서 1~8회 |
| 단계 시간 | 설명 20초 / 입찰 25초 / 밤 각자 45초 |
| 상품 상태 확률 | 정상 65% / 낡음 25% / 고장 10% |

공개 도감은 목표 상품 10종과 자물쇠·고무 망치·쇠지렛대 3종이다. 판매 턴에 상품 하나를 무작위 발급한다. 한 주머니에서 모든 종류를 한 번씩 추첨한 뒤 다시 섞어 특정 종류의 편중을 줄인다. 한 회차에 좌석 순서대로 네 번 판매하며 판매자는 자신의 상품에 입찰할 수 없다.

원가·상태는 처음에 판매자만 알고 낙찰 후 낙찰자에게만 추가 공개한다. 이전 판매자와 낙찰자는 이후에도 그 정보를 기억한다. 다른 사람에게는 원가·상태를 전송하지 않으며, 최종 결과에서도 상품별 상태를 일괄 공개하지 않는다. 유찰 상품은 거래 없이 제외한다. 구매 보상이나 회차별 무료 자금은 없다.

오른쪽 사각형에는 본인의 목표·소지금·확인된 점수·보유품을 표시하고, 상단 막대에는 현재 설명·입찰·밤 시간의 잔여 비율을 표시한다. 도둑질한 상품은 원가·상태를 새로 공개하지 않는다. 숨겨진 상태를 점수로 역산할 수 없도록 미확인 보유품은 최종 결과 전까지 확인 점수에 더하지 않고 개수만 안내한다.

## 밤 행동과 조작

네 판매가 끝나면 무작위 순서로 각자 한 번의 침입 턴을 가진다. 자신의 턴에만 집 밖으로 나갈 수 있다. 나머지 사람도 자기 집 안에서는 움직이고 방어할 수 있다. 각 턴 시작 때 모두 자기 집으로 돌아가 체력을 회복하며, 자물쇠 파괴 상태는 그 밤이 끝날 때까지 유지한다.

- **WASD**: 이동. 밤에는 본인 캐릭터를 따라가는 카메라.
- **E**: 가까운 문 열기·닫기, 지렛대로 잠긴 문 파괴, 상대 집 안의 물건 들기.
- **Q**: 가까운 자기 집 문 잠금·해제. 작동하는 자물쇠를 보유해야 하며 파괴된 문은 그 밤에 다시 잠글 수 없다.
- **Space**: 고무 망치로 자기 집 안의 침입자 공격. 정상은 40, 낡은 망치는 20 피해이며 0.7초 간격.
- **이번 밤 행동 마치기**: 본인의 침입 턴만 종료. 운반 중인 상품은 실패 처리.
- **Esc / 설정**: 볼륨·개별 음성 음량·해상도. 창이나 입찰 입력에 집중하는 동안 이동 입력을 차단한다.

침입자는 물건 하나를 들어 자기 집 내부까지 돌아와야 소유권을 얻는다. 운반 중에도 원래 소유자는 유지되며, 제압·시간 초과·턴 종료·게임 중단이면 원래 집으로 되돌린다. 탈락은 없다. 문과 벽은 실제 Collider로 이동을 막고, 호스트가 이동·거리·도구·공격·소유권을 판정한다.

밤에는 닉네임을 게임 화면과 설정창에서 숨기며 모든 캐릭터의 외형·색상을 동일한 어두운 실루엣으로 표시한다. 개인 음성 음량은 그대로 적용한다. 실제 목소리까지 변조하는 기능은 포함하지 않는다.

## Inspector 연결과 조정

세 씬은 공통 Assets/3. Prefabs/AuctionGame.prefab을 참조한다. 연결은 자동 구성했으므로 신규 필드 연결을 직접 할 필요가 없다.

| 스크립트 | 역할 / Inspector 설정 |
| --- | --- |
| AuctionGame | 13개 담당 Component 참조와 단계 전환 조율 |
| AuctionRoundComponent | 회차 수·설명·입찰·밤 제한 시간 |
| AuctionItemComponent | 전체 도감·원가 범위·상품 상태 확률 |
| AuctionInventoryComponent | 소유권·운반·개별 열람 권한 |
| AuctionObjectiveComponent | 목표 이름·50/30/20점 상품 구성 |
| AuctionNightComponent | 무작위 행동 순서·출입문·도구 사용·운반·방어 규칙 |
| AuctionBidComponent / AuctionEconomyComponent | 입찰 증가액 / 초기 자금과 단일 정산 |
| AuctionNetworkComponent | 방 목록·입장 검증·씬 전환·러너 및 Player 프리팹 |
| AuctionItemViewComponent | Home·StandBy 공간과 Play 테이블·네 집·보관품·카메라 |
| AuctionUIComponent | 도감·목표·시간 막대·입찰·밤 안내·결과·기존 메뉴 |
| AuctionVoiceComponent / AuctionVoiceClientComponent | Fusion Voice·마이크 테스트·개별 수신 음량 |
| AuctionSettingsComponent / AuctionAudioComponent | 저장·해상도 미리보기·음악과 효과음 채널 |
| Player | Fusion 캐릭터 파사드·낮 스킨·밤 실루엣·체력·운반 표시 |
| PlayerInputComponent | Input System에서 Fusion 입력 수집 |
| PlayerMovementComponent | NetworkCharacterController의 이동·충돌·집 경계 |
| PlayerInteractionComponent / PlayerCombatComponent | 상호작용 거리·연타 제한 / 공격 거리·간격 |
| PlayerHouse / HouseDoorComponent | 집의 진입점·보관 위치 / 문 충돌과 개방 표시 |

Assets/3. Prefabs/Player.prefab에는 NetworkObject와 NetworkCharacterController 및 Player 구성을 연결했다. NetworkProjectConfig의 AssembliesToWeave에 CantResell.Runtime을 추가하고 Fusion 프리팹 목록에 등록한다. 네트워크 비밀 정보를 Player의 공용 Networked 프로퍼티에 넣지 않는다.

UI와 집 및 상품은 규칙 테스트용 임시 도형이며 정식 아트로 교체할 수 있다. OS 한글 글꼴을 사용하므로 다른 플랫폼 출시에는 배포 가능한 글꼴을 연결해야 한다. 코드·메타·프리팹은 프로젝트에 직접 반영한다.

## 네트워크와 종료 동작

- 설치된 Fusion 2.1.3의 Host 모드를 사용한다. 방장이 단계·입찰·비밀 정보·정산을 판정한다.
- `NetworkRunner`와 `NetworkSceneManagerDefault`가 접속과 씬 이동을 담당한다.
- 경매·개인 목표·보유품 상태는 Fusion Reliable Data API로 참가자별 전송한다. 실제 캐릭터 입력과 위치는 Fusion NetworkObject와 NetworkCharacterController로 동기화한다. 상태 순번으로 오래된 결과를 버린다. 비밀 상태를 포함한 공용 Networked 프로퍼티는 만들지 않는다.
- 경기 시작 뒤에는 신규 참가를 닫는다. 모든 참가자의 Play 입장을 확인한 뒤 설명 시간을 시작한다.
- 플레이어가 경기 중 나가면 경기를 중단하고 방장이 대기실로 돌아갈 수 있다. 방장이 나가면 방을 종료한다. 호스트 이관과 진행 중 재접속은 후속 범위다.
- `AuctionRunner.prefab`의 AuctionVoiceClientComponent와 Primary Recorder가 게임 방의 음성 연결을 담당하고, `AuctionSpeaker.prefab`이 상대 음성을 재생한다. Recorder.UserData의 Fusion 참가자 ID로 음성 출력을 연결한다. 별도 Voice 서버의 입장 번호를 게임 참가자 ID로 사용하지 않는다. StandBy↔Play 이동 중 같은 러너와 음성 방을 유지하고 퇴장 시 마이크와 연결을 정리한다. 방 목록 호환 버전은 4이며 참가자별 음량을 지원하지 않는 이전 빌드와 방 목록을 섞지 않는다.
- 방 목록에는 제목·인원·잠금 여부만 공개한다. 비밀번호 평문이나 검증 값은 SessionProperties에 넣지 않는다. 입장 요청에는 방 코드와 비밀번호로 만든 SHA-256 값을 전달하여 호스트가 비교한다. 별도 인증 서버를 사용하는 계정 인증 체계는 아니다.

Photon 연결 설정 파일은 사용자가 관리하며 코드에서 App ID를 저장하거나 덮어쓰지 않는다.

설치한 SDK의 `Assets/Photon/PhotonRealtime/Code/EventBetter.cs`에는 Unity 6000.6 호환 분기가 남아 있다. 현재 사용하는 6000.3 LTS에서는 원래 SDK의 `int`와 `GetInstanceID()` 분기로 컴파일되며, 6000.6 이상에서만 `EntityId`와 `GetEntityId()`를 사용한다. [Unity의 식별자 이전 안내](https://docs.unity.com/en-us/engine/6000.6/manual/scripting/fundamental-unity-types/instanceid-to-entityid-migration).

## 검증과 유지보수

Unity Test Runner의 EditMode에서 `CantResell.Tests`를 실행한다. 잔액·판매자·증가액 검증, 자금 보존과 중복 정산 방지, 유찰, 상품 비밀 분리, 운반 성공·실패, 목표 점수와 중복 상품, 무작위 밤 순서와 정체 숨김, 시간 경계, 실제 씬 참조를 확인한다.

`CantResell > Set Up Prototype` 메뉴는 누락된 초기 프리팹과 음성 프리팹을 생성하고 세 씬의 진입점을 연결한다. 음성 참조와 공통 패널 색상을 적용하며 기존 경매 설정을 유지한다. 저장되지 않은 씬 변경이 있으면 중단한다.

자동 검증 결과는 `Logs/AuctionTests.xml`, `Logs/AuctionTests.txt`에 기록한다. 오프라인 규칙·씬 검증만으로 실제 4인 Photon 접속과 시차가 있는 환경의 게임 진행을 검증했다고 간주하지 않는다.

PlayMode의 `CantResell.PlayTests`는 네트워크 없이 수신자용 표시 데이터를 주입하여 방 목록·페이지 이동·잠금 방 팝업·생성 입력 검증, 세 씬의 초기화, 싱글턴 유지, 설정 창, 개인 정보 표시와 공동 순위를 검증한다. 화면 캡처는 `Logs/PrototypePreview`에 저장하며 캡처의 방 목록은 테스트용 데이터다. 배치 실행에서는 Home의 자동 로비 접속을 생략하여 오프라인 검증이 외부 연결을 시작하지 않는다.

`AuctionProjectSetup.buildConnectionSmoke`는 `CANTRESELL_CONNECTION_SMOKE` 심볼을 사용하는 별도 빌드를 만든다. 일반 `buildWindows` 빌드에는 검증 실행기가 포함되지 않는다. 2026-09-30에 로컬 Windows 프로세스 네 개로 실제 Photon 목록 조회, 틀린 비밀번호 거부와 재시도, 비공개·공개 입장, 준비 및 Home→StandBy→Play→StandBy→Home, 방장 퇴장 후 정리를 통과했다. 생성한 PCM 음원을 보내 다른 프로세스 Speaker에서 재생 상태를 확인했다. 모든 실제 마이크는 꺼진 상태이며 실제 장치 녹음 품질과 서로 다른 PC의 환경은 별도 확인 대상이다. 결과는 `Logs/ConnectionSmoke/run01`에 기록했다.

설정 기능 추가 후 EditMode 34개와 PlayMode 시나리오 1개를 통과했다. 채널별 저장과 기존 마스터 설정 보존, 출력 분리, 참가자 입퇴장과 ID별 음량, 세 씬의 설정창·해상도 목록·배경 입력 차단을 검사했다. 실제 Photon 4인 검증에서는 서로 다른 두 참가자의 생성 PCM을 수신하여 UI에서 한 명 음소거·다른 한 명 35% 조절과 음성 스트림 재시작 후 음량 유지를 확인했다. 별도 Windows 실행에서는 1280×720 저장, 1600×900 미리보기 취소, 15초 자동 원복, 전체 화면 전환과 창 모드 복귀를 통과했다. 결과는 `Logs/SettingsSmoke/run01`, 설정창 이미지는 `Logs/PrototypePreview/SettingsHome.png`, `SettingsStandBy.png`에 있다.

참고: [Fusion 방 목록](https://doc.photonengine.com/fusion/v2/manual/connection-and-matchmaking/matchmaking), [Fusion Voice 연동](https://doc.photonengine.com/voice/v2/getting-started/voice-for-fusion), [Fusion 2 데이터 전송](https://doc.photonengine.com/fusion/v2/manual/data-transfer/data-streaming).

## 2026-10-01 메인 플레이 검증

Unity 6000.3.25f1에서 EditMode 37개, PlayMode 통합 시나리오 1개를 통과했다. EditMode는 실제 게임 조율기를 통해 기본 3회차의 12번 판매와 12번 밤 턴까지 진행하고 자금 보존·중복 정산·비밀 열람·도둑질 소유권·목표 점수·종료를 확인한다. PlayMode는 Home·StandBy·Play 및 설정 연결, 밤의 익명 표시, 실제 CharacterController의 바닥 착지와 벽 충돌을 포함한다. 결과는 Logs/AuctionTests.xml과 Logs/Gameplay_PlayModeCompleted.xml에 있다.

일반 입력 경로를 사용하는 검증용 Windows 프로세스 네 개로 실제 Photon 연결, 캐릭터 생성, 판매와 낙찰, 밤 진입, WASD 물리 이동, 자물쇠 파괴, 상대 물건 운반 후 자기 집 귀환에 따른 소유권 이전, 망치의 실제 피해, 운반 중 제한 시간 종료와 원래 소유권 복구, 이전 밤 턴 종료 요청 거부, 결과 공개, 대기실 복귀, 방장 종료에 따른 모든 연결 정리를 확인했다. 네 프로세스 모두 종료 코드 0이며 Logs/GameplaySmoke/run07에 COMPLETE 기록이 있다. 밤 순서는 이동을 재현하기 위해 이 검증 빌드에서 고정했고, 무작위 순서 자체는 EditMode에서 검사했다. 도둑질로 얻은 상품의 원가·상태는 추가 공개되지 않는다.

일반 Windows 빌드 Builds/Windows/CantResell.exe를 생성하고 15초 시작 검사에서 런타임 오류가 없음을 확인했다. 일반 빌드의 런타임 어셈블리에 검증 실행기가 포함되지 않는 것도 확인했다. 기록은 Logs/Gameplay_WindowsBuild.log와 Logs/Gameplay_WindowsStartup.log에 있다. 서로 다른 PC에서의 지연·마이크 장치 품질은 이번 로컬 네 프로세스 검사에서 확인하지 않았다. 현재 캐릭터와 집은 조작·규칙 검증용 임시 도형이다.
