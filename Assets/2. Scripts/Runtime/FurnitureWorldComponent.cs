using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace CantResell
{
    public sealed class FurnitureWorldComponent : MonoBehaviour
    {
        private GameObject root; //씬 안에 생성한 공간
        private GameObject shopRoot; //낮의 가게와 놀이 공간
        private GameObject houseRoot; //밤의 침입 장소
        private GameObject owner; //신고하는 집주인 표시
        private GameObject police; //추격하는 경찰 표시
        private GameObject customer; //주문 중인 손님 표시
        private GameObject door; //실제 충돌을 가진 출입문
        private readonly List<GameObject> lasers = new List<GameObject>(); //레이저 표시
        private readonly Dictionary<int, GameObject> furniture = new Dictionary<int, GameObject>(); //가구별 표시
        private readonly Dictionary<Color, Material> materials = new Dictionary<Color, Material>(); //재사용할 색상 머티리얼
        private readonly List<TextMesh> labels = new List<TextMesh>(); //카메라를 향할 이름표
        private readonly TextMesh[] playerLabels = new TextMesh[4]; //밤에도 구별할 동료 이름
        private Camera sceneCamera; //현재 Play 카메라
        private Material surface; //기존 URP 머티리얼
        private Font font; //한글 이름표 글꼴
        private AuctionState displayed; //현재 화면 상태
        private Light daylight; //낮과 밤의 방향광
        private float daylightIntensity; //씬의 기본 광량

        public void build(Scene scene, Camera camera, Material material) //가게와 집의 시제품 공간 생성
        {
            clear();
            surface = material;
            sceneCamera = camera;
            sceneCamera.transform.position = new Vector3(0, 14, -10);
            sceneCamera.transform.LookAt(new Vector3(0, 0, 2));
            sceneCamera.fieldOfView = 48;
            daylight = scene.GetRootGameObjects().SelectMany(value => value.GetComponentsInChildren<Light>()).FirstOrDefault(value => value.type == LightType.Directional);
            daylightIntensity = daylight != null ? daylight.intensity : 1;
            font = Font.CreateDynamicFontFromOSFont(new[] { "Malgun Gothic", "Arial" }, 32);
            root = new GameObject("FurnitureStoreWorld");
            SceneManager.MoveGameObjectToScene(root, scene);
            shape(root.transform, "Ground", new Vector3(0, -0.15f, 3), new Vector3(36, 0.3f, 30), new Color(0.14f, 0.19f, 0.2f), true);
            shape(root.transform, "WestBoundary", new Vector3(-17, 1, 3), new Vector3(0.3f, 2, 30), Color.gray, true);
            shape(root.transform, "EastBoundary", new Vector3(17, 1, 3), new Vector3(0.3f, 2, 30), Color.gray, true);
            shape(root.transform, "NorthBoundary", new Vector3(0, 1, 17), new Vector3(34, 2, 0.3f), Color.gray, true);
            shape(root.transform, "SouthBoundary", new Vector3(0, 1, -11), new Vector3(34, 2, 0.3f), Color.gray, true);
            shopRoot = new GameObject("DayShop");
            shopRoot.transform.SetParent(root.transform, false);
            shape(shopRoot.transform, "ShopFloor", new Vector3(0, 0.02f, 4), new Vector3(15, 0.08f, 18), new Color(0.39f, 0.28f, 0.17f));
            shape(shopRoot.transform, "ShopBack", new Vector3(0, 1, 13), new Vector3(15, 2, 0.3f), new Color(0.45f, 0.32f, 0.2f), true);
            shape(shopRoot.transform, "Counter", new Vector3(0, 0.55f, -4.2f), new Vector3(3, 1.1f, 0.7f), new Color(0.39f, 0.19f, 0.08f), true);
            label(shopRoot.transform, "판매: 주문 가구를 들고 계산대에서 E", new Vector3(0, 2.8f, -4.2f));
            customer = shape(shopRoot.transform, "Customer", new Vector3(0, 0.85f, -5.3f), new Vector3(0.65f, 1.7f, 0.65f), new Color(0.2f, 0.7f, 0.8f));
            shape(shopRoot.transform, "Gambling", FurnitureStore.gamblingTable + Vector3.up * 0.5f, new Vector3(2, 1, 1.2f), new Color(0.08f, 0.45f, 0.22f));
            label(shopRoot.transform, "공금 도박장\n가까이에서 오른쪽 버튼 사용", FurnitureStore.gamblingTable + Vector3.up * 2.3f);
            shape(shopRoot.transform, "Arcade", FurnitureLeisureComponent.machine + Vector3.up * 0.8f, new Vector3(1, 1.6f, 0.8f), new Color(0.48f, 0.16f, 0.66f));
            label(shopRoot.transform, "무료 타이밍 놀이", FurnitureLeisureComponent.machine + Vector3.up * 2.3f);
            houseRoot = new GameObject("NightHouse");
            houseRoot.transform.SetParent(root.transform, false);
            shape(houseRoot.transform, "HouseFloor", new Vector3(0, 0.02f, 8), new Vector3(16, 0.08f, 16), new Color(0.23f, 0.2f, 0.3f));
            foreach (int side in new[] { -1, 1 }) //양쪽 벽과 출입문 옆의 벽
            {
                shape(houseRoot.transform, "SideWall" + side, new Vector3(side * 8, 1.2f, 8), new Vector3(0.3f, 2.4f, 16), Color.gray, true);
                shape(houseRoot.transform, "FrontWall" + side, new Vector3(side * 4.6f, 1.2f, 0), new Vector3(6.8f, 2.4f, 0.3f), Color.gray, true);
            }
            shape(houseRoot.transform, "BackWall", new Vector3(0, 1.2f, 16), new Vector3(16, 2.4f, 0.3f), Color.gray, true);
            door = shape(houseRoot.transform, "Door", new Vector3(0, 1.2f, 0), new Vector3(2.4f, 2.4f, 0.3f), new Color(0.38f, 0.2f, 0.08f), true);
            label(houseRoot.transform, "E 문 열기", new Vector3(0, 3, 0));
            shape(houseRoot.transform, "SensorSwitch", FurnitureNightComponent.sensorSwitch + Vector3.up * 0.5f, new Vector3(0.35f, 1, 0.35f), Color.green);
            label(houseRoot.transform, "E 센서 10초 차단", FurnitureNightComponent.sensorSwitch + Vector3.up * 2);
            lasers.Add(shape(houseRoot.transform, "LaserLeft", new Vector3(-4.7f, 0.6f, 5.5f), new Vector3(6.2f, 0.08f, 0.08f), Color.red));
            lasers.Add(shape(houseRoot.transform, "LaserRight", new Vector3(4.7f, 0.6f, 5.5f), new Vector3(6.2f, 0.08f, 0.08f), Color.red));
            lasers.Add(shape(houseRoot.transform, "LaserBack", new Vector3(0, 0.6f, 10), new Vector3(15.5f, 0.08f, 0.08f), Color.red));
            shape(houseRoot.transform, "Truck", new Vector3(0, 0.6f, -7.8f), new Vector3(6, 1.2f, 1.3f), new Color(0.15f, 0.37f, 0.55f));
            label(houseRoot.transform, "차량에서 E: 적재 / 빈손이면 철수", new Vector3(0, 3, -6));
            owner = shape(houseRoot.transform, "Owner", new Vector3(5.8f, 0.9f, 14), new Vector3(0.6f, 1.8f, 0.6f), new Color(0.9f, 0.5f, 0.3f));
            label(houseRoot.transform, "집주인 / 전화", new Vector3(4, 2.6f, 14));
            police = shape(houseRoot.transform, "Police", new Vector3(0, 0.9f, -9), new Vector3(0.7f, 1.8f, 0.7f), new Color(0.1f, 0.25f, 0.95f));
            houseRoot.SetActive(false);
            for (int slot = 0; slot < 4; slot++) //협력 중 동료를 식별할 이름표
                playerLabels[slot] = label(root.transform, "", Vector3.zero);
            Physics.SyncTransforms();
        }
        public void setDoor(bool open) //호스트와 각 클라이언트에서 출입문 충돌 일치
        {
            if (door == null)
                return;
            door.GetComponent<BoxCollider>().enabled = !open;
            door.GetComponent<Renderer>().enabled = !open;
            Physics.SyncTransforms();
        }
        public void showState(AuctionState state) //공유 상태에 맞춰 공간과 재고 표시
        {
            displayed = state;
            if (root == null || state.store == null)
                return;
            bool night = state.phase == AuctionState.Phase.Night; //현재 공간의 시간대
            if (daylight != null)
                daylight.intensity = daylightIntensity * (night ? 0.38f : 1);
            shopRoot.SetActive(!night);
            houseRoot.SetActive(night);
            setDoor(state.store.doorOpen);
            customer.SetActive(!night && state.store.orderKind >= 0);
            owner.transform.position = state.store.ownerPosition + Vector3.up * 0.9f;
            police.SetActive(night && state.store.alarm == FurnitureState.Alarm.Pursuit);
            police.transform.position = state.store.policePosition + Vector3.up * 0.9f;
            foreach (GameObject laser in lasers) //레이저 작동의 시각 피드백
                laser.SetActive(state.store.sensorActive);
            HashSet<int> ids = new HashSet<int>(state.store.furniture.Select(item => item.id)); //현재 남아 있는 가구
            foreach (int id in furniture.Keys.Where(id => !ids.Contains(id)).ToArray()) //판매하거나 몰수된 표시 제거
            {
                Destroy(furniture[id]);
                furniture.Remove(id);
            }
            foreach (FurnitureState.Furniture item in state.store.furniture) //공동 재고의 형태와 위치
            {
                if (!furniture.TryGetValue(item.id, out GameObject visual))
                {
                    visual = createFurniture(item);
                    furniture.Add(item.id, visual);
                }
                visual.SetActive(item.location == FurnitureState.Location.Carried || (night ? item.location == FurnitureState.Location.House || item.location == FurnitureState.Location.Truck : item.location == FurnitureState.Location.Shop));
                if (item.location != FurnitureState.Location.Carried)
                    visual.transform.position = item.position;
            }
        }
        private GameObject createFurniture(FurnitureState.Furniture item) //종류가 구별되는 간단한 가구 모델 조립
        {
            GameObject model = new GameObject("Furniture_" + item.id); //가구 모델의 루트
            model.transform.SetParent(root.transform, false);
            Color wood = new Color(0.56f, 0.33f, 0.16f); //공통 나무 색상
            Vector3 size = item.kind == 5 ? new Vector3(1.7f, 0.35f, 2.1f) : item.kind == 4 ? new Vector3(1.7f, 0.5f, 0.8f) : new Vector3(1.1f, 0.15f, 0.8f); //몸체 크기
            if (item.kind == 1)
            {
                shape(model.transform, "LampStand", Vector3.up * 0.65f, new Vector3(0.12f, 1.3f, 0.12f), wood);
                shape(model.transform, "LampShade", Vector3.up * 1.4f, new Vector3(0.7f, 0.35f, 0.7f), Color.yellow);
            }
            else if (item.kind == 3)
            {
                shape(model.transform, "Cabinet", Vector3.up * 0.65f, new Vector3(1.2f, 1.3f, 0.65f), wood);
                shape(model.transform, "CabinetHandle", new Vector3(0.25f, 0.65f, -0.35f), new Vector3(0.1f, 0.2f, 0.08f), Color.yellow);
            }
            else
            {
                shape(model.transform, "Seat", Vector3.up * 0.6f, size, item.kind >= 4 ? new Color(0.45f, 0.6f, 0.75f) : wood);
                foreach (int x in new[] { -1, 1 }) //가구 다리
                    foreach (int z in new[] { -1, 1 })
                        shape(model.transform, "Leg", new Vector3(x * size.x * 0.38f, 0.28f, z * size.z * 0.38f), new Vector3(0.1f, 0.56f, 0.1f), wood);
                if (item.kind == 0 || item.kind == 4)
                    shape(model.transform, "Back", new Vector3(0, 1, size.z * 0.4f), new Vector3(size.x, 0.8f, 0.12f), wood);
            }
            label(model.transform, "#" + item.id + " " + FurnitureInventoryComponent.names[item.kind] + " " + item.price, Vector3.up * 1.9f);
            return model;
        }
        private GameObject shape(Transform parent, string name, Vector3 position, Vector3 size, Color color, bool solid = false) //재사용 머티리얼과 선택적 충돌을 가진 도형
        {
            GameObject value = GameObject.CreatePrimitive(PrimitiveType.Cube); //임시 도형
            value.name = name;
            value.transform.SetParent(parent, false);
            value.transform.localPosition = position;
            value.transform.localScale = size;
            value.GetComponent<BoxCollider>().enabled = solid;
            if (!materials.TryGetValue(color, out Material material))
            {
                material = surface != null ? new Material(surface) : new Material(Shader.Find("Universal Render Pipeline/Lit"));
                material.color = color;
                materials.Add(color, material);
            }
            value.GetComponent<Renderer>().sharedMaterial = material;
            return value;
        }
        private TextMesh label(Transform parent, string text, Vector3 position) //공통 한글 글꼴을 사용하는 월드 안내
        {
            GameObject value = new GameObject("Label", typeof(TextMesh)); //텍스트 오브젝트
            value.transform.SetParent(parent, false);
            value.transform.localPosition = position;
            TextMesh mesh = value.GetComponent<TextMesh>(); //이름표 표시
            mesh.font = font;
            mesh.GetComponent<Renderer>().sharedMaterial = font.material;
            mesh.fontSize = 32;
            mesh.characterSize = 0.055f;
            mesh.anchor = TextAnchor.MiddleCenter;
            mesh.color = Color.white;
            mesh.text = text;
            labels.Add(mesh);
            return mesh;
        }
        private void LateUpdate() //낮과 밤 모두 로컬 플레이어를 따라가며 운반 표시 보간
        {
            if (root == null || displayed?.store == null || sceneCamera == null)
                return;
            AuctionGame game = AuctionGame.current; //캐릭터를 조회할 세션 진입점
            Player local = game?.localPlayer; //카메라가 따라갈 참가자
            if (local != null)
            {
                sceneCamera.transform.position = local.transform.position + new Vector3(0, 14, -9);
                sceneCamera.transform.LookAt(local.transform.position + Vector3.up * 0.5f);
                sceneCamera.fieldOfView = 48;
                sceneCamera.backgroundColor = displayed.phase == AuctionState.Phase.Night ? new Color(0.02f, 0.025f, 0.06f) : new Color(0.14f, 0.2f, 0.24f);
            }
            foreach (FurnitureState.Furniture item in displayed.store.furniture) //실제 네트워크 캐릭터에 운반품 표시
                if (item.location == FurnitureState.Location.Carried && furniture.TryGetValue(item.id, out GameObject model))
                {
                    Player carrier = game?.getPlayer(item.carrier); //운반하는 참가자
                    if (carrier != null)
                        model.transform.position = carrier.transform.position + carrier.transform.forward * 0.65f + Vector3.up * 0.6f;
                }
            for (int slot = 0; slot < 4; slot++) //동료의 이름과 철수 상태
            {
                Player player = game?.getPlayer(slot); //표시할 참가자
                if (playerLabels[slot] == null)
                    continue;
                playerLabels[slot].gameObject.SetActive(player != null);
                if (player != null)
                {
                    playerLabels[slot].transform.position = player.transform.position + Vector3.up * 2.6f;
                    playerLabels[slot].text = displayed.players[slot]?.name + (displayed.phase == AuctionState.Phase.Night && displayed.store.escaped[slot] ? " (철수)" : "");
                }
            }
            foreach (TextMesh mesh in labels) //이름표를 카메라 방향으로 정렬
                if (mesh != null)
                    mesh.transform.rotation = sceneCamera.transform.rotation;
        }
        public void clear() //씬 이동에서 생성 공간과 표시 자원 해제
        {
            if (root != null)
            {
                root.SetActive(false);
                Destroy(root);
            }
            foreach (Material material in materials.Values)
                Destroy(material);
            if (font != null)
                Destroy(font);
            materials.Clear();
            furniture.Clear();
            labels.Clear();
            lasers.Clear();
            displayed = null;
            root = null;
            daylight = null;
        }
        private void OnDestroy() //세션 종료의 자원 정리
        {
            clear();
        }
    }
}
