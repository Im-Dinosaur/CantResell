# Unity 6.3 LTS 전환 기록

## 버전과 경로

- 전환일: 2026-09-30
- 이전 버전: Unity 6000.6.0f1
- 현재 버전: Unity 6000.3.25f1 LTS (`e1dba0a9aba4`)
- 프로젝트: `C:\Users\User\CantResell`
- 에디터: `C:\Program Files\Unity\Hub\Editor\6000.3.25f1\Editor\Unity.exe`
- GitHub: `https://github.com/Im-Dinosaur/CantResell.git`

C 드라이브의 설치 중 임시 공간이 부족하여 새 에디터를 D 드라이브에 먼저 설치했다. 사용자가 공간을 확보한 후 C 드라이브로 이전했다. 프로젝트 폴더, 기존 Git 저장소와 Unity 6000.6.0f1 설치는 유지한다.

에디터 설치 파일 49,652개(8,419,074,062바이트)의 SHA-256이 C/D에서 모두 일치하는 것을 확인한 뒤 D의 중복 6.3 설치본을 제거했다. Unity Hub도 C 경로를 사용하며, 기존 CantResell 프로젝트의 버전을 6000.3.25f1로 인식한다. 검증 기록은 `Logs/Unity63_EditorCopy.json`에 있다.

## 전환 전 백업

`C:\Users\User\CantResell_Backups\CantResell_6000.6.0f1_20260930_034129`

Assets와 모든 `.meta`, Packages, ProjectSettings, UserSettings, 루트 파일, 기존 로그 및 `.git`을 복사했다. 커밋되지 않은 파일도 포함한다. 2,408개 파일의 SHA-256을 원본과 대조했으며, 백업 폴더의 `BACKUP_SHA256.json`과 `BACKUP_INFO.json`에 결과를 보관한다.

재생성 가능한 Library, Temp, obj, .vs, Build, Builds는 백업 대상에서 제외한다. 백업본은 6000.6.0f1에서 열면 Library를 다시 생성하여 사용할 수 있다. 백업 후 발생한 변경은 이 백업에 포함되지 않는다.

복구가 필요하면 Unity를 종료하고 현재 작업도 별도로 보존한 뒤, 백업본을 새 폴더에 복사하여 6000.6.0f1에서 연다. 현재 프로젝트 위에 백업을 단순 병합하면 전환 후 추가된 파일이 남을 수 있으므로 피한다.

## 적용 내용

| 대상 | 변경 |
| --- | --- |
| ProjectVersion.txt | 6000.3.25f1 LTS와 해당 리비전 지정 |
| Universal RP | 17.6.0 → 17.3.0 |
| Test Framework | 1.8.0 → 1.6.0 |
| Timeline | 6.6.0 → 1.8.13 |
| uGUI | 2.6.0 → 2.0.0 |
| 내장 모듈 | 6.3에 없는 physicscore2d, tetgen, timelinefoundation 직접 의존성 제거 |
| packages-lock.json / Library | 6.3 에디터에서 다시 생성 |
| AuctionFlowSmokeTests | 6.3 오브젝트 검색 API 사용, URP 셰이더 지원과 대기실 중앙의 실제 렌더링 픽셀 검사 |
| AuctionRulesTests | 세 씬의 누락된 스크립트와 URP 전역 설정의 누락 형식 검사 추가 |
| URP 전역 설정 | 6.6 전용 리소스 참조 6개 정리, 17.3의 설정 형식 버전 10으로 저장 |

런타임 파사드와 Component 구성, 게임 규칙, 씬·프리팹 GUID, Photon 연결 설정을 전환 대상으로 재작성하지 않는다. Photon SDK의 기존 Unity 6.6 조건부 수정은 남기되 6.3에서는 원래 `GetInstanceID()` 경로를 사용한다.

## 검증

| 검사 | 결과 | 기록 |
| --- | --- | --- |
| C 경로의 6.3 컴파일 및 EditMode | 14개 통과, 실패 0 | `Logs/AuctionTests.xml`, `Logs/Unity63_Migration.log` |
| PlayMode 씬·UI·렌더링 | 1개 통과, 실패 0 | `Logs/Unity63_PlayModeFinal.xml`, `Logs/Unity63_PlayModeFinal.log` |
| Windows x64 Mono 빌드 | 성공 | `Logs/Unity63_WindowsBuild.log` |
| Windows 실행 파일 시작 | 15초 동안 정상 실행, 시작 로그에 예외·오류 없음 | `Logs/Unity63_PlayerSmoke.log` |

실행 파일은 `Builds/Windows/CantResell.exe`이다. 배포할 때는 같은 폴더의 데이터와 DLL도 함께 포함해야 한다. 검증용 플레이어 프로세스는 확인 후 종료했다.

화면 캡처는 `Logs/PrototypePreview`에 있다. 에디터의 첫 셰이더 컴파일이 끝나기 전에 배경 없는 캡처가 저장되는 것을 막도록 테스트에서만 동기 셰이더 컴파일을 사용하고, 종료 시 원래 설정을 복원한다. 대기실의 캐릭터·테이블·상품이 표시된 최종 이미지도 확인했다.

초기 재가져오기 로그에는 에셋 등록 전 Photon 설정을 조회하면서 발생한 초기화 오류가 있으나, 최종 PlayMode 재실행과 빌드는 통과했다. Photon SDK의 사용 중단 예정 API 경고는 남아 있다. SDK 연결 설정 값은 변경하지 않았다.

오프라인 테스트와 Windows 빌드는 실제 4인 Photon 온라인 플레이 검증을 대신하지 않는다.

Git 원격 연결과 기존 브랜치는 유지했다. 전환 이후 변경은 작업별 검증을 마친 뒤 커밋·푸시하여 관리한다.

## 참고

- [Unity 6000.3.25f1 공식 릴리스](https://unity.com/releases/editor/whats-new/6000.3.25f1)
- [Unity 6.3 Timeline 지원 버전](https://docs.unity3d.com/6000.3/Documentation/Manual/com.unity.timeline.html)

## 2026-10-01 물리 충돌 설정 보완

밤의 캐릭터 이동 검증에서 바닥과 벽 충돌이 적용되지 않는 문제를 확인했다. DynamicsManager.asset의 레이어 충돌 행렬이 6000.6의 형식 객체로 남아 있었으며, 6000.3에서 읽는 기존 16진수 저장 형식으로 정리했다. 원래의 충돌 허용 비트 값은 그대로 보존했다.

기본 레이어의 실제 충돌 허용 여부를 확인하고 CharacterController가 바닥에 착지하며 집 벽을 통과하지 않는 PlayMode 검사를 통과했다. 결과는 Logs/Gameplay_Physics.xml과 Logs/Gameplay_Physics.log에 있다. Play 공간은 BoxCollider를 명시적으로 참조하고 문 개방에 따라 해당 충돌을 켜고 끈다.
