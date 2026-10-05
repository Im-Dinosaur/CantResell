# CantResell 프로젝트 구조

## 에셋 폴더

| 폴더 | 용도 |
| --- | --- |
| `1. Scenes` | Home, StandBy, Play 씬 |
| `2. Scripts` | C# 진입점 및 기능별 Component 스크립트 |
| `3. Prefabs` | 재사용할 게임 오브젝트 프리팹 |
| `4. Images` | UI 이미지, 스프라이트, 텍스처 |
| `5. Audios` | 효과음과 배경음 |
| `6. Data` | 게임 설정과 데이터 에셋 |
| `7. Document` | 기획 및 개발 문서 |

빈 에셋 폴더의 `.gitkeep`은 Git에서 폴더를 유지하기 위한 파일이다.
기존 URP 설정과 템플릿 안내 에셋은 Settings, TutorialInfo에 있다.

## 씬 구성

| 빌드 순서 | 씬 | 담당 역할 및 구현 예정 기능 |
| --- | --- | --- |
| 0 | `Home` | 게임 시작 화면. 방 만들기, 참가하기, 설정, 게임 나가기 |
| 1 | `StandBy` | 게임 시작 전 참가자가 모이는 대기실. 캐릭터 커스터마이징, 음성 테스트 등 |
| 2 | `Play` | 낮 공동 가구점과 밤 동시 침입 진행 |

목표 흐름은 Home에서 방을 만들거나 참가한 뒤 StandBy에서 준비하고 Play에서 게임을 진행하는 것이다.

세 씬은 공통 `AuctionGame` 프리팹으로 연결한다. 첫 진입점은 씬 이동 중 유지되고 중복 인스턴스는 제거된다.
진입점은 씬 역할에 맞는 메뉴 UI와 임시 3D 테이블·캐릭터·상품을 실행 시 구성한다.
Home에는 방 목록과 제목·공개 여부·비밀번호를 설정하는 방 생성 팝업이 있다. 방 코드는 자동 생성하고 목록에서 선택하여 참가한다.
StandBy는 네 참가자의 정면 배치와 준비·색상 선택·마이크 테스트를 제공한다. Fusion Voice로 대기실과 게임 중 대화하며 마이크 송신은 사용자가 켠 뒤 시작한다.
Play는 `AuctionGame → FurnitureStore → Furniture…Component` 호출 구조로 공동 가구점 게임을 진행한다. 역할은 강제하지 않는다. 정식 캐릭터 아트와 추가 외형 커스터마이징은 후속 작업이다.

| 진입점 / 구성 요소 | 책임 |
| --- | --- |
| `AuctionGame` | 방·권한·씬·상태 전송, FurnitureStore 호출 |
| `FurnitureStore` | 게임 담당 구성 요소 연결과 행동/단계 조율 |
| `FurnitureRoundComponent` | 4일 영업·3밤 침입과 마지막 공동 결과 |
| `FurnitureEconomyComponent` | 공금·판매·도박·벌금·장부 |
| `FurnitureInventoryComponent` | 가구 생성·운반·적재·몰수 |
| `FurnitureShopComponent` | 주문·손님 인내·판매 |
| `FurnitureNightComponent` | 센서·신고·경찰 추격·철수 |
| `FurnitureLeisureComponent` | 무료 타이밍 놀이와 최고 기록 |
| `FurnitureWorldComponent` | 공간·가구 모형·카메라·문 충돌·동료 표시 |
| `FurnitureUIComponent` | 주문·공금·도박 입력·장부·경보·결과 화면 |
| `FurnitureState` | 호스트에서 복사하여 전송하는 공동 상태 |

기존 Auction 이름의 방·음성·설정·캐릭터 코드를 재사용한다. 이전 경매·개인 목표·순차 침입 구성 요소는 과거 규칙의 회귀 검사와 자산 호환을 위해 보존하며, 연결된 FurnitureStore가 실제 Play를 담당한다. 일반 빌드에는 자동 접속/이동 검증 실행기가 들어가지 않는다.
기존 SampleScene은 같은 폴더에 보존하며 빌드 목록에서는 제외한다.

실행 방법과 Inspector 조정 항목은 `PrototypeGuide.md`를 참고한다.

## 코드와 커밋 규칙

Unity 공통 개발 지침에 따라 기능별 진입점이 담당 Component를 연결하고 호출한다.
커밋 제목은 `[Type] 한국어 변경 요약` 형식을 사용한다.
유형은 Feat, Fix, Build, Chore, Ci, Docs, Style, Refactor, Test, Release를 사용한다.
