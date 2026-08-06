# Sprint 1-2 — Data Refactor Spec (รื้อฐานข้อมูล)

**เอกสารนี้คือ:** สเปคเชิงเทคนิคสำหรับ EP-03 (Data Model) และ EP-04/05 (NightPlan)
**อ้างอิง:** `Docs/Roadmap-8-Weeks-Steam.md` §4 Sprint 1-2
**เวลารวม:** ~57 ชม. (Sprint 1: 25 ชม. เฉพาะส่วน data + Sprint 2: 32 ชม.)

---

## 0. ทำไมต้องทำงานนี้ก่อน (และทำไมมันไม่มีอะไรให้ผู้เล่นเห็นเลย)

ปัญหาปัจจุบันไม่ใช่ "โค้ดไม่ดี" แต่คือ **ข้อมูลเกมกระจายอยู่ใน 5 ที่ที่ไม่รู้จักกัน**:

```
ชนิด anomaly    → string ใน prefab แต่ละตัว (7 ไฟล์)
ชื่อห้อง         → List<string> ใน IncidentReportManager (1 ที่)
                 + List<string> ใน AnomalyOptionsCatalog (อีก 1 ที่ ต้อง sync มือ)
ตำแหน่งกล้อง     → float[] hardcode ใน GameManager
ตารางเกิด        → List<Entry> ใน Inspector ของ AnomalyScheduler
เกณฑ์ชนะ         → int hardcode ใน ScoreManager
```

ทุกครั้งที่เพิ่ม anomaly หรือห้อง คุณต้องแก้ 4-5 ที่พร้อมกัน **และบั๊ก `winThreshold = 9` ที่ทำให้เกมชนะไม่ได้ ก็เกิดจากตรงนี้เป๊ะๆ** — มีคนแก้ schedule แต่ลืมแก้ threshold เพราะมันอยู่คนละที่กัน

ถ้าไม่รื้อตรงนี้ก่อน Sprint 3-6 ที่ต้องเพิ่ม anomaly 8 ชนิด + ห้อง 5 ห้อง + haunt loop 5 ตัว จะกลายเป็นนรกของการ sync ข้อมูลด้วยมือ

**หลักการเดียวที่ต้องจำ:** *ข้อมูลชิ้นหนึ่ง มีที่อยู่ที่เดียว*

---

## 1. ลำดับการทำ (ห้ามสลับ — แต่ละขั้นพึ่งขั้นก่อนหน้า)

```
① commit งานค้าง 919 ไฟล์          ← ทำก่อนแตะโค้ดแม้แต่บรรทัดเดียว
      ↓
② RoomDefinition + RoomAnchor + RoomRegistry
      ↓
③ AnomalyDefinition + migrate prefab 8 ตัว
      ↓
④ GameFlowManager + NightResult   ← ลบ PlayerPrefs ที่กระจาย 3 ที่
      ↓
⑤ แยก Anomaly.cs                  ← ต้องมี ④ ก่อน ไม่งั้นแยกไม่ออก
      ↓ ══════ จบ Sprint 1 ══════
⑥ NightPlan (โครงสร้างข้อมูลเปล่า)
      ↓
⑦ NightPlanGenerator (สุ่มดิบ ยังไม่มีกฎ)
      ↓
⑧ Constraint solver
      ↓
⑨ Solvability check
      ↓
⑩ ต่อ NightPlan เข้ากับ Scheduler ทั้ง 3
      ↓
⑪ Debug tools (Dump Plan / Gizmo)
```

> ⚠️ **ขั้น ① ไม่ใช่พิธีกรรม** — งาน refactor นี้จะแตะไฟล์นับพัน ถ้าเกิดพังกลางทางแล้วไม่มีจุดย้อนกลับที่สะอาด คุณจะเสียเวลาทั้งสัปดาห์ ตั้ง tag ไว้ด้วย: `git tag pre-refactor`

---

## 2. ขั้น ② — RoomDefinition + RoomAnchor + RoomRegistry (5 ชม.)

### 2.1 ปัญหาที่ต้องแก้

- `GameManager.CameraPositionsX = {0f, 17.73f, 36.12f}` — hardcode, เพิ่มห้องต้องแก้โค้ด
- `IncidentReportManager.roomNames` — List<string> 6 ห้อง แต่กล้องมี 3 → dropdown โกหกผู้เล่น
- `AnomalyOptionsCatalog.locations` — รายชื่อห้องซ้ำอีกชุด ต้อง sync มือ
- `DemonAnomaly.Start()` เดาห้องตัวเองด้วยการหา `CameraPositionsX` ที่ใกล้ที่สุด

### 2.2 โครงที่ต้องสร้าง

```csharp
// Assets/Scripts/GameLogic/Data/RoomDefinition.cs
[CreateAssetMenu(fileName = "Room_", menuName = "Give Me A Sign/Room Definition")]
public class RoomDefinition : ScriptableObject
{
    [Tooltip("คีย์ถาวร ห้ามเปลี่ยนหลังใช้งานแล้ว (ใช้ใน save/seed)")]
    public string roomId = "hallway";

    [Tooltip("ชื่อที่แสดงใน dropdown และคู่มือ")]
    public string displayName = "Hallway";

    [Tooltip("ตำแหน่ง X ของกล้องสำหรับห้องนี้")]
    public float cameraX;

    [Tooltip("ลำดับในตัวสลับกล้อง")]
    public int cameraOrder;

    [TextArea] public string manualNote;
}
```

### 2.3 ⚠️ Gotcha ที่ต้องรู้ก่อนเขียน

**ScriptableObject อ้างอิง Transform ในซีนไม่ได้** — asset ไม่สามารถ reference scene object ได้ ดังนั้น `spawnPoints` จะใส่ใน `RoomDefinition` ตรงๆ ไม่ได้

**วิธีแก้:** ใช้ MonoBehaviour ในซีนเป็นตัวเชื่อม

```csharp
// Assets/Scripts/GameLogic/Data/RoomAnchor.cs
// วางไว้ในซีน 1 ตัวต่อ 1 ห้อง เป็นสะพานระหว่าง asset กับ scene
public class RoomAnchor : MonoBehaviour
{
    [SerializeField] private RoomDefinition room;
    [SerializeField] private Transform[] spawnPoints;

    public RoomDefinition Room => room;

    public Transform GetSpawnPoint(System.Random rng)
        => spawnPoints[rng.Next(spawnPoints.Length)];

    void OnEnable()  => RoomRegistry.Register(this);
    void OnDisable() => RoomRegistry.Unregister(this);

    void OnDrawGizmos() { /* วาดจุดเกิดทุกจุดพร้อมชื่อห้อง */ }
}
```

```csharp
// Assets/Scripts/GameLogic/Data/RoomRegistry.cs
// จุดเดียวที่ทั้งเกมถามว่า "มีห้องอะไรบ้าง"
public static class RoomRegistry
{
    private static readonly List<RoomAnchor> _anchors = new();

    public static IReadOnlyList<RoomAnchor> All => _anchors;

    public static void Register(RoomAnchor a)   { if (!_anchors.Contains(a)) _anchors.Add(a); Sort(); }
    public static void Unregister(RoomAnchor a) => _anchors.Remove(a);

    public static RoomAnchor Get(string roomId) => _anchors.Find(a => a.Room.roomId == roomId);
    public static List<string> DisplayNames()   => _anchors.ConvertAll(a => a.Room.displayName);

    private static void Sort() => _anchors.Sort((x, y) => x.Room.cameraOrder.CompareTo(y.Room.cameraOrder));
}
```

### 2.4 สิ่งที่ต้องแก้ตามมา

| ไฟล์ | แก้อะไร |
|---|---|
| `GameManager.cs` | ลบ `CameraPositionsX` → อ่านจาก `RoomRegistry.All[i].Room.cameraX` |
| `IncidentReportManager.cs` | ลบ `List<string> roomNames` → `reportUI.Show(caseNo, RoomRegistry.DisplayNames())` |
| `AnomalyOptionsCatalog.cs` | **ลบ field `locations` ทิ้ง** (เหลือแค่ anomalyTypes ชั่วคราว แล้วลบทั้งไฟล์ในขั้น ③) |
| `DemonAnomaly.cs` | ลบ logic เดาห้อง → รับ `RoomDefinition` จากตอน spawn |

### ✅ เกณฑ์ผ่านขั้นนี้
- grep คำว่า `17.73` ในโปรเจคแล้วไม่เจอในโค้ดเลย
- เพิ่มห้องใหม่ = สร้าง asset 1 ไฟล์ + วาง RoomAnchor 1 ตัว **ไม่ต้องแก้โค้ด**
- dropdown ในฟอร์มแสดงจำนวนห้องเท่ากับกล้องที่มีจริงเสมอ

---

## 3. ขั้น ③ — AnomalyDefinition + migrate (6 ชม.)

### 3.1 แนวคิดสำคัญที่สุดของขั้นนี้

ตอนนี้ prefab เก็บทั้ง **ชนิด** และ **ห้อง** ไว้ด้วยกัน:
```csharp
public string correctAnomalyType;    // "Shadow"  ← เป็นคุณสมบัติถาวรของชนิด
public string correctLocationName;   // ""        ← เป็นผลของการ spawn ครั้งนั้น
```

แต่พอทำ procedural แล้ว **ห้องจะถูกสุ่มตอนรันไทม์** ไม่ใช่ค่าที่ฝังใน prefab

> **กฎ:** ชนิด = static (อยู่ใน asset) · ห้อง = runtime (ตั้งตอน spawn)

### 3.2 โครงที่ต้องสร้าง

```csharp
// Assets/Scripts/GameLogic/Data/AnomalyDefinition.cs
[CreateAssetMenu(fileName = "Anomaly_", menuName = "Give Me A Sign/Anomaly Definition")]
public class AnomalyDefinition : ScriptableObject
{
    [Header("Identity")]
    public string anomalyId = "shadow";
    public string displayName = "Shadow Figure";

    [Tooltip("คำที่นับว่าถูกทั้งหมด รวมคำที่ผู้เล่นน่าจะพูดพลาด")]
    public string[] correctKeywords = { "Shadow", "Shadow Figure" };

    [Header("Spawning")]
    public GameObject prefab;
    public Anomaly.RespondType respondType = Anomaly.RespondType.MoveToTargetThenDisappear;

    [Tooltip("ราคาในงบภัยคุกคามของคืน ยิ่งสูง = ยิ่งอันตราย")]
    [Min(1)] public int threatCost = 1;

    [Tooltip("ห้ามโผล่ก่อนคืนที่เท่าไหร่ (1 = โผล่ได้ตั้งแต่คืนแรก)")]
    [Min(1)] public int minNightIndex = 1;

    [Tooltip("เว้นว่าง = เกิดได้ทุกห้อง")]
    public RoomDefinition[] allowedRooms;

    [Header("Timing")]
    public float moveSpeed = 3f;
    [Tooltip("เวลาที่ผู้เล่นมีก่อนแพ้ หลังมันเข้าโหมดคุกคาม")]
    public float threatTimeoutSeconds = 30f;

    [Header("Field Manual")]
    public Sprite manualImage;
    [TextArea(3, 8)] public string manualDescription;
    [TextArea(2, 4)] public string howToSpot;

    [Header("Links")]
    public HauntLoopId linkedHaunt = HauntLoopId.None;  // Sprint 4 ค่อยใช้
}
```

### 3.3 `Anomaly` เปลี่ยนเป็นอะไร

```csharp
public class Anomaly : MonoBehaviour
{
    [SerializeField] private AnomalyDefinition definition;
    public AnomalyDefinition Definition => definition;

    // ตั้งโดย spawner ตอนรันไทม์ ไม่ใช่ค่าใน prefab
    public RoomDefinition AssignedRoom { get; private set; }
    public void AssignRoom(RoomDefinition room) => AssignedRoom = room;

    // ── ลบทิ้ง ──
    // public string correctLocationName;
    // public string correctAnomalyType;
}
```

### 3.4 แก้ validation ใน `IncidentReportManager`

```csharp
// เดิม
bool typeMatches = IsKeywordMatch(_recognizedKeyword, _currentAnomaly.correctAnomalyType);
bool locationMatches = !requireCorrectLocation ||
    string.Equals(selectedRoom, _currentAnomaly.correctLocationName, ...);

// ใหม่ — รองรับหลาย keyword
bool typeMatches = false;
foreach (var kw in _currentAnomaly.Definition.correctKeywords)
    if (IsKeywordMatch(_recognizedKeyword, kw)) { typeMatches = true; break; }

bool locationMatches = !requireCorrectLocation ||
    selectedRoom == _currentAnomaly.AssignedRoom.displayName;
```

### 3.5 แผน migration (มีแค่ 8 prefab — ทำมือได้ แต่ต้องมีตัวตรวจ)

1. สร้าง asset 8 ไฟล์ใน `Assets/Settings/Anomalies/`
2. คัดลอกค่าจาก prefab เดิม (respondType / moveSpeed / timeToDisappear) เข้า asset
3. ลาก asset ใส่ช่อง `definition` ในแต่ละ prefab
4. **ยังไม่ลบ field เก่า** — ใส่ `[HideInInspector]` ไว้ก่อน
5. เขียน validator แล้วรัน:

```csharp
// Assets/Editor/DataValidator.cs
[MenuItem("Give Me A Sign/Validate Data")]
static void Validate()
{
    // - ทุก AnomalyDefinition มี prefab และ keyword อย่างน้อย 1 คำ
    // - ไม่มี anomalyId ซ้ำ / ไม่มี roomId ซ้ำ
    // - ไม่มี keyword ซ้ำข้ามชนิด  ← ป้องกันปัญหา "Shadow ทั้ง 7 ตัว" กลับมา
    // - ทุก prefab ที่มี Anomaly มี definition แล้ว
    // - allowedRooms ทุกตัวมี RoomAnchor ในซีนจริง
}
```
6. รัน validator ผ่านหมด → **ค่อยลบ field เก่าทิ้ง**

> 💡 ขั้นตอนที่ 4-6 คือสิ่งที่กันไม่ให้ข้อมูลหายเงียบๆ ตอนลบ field ออกจาก prefab — Unity จะไม่เตือนอะไรเลยถ้าคุณลบเลย

### ✅ เกณฑ์ผ่านขั้นนี้
- Validator ผ่าน 100%
- ไม่มี `correctAnomalyType` เหลือในโปรเจค
- เพิ่ม anomaly ชนิดใหม่ = สร้าง asset + prefab เท่านั้น

---

## 4. ขั้น ④ — GameFlowManager + NightResult (6 ชม.)

### 4.1 ปัญหาปัจจุบัน

`PlayerPrefs` ถูกเขียนจาก **3 ที่** ที่ไม่รู้จักกัน:

| ไฟล์ | เขียนอะไร |
|---|---|
| `Anomaly.cs` | `FinalScore=0, GameWon=0, AnomalyTimeout=1` + `LoadScene("Result")` |
| `DemonAnomaly.cs` | ชุดเดียวกันเป๊ะ (copy-paste) |
| `ScoreManager.cs` | `FinalScore, GameWon, WinThreshold` + ต้องเช็ค `AnomalyTimeout` เพื่อไม่เขียนทับ |

โค้ดที่ต้องเช็คว่า "อีกฝ่ายเขียนไปหรือยัง" คือสัญญาณว่าไม่มีใครเป็นเจ้าของข้อมูลนี้จริง

### 4.2 โครงที่ต้องสร้าง

```csharp
// Assets/Scripts/GameLogic/Flow/NightResult.cs
public enum NightOutcome { Survived, KilledByAnomaly, KilledByDemon, Negligence }

[System.Serializable]
public class NightResult
{
    public NightOutcome outcome;
    public int nightIndex;
    public int seed;
    public int score;
    public int requiredScore;
    public int anomaliesTotal;
    public int reportsFiled;
    public int reportsFailed;
    public float survivedUntilHour;
    public string killedByAnomalyId;
    public string killedInRoomId;

    public bool Won => outcome == NightOutcome.Survived && score >= requiredScore;
}
```

```csharp
// Assets/Scripts/GameLogic/Flow/GameFlowManager.cs
public class GameFlowManager : MonoBehaviour
{
    public static GameFlowManager Instance { get; private set; }

    // ที่เดียวที่ Result scene อ่าน — ไม่ต้องผ่าน PlayerPrefs อีกต่อไป
    public static NightResult LastResult { get; private set; }

    private bool _ending;   // กัน EndNight ถูกเรียกซ้อน

    public void EndNight(NightOutcome outcome, string causeAnomalyId = null)
    {
        if (_ending) return;
        _ending = true;

        // ปิดฟอร์มก่อนเสมอ (กัน Whisper ค้างแบบที่เคยเจอ)
        if (IncidentReportManager.Instance?.IsReportOpen == true)
            IncidentReportManager.Instance.CancelReport();

        LastResult = BuildResult(outcome, causeAnomalyId);
        StartCoroutine(PlayDeathThenLoad(outcome));   // Sprint 6 ค่อยใส่ death sequence
    }
}
```

**PlayerPrefs เหลือหน้าที่เดียว:** เก็บ progression (คืนที่ปลดล็อก) + volume — ไม่ใช่ผลของคืนนี้

### 4.3 สิ่งที่ต้องแก้ตามมา

| ไฟล์ | แก้อะไร |
|---|---|
| `Anomaly.cs` | ลบ PlayerPrefs + LoadScene → `GameFlowManager.Instance.EndNight(KilledByAnomaly, Definition.anomalyId)` |
| `DemonAnomaly.cs` | เหมือนกัน |
| `ScoreManager.cs` | ลบ `SaveScoreData` / `ClearSavedData` / เช็ค `AnomalyTimeout` ทิ้ง |
| `ResultDisplay.cs` | อ่านจาก `GameFlowManager.LastResult` แทน PlayerPrefs |
| `NightTimer.cs` | `EndNight()` → เรียก `GameFlowManager.EndNight(Survived)` |

> 💡 `ResultDisplay` ควรมี fallback: ถ้า `LastResult == null` (เปิด Result scene ตรงๆ ตอนเทสต์) ให้ใช้ค่า dummy แทนที่จะ null-ref

### ✅ เกณฑ์ผ่านขั้นนี้
- grep `PlayerPrefs` ใน `Assets/Scripts` เจอเฉพาะใน `AudioManager` และ progression
- ไม่มี `SceneManager.LoadScene` นอก `GameFlowManager` และ `SceneTransition`
- แพ้ทั้ง 3 แบบ (anomaly timeout / demon timeout / คะแนนไม่ถึง) แสดงผลถูกต้อง

---

## 5. ขั้น ⑤ — แยก Anomaly.cs (8 ชม.)

### 5.1 ตอนนี้ `Anomaly.cs` ทำ 9 อย่างพร้อมกัน

registry · movement · scale animation · animator trigger · audio · pray panel · report state · แพ้/ชนะ · โหลดซีน

### 5.2 แยกเป็น 4 component

```
Anomaly (core / facade)          ← ตัวเดียวที่ระบบอื่นรู้จัก
├── identity: Definition, AssignedRoom
├── state machine: Hidden → Visible → Threatening → Resolved
├── static registry + events
└── ประสานงาน 3 ตัวล่าง

AnomalyMovement      : เดินเข้าหาเป้า, ขยายสเกล
AnomalyPresenter     : animator trigger + AudioSource
AnomalyThreatTimer   : นับถอยหลัง → ยิง event OnThreatExpired
```

### 5.3 หลักการเดียวที่ต้องยึด

> **Anomaly แจ้งว่า "เกิดอะไรขึ้น" — ไม่ตัดสินว่า "เกมจบยังไง"**

```csharp
// ❌ เดิม — anomaly ตัดสินเกมเอง
if (_canPrayDisappear && gameObject.activeInHierarchy) {
    PlayerPrefs.SetInt("GameWon", 0);
    SceneManager.LoadScene("Result");
}

// ✅ ใหม่ — anomaly แค่แจ้ง
_threatTimer.OnExpired += () => OnThreatExpired?.Invoke(this);
// GameFlowManager เป็นคนฟังแล้วตัดสินใจ
```

ประโยชน์ที่ได้ทันที: Sprint 4-6 จะเพิ่มเงื่อนไขแพ้ใหม่ (negligence strikes, Silence Protocol) โดย **ไม่ต้องแตะ `Anomaly.cs` เลย**

### 5.4 ลำดับการแยกที่ปลอดภัย

ทำทีละตัวและเทสต์ทุกครั้ง อย่าแยกรวดเดียว:
1. ดึง audio + animator ออกเป็น `AnomalyPresenter` (เสี่ยงต่ำสุด)
2. ดึง movement + scale ออกเป็น `AnomalyMovement`
3. ดึง countdown ออกเป็น `AnomalyThreatTimer` + เปลี่ยนเป็น event
4. ที่เหลือใน `Anomaly` = identity + state machine + registry

> ⚠️ `[RequireComponent]` ทั้ง 3 ตัวใน `Anomaly` แล้วใช้ `GetComponent` ใน `Awake` — ไม่ต้องลากใส่ Inspector ทีละ prefab

### ✅ เกณฑ์ผ่านขั้นนี้
- ทุก class < 150 บรรทัด
- `Anomaly.cs` ไม่ `using UnityEngine.SceneManagement` อีกต่อไป
- เกมเล่นได้เหมือนเดิมทุกประการ (ขั้นนี้ไม่ควรมีอะไรเปลี่ยนในสายตาผู้เล่น)

---

## 6. ขั้น ⑥-⑨ — NightPlan + Generator (Sprint 2, 28 ชม.)

### 6.1 โครงสร้างข้อมูล (⑥ — 4 ชม.)

```csharp
[System.Serializable]
public class NightPlan
{
    public int seed;
    public int nightIndex;
    public float durationMinutes;

    public List<AnomalyPlacement> anomalies = new();
    public List<GlitchBeat>       glitches  = new();
    public List<HauntBeat>        haunts    = new();   // Sprint 4 ค่อยเติม

    public GlitchProfile glitchProfile;
    public int requiredScore;     // ⭐ คำนวณจากแผน ไม่ hardcode
}

[System.Serializable]
public struct AnomalyPlacement
{
    public AnomalyDefinition definition;
    public RoomDefinition    room;
    public float             atMinute;
}
```

### 6.2 ⚠️ Gotcha ที่สำคัญที่สุดของ Sprint 2

**ห้ามใช้ `UnityEngine.Random` ในเจนเนอเรเตอร์เด็ดขาด**

`UnityEngine.Random` เป็น global state — ระบบอื่น (glitch weight, animation, VFX) ก็ดึงจากบ่อเดียวกัน แค่มี glitch ยิงเพิ่ม 1 ครั้ง seed เดิมก็จะให้ผลต่างกันทันที → ฟีเจอร์ "Replay this seed" พังทั้งอัน

```csharp
// ❌ พัง
UnityEngine.Random.InitState(seed);
var pick = list[UnityEngine.Random.Range(0, list.Count)];

// ✅ ถูก — instance ของตัวเอง ไม่มีใครมายุ่ง
private readonly System.Random _rng;
public NightPlanGenerator(int seed) => _rng = new System.Random(seed);
var pick = list[_rng.Next(list.Count)];
```

### 6.3 Generator (⑦ — 10 ชม.)

```csharp
public class NightPlanGenerator
{
    private readonly System.Random _rng;
    private readonly DifficultyProfile _profile;

    public NightPlan Generate(int nightIndex, float durationMinutes)
    {
        // 1. งบภัยคุกคามของคืนนี้
        int budget = _profile.ThreatBudgetFor(nightIndex);

        // 2. คัดชนิดที่ใช้ได้ (minNightIndex <= nightIndex)
        var pool = AllDefinitions.Where(d => d.minNightIndex <= nightIndex).ToList();

        // 3. หยิบจนงบหมด
        while (budget > 0) {
            var def  = PickWeighted(pool, budget);
            var room = PickRoom(def);
            float t  = PickTime(def);
            plan.anomalies.Add(new AnomalyPlacement { ... });
            budget -= def.threatCost;
        }

        // 4. วาง glitch + haunt
        // 5. requiredScore = Mathf.CeilToInt(plan.anomalies.Count * _profile.winRatio);
        return plan;
    }
}
```

### 6.4 Constraint solver (⑧ — 8 ชม.)

ใช้รูปแบบ **generate → validate → retry** (ง่ายกว่าและ debug ง่ายกว่าการเขียน solver ฉลาดๆ):

```csharp
public NightPlan GenerateValid(int nightIndex, float duration, int maxAttempts = 50)
{
    for (int i = 0; i < maxAttempts; i++) {
        var plan = Generate(nightIndex, duration);
        if (Validate(plan, out string reason)) return plan;
        Debug.Log($"[Gen] attempt {i} rejected: {reason}");
    }
    return GenerateFallback(nightIndex, duration);   // แผนสำรองที่การันตีว่าผ่าน
}
```

กฎที่ต้อง validate:

| กฎ | เงื่อนไข |
|---|---|
| Minimum Spacing | เหตุการณ์ 2 อันห่างกัน ≥ 25 วินาที |
| No Overlap | anomaly ที่มี threat timer ห้ามซ้อนช่วงเวลากัน |
| Room Spread | ห้ามใช้ห้องเดิม 2 ครั้งติด · ทุกห้องถูกใช้ ≥ 1 ครั้ง |
| Type Spread | ห้ามชนิดเดิม 2 ครั้งติด |
| Onboarding | 20% แรกของคืน ไม่มี glitch/haunt |
| Climax | เหตุการณ์ tier สูงสุด ต้องอยู่ใน 25% ท้ายคืน |

### 6.5 Solvability check (⑨ — 4 ชม.)

**นี่คือกฎที่ป้องกันบั๊ก "ชนะไม่ได้" ไม่ให้กลับมาอีก**

จำลองผู้เล่นที่เล่นสมบูรณ์แบบ โดยตั้งต้นทุนการจัดการ 1 ตัวไว้ตามจริง:

```
เปิดฟอร์ม (0.3s) + เลือกห้อง (1.5s) + พูด+รอ STT (4-6s)
+ กด submit + รอผล (1.5s) + สลับกล้อง (1-2s)
≈ 10 วินาที ต่อ anomaly 1 ตัว
```

```csharp
private bool IsSolvable(NightPlan plan)
{
    const float HANDLE_COST = 10f;
    float playerFreeAt = 0f;
    int resolvable = 0;

    foreach (var p in plan.anomalies.OrderBy(a => a.atMinute)) {
        float appearAt = p.atMinute * 60f;
        float startAt  = Mathf.Max(appearAt, playerFreeAt);
        float deadline = appearAt + p.definition.threatTimeoutSeconds;

        if (startAt + HANDLE_COST <= deadline) {
            resolvable++;
            playerFreeAt = startAt + HANDLE_COST;
        }
    }
    return resolvable >= plan.requiredScore;
}
```

**เทสต์ที่ต้องมี:** สุ่ม 1000 seed × 5 คืน แล้วยืนยันว่าไม่มี seed ไหนตกเกณฑ์ + เก็บสถิติ (จำนวน anomaly เฉลี่ย, การกระจายห้อง, ช่องว่างที่แคบที่สุด) เอาไว้ดูว่าการสุ่มมีอคติหรือเปล่า

### 6.6 ต่อเข้ากับ Scheduler (⑩ — 8 ชม.)

```csharp
// AnomalyScheduler
public enum ScheduleSource { NightPlan, ManualList }   // เก็บ Manual ไว้เทสต์

void Start() {
    var plan = NightPlanProvider.Current;
    _sorted = plan.anomalies.OrderBy(a => a.atMinute).ToList();
}

private void Spawn(AnomalyPlacement p) {
    var anchor = RoomRegistry.Get(p.room.roomId);
    var go = Instantiate(p.definition.prefab, anchor.GetSpawnPoint(_rng).position, ...);
    go.GetComponent<Anomaly>().AssignRoom(p.room);      // ⭐ ห้องถูกตั้งตอนนี้
    AudioManager.RegisterHierarchy(go);
}
```

พร้อมกัน: `ScoreManager.winThreshold` ถูกลบทิ้ง → อ่าน `NightPlanProvider.Current.requiredScore`

> ⭐ **ตรงนี้คือจุดที่บั๊ก blocker หายไปถาวร** — threshold กับ schedule มาจากแหล่งเดียวกันแล้ว เลยไม่มีทางหลุด sync ได้อีก

### 6.7 Debug tools (⑪ — 3 ชม.)

อย่ามองข้ามข้อนี้ — ถ้าไม่มี คุณจะต้องเล่นเกม 5 นาทีทุกครั้งที่อยากรู้ว่าสุ่มได้อะไร

- `[ContextMenu("Dump Night Plan")]` → พิมพ์ตารางทั้งคืนเป็นข้อความ
- ปุ่ม "Generate 100 plans → CSV" สำหรับดูสถิติ
- Gizmo timeline ในซีน + overlay ตอนเล่นบอก seed
- ช่องใส่ seed เองในหน้าเมนู debug

### ✅ เกณฑ์ผ่าน Sprint 2
- seed เดิม → แผนเดิมเป๊ะ 100% (รันซ้ำ 10 ครั้ง)
- สุ่ม 1000 seed ไม่มีคืนที่ชนะไม่ได้
- แก้ `AnomalyDefinition` แล้วแผนเปลี่ยนตามโดยไม่ต้องแตะโค้ด
- `winThreshold` ไม่มีอยู่ในโปรเจคอีกต่อไป

---

## 7. สิ่งที่ห้ามทำใน 2 สัปดาห์นี้

| ❌ ห้าม | เหตุผล |
|---|---|
| แต่ง visual / เพิ่ม anomaly ระหว่าง refactor | จะแยกไม่ออกว่าบั๊กมาจาก refactor หรือของใหม่ |
| แยก `Anomaly.cs` ก่อนทำ `GameFlowManager` | แยกไม่ออกเพราะ scene loading ผูกอยู่ในนั้น |
| ทำ Haunt Loop ล่วงหน้า | Sprint 4 มี framework รองรับแล้ว ทำตอนนี้ = เขียนสองรอบ |
| ใช้ `UnityEngine.Random` ใน generator | พังฟีเจอร์ seed ทั้งหมด (ดู §6.2) |
| ลบ field เก่าใน prefab ก่อน validator ผ่าน | Unity ไม่เตือน ข้อมูลหายเงียบ |
| commit ครั้งเดียวตอนจบ 2 สัปดาห์ | ย้อนกลับไม่ได้ตอนพัง — commit ทุกขั้น ①-⑪ |

---

## 8. สรุป — จบ 2 สัปดาห์นี้แล้วได้อะไร

**ในสายตาผู้เล่น:** เกมยังหน้าตาเหมือนเดิมเกือบทั้งหมด (ต่างแค่ชนะได้แล้ว และทุกคืนไม่ซ้ำ)

**ในสายตาคุณ — นี่คือของจริงที่ได้:**

| ก่อน | หลัง |
|---|---|
| เพิ่ม anomaly = แก้ 4-5 ที่ | สร้าง asset 1 ไฟล์ |
| เพิ่มห้อง = แก้โค้ด | สร้าง asset + วาง anchor |
| เกณฑ์ชนะตั้งมือ (และผิดอยู่) | คำนวณเอง ผิดไม่ได้ |
| ทุกคืนเหมือนกันเป๊ะ | สุ่ม + reproducible จาก seed |
| เพิ่มเงื่อนไขแพ้ = แก้ `Anomaly.cs` | เพิ่มใน `GameFlowManager` ที่เดียว |
| เทสต์ต้องเล่นจริง 5 นาที | dump แผนได้ทันที + สุ่ม 1000 คืนตรวจอัตโนมัติ |

**นี่คือเหตุผลที่ Sprint 3-6 จะทำเสร็จทัน** — anomaly 8 ชนิด, ห้อง 5 ห้อง, haunt loop 5 ตัว จะกลายเป็นงาน "สร้าง asset แล้วกรอกค่า" แทนที่จะเป็น "แก้โค้ดแล้วภาวนาว่าไม่พัง"

ถ้าข้าม 2 สัปดาห์นี้ไปทำ content เลย คุณจะทำ Sprint 3-6 ได้เร็วกว่าประมาณ 1 สัปดาห์ตอนต้น แล้วเสียเวลากลับมามากกว่านั้นตอน Sprint 6-7 ตอนที่ต้อง balance และ debug ระบบที่ข้อมูลกระจายอยู่ 5 ที่
