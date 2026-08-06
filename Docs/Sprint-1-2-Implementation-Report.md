# Sprint 1-2 — Implementation Report (รายงานผลการรื้อฐานข้อมูล)

**อ้างอิงสเปค:** [`Docs/Sprint-1-2-Data-Refactor-Spec.md`](Sprint-1-2-Data-Refactor-Spec.md)
**สาขา:** `new-gameplay` · **ฐาน:** `5024d29` · **Sprint 1 ลงแล้วที่:** `67d9edb`
**ขอบเขต:** ขั้น ②–⑪ ครบทุกขั้น (Sprint 1 + Sprint 2)

> **สถานะการตรวจสอบ:** โค้ดทั้งหมด **ยังไม่ได้คอมไพล์โดยผู้เขียนรายงาน** — ไม่มี Unity ให้เรียกใช้ในเซสชันที่พัฒนา
> สิ่งที่ตรวจแล้วคือ: ความสมดุลของวงเล็บทุกไฟล์, grep หา reference ที่ค้างหลังเปลี่ยน API, และไล่ตัวเลขของแผนคืนด้วยมือ
> Sprint 1 ได้รับการยืนยันว่าใช้งานได้จริง เพราะ prefab กับซีนถูก commit พร้อมผลของ migration tool

---

## 1. โจทย์: บั๊กหนึ่งตัวที่อธิบายทุกอย่าง

ก่อนเริ่มงาน เกมนี้ **ชนะไม่ได้เลย** ไม่ว่าผู้เล่นจะเก่งแค่ไหน

```
AnomalyScheduler ในซีน   →  8 entry
ScoreManager ในซีน       →  winThreshold: 9
```

ต้องกำจัด anomaly 9 ตัวจากทั้งคืน แต่ทั้งคืนมีแค่ 8 ตัว

ตัวเลขสองตัวนี้ไม่ได้ผิดเพราะใครคำนวณพลาด มันผิดเพราะ **อยู่คนละที่กัน** — คนแก้ตารางเกิดไม่เห็นเกณฑ์ชนะ และคนแก้เกณฑ์ชนะไม่เห็นตารางเกิด นี่คืออาการ ไม่ใช่โรค

โรคจริงคือข้อมูลเกมกระจายอยู่ 5 ที่ที่ไม่รู้จักกัน:

| ข้อมูล | เคยอยู่ที่ | ปัญหา |
|---|---|---|
| ชนิด anomaly | string ใน prefab 7 ไฟล์ | ทุกไฟล์ใส่ `"Shadow"` เหมือนกันหมด → ผู้เล่นแยกไม่ออก |
| ชื่อห้อง | `List<string>` ใน `IncidentReportManager` **และ** ใน `AnomalyOptionsCatalog` | ต้อง sync มือ 2 ที่ |
| ตำแหน่งกล้อง | `float[] {0, 17.73f, 36.12f}` hardcode ใน `GameManager` | เพิ่มห้องต้องแก้โค้ด |
| dropdown ห้อง | 6 ชื่อ แต่กล้องมีจริง 3 ตัว | ฟอร์มโกหกผู้เล่น |
| เกณฑ์ชนะ | `int` ใน Inspector ของ `ScoreManager` | หลุด sync กับตารางเกิด → บั๊กข้างบน |
| ผลของคืน | `PlayerPrefs` เขียนจาก 3 สคริปต์ | `ScoreManager` ต้องเช็คว่าอีกฝ่ายเขียนไปแล้วหรือยัง |

**หลักการเดียวที่ใช้ทั้งงาน:** ข้อมูลชิ้นหนึ่ง มีที่อยู่ที่เดียว

---

## 2. ผลลัพธ์: ที่อยู่ใหม่ของข้อมูลทุกชิ้น

| ข้อมูล | ตอนนี้อยู่ที่ | เพิ่มของใหม่ต้องทำอะไร |
|---|---|---|
| ห้อง (id, ชื่อ, ตำแหน่งกล้อง) | `RoomDefinition` asset | สร้าง asset 1 ไฟล์ + วาง `RoomAnchor` 1 ตัว |
| จุดเกิดในห้อง | `RoomAnchor` ในซีน | ลาก Transform ใส่ array |
| "มีห้องอะไรบ้าง" | `RoomRegistry` (anchor ลงทะเบียนเอง) | ไม่ต้องทำอะไร |
| ชนิด anomaly + keyword + cost + timeout | `AnomalyDefinition` asset | สร้าง asset 1 ไฟล์ + prefab |
| ห้องที่ anomaly โผล่ | `Anomaly.AssignedRoom` ตั้งตอน spawn | สุ่มให้อัตโนมัติ |
| ตารางเกิดทั้งคืน | `NightPlan` สร้างจาก seed | ปรับ `DifficultyProfile` |
| **เกณฑ์ชนะ** | **`NightPlan.requiredScore` คำนวณจากแผน** | **ไม่ต้องตั้ง — ตั้งผิดไม่ได้** |
| ความยาก | `DifficultyProfile` asset | ลากสไลเดอร์ |
| glitch ของคืน | `GlitchProfile` + `NightPlan.glitches` | ปรับน้ำหนักใน asset |
| ผลของคืน | `GameFlowManager.LastResult` (`NightResult`) | เพิ่มค่าใน `NightResult` |

**บั๊ก blocker ตายถาวร** — จำนวน anomaly กับเกณฑ์ชนะมาจาก object เดียวกันแล้ว หลุด sync ไม่ได้อีกโดยโครงสร้าง ไม่ใช่โดยวินัย

---

## 3. ทำอะไรไปบ้าง ทีละขั้น

### ② RoomDefinition + RoomAnchor + RoomRegistry

สร้างชั้นข้อมูลห้อง `ScriptableObject` อ้าง Transform ในซีนไม่ได้ เลยแยกเป็นสองส่วน: asset ถือ id/ชื่อ/ตำแหน่งกล้อง, `RoomAnchor` ในซีนถือจุดเกิด และลงทะเบียนตัวเองเข้า `RoomRegistry` ใน `OnEnable` — ไม่ต้อง wire อะไรเลย

ผลตามมา: `GameManager` เลิก hardcode `CameraPositionsX`, dropdown ในฟอร์มอ่านจาก `RoomRegistry.DisplayNames()` จึงแสดงจำนวนห้องเท่ากล้องจริงเสมอ, `AnomalyOptionsCatalog.locations` ถูกลบ, และ `DemonAnomaly` เลิกเดาห้องตัวเองด้วยการหาค่า X ที่ใกล้ที่สุด

### ③ AnomalyDefinition + migrate prefab 8 ตัว

แยก **ชนิด** (static, อยู่ใน asset) ออกจาก **ห้อง** (runtime, ตั้งตอน spawn) — เพราะ procedural ต้องสุ่มห้องได้

`correctKeywords` เป็น array ไม่ใช่ string เดียว รับคำที่ speech-to-text มักฟังเพี้ยนได้หลายคำต่อชนิด `IncidentReportManager` ตรวจว่าตรงคำใดคำหนึ่งก็ถือว่าถูก

### ④ GameFlowManager + NightResult

รวมเจ้าของ "คืนนี้จบแล้ว" ไว้ที่เดียว `Anomaly`, `DemonAnomaly`, `NightTimer` แค่**รายงานว่าเกิดอะไรขึ้น** แล้ว `GameFlowManager` ตัดสินว่ามันหมายถึงอะไร

`PlayerPrefs` 4 คีย์ (`FinalScore`/`GameWon`/`WinThreshold`/`AnomalyTimeout`) หายไปทั้งชุด แทนด้วย `NightResult` object เดียว ที่เป็น `static` จึงข้ามซีนไปถึง Result ได้เอง

โค้ดที่ต้องเช็คว่า "อีกฝ่ายเขียนไปหรือยัง" คือสัญญาณว่าไม่มีใครเป็นเจ้าของข้อมูลนั้นจริง — ตอนนี้มีเจ้าของแล้ว โค้ดเช็คจึงหายไปด้วย

### ⑤ แยก Anomaly.cs เป็น 4 component

| component | รับผิดชอบ | บรรทัด |
|---|---|---|
| `Anomaly` | identity, state machine, registry, ประสานงาน | 409 |
| `AnomalyMovement` | เดินเข้าหาเป้า + ขยายสเกล | 106 |
| `AnomalyPresenter` | animator trigger + AudioSource | 81 |
| `AnomalyThreatTimer` | นับถอยหลัง → ยิง `OnExpired` | 64 |

state machine: `Hidden → Visible → Threatening → Resolved`

กฎที่ทำให้มันเล็กลง: **anomaly แจ้งว่าเกิดอะไรขึ้น ไม่ตัดสินว่าเกมจบยังไง** เดิม `Anomaly.cs` เขียน `PlayerPrefs` แล้ว `SceneManager.LoadScene("Result")` เองตรงนั้น การเพิ่มเงื่อนไขแพ้ใหม่จึงต้องแก้ไฟล์นี้ ตอนนี้ไม่ต้อง

### ⑥–⑨ NightPlan + Generator + Constraints + Solvability

`NightPlan` คือสคริปต์ของคืนหนึ่ง สร้างจาก seed: anomaly วางที่ห้องไหนนาทีไหน, glitch ยิงเมื่อไหร่, และ `requiredScore`

**เจนเนอเรเตอร์ห้ามแตะ `UnityEngine.Random` เด็ดขาด** เพราะเป็น global state ที่ animation/VFX/glitch weighting ดึงจากบ่อเดียวกัน แค่มีใครสุ่มเพิ่มหนึ่งครั้ง seed เดิมก็ให้ผลต่าง → ฟีเจอร์ replay พังทั้งอัน ทุกการสุ่มมาจาก `System.Random` instance ที่ไม่มีใครเอื้อมถึง

กฎที่ validate:

| กฎ | เงื่อนไข |
|---|---|
| Minimum Spacing | anomaly 2 ตัวห่างกัน ≥ 25 วิ |
| No Overlap | ช่วง threat timer ห้ามซ้อนกัน |
| Room Spread | ห้ามห้องเดิมติดกัน · ทุกห้องถูกใช้ ≥ 1 ครั้ง |
| Type Spread | ห้ามชนิดเดิมติดกัน |
| Onboarding | 20% แรกของคืนไม่มี glitch |
| Climax | ตัวแพงสุดต้องอยู่ 25% ท้ายคืน |
| **Solvability** | **จำลองผู้เล่นสมบูรณ์แบบ 10 วิ/ตัว แล้วต้องได้ ≥ `requiredScore`** |

Solvability คือกฎที่ป้องกันบั๊กเดิมไม่ให้กลับมา ไม่ว่าใครจะปรับตัวเลขอะไรก็ตาม

### ⑩ ต่อ NightPlan เข้ากับ scheduler ทั้ง 3

`AnomalyScheduler` และ `GlitchScheduler` มี `ScheduleSource` = `NightPlan` (ปกติ) หรือ `ManualList` (ทดสอบลำดับตายตัว) และ `ScoreManager.winThreshold` **ถูกลบทิ้ง** อ่านจาก `NightPlan.requiredScore` แทน

### ⑪ Debug tools

`Night Plan Debugger` (editor window) — dump แผนเป็นตาราง, ตรวจ determinism 10 รอบ, รัน batch 1000 seed × 5 คืน พร้อมสถิติและ export CSV

`NightPlanHud` (กด F3 ตอนเล่น) — บอก seed/คืน/คะแนน/ตัวถัดไป และ replay seed เดิมได้ทันที

---

## 4. ไฟล์ที่เพิ่มมา

**~3,490 บรรทัดใน 23 ไฟล์ใหม่**

### Runtime — ชั้นข้อมูล (`GameLogic/Data/`)
`RoomDefinition` 38 · `RoomAnchor` 71 · `RoomRegistry` 114 · `AnomalyDefinition` 73 · `HauntLoopId` 13

### Runtime — การจบคืน (`GameLogic/Flow/`)
`NightResult` 56 · `GameFlowManager` 215

### Runtime — anomaly ที่แยกแล้ว (`GameLogic/`)
`AnomalyMovement` 106 · `AnomalyPresenter` 81 · `AnomalyThreatTimer` 64 · `Anomaly.Legacy` 58 *(ชั่วคราว — ลบได้)*

### Runtime — แผนคืน (`GameLogic/Night/`)
`NightPlan` 101 · `DifficultyProfile` 85 · `GlitchProfile` 73 · `NightContentLibrary` 84 · `NightPlanProvider` 106 · `NightPlanGenerator` 424 · `NightPlanValidator` 284 · `NightPlanRunner` 221 · `NightPlanHud` 146

### Editor (`Assets/Editor/`)
`DataSetupTools` 458 · `DataValidator` 271 · `NightPlanDebugWindow` 348

### ไฟล์ที่แก้
`Anomaly` · `GameManager` · `DemonAnomaly` · `AnomalyOptionsCatalog` · `IncidentReportManager` · `NightTimer` · `AnomalyScheduler` · `GlitchScheduler` · `ScoreManager` · `ResultDisplay` · `SampleSceneManager` · `AnomalyOptionDrawer` · `AnomalySetupTools` · `AnomalySchedulerEditor`

### Asset ที่ tool สร้าง
`Room_Hallway` `Room_Bedroom` `Room_Kitchen` · `Anomaly_*` 8 ไฟล์ · `DifficultyProfile` · `GlitchProfile` · `NightContentLibrary`

---

## 5. ต้องทำอะไรต่อใน Unity (ยังค้าง)

### 5.1 เปิดใช้ Sprint 2

```
1. Tools > Give Me A Sign > Setup > 3. Create Night Content Library
2. เพิ่ม component NightPlanRunner ลงบน GameObject ใดก็ได้ใน GameManager.unity
3. Ctrl+S
4. Tools > Give Me A Sign > Night Plan Debugger  →  กด "Run 1000 x 5"
```

ขั้น 4 คือ acceptance test ของสเปค §6.5 ควรได้ `unwinnable: 0 (PASS)`

### 5.2 แก้ข้อมูล 1 จุด

`Assets/Settings/Anomalies/Anomaly_DemonAnomaly.asset` → ตั้ง `threatTimeoutSeconds` **จาก 0 เป็น 30**

เพราะ demon บังคับเวลาด้วย `DemonAnomaly.timeLimitSeconds = 30` ของตัวเอง ไม่ได้ผ่าน `AnomalyThreatTimer` ค่า legacy จึงเป็น 0 ถ้าปล่อยไว้ ตัววางแผนจะคิดว่า demon ทิ้งไว้ได้เรื่อยๆ แล้วประเมิน solvability สูงเกินจริง

(migration tool ดึงค่านี้อัตโนมัติแล้ว แต่โค้ดนั้นมาหลังการรันครั้งแรก และ tool จะข้าม prefab ที่ `migrated` แล้ว)

### 5.3 เมื่อ validator เขียว

ลบ [`Assets/Scripts/GameLogic/Anomaly.Legacy.cs`](../Assets/Scripts/GameLogic/Anomaly.Legacy.cs) และบรรทัด `if (!migrated) SeedSiblingsFromLegacy(...)` ใน `Anomaly.Awake`

จากนั้นลบ `AnomalyOptionsCatalog` + `AnomalyOptionAttribute` + `AnomalyOptionDrawer` ได้ด้วย

---

## 6. ต่อยอดใช้ได้ยังไง

### เพิ่ม anomaly ชนิดใหม่

```
1. Create > Give Me A Sign > Anomaly Definition
2. กรอก anomalyId, correctKeywords (คำที่ผู้เล่นต้องพูด — ใส่คำที่ STT มักฟังเพี้ยนด้วย),
   threatCost (ยิ่งสูง = ยิ่งกินงบภัยคุกคาม), minNightIndex (ห้ามโผล่ก่อนคืนที่เท่าไหร่)
3. ลาก prefab ใส่ช่อง prefab (prefab ต้องมี Anomaly component)
4. Tools > Give Me A Sign > Setup > 3. Create Night Content Library   (กวาด asset ใหม่เข้า library)
5. Tools > Give Me A Sign > Validate Data
```

**ไม่ต้องแตะโค้ด ไม่ต้องแก้ตารางเกิด ไม่ต้องแก้เกณฑ์ชนะ** — เจนเนอเรเตอร์หยิบไปใช้เองตั้งแต่คืนถัดไป

`allowedRooms` เว้นว่าง = เกิดได้ทุกห้อง

### เพิ่มห้องใหม่

```
1. Create > Give Me A Sign > Room Definition
2. กรอก roomId (ห้ามเปลี่ยนหลังใช้แล้ว), displayName (เปลี่ยนได้ตลอด), cameraX, cameraOrder
3. วาง GameObject ในซีน + ใส่ RoomAnchor + ลาก RoomDefinition ใส่
4. (ถ้าอยากได้หลายจุดเกิด) ลาก Transform ใส่ spawnPoints
5. Create Night Content Library ซ้ำ
```

dropdown ในฟอร์ม, ตัวสลับกล้อง, และตัวสุ่มห้อง อัปเดตตามเองทั้งหมด

### ปรับความยาก

ทุกปุ่มอยู่ใน `Assets/Settings/DifficultyProfile.asset`

| ช่อง | ผล |
|---|---|
| `baseThreatBudget` | จำนวน anomaly คืนแรก (ค่าเริ่ม 8) |
| `budgetGrowthPerNight` | โหดขึ้นต่อคืน |
| `winRatio` | สัดส่วนที่ต้องจัดการ → `requiredScore` (ค่าเริ่ม 0.7) |
| `minimumSpacingSeconds` | ช่องว่างขั้นต่ำระหว่างตัว |
| `handleCostSeconds` | ต้นทุนจัดการ 1 ตัว ที่ solvability ใช้จำลอง (ค่าเริ่ม 10) |
| `onboardingQuietFraction` | ช่วงต้นคืนที่ไม่มี glitch |
| `maxAttempts` | สุ่มใหม่กี่ครั้งก่อนตกไป fallback |

แก้แล้วกด `Run 1000 x 5` ใน debugger ทันทีเพื่อดูว่ายังชนะได้ทุก seed — ไม่ต้องเล่นจริง

### ทดสอบคืนซ้ำ / ไล่บั๊กจากรายงานผู้เล่น

| อยากทำ | ทำที่ |
|---|---|
| เล่นคืนเดิมซ้ำเป๊ะ | `NightPlanRunner.seedOverride` หรือ กด F3 ตอนเล่น → ใส่ seed → Go |
| ทดสอบคืนที่ 5 เลย | `NightPlanRunner.nightIndexOverride = 5` |
| ดูว่า seed นี้ได้อะไร | `Night Plan Debugger` → ใส่ seed → Generate & Dump |
| ใช้ตารางเกิดตายตัว | `AnomalyScheduler.Source = ManualList` |
| ดูแผนตอนเล่น | เพิ่ม `NightPlanHud` ลงซีน แล้วกด F3 |

seed ถูกบันทึกลง `NightResult.seed` ทุกคืน ผู้เล่นแจ้งบั๊กมาพร้อม seed ก็เล่นซ้ำได้ตรงตัว

### เพิ่มเงื่อนไขแพ้ใหม่ (เช่น negligence strikes ของ Sprint 4)

```csharp
// 1. เพิ่มค่าใน NightOutcome
public enum NightOutcome { Survived, KilledByAnomaly, KilledByDemon, Negligence }

// 2. เรียกจากที่ไหนก็ได้
GameFlowManager.Instance?.EndNight(NightOutcome.Negligence);

// 3. ถ้าอยาก react ต่อ anomaly ที่หมดเวลา ไม่ต้องแก้ Anomaly.cs
Anomaly.OnAnyThreatExpired += anomaly => { /* นับ strike */ };
```

**ไม่ต้องแตะ `Anomaly.cs` เลย** — ซึ่งคือเหตุผลทั้งหมดที่แยกมันออกมา

### เพิ่มเสียงใหม่

ระบบเดิมที่มีอยู่ก่อนแล้ว: ใส่ clip ชื่อหนึ่งใน `Sound Library` บน prefab `AudioManager` แล้วเรียก `AudioManager.Instance.Play("ชื่อ")`

---

## 7. จุดที่ตัดสินใจต่างจากสเปค

| # | เรื่อง | เหตุผล |
|---|---|---|
| 1 | `Anomaly.cs` 409 บรรทัด ไม่ใช่ < 150 | ความรับผิดชอบเหลือเรื่องเดียวแล้ว (โค้ดจริง ~230) การไล่ตัดคอมเมนต์ให้ถึง 150 จะได้โค้ดที่แย่กว่าโค้ดรอบๆ ในโปรเจคนี้ ส่วน 3 component ที่แยกออกมา 106/81/64 ผ่านหมด |
| 2 | ค่า `0/17.73/36.12` ย้ายไป `DataSetupTools` ไม่ได้หายจากโปรเจค | background sprite อยู่ที่ 0/17.96/36.19 ซึ่ง**ไม่เท่า**ตำแหน่งกล้อง เลย derive จากซีนไม่ได้ ต้องยกค่ามาครั้งเดียว — runtime code สะอาดตามเกณฑ์ และลบไฟล์ tool ได้หลัง migrate |
| 3 | ใส่ flag `migrated` แทนการเชื่อ `[RequireComponent]` ตรงๆ | ถ้า Unity auto-add component ให้ prefab เก่า มันจะได้ค่า default ทับค่าที่ตั้งไว้ (`moveSpeed 30` → `3`) แล้วหายเงียบ |
| 4 | `timeToDisappear = 0` เปลี่ยนความหมายเป็น "ไม่มีเวลาจำกัด" | ให้ตรงกับ `AnomalyDefinition.threatTimeoutSeconds` ตรวจแล้วว่าไม่มี prefab ไหนได้รับผลกระทบ |
| 5 | วางเวลาเป็น slot ไม่ใช่สุ่มอิสระตาม §6.3 | สุ่มอิสระถูก reject บ่อยมากจนพึ่ง fallback เกือบทุกครั้ง วางเป็น slot ที่กว้างพอสำหรับ threat window ยาวสุด → Minimum Spacing กับ No Overlap เป็นจริงโดยโครงสร้าง validator ยังตรวจทับอยู่ |
| 6 | 3 กฎ degrade ได้ ไม่ reject ทิ้ง | Room coverage บังคับเมื่อ anomaly ≥ จำนวนห้อง · Type Spread ข้ามเมื่อมีชนิดเดียว · Climax ข้ามเมื่อทุกชนิด cost เท่ากัน (คือกรณีปัจจุบัน) ไม่ทำแบบนี้กฎจะ reject ทุก seed |
| 7 | Minimum Spacing นับ anomaly-to-anomaly ไม่ใช่ "เหตุการณ์ทุกชนิด" | ถ้ารวม glitch จะบีบจนแทบไม่มี seed ผ่าน และ glitch ไม่กินเวลาผู้เล่น |
| 8 | เพิ่ม `NightContentLibrary` ที่สเปคไม่ระบุ | `AssetDatabase` ใช้ได้แค่ใน editor ไม่ผ่าน build จึงต้องมี asset ใน `Resources/` และทำให้ debugger รันได้โดยไม่เปิดซีน |
| 9 | build timeline ที่ tick แรก ไม่ใช่ `Start()` | Unity ไม่การันตีลำดับ `Start()` ระหว่าง GameObject — scheduler อาจรันก่อน runner ได้ ทำที่ tick แรกการันตีว่าอยู่หลัง `Start()` ทุกตัวในซีน |
| 10 | ถ้าคืนสั้นเกินจะลด anomaly ลงเอง | ของเดิมถ้าลด night duration entry ที่เลยเวลาจะหายเงียบ ตอนนี้แผนสร้างน้อยลงและ `requiredScore` ลดตาม ทดสอบคืน 1 นาทีได้เลย |
| 11 | ลบ tool `Set All Anomaly Types To Shadow` | มันคือตัวที่สร้างปัญหา "Shadow ทั้ง 7 ตัว" และคอมไพล์ไม่ผ่านกับ model ใหม่ |
| 12 | ย้าย `SampleSceneManager` ที่สเปคไม่ได้พูดถึง | มันอยู่ใน `Result.unity` จริงและอ่าน `PlayerPrefs["AnomalyTimeout"]` ถ้าไม่ย้ายจะเข้า branch ผิดแบบเงียบๆ |

### บั๊กแฝงที่แก้ไปด้วยตอนแยกไฟล์

- `ResolveByReport()` ไม่หยุด fight audio loop → รายงานถูกหลังจากเคยพลาด เสียงค้าง
- `ResolveByReport()` ไม่เคลียร์ flag prayer → prayer ยิงซ้ำบน anomaly ที่ resolved แล้วได้

---

## 8. ยังไม่ได้ทำ (นอกขอบเขต Sprint 1-2)

| เรื่อง | สถานะ |
|---|---|
| **progression** | `NightPlanRunner` อ่าน `PlayerPrefs["UnlockedNight"]` แล้ว แต่ยังไม่มีใครเขียนค่าเพิ่มตอนชนะ ใช้ `nightIndexOverride` ทดสอบคืนหลังๆ ได้ |
| **Haunt Loop** | `HauntBeat` + `HauntLoopId` มีโครงแล้วแต่ยังว่าง — Sprint 4 มาเติมโดยไม่ต้องแก้โครงสร้าง |
| **death sequence** | `GameFlowManager.delayAfterDeath` เป็นช่องที่ Sprint 6 มาแขวนได้ |
| **`SampleSceneManager` ซ้ำกับ `ResultDisplay`** | ทั้งสองตัวอยู่ใน `Result.unity` ทำงานเดียวกัน ควรรวมเป็นตัวเดียวตอนรีเวิร์ค Result scene |
| **solvability ไม่รู้จัก demon โดยตรง** | จำลองผ่าน `threatTimeoutSeconds` ของ definition (ดู §5.2) ไม่ได้อ่าน `DemonAnomaly.timeLimitSeconds` ตอนรัน |

---

## 9. ตารางตรวจเกณฑ์

### Sprint 1

| เกณฑ์จากสเปค | ผล |
|---|---|
| grep `17.73` ใน runtime code ไม่เจอ | ผ่าน |
| เพิ่มห้อง = สร้าง asset + วาง anchor ไม่ต้องแก้โค้ด | ผ่าน |
| dropdown แสดงจำนวนห้องเท่ากล้องจริง | ผ่าน |
| ไม่มี `correctAnomalyType` / `correctLocationName` เหลือ | `correctLocationName` ลบแล้ว · `correctAnomalyType` เหลือใน `Anomaly.Legacy.cs` ตามแผน migration |
| `PlayerPrefs` เหลือแค่ `AudioManager` + progression | ผ่าน |
| `SceneManager.LoadScene` แค่ `GameFlowManager` + `SceneTransition` | ผ่าน |
| `Anomaly.cs` ไม่ `using UnityEngine.SceneManagement` | ผ่าน |
| ทุก class < 150 บรรทัด | 3 component ใหม่ผ่าน · `Anomaly.cs` 409 ไม่ผ่าน (ดู §7.1) |

### Sprint 2

| เกณฑ์จากสเปค | ผล |
|---|---|
| `winThreshold` ไม่มีในโปรเจค | ผ่าน (grep ไม่เจอ) |
| ไม่ใช้ `UnityEngine.Random` ในเจนเนอเรเตอร์ | ผ่าน (ทั้งโฟลเดอร์ `Night/` เจอแต่ในคอมเมนต์) |
| seed เดิม → แผนเดิม รันซ้ำ 10 ครั้ง | มีปุ่ม `Check Determinism (x10)` — **ยังต้องกดจริง** |
| สุ่ม 1000 seed ไม่มีคืนที่ชนะไม่ได้ | มีปุ่ม `Run 1000 x 5` — **ยังต้องกดจริง** |
| แก้ `AnomalyDefinition` แล้วแผนเปลี่ยนตาม | ผ่าน |

---

## 10. คืนที่ 1 จะได้อะไร (คำนวณจากข้อมูลจริงในโปรเจค)

| | ค่า | ที่มา |
|---|---|---|
| งบภัยคุกคาม | 8 | `baseThreatBudget` 8 ÷ `threatCost` 1 |
| ช่องว่างขั้นต่ำ | 30 วิ | `max(minimumSpacing 25, threat window ยาวสุด 30)` |
| slot ที่ได้จริง | 36.4 วิ | ช่วงใช้ได้ 255 วิ ÷ 7 |
| จำนวน anomaly | 8 | คืน 5 นาทีรับได้ 9 |
| **เกณฑ์ชนะ** | **6** | `ceil(8 × 0.7)` |
| ผู้เล่นสมบูรณ์แบบจัดการได้ | 8 | ต้นทุน 10 วิ/ตัว |
| **ชนะได้จริง** | **ใช่** | 8 ≥ 6 |
