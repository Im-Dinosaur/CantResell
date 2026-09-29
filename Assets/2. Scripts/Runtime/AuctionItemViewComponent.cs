using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace CantResell
{
    public sealed class AuctionItemViewComponent : MonoBehaviour
    {
        [SerializeField] private Material surfaceMaterial; //빌드에 포함할 URP 기본 머티리얼
        [SerializeField] private Vector3 homeCameraPosition = new Vector3(0, 5.8f, -8.6f); //Home 테이블을 내려다볼 카메라 위치
        [SerializeField] private Vector3 lobbyCameraPosition = new Vector3(0, 2.35f, -10); //StandBy 참가자 정면 카메라 위치
        [SerializeField] private Color[] playerColors = { new Color(1, 0.36f, 0.33f), new Color(0.24f, 0.83f, 0.70f), new Color(1, 0.77f, 0.25f), new Color(0.64f, 0.45f, 0.96f) }; //선택 가능한 캐릭터 색상
        private GameObject roomRoot; //현재 씬의 테이블과 캐릭터 루트
        private readonly List<Material> materials = new List<Material>(); //생성한 머티리얼의 수명 관리
        private readonly Renderer[] characters = new Renderer[4]; //임시 캐릭터 렌더러
        private Transform toaster; //시연할 토스터
        private Transform toast; //정상 작동 때 튀어나올 식빵
        private readonly Transform[] smoke = new Transform[6]; //불량 시연의 연기 표현
        private Vector3 toasterPosition; //토스터의 기본 위치
        private int shownMatch = -1; //시연 중인 게임 번호
        private int shownRound = -1; //시연 중인 라운드 번호
        private float revealAt; //시연 시작 시각
        private bool revealing; //시연 재생 여부
        private bool good; //전체 공개된 상품 상태

        public void showScene(string sceneName) //기본 3D 방과 임시 상품 구성
        {
            clearRoom();
            roomRoot = new GameObject("AuctionRoomView");
            Scene targetScene = SceneManager.GetSceneByName(sceneName); //전환 중 활성 씬과 구분할 표시 대상 씬
            Camera camera = null; //표시 대상 씬에 속한 카메라
            if (targetScene.IsValid() && targetScene.isLoaded)
            {
                SceneManager.MoveGameObjectToScene(roomRoot, targetScene);
                foreach (GameObject sceneRoot in targetScene.GetRootGameObjects()) //대상 씬의 카메라 검색
                {
                    camera = sceneRoot.GetComponentInChildren<Camera>();
                    if (camera != null)
                        break;
                }
            }
            if (camera != null)
            {
                camera.transform.position = sceneName == "StandBy" ? lobbyCameraPosition : sceneName == "Home" ? homeCameraPosition : new Vector3(0, 5.3f, -8.6f);
                camera.transform.LookAt(sceneName == "StandBy" ? new Vector3(0, 1.4f, 0) : new Vector3(0, 0.8f, 0.5f));
                camera.fieldOfView = sceneName == "StandBy" ? 46 : 42;
                camera.clearFlags = CameraClearFlags.SolidColor;
                camera.backgroundColor = new Color(0.03f, 0.021f, 0.017f);
            }
            if (sceneName != "Play")
            {
                buildLounge(sceneName == "StandBy");
                return;
            }
            createShape("Floor", PrimitiveType.Cube, new Vector3(0, -0.2f, 0), new Vector3(14, 0.2f, 12), new Color(0.07f, 0.10f, 0.17f));
            createShape("Table", PrimitiveType.Cylinder, new Vector3(0, 0.85f, 0.5f), new Vector3(3.8f, 0.12f, 2.8f), new Color(0.18f, 0.33f, 0.33f));
            createShape("TableBase", PrimitiveType.Cylinder, new Vector3(0, 0.35f, 0.5f), new Vector3(0.6f, 0.4f, 0.6f), new Color(0.15f, 0.19f, 0.25f));
            Vector3[] seats = { new Vector3(-2.3f, 0.9f, -0.3f), new Vector3(-1.7f, 0.9f, 2.5f), new Vector3(1.7f, 0.9f, 2.5f), new Vector3(2.3f, 0.9f, -0.3f) }; //테이블 주변 좌석 위치
            for (int slot = 0; slot < 4; slot++) //임시 캐릭터 생성 번호
                characters[slot] = createShape("Player" + (slot + 1), PrimitiveType.Capsule, seats[slot], new Vector3(0.52f, 0.66f, 0.52f), playerColors[slot]).GetComponent<Renderer>();
            toasterPosition = new Vector3(0, 1.25f, 0.5f);
            toaster = createShape("Toaster", PrimitiveType.Cube, toasterPosition, new Vector3(0.85f, 0.52f, 0.52f), new Color(0.83f, 0.85f, 0.91f)).transform;
            createShape("ToasterSlot", PrimitiveType.Cube, new Vector3(0, 1.519f, 0.5f), new Vector3(0.6f, 0.02f, 0.17f), new Color(0.07f, 0.08f, 0.11f));
            toast = createShape("Toast", PrimitiveType.Cube, new Vector3(0, 1.6f, 0.5f), new Vector3(0.48f, 0.4f, 0.12f), new Color(0.96f, 0.73f, 0.35f)).transform;
            toast.gameObject.SetActive(false);
            for (int index = 0; index < smoke.Length; index++) //불량 시연의 연기 조각 번호
            {
                smoke[index] = createShape("Smoke" + index, PrimitiveType.Sphere, toasterPosition, Vector3.one * 0.18f, new Color(0.3f, 0.32f, 0.38f)).transform;
                smoke[index].gameObject.SetActive(false);
            }
        }

        private void buildLounge(bool lobby) //테이블 메뉴와 참가자 정면 대기실의 임시 공간 구성
        {
            createShape("Floor", PrimitiveType.Cube, new Vector3(0, -0.2f, 0), new Vector3(18, 0.2f, 16), new Color(0.15f, 0.085f, 0.05f));
            for (int plank = 0; plank < 22; plank++) //나무 바닥의 판자 간격
                createShape("FloorPlank" + plank, PrimitiveType.Cube, new Vector3(-8.4f + plank * 0.8f, -0.08f, 0), new Vector3(0.78f, 0.05f, 16),
                    plank % 2 == 0 ? new Color(0.22f, 0.12f, 0.062f) : new Color(0.18f, 0.093f, 0.045f));
            createShape("BackWall", PrimitiveType.Cube, new Vector3(0, 3.7f, 6), new Vector3(30, 8, 0.25f), new Color(0.085f, 0.043f, 0.029f));
            for (int beam = 0; beam < 7; beam++) //벽의 수직 목재 기둥
                createShape("WallBeam" + beam, PrimitiveType.Cube, new Vector3(-7.5f + beam * 2.5f, 2.3f, 5.78f), new Vector3(0.17f, 5, 0.2f), new Color(0.23f, 0.13f, 0.068f));
            createShape("BackCounter", PrimitiveType.Cube, new Vector3(0, 0.8f, 4.5f), new Vector3(12, 1.6f, 1), new Color(0.17f, 0.09f, 0.045f));
            createShape("CounterTop", PrimitiveType.Cube, new Vector3(0, 1.65f, 4.5f), new Vector3(12.2f, 0.12f, 1.2f), new Color(0.33f, 0.19f, 0.075f));
            for (int shelf = 0; shelf < 2; shelf++) //선반과 병 형태의 공간 장식
            {
                createShape("Shelf" + shelf, PrimitiveType.Cube, new Vector3(0, 2.35f + shelf * 0.85f, 5.55f), new Vector3(10, 0.1f, 0.6f), new Color(0.31f, 0.19f, 0.09f));
                for (int bottle = 0; bottle < 12; bottle++) //선반의 장식 병
                    createShape("Bottle" + shelf + "_" + bottle, PrimitiveType.Cylinder, new Vector3(-4.3f + bottle * 0.78f, 2.6f + shelf * 0.85f, 5.48f),
                        new Vector3(0.16f, 0.19f, 0.16f), bottle % 2 == 0 ? new Color(0.13f, 0.24f, 0.13f) : new Color(0.39f, 0.20f, 0.06f));
            }
            createLamp(new Vector3(-5, 3.6f, 3.8f));
            createLamp(new Vector3(5, 3.6f, 3.8f));
            float tableZ = lobby ? 3.5f : 0.5f; //대기실에서는 참가자 뒤에 놓을 테이블
            createShape("Table", PrimitiveType.Cylinder, new Vector3(0, 0.91f, tableZ), new Vector3(4.25f, 0.14f, 3.6f), new Color(0.36f, 0.15f, 0.055f));
            createShape("TableRim", PrimitiveType.Cylinder, new Vector3(0, 0.83f, tableZ), new Vector3(4.36f, 0.07f, 3.7f), new Color(0.18f, 0.07f, 0.027f));
            createShape("TableBase", PrimitiveType.Cylinder, new Vector3(0, 0.4f, tableZ), new Vector3(0.75f, 0.43f, 0.75f), new Color(0.17f, 0.073f, 0.035f));
            if (lobby)
            {
                for (int slot = 0; slot < 4; slot++) //동일 크기로 전면에 배치할 참가자
                    createAvatar(slot, new Vector3(-4.55f + slot * 3.033f, 0, 0));
            }
            else
            {
                Vector3[] seats = { new Vector3(-2.6f, 0, 0.5f), new Vector3(2.6f, 0, 0.5f), new Vector3(0, 0, -1.9f), new Vector3(0, 0, 2.9f) }; //메뉴 테이블의 네 의자
                for (int slot = 0; slot < 4; slot++) //빈 의자의 목재 구성
                {
                    createShape("ChairSeat" + slot, PrimitiveType.Cube, seats[slot] + Vector3.up * 0.53f, new Vector3(0.9f, 0.13f, 0.8f), new Color(0.29f, 0.125f, 0.055f));
                    createShape("ChairBack" + slot, PrimitiveType.Cube, seats[slot] + new Vector3(0, 1.04f, 0.32f), new Vector3(0.9f, 1.04f, 0.13f), new Color(0.25f, 0.11f, 0.045f));
                }
                createShape("DisplayToaster", PrimitiveType.Cube, new Vector3(0, 1.3f, tableZ), new Vector3(1, 0.54f, 0.6f), new Color(0.7f, 0.62f, 0.48f));
                createShape("DisplaySlot", PrimitiveType.Cube, new Vector3(0, 1.574f, tableZ), new Vector3(0.7f, 0.012f, 0.2f), new Color(0.07f, 0.05f, 0.03f));
                for (int coin = 0; coin < 6; coin++) //테이블 위 경매 코인
                    createShape("Coin" + coin, PrimitiveType.Cylinder, new Vector3(-0.8f + coin * 0.18f, 1.07f, -0.4f), new Vector3(0.18f, 0.026f, 0.18f), new Color(0.9f, 0.61f, 0.18f));
            }
        }

        private void createLamp(Vector3 position) //따뜻한 실내 포인트 조명 배치
        {
            createShape("LampShade", PrimitiveType.Cylinder, position, new Vector3(0.75f, 0.18f, 0.75f), new Color(0.54f, 0.30f, 0.09f));
            GameObject lamp = new GameObject("WarmLamp"); //공간의 조명 오브젝트
            lamp.transform.SetParent(roomRoot.transform, false);
            lamp.transform.localPosition = position + Vector3.down * 0.3f;
            Light light = lamp.AddComponent<Light>(); //따뜻한 주변 조명
            light.type = LightType.Point;
            light.color = new Color(1, 0.57f, 0.25f);
            light.range = 10;
            light.intensity = 3;
        }

        private void createAvatar(int slot, Vector3 position) //교체 가능한 임시 인물의 몸통과 머리 및 팔다리 구성
        {
            Color suit = playerColors[slot]; //선택한 옷 색상
            characters[slot] = createShape("Player" + (slot + 1), PrimitiveType.Capsule, position + Vector3.up * 1.72f, new Vector3(0.96f, 0.77f, 0.66f), suit).GetComponent<Renderer>();
            createShape("Head" + slot, PrimitiveType.Sphere, position + new Vector3(0, 2.8f, 0), new Vector3(0.79f, 0.9f, 0.73f), new Color(0.65f, 0.46f, 0.32f));
            for (int side = -1; side <= 1; side += 2) //좌우 팔다리와 눈 배치
            {
                createShape("Arm" + slot + "_" + side, PrimitiveType.Capsule, position + new Vector3(side * 0.64f, 1.66f, 0), new Vector3(0.27f, 0.67f, 0.27f), new Color(0.34f, 0.25f, 0.19f));
                createShape("Leg" + slot + "_" + side, PrimitiveType.Capsule, position + new Vector3(side * 0.25f, 0.55f, 0), new Vector3(0.35f, 0.56f, 0.4f), new Color(0.07f, 0.06f, 0.05f));
                createShape("Shoe" + slot + "_" + side, PrimitiveType.Cube, position + new Vector3(side * 0.25f, 0.07f, -0.11f), new Vector3(0.42f, 0.18f, 0.6f), new Color(0.08f, 0.04f, 0.023f));
                createShape("Eye" + slot + "_" + side, PrimitiveType.Sphere, position + new Vector3(side * 0.17f, 2.88f, -0.35f), Vector3.one * 0.1f, new Color(0.035f, 0.025f, 0.018f));
            }
            createShape("Hat" + slot, PrimitiveType.Cylinder, position + Vector3.up * 3.25f, new Vector3(0.9f, 0.07f, 0.8f), new Color(0.13f, 0.085f, 0.05f));
            createShape("HatTop" + slot, PrimitiveType.Cylinder, position + Vector3.up * 3.4f, new Vector3(0.57f, 0.19f, 0.53f), new Color(0.15f, 0.10f, 0.06f));
        }

        public void showState(AuctionState state) //공개 상태에 따라 캐릭터와 시연 갱신
        {
            if (roomRoot == null)
                return;
            for (int slot = 0; slot < characters.Length; slot++) //캐릭터별 참가 및 색상 반영
                if (characters[slot] != null)
                    characters[slot].sharedMaterial.color = state.players[slot] == null ? new Color(0.23f, 0.27f, 0.34f) : playerColors[Mathf.Clamp(state.players[slot].color, 0, playerColors.Length - 1)];
            if (toast == null || !state.isRevealed())
            {
                resetReveal();
                return;
            }
            if (state.match == shownMatch && state.round == shownRound)
                return;
            shownMatch = state.match;
            shownRound = state.round;
            revealing = true;
            revealAt = Time.unscaledTime;
            good = state.goodCondition;
            toast.gameObject.SetActive(good);
            foreach (Transform puff in smoke) //불량일 때만 연기 표시
                puff.gameObject.SetActive(!good);
        }

        private void Update() //정상 식빵 튀기기와 불량 흔들림 연출
        {
            if (!revealing || toaster == null)
                return;
            float elapsed = Time.unscaledTime - revealAt; //시연 경과 시간
            if (good)
            {
                toast.position = new Vector3(0, 1.8f + Mathf.Abs(Mathf.Sin(elapsed * 2.5f)) * 0.65f, 0.5f);
                toast.rotation = Quaternion.Euler(0, elapsed * 95, 0);
            }
            else
            {
                toaster.position = toasterPosition + new Vector3(Mathf.Sin(elapsed * 60) * 0.06f, 0, 0);
                for (int index = 0; index < smoke.Length; index++) //연기 조각의 상승 위치 계산
                {
                    float progress = Mathf.Repeat(elapsed * 0.55f + index / (float)smoke.Length, 1); //연기 조각의 반복 진행률
                    smoke[index].position = toasterPosition + new Vector3(Mathf.Sin(index * 2.3f) * progress * 0.4f, 0.3f + progress * 1.5f, Mathf.Cos(index * 2.3f) * progress * 0.4f);
                    smoke[index].localScale = Vector3.one * (0.12f + progress * 0.43f);
                }
            }
        }

        private GameObject createShape(string name, PrimitiveType type, Vector3 position, Vector3 scale, Color color) //시제품의 기본 3D 도형 생성
        {
            GameObject shape = GameObject.CreatePrimitive(type); //생성한 도형
            shape.name = name;
            shape.transform.SetParent(roomRoot.transform, false);
            shape.transform.localPosition = position;
            shape.transform.localScale = scale;
            Destroy(shape.GetComponent<Collider>());
            Material material = surfaceMaterial != null ? new Material(surfaceMaterial) : new Material(Shader.Find("Universal Render Pipeline/Lit")); //도형 전용 색상 머티리얼
            material.color = color;
            materials.Add(material);
            shape.GetComponent<Renderer>().sharedMaterial = material;
            return shape;
        }

        private void resetReveal() //라운드 사이에 공개 연출 초기화
        {
            revealing = false;
            shownMatch = shownRound = -1;
            if (toaster != null)
                toaster.position = toasterPosition;
            if (toast != null)
                toast.gameObject.SetActive(false);
            foreach (Transform puff in smoke) //기존 연기 표시 종료
                if (puff != null)
                    puff.gameObject.SetActive(false);
        }

        private void clearRoom() //이전 씬의 임시 오브젝트와 머티리얼 정리
        {
            resetReveal();
            if (roomRoot != null)
                Destroy(roomRoot);
            foreach (Material material in materials) //동적으로 만든 머티리얼 해제
                Destroy(material);
            materials.Clear();
        }

        private void OnDestroy() //종료 시 생성 자원 정리
        {
            clearRoom();
        }
    }
}
