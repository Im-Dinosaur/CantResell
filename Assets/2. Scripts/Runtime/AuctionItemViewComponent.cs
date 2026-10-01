using System.Collections.Generic;
using System.Linq;
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
        private readonly PlayerHouse[] houses = new PlayerHouse[4]; //밤의 충돌을 갖춘 집
        private readonly Dictionary<int, GameObject> loot = new Dictionary<int, GameObject>(); //공개 보관품 표시
        private GameObject lotVisual; //현재 판매품 표시
        private Camera sceneCamera; //현재 씬의 카메라
        private Light daylight; //낮과 밤에 밝기를 바꿀 조명
        private float daylightIntensity; //원래 씬 조명 밝기
        private AuctionState displayed; //로컬에 허용된 상태
        private string currentScene; //구성한 씬 이름

        public void showScene(string sceneName) //기본 3D 방과 임시 상품 구성
        {
            clearRoom();
            currentScene = sceneName;
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
            sceneCamera = camera;
            daylight = targetScene.IsValid() ? targetScene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<Light>()).FirstOrDefault(light => light.type == LightType.Directional) : null;
            daylightIntensity = daylight != null ? daylight.intensity : 1;
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
            createShape("Floor", PrimitiveType.Cube, new Vector3(0, -0.15f, 0), new Vector3(38, 0.3f, 36), new Color(0.12f, 0.15f, 0.18f), true);
            createShape("AuctionTable", PrimitiveType.Cylinder, new Vector3(0, 0.75f, 0), new Vector3(3.3f, 0.12f, 3.3f), new Color(0.36f, 0.19f, 0.08f), true);
            lotVisual = createShape("AuctionLot", PrimitiveType.Cube, new Vector3(0, 1.35f, 0), Vector3.one * 0.65f, new Color(0.9f, 0.7f, 0.25f));
            Vector3[] centers = { new Vector3(-8, 0, 9), new Vector3(8, 0, 9), new Vector3(-8, 0, -9), new Vector3(8, 0, -9) }; //중앙을 향한 네 집
            for (int slot = 0; slot < 4; slot++) //좌석별 집
                buildHouse(slot, centers[slot]);
            createShape("BoundaryWest", PrimitiveType.Cube, new Vector3(-18, 1, 0), new Vector3(0.3f, 3, 36), Color.gray, true);
            createShape("BoundaryEast", PrimitiveType.Cube, new Vector3(18, 1, 0), new Vector3(0.3f, 3, 36), Color.gray, true);
            createShape("BoundaryNorth", PrimitiveType.Cube, new Vector3(0, 1, 17), new Vector3(36, 3, 0.3f), Color.gray, true);
            createShape("BoundarySouth", PrimitiveType.Cube, new Vector3(0, 1, -17), new Vector3(36, 3, 0.3f), Color.gray, true);
            Physics.SyncTransforms();
        }

        private void buildHouse(int slot, Vector3 center) //문 틈과 실제 벽 충돌을 가진 집 구성
        {
            GameObject root = new GameObject("House" + (slot + 1)); //집의 기능 진입점
            root.transform.SetParent(roomRoot.transform, false);
            root.transform.position = center;
            float front = center.z > 0 ? -3 : 3; //중앙 경매장을 향한 문
            Color color = new Color(0.25f, 0.24f, 0.27f); //밤에 동일하게 보이는 벽 색상
            createShape("HouseFloor" + slot, PrimitiveType.Cube, center + Vector3.down * 0.03f, new Vector3(6, 0.05f, 6), new Color(0.3f, 0.21f, 0.14f));
            createShape("HouseBack" + slot, PrimitiveType.Cube, center + new Vector3(0, 1.1f, -front), new Vector3(6.2f, 2.2f, 0.2f), color, true);
            for (int side = -1; side <= 1; side += 2) //양쪽 벽과 출입문 옆
            {
                createShape("HouseSide" + slot + side, PrimitiveType.Cube, center + new Vector3(side * 3, 1.1f, 0), new Vector3(0.2f, 2.2f, 6), color, true);
                createShape("HouseFront" + slot + side, PrimitiveType.Cube, center + new Vector3(side * 2, 1.1f, front), new Vector3(2, 2.2f, 0.2f), color, true);
            }
            GameObject panel = createShape("Door" + slot, PrimitiveType.Cube, center + new Vector3(0, 1, front), new Vector3(2, 2, 0.2f), new Color(0.42f, 0.3f, 0.18f), true); //출입문
            HouseDoorComponent door = panel.AddComponent<HouseDoorComponent>(); //문의 충돌 담당
            door.initialize(panel.GetComponent<Collider>(), panel.GetComponent<Renderer>());
            houses[slot] = root.AddComponent<PlayerHouse>();
            houses[slot].initialize(slot, door);
        }

        public PlayerHouse getHouse(int slot) //좌석에 대응하는 집 파사드 조회
        {
            return slot >= 0 && slot < houses.Length ? houses[slot] : null;
        }

        public Vector3 getSeatPosition(int slot) //낮 경매 테이블의 좌석
        {
            Vector3[] seats = { new Vector3(-3, 0, -1), new Vector3(-3, 0, 2), new Vector3(3, 0, 2), new Vector3(3, 0, -1) }; //실제 캐릭터 좌석
            return seats[slot];
        }

        public Vector3 getItemPosition(AuctionState.Item item, IEnumerable<AuctionState.Item> items) //소유자의 집 안에서 일정한 상품 위치 계산
        {
            int index = items.Count(other => other.owner == item.owner && other.id < item.id &&
                (other.status == AuctionState.ItemStatus.Stored || other.status == AuctionState.ItemStatus.Carried)); //운반 중에도 원래 위치 유지
            return houses[item.owner].getStoragePosition(index);
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

        public void showState(AuctionState state) //원가와 성능을 노출하지 않는 공개 공간 표시
        {
            displayed = state;
            if (roomRoot == null)
                return;
            for (int slot = 0; slot < characters.Length; slot++) //대기실 스킨 색상 반영
                if (characters[slot] != null)
                    characters[slot].sharedMaterial.color = state.players[slot] == null ? Color.gray : playerColors[Mathf.Clamp(state.players[slot].color, 0, playerColors.Length - 1)];
            if (currentScene != "Play")
                return;
            if (daylight != null)
                daylight.intensity = state.phase == AuctionState.Phase.Night ? daylightIntensity * 0.28f : daylightIntensity;
            for (int slot = 0; slot < houses.Length; slot++) //문 개방에 따른 충돌 갱신
                houses[slot].showDoor(state.doorOpen[slot], state.doorStrength[slot]);
            foreach (GameObject visual in loot.Values) //없어진 보관품 숨기기
                visual.SetActive(false);
            foreach (AuctionState.Item item in state.items) //보관 중인 공개 상품 형태
            {
                if (item.status != AuctionState.ItemStatus.Stored)
                    continue;
                if (!loot.TryGetValue(item.id, out GameObject visual))
                {
                    visual = createShape("StoredItem_" + item.id, PrimitiveType.Cube, Vector3.zero, Vector3.one * 0.45f, new Color(0.66f, 0.58f, 0.39f));
                    GameObject tag = new GameObject("ItemLabel", typeof(TextMesh)); //상품 종류를 알 수 있는 공개 이름표
                    tag.transform.SetParent(visual.transform, false);
                    tag.transform.localPosition = Vector3.up * 1.2f;
                    TextMesh label = tag.GetComponent<TextMesh>(); //원가와 성능을 포함하지 않는 설명
                    Font font = Font.CreateDynamicFontFromOSFont(new[] { "Malgun Gothic", "Arial" }, 32); //한글 표시 글꼴
                    label.font = font;
                    label.GetComponent<Renderer>().sharedMaterial = font.material;
                    label.fontSize = 32;
                    label.characterSize = 0.5f;
                    label.anchor = TextAnchor.MiddleCenter;
                    label.text = "#" + item.id + " " + AuctionItemComponent.itemName(item.kind);
                    loot.Add(item.id, visual);
                }
                visual.transform.position = getItemPosition(item, state.items);
                visual.SetActive(true);
            }
            lotVisual.SetActive(state.phase == AuctionState.Phase.Pitch || state.phase == AuctionState.Phase.Bidding);
        }

        private void LateUpdate() //밤에는 자신의 캐릭터를 따라가는 카메라
        {
            if (currentScene != "Play" || sceneCamera == null)
                return;
            foreach (GameObject item in loot.Values)
                if (item.activeSelf && item.transform.childCount > 0)
                    item.transform.GetChild(0).rotation = sceneCamera.transform.rotation;
            Player local = AuctionGame.current?.localPlayer; //따라갈 로컬 캐릭터
            if (displayed?.phase == AuctionState.Phase.Night && local != null)
            {
                sceneCamera.transform.position = local.transform.position + new Vector3(0, 14, -9);
                sceneCamera.transform.LookAt(local.transform.position + Vector3.up * 0.6f);
                sceneCamera.fieldOfView = 48;
                sceneCamera.backgroundColor = new Color(0.015f, 0.018f, 0.04f);
            }
            else
            {
                sceneCamera.transform.position = new Vector3(0, 8, -12);
                sceneCamera.transform.LookAt(new Vector3(0, 0.8f, 0));
                sceneCamera.fieldOfView = 46;
            }
        }

        private GameObject createShape(string name, PrimitiveType type, Vector3 position, Vector3 scale, Color color, bool solid = false) //시제품의 기본 3D 도형 생성
        {
            GameObject shape = GameObject.CreatePrimitive(type); //생성한 도형
            shape.name = name;
            shape.transform.SetParent(roomRoot.transform, false);
            shape.transform.localPosition = position;
            shape.transform.localScale = scale;
            Collider primitiveCollider = shape.GetComponent<Collider>(); //빌드에서 제거될 수 있는 기본 도형 충돌
            if (primitiveCollider != null && (!solid || !(primitiveCollider is BoxCollider)))
            {
                primitiveCollider.enabled = false;
                Destroy(primitiveCollider);
            }
            if (solid && !(primitiveCollider is BoxCollider))
                shape.AddComponent<BoxCollider>();
            Material material = surfaceMaterial != null ? new Material(surfaceMaterial) : new Material(Shader.Find("Universal Render Pipeline/Lit")); //도형 전용 색상 머티리얼
            material.color = color;
            materials.Add(material);
            shape.GetComponent<Renderer>().sharedMaterial = material;
            return shape;
        }

        private void clearRoom() //이전 공간의 충돌과 표시 자원 정리
        {
            if (roomRoot != null)
            {
                roomRoot.SetActive(false);
                Destroy(roomRoot);
            }
            foreach (Material material in materials) //동적으로 만든 머티리얼
                Destroy(material);
            materials.Clear();
            loot.Clear();
            System.Array.Clear(characters, 0, characters.Length);
            System.Array.Clear(houses, 0, houses.Length);
            displayed = null;
        }

        private void OnDestroy() //종료 시 생성 자원 정리
        {
            clearRoom();
        }
    }
}
