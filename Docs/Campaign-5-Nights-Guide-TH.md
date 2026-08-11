# คู่มือระบบแคมเปญ 5 คืน (Give Me A Sign)

เอกสารนี้อธิบายว่าเกมสร้าง "คืน" ขึ้นมายังไง, ความยากถูกไต่ขึ้นยังไงในแต่ละคืน,
และถ้าอยากเพิ่ม Anomaly แบบใหม่ / ปรับจูนอะไร ต้องไปแตะตรงไหนบ้าง

---

## 1. ภาพรวม: คืนหนึ่งเกิดขึ้นได้ยังไง

เกมนี้ **ไม่ได้วางไทม์ไลน์ด้วยมือ** ทุกคืนถูก "สุ่มสร้างจาก seed" แล้วเอาไปตรวจว่าเล่นชนะได้จริงก่อนใช้งาน
ลำดับการทำงานตอนกด Play ในซีน `GameManager.unity`:

```
1. RoomAnchor ทุกตัวในซีน  ──register──►  RoomRegistry        (มีห้องอะไรบ้าง)
2. NightPlanRunner.Start()
      ├─ อ่านว่าตอนนี้คืนที่เท่าไหร่        (PlayerPrefs "UnlockedNight")
      ├─ ขอความยาวคืนจาก DifficultyProfile  แล้วยัดใส่ NightTimer
      ├─ NightPlanGenerator.GenerateValid() ──► NightPlan   (สุ่ม + ตรวจ + สุ่มใหม่ถ้าไม่ผ่าน)
      └─ NightPlanProvider.Publish(plan)        (ประกาศให้ทั้งเกมอ่าน)
3. NightTimer เริ่มเดิน ──tick แรก──► AnomalyScheduler / GlitchScheduler / HauntDirector
      สร้างไทม์ไลน์ของตัวเองจาก NightPlan
4. เล่นจนถึง 6:00 AM หรือตาย ──► GameFlowManager.EndNight() ──► ซีน Result
```

**จุดสำคัญที่ต้องเข้าใจ:** Scheduler ทุกตัว *ไม่* อ่านแผนใน `Start()` ของตัวเอง
แต่รอ **tick แรกของ NightTimer** เพราะ tick แรกการันตีว่าเกิดหลัง `Start()` ของทุกสคริปต์ในซีนแล้ว
→ ไม่ต้องไปตั้ง Script Execution Order ให้ปวดหัว

---

## 2. หน้าที่ของแต่ละ Script

### 2.1 กลุ่มข้อมูล (ScriptableObject — ไม่มีตัวตนในซีน เป็นไฟล์ Asset)

| Script | ไฟล์ Asset | หน้าที่ |
|---|---|---|
| `Data/AnomalyDefinition.cs` | `Assets/Settings/Anomalies/*.asset` | นิยาม **"ชนิด" ของ Anomaly หนึ่งตัว** — ชื่อ, คำที่ต้องพูด, prefab, ราคาภัยคุกคาม, คืนที่ปลดล็อก |
| `Data/RoomDefinition.cs` | `Assets/Settings/Rooms/*.asset` | นิยามห้องหนึ่งห้อง — id, ชื่อที่โชว์, ตำแหน่ง X ของกล้อง, ลำดับกล้อง |
| `Night/DifficultyProfile.cs` | `Assets/Settings/DifficultyProfile.asset` | **ตัวคุมความยากทั้งหมด** รวมตารางจูนรายคืน 1-5 |
| `Night/GlitchProfile.cs` | `Assets/Settings/GlitchProfile.asset` | ฟอร์มจะเพี้ยนแบบไหนบ้าง + น้ำหนักการสุ่ม |
| `Night/HauntProfile.cs` | `Assets/Settings/HauntProfile.asset` | Haunt loop ไหนใช้ได้คืนไหน + กี่ครั้งต่อคืน |
| `Night/NightContentLibrary.cs` | `Assets/Resources/NightContentLibrary.asset` | **รวมทุกอย่างข้างบนไว้ที่เดียว** — generator ดึงของจากที่นี่ที่เดียว |

> `NightContentLibrary` อยู่ในโฟลเดอร์ `Resources/` เพื่อให้ `NightContentLibrary.Load()`
> หาเจอโดยไม่ต้องลากใส่ช่องไหนในซีนเลย และติดไปกับ build ด้วย

### 2.2 กลุ่มสร้างคืน

| Script | หน้าที่ |
|---|---|
| `Night/NightPlan.cs` | **โครงสร้างข้อมูลของ "คืนหนึ่งคืน"** — list ของ anomaly/glitch/haunt พร้อมนาทีที่จะเกิด, คะแนนที่ต้องได้, ค่าปรับเมื่อรายงานผิด |
| `Night/NightPlanGenerator.cs` | สุ่มสร้าง `NightPlan` จาก seed |
| `Night/NightPlanValidator.cs` | ตรวจว่าแผนที่สุ่มมา "เล่นชนะได้จริงไหม" ถ้าไม่ผ่านให้สุ่มใหม่ |
| `Night/NightPlanProvider.cs` | ที่เก็บแผนปัจจุบันแบบ static ให้ทั้งเกมอ่านได้ (`NightPlanProvider.Current`) |
| `Night/NightPlanRunner.cs` | ตัวสั่งงานในซีน — เรียก generator แล้ว publish + ตั้งค่าคืนนี้เป็นคืนสอน (tutorial) ถ้าเป็นคืนที่ 1 |
| `Night/NightPlanHud.cs` | HUD ดีบัก (กด F3) โชว์แผนคืนนี้ + ปุ่มเล่นซ้ำ seed เดิม |

#### `NightPlanGenerator` ทำงานยังไง (ละเอียด)

```
GenerateValid(nightIndex, duration, seed)
  └─ วน maxAttempts (50) ครั้ง:
       Generate(..., attempt)  ← ใช้ seed + attempt*7919 เป็น RNG
       ├─ SelectAnomalies()  : ใช้ "งบภัยคุกคาม" ซื้อ Anomaly ทีละตัวจนงบหมด
       │                        เลี่ยงหยิบชนิดซ้ำกับตัวก่อนหน้า
       ├─ LayOutTimes()      : แบ่งช่วงเวลาที่ใช้ได้เป็นช่องเท่าๆ กัน แล้วสุ่มขยับในช่อง
       │                        ความกว้างช่อง >= ระยะห่างขั้นต่ำเสมอ → สเปซกันชนโดยธรรมชาติ
       ├─ MoveCostliestLast(): ย้ายตัวแพงสุดไปท้ายสุด (กฎ Climax)
       ├─ AssignRooms()      : แจกห้องแบบ "ถุงจับสลาก" ให้ครบทุกห้องก่อนวนซ้ำ
       ├─ PlaceGlitches()    : วาง glitch หลังช่วงเงียบตอนต้นคืน
       └─ PlaceHaunts()      : วาง haunt แบบเดียวกัน
       ▼
       NightPlanValidator.Validate()  ผ่าน? → ใช้เลย : สุ่มใหม่
  └─ ครบ 50 ครั้งยังไม่ผ่าน → GenerateFallback() (แผนง่ายๆ ที่การันตีว่าชนะได้)
```

**ห้ามแตะ:** generator ใช้ `System.Random` ของตัวเองเท่านั้น **ห้ามใช้ `UnityEngine.Random`**
เพราะ `UnityEngine.Random` เป็น global state ที่ animation/VFX แย่งกันหมุน → seed เดิมจะได้คืนไม่เหมือนเดิม

#### `NightPlanValidator` ตรวจ 7 ข้อ

| กฎ | ความหมาย |
|---|---|
| Spacing | Anomaly 2 ตัวติดกันต้องห่างกัน >= `MinimumSpacingFor(คืนนี้)` |
| NoOverlap | ห้ามมี 2 ตัวที่มี deadline ทับกัน (ผู้เล่นอยู่ 2 ห้องพร้อมกันไม่ได้) |
| RoomSpread | ห้ามใช้ห้องเดิมติดกัน + ถ้า anomaly มากพอต้องใช้ครบทุกห้อง |
| TypeSpread | ห้ามชนิดเดิมติดกัน |
| Onboarding | ช่วงต้นคืน (20%) ต้องไม่มี glitch/haunt เลย |
| Climax | ตัวแพงสุดของคืนต้องอยู่ใน 25% ท้ายคืน |
| **Solvable** | **จำลองผู้เล่นที่เล่นเพอร์เฟกต์ ต้องทำคะแนนถึงเกณฑ์ได้จริง** ← ข้อสำคัญสุด |

ข้อ Solvable คือตัวที่กันบั๊กคลาสสิก "ต้องได้ 9 แต้ม จาก anomaly 8 ตัว" ไม่ให้หลุดไป build อีก

### 2.3 กลุ่มรันคืน (อยู่ในซีน `GameManager.unity`)

| Script | หน้าที่ |
|---|---|
| `SpawnAndTime/NightTimer.cs` | นาฬิกา 0:00→6:00 AM, map เวลาจริงเป็นเวลาในเกม, ยิง event `OnTimeChanged` ทุกเฟรม |
| `SpawnAndTime/AnomalyScheduler.cs` | เกิด Anomaly ตามนาทีในแผน + **เกิด Anomaly เพิ่มเป็นบทลงโทษเมื่อรายงานผิด** |
| `Report/GlitchScheduler.cs` | ยิง glitch ตามนาทีในแผน (ถ้าฟอร์มปิดอยู่จะรอจนเปิดแล้วค่อยยิง ไม่มีการข้าม) |
| `Report/GlitchDirector.cs` | ตัวคุม glitch ทั้งหมด — ทั้งแบบสุ่ม ambient, แบบสคริปต์, และ blackout (ช่วงห้ามเพี้ยน) |
| `Report/HauntDirector.cs` | ยิง haunt loop ตามแผน |
| `Report/IncidentReportManager.cs` | ลูปหลักของเกม — เปิดฟอร์ม, ตรวจคำตอบ, ตัดสินถูก/ผิด |
| `Score/ScoreManager.cs` | นับแต้ม โดยฟัง event `Anomaly.OnAnyAnomalyDisappeared` |
| `Flow/GameFlowManager.cs` | **เจ้าของเดียวของ "คืนจบแล้ว"** — สรุปผล, บันทึกความคืบหน้า, โหลดซีน Result |
| `Flow/NightResult.cs` | ผลลัพธ์ของคืนหนึ่ง + ค่าคงที่ `FinalNightIndex = 5` |
| `Score/ResultDisplay.cs` | หน้าจอสรุปผล + ปุ่ม Play Again / Replay Seed / Restart Campaign |

### 2.4 กลุ่ม Anomaly ตัวเป็นๆ

`Anomaly.cs` ถูกแตกออกเป็น 4 component ที่อยู่ด้วยกันบน prefab เดียว:

| Component | หน้าที่ |
|---|---|
| `Anomaly.cs` | เจ้าของ **สถานะ** (Hidden→Visible→Threatening→Resolved), ตัวทะเบียนกลาง `ActiveAnomalies`, และ event ต่างๆ |
| `AnomalyMovement.cs` | การเคลื่อนที่ |
| `AnomalyPresenter.cs` | ภาพ/เสียง/อนิเมชัน |
| `AnomalyThreatTimer.cs` | ตัวนับถอยหลังก่อนแพ้ |
| `DemonAnomaly.cs` | ปีศาจ — jumpscare ที่เป็น Anomaly ในตัว มีนาฬิกาของตัวเองแยกต่างหาก |

> **กฎที่ทำให้ `Anomaly.cs` ไม่บวม:** Anomaly แค่ *รายงานว่าเกิดอะไรขึ้น* มันไม่เคยตัดสินว่า
> *คืนนี้จบยังไง* — มันโยนให้ `GameFlowManager` ตัดสิน ทำให้เพิ่มเงื่อนไขแพ้แบบใหม่ได้โดยไม่ต้องแก้ไฟล์นี้

---

## 3. ระบบไต่ระดับ 5 คืน

### 3.1 ความคืบหน้าถูกเก็บยังไง

- เก็บใน PlayerPrefs key เดียว: `"UnlockedNight"` (ค่าคงที่ `NightPlanRunner.UnlockedNightKey`)
- **ชนะ** → `GameFlowManager.AdvanceProgression()` เลื่อนเป็นคืนถัดไป (จำกัดสูงสุดที่ 5)
- **แพ้** → ไม่แตะค่า → กด Play Again = เล่นคืนเดิมซ้ำ
- ชนะคืนที่ 5 → `NightResult.IsCampaignComplete` = true → หน้า Result ขึ้น **"YOU SURVIVED THE WEEK"**
- ปุ่ม Restart Campaign เรียก `GameFlowManager.ResetProgression()` → กลับไปคืน 1

**ทดสอบคืนใดคืนหนึ่งเร็วๆ:** ใส่เลขในช่อง `Night Index Override` บน GameObject `NightPlanRunner` ในซีน
(0 = ใช้ progression จริง, 1-5 = บังคับคืนนั้น)

### 3.2 ตารางจูนรายคืน — หัวใจของระบบใหม่

เดิมความยากเป็นสูตรเชิงเส้นอย่างเดียว (`base + growth × (คืน-1)`) ซึ่ง **อิ่มตัว**:
งบเพิ่มขึ้นก็จริง แต่ `LayOutTimes()` ยัด Anomaly ลงในคืน 5 นาทีได้สูงสุดแค่ 9 ตัว
→ คืน 2, 3, 4, 5 ได้ Anomaly เท่ากันหมด ไม่ยากขึ้นจริง

จึงเพิ่ม **`NightTuning`** — ตารางจูนรายคืนใน `DifficultyProfile` ที่ override สูตรได้ทีละช่อง
(สูตรเดิมยังอยู่ข้างล่างเป็น fallback สำหรับคืนที่ไม่มีแถว)

**ค่าที่ตั้งไว้ตอนนี้** (`Assets/Settings/DifficultyProfile.asset` → ช่อง `Nights`):

| คืน | ความยาว | งบภัย | ต้องได้ % | ห่างขั้นต่ำ | Glitch | โทษตอบผิด | Anomaly ที่ปลดล็อก |
|---|---|---|---|---|---|---|---|
| **1 — สอนเล่น** | 4 นาที | 4 | 50% | 35 วิ | **0** | **0 ตัว** | 2 ชนิด (แบบง่าย) |
| **2** | 5 นาที | 7 | 60% | 30 วิ | 2 | 1 ตัว | +2 ชนิด |
| **3** | 6 นาที | 10 | 65% | 26 วิ | 4 | 1 ตัว | **+ปีศาจ** |
| **4** | 7 นาที | 14 | 70% | 23 วิ | 6 | **2 ตัว** | +1 ชนิด |
| **5 — คืนสุดท้าย** | 8 นาที | 18 | 75% | 20 วิ | 8 | 2 ตัว | +1 ชนิด (แพงสุด) |

ผลลัพธ์โดยประมาณ (จำนวน Anomaly จริงขึ้นกับ seed เพราะราคาแต่ละชนิดไม่เท่ากัน):

| คืน | Anomaly ราว | ต้องเก็บ | Haunt |
|---|---|---|---|
| 1 | 4 | 2 | 0 |
| 2 | 5-7 | 3-5 | 2 |
| 3 | 7-9 | 5-6 | 3 |
| 4 | 9-12 | 7-9 | 3 |
| 5 | 11-14 | 9-11 | 4 |

**ทำไมต้องยืดความยาวคืนด้วย?** เพราะมันคือคันโยกเดียวที่แก้เพดานได้จริง
งบภัยบอกว่า *ซื้อได้กี่ตัว* แต่ความยาวคืนบอกว่า *ยัดลงไปได้กี่ตัว* ถ้าไม่ยืดคืน เพิ่มงบไปก็ตันเหมือนเดิม

### 3.3 คืนที่ 1 เป็นบทสอนเล่นยังไง

`NightPlanRunner.ApplyGlitchProfile()` สั่ง `GlitchDirector.SetFlag("tutorial", nightIndex == 1)`
แล้วสิ่งเหล่านี้ก็เงียบหมดโดยอัตโนมัติ:

1. **Glitch แบบสุ่ม (ambient)** — `GlitchDirector` มีรายการ blackout ชื่อ *"No glitches during the night 1 tutorial"* (condition `AlwaysWhenFlagSet`, flag `tutorial`) ← ตัวนี้มีอยู่ในซีนแล้ว
2. **Glitch ตามแผน** — ตั้ง `glitchCount: 0` ในตารางคืน 1 → ไม่มีอะไรให้ยิง
3. **Haunt ทุกชนิด** — `HauntDirector.Fire()` อ่าน flag `tutorial` ตรงๆ แล้วข้ามทุก beat
4. **ปีศาจ** — ตั้ง `minNightIndex: 3` ใน `Anomaly_DemonAnomaly.asset` → ไม่มีทางโผล่คืน 1-2
5. **ตอบผิดฟรี** — `penaltyAnomaliesPerWrongReport: 0` → ผิดแล้วไม่โดนอะไรเลย

---

## 4. Anomaly: ราคาและการปลดล็อก

`threatCost` = ราคาที่ generator ต้องจ่ายจากงบภัยคุกคามเพื่อ "ซื้อ" Anomaly ตัวนั้นลงคืน
ยิ่งแพง = ยิ่งอันตราย = ยิ่งโผล่น้อยครั้ง และ **ตัวแพงสุดจะถูกบังคับให้ไปอยู่ท้ายคืน** (กฎ Climax)

`minNightIndex` = ห้ามโผล่ก่อนคืนที่เท่าไหร่ → นี่คือวิธี "ปล่อยของใหม่" ให้ผู้เล่นเจอทีละอย่าง

**ค่าที่ตั้งไว้ตอนนี้:**

| Asset | RespondType | ราคา | ปลดล็อกคืน |
|---|---|---|---|
| `Anomaly_Anomaly2Res1` | DisappearInstantly | 1 | 1 |
| `Anomaly_Anomaly3Res1` | DisappearInstantly | 1 | 1 |
| `Anomaly_Anomaly5Res1` | DisappearInstantly | 1 | 2 |
| `Anomaly_Anomaly1Res2` | MoveToTargetThenDisappear | 2 | 2 |
| `Anomaly_Anomaly4Res2` | MoveToTargetThenDisappear | 2 | 3 |
| `Anomaly_DemonAnomaly` | MoveOnly | **4** | **3** |
| `Anomaly_Anomaly6Res2` | MoveToTargetThenDisappear | 2 | 4 |
| `Anomaly_Anomaly7Res2` | MoveToTargetThenDisappear | **3** | **5** |

> **หมายเหตุสำคัญ:** `threatTimeoutSeconds` ของ Anomaly ธรรมดาถูกตั้งเป็น **0** แล้ว
> เพราะหลังยกเลิกระบบไล่ล่า ตัวจับเวลานี้ไม่เคยถูกสตาร์ทอีกเลย ถ้าปล่อยเป็น 30 ไว้
> generator จะยังจองเวลาเผื่อ 30 วินาทีต่อตัว ทำให้ตั้งค่า "ระยะห่างขั้นต่ำ" ต่ำกว่า 30 ไปก็ไม่มีผล
> ส่วนปีศาจยังเป็น 30 อยู่ เพราะมันมีนาฬิกาจริง (`timeLimitSeconds` บน prefab) ที่ generator ควรจองเวลาให้

---

## 5. วิธีทำสิ่งต่างๆ

### 5.1 เพิ่ม Anomaly ชนิดใหม่

1. **ทำ prefab** — copy จาก `Assets/Prefabs/Anomaly2Res1.prefab` เป็นตัวตั้งต้น
   ต้องมี component `Anomaly` (component `AnomalyMovement` / `AnomalyPresenter` / `AnomalyThreatTimer`
   ถูก `[RequireComponent]` บังคับให้มีอยู่แล้ว)
2. **ทำ Definition** — คลิกขวาในโปรเจกต์ → `Create ▸ Give Me A Sign ▸ Anomaly Definition`
   เซฟไว้ที่ `Assets/Settings/Anomalies/`
3. **กรอกค่า:**
   - `anomalyId` — key ถาวร **ห้ามเปลี่ยนหลังปล่อยเกม** (seed/save อ้างถึงมัน)
   - `correctKeywords` — คำที่พูดแล้วนับว่าถูก **ใส่คำที่ระบบฟังผิดบ่อยๆ ลงไปด้วย** เช่น `["Demon", "Daemon", "Demons"]`
   - `prefab` — ลาก prefab จากข้อ 1 มาใส่
   - `respondType` — พฤติกรรม (ดูข้อ 5.2)
   - `threatCost` — ราคา (1 = ธรรมดา, 3-4 = ตัวชูโรงของคืน)
   - `minNightIndex` — คืนที่เริ่มโผล่
   - `allowedRooms` — เว้นว่าง = โผล่ได้ทุกห้อง
   - `threatTimeoutSeconds` — ใส่ 0 ถ้าไม่มีนาฬิกาแพ้เป็นของตัวเอง
4. **ลงทะเบียน** — เปิด `Assets/Resources/NightContentLibrary.asset` แล้วเพิ่ม Definition ใหม่เข้า list `anomalies`
   **ถ้าลืมขั้นนี้ generator จะไม่มีทางเห็นมันเลย**
5. เสร็จ — ไม่ต้องแก้โค้ด ไม่ต้องแตะซีน

### 5.2 RespondType หมายถึงอะไร (หลังแก้ระบบใหม่)

`RespondType` มีผลเฉพาะตอน **รายงานผิด** เท่านั้น (ตอนรายงานถูกทุกตัวหายเหมือนกันหมด + ได้แต้ม)

| RespondType | ตอบผิดแล้วเกิดอะไร |
|---|---|
| `DisappearInstantly` | หายไปเงียบๆ (ไม่ได้แต้ม) + สปอนตัวใหม่เป็นบทลงโทษ |
| `MoveToTargetThenDisappear` | เหมือนกัน (ระบบไล่ล่าถูกยกเลิกแล้ว) |
| `MoveOnly` **ที่ไม่มี moveTarget** | **ไม่เกิดอะไรเลย** — อยู่ต่อ ให้ลองรายงานใหม่ได้ (ปีศาจใช้ทางนี้) |

### 5.3 ปรับความยากคืนใดคืนหนึ่ง

เปิด `Assets/Settings/DifficultyProfile.asset` → กาง list `Nights` → หาแถวของคืนนั้น

| ช่อง | ผล | ค่า sentinel ที่แปลว่า "ไม่ override" |
|---|---|---|
| `Night Duration Minutes` | ยืด/หดคืน (เพดานจำนวน Anomaly) | `0` |
| `Threat Budget` | ซื้อ Anomaly ได้กี่ตัว | `0` |
| `Win Ratio` | ต้องเก็บกี่ % ถึงรอด | `0` |
| `Minimum Spacing Seconds` | Anomaly มาถี่แค่ไหน | `0` |
| `Glitch Count` | ฟอร์มโกหกกี่ครั้ง | `-1` (ใส่ `0` = ไม่มีเลย ซึ่งถูกต้อง) |
| `Penalty Anomalies Per Wrong Report` | ตอบผิด 1 ครั้ง โดนกี่ตัว | `-1` (ใส่ `0` = ตอบผิดฟรี) |

> ที่ต้องใช้ `-1` กับ 2 ช่องล่างเพราะ `0` เป็นค่าที่ *มีความหมายจริง* — ถ้าใช้ `0` เป็น sentinel
> จะแยกไม่ออกระหว่าง "ไม่ได้ตั้ง" กับ "ตั้งใจให้เป็นศูนย์"

### 5.4 เพิ่มคืนที่ 6, 7, ...

1. แก้ `NightResult.FinalNightIndex` (ตอนนี้ = 5) — ตัวนี้คุมทั้งเพดาน progression และข้อความจบเกม
2. เพิ่มแถวใหม่ใน `DifficultyProfile.nights`
3. ถ้าไม่เพิ่มแถว คืนใหม่จะตกไปใช้สูตรเชิงเส้นแทน (เล่นได้ แต่จะตันที่เพดานเวลาเหมือนเดิม)

### 5.5 ปรับ Haunt / Glitch

- **Haunt** (`Assets/Settings/HauntProfile.asset`): `loops` แต่ละแถวมี `weight` (น้ำหนักสุ่ม) และ `minNightIndex`
  ตอนนี้ตั้งไว้: SilenceProtocol + RadioCheck ปลดล็อกคืน 2, CameraBetrayal คืน 3, ImpostorCase คืน 4
  จำนวนต่อคืน = `baseHauntCount(1) + round(0.75 × (คืน-1))` เพดาน 4
- **Glitch** (`Assets/Settings/GlitchProfile.asset`): `weights` ว่าง = สุ่มทุกชนิดเท่ากัน,
  `intensity` = ตัวคูณความถี่ของ ambient glitch ทั้งคืน

### 5.6 ดีบัก

| วิธี | ได้อะไร |
|---|---|
| กด **F3** ตอนเล่น | HUD โชว์แผนคืนนี้ทั้งหมด + ปุ่มเล่นซ้ำ seed เดิม |
| ติ๊ก `Log Plan On Start` บน `NightPlanRunner` | พิมพ์ตารางคืนทั้งคืนลง Console ตอนเริ่ม |
| คลิกขวาที่ component `NightPlanRunner` → **Dump Night Plan** | พิมพ์แผนซ้ำได้ตลอดเวลา |
| ใส่เลขใน `Seed Override` | เล่นคืนเดิมเป๊ะๆ ซ้ำได้ |
| ติ๊ก `Show Debug Info` บน `AnomalyScheduler` | เห็น log ตอนสปอน รวมถึงบทลงโทษ (`PENALTY: ...`) |
| คลิกขวา `AnomalyScheduler` → **Force Spawn Next / All** | เร่งสปอนโดยไม่ต้องรอเวลา |

บรรทัดที่ควรดูใน Console ตอนเริ่มคืน:

```
=== Night 3 | seed 812345 | 6 min ===
  anomalies : 8 (threat cost 10)
  required  : 6 to survive
  wrong rpt : +1 anomaly/anomalies per failed report
  generator : accepted on first attempt
```

---

## 6. สิ่งที่เปลี่ยนไปจากการยกเลิกระบบไล่ล่า (ผลข้างเคียงที่ต้องรู้)

1. **ทางแพ้เหลือทางเดียว** — ตอนนี้แพ้ได้จาก *ปีศาจหมดเวลา* เท่านั้น (`KilledByDemon`)
   Anomaly ธรรมดาไม่ฆ่าผู้เล่นอีกแล้ว ความกดดันย้ายไปเป็น "ทำแต้มให้ทันก่อน 6 โมง"
   → นี่คือเหตุผลที่คืน 4-5 ต้องดัน `winRatio` ขึ้นเป็น 70-75% และเพิ่มโทษตอบผิดเป็น 2 ตัว
   ไม่งั้นจะไม่มีแรงกดดันอะไรเลย
2. **ระบบสวดมนต์กลายเป็นโค้ดที่ไม่ถูกเรียก** — `PrayUiManager`, การ match บทสวดใน `VoiceCommandRouter`,
   และ `Anomaly.OnPrayerSuccessful()` ยังคอมไพล์ผ่านแต่ไม่มีทางถูกเรียกถึง (ตามที่ตกลงว่าเก็บไว้ก่อน)
3. **`Anomaly.State` ไม่มีทางเป็น `Threatening` อีกแล้ว** — ไม่มีใครอ่านค่านี้อยู่ จึงไม่กระทบอะไร
4. **บั๊กที่แก้ไปด้วย:** ตอนแรกที่เปลี่ยนพฤติกรรม การรายงานผิดจะ **แจกแต้มให้ผู้เล่น**
   เพราะ `HandleDisappear()` ยิง event ที่ `ScoreManager` นับเป็นแต้มเสมอ
   แก้แล้วโดยเพิ่มพารามิเตอร์ `HandleDisappear(bool scores)` และเส้นทางตอบผิดส่ง `scores: false`

---

## 7. ไฟล์ที่ถูกแก้ในรอบนี้

**โค้ด**
- `Assets/Scripts/GameLogic/Anomaly.cs` — ยกเลิกไล่ล่า/jumpscare, เพิ่มบทลงโทษ, แก้บั๊กแจกแต้ม
- `Assets/Scripts/GameLogic/SpawnAndTime/AnomalyScheduler.cs` — singleton + `SpawnPenaltyAnomalies()`
- `Assets/Scripts/GameLogic/SpawnAndTime/NightTimer.cs` — `SetNightDuration()`
- `Assets/Scripts/GameLogic/Night/DifficultyProfile.cs` — คลาส `NightTuning` + ตัว resolve รายคืน
- `Assets/Scripts/GameLogic/Night/NightPlan.cs` — ฟิลด์ `penaltyAnomaliesPerWrongReport`
- `Assets/Scripts/GameLogic/Night/NightPlanGenerator.cs` — ใช้ค่ารายคืน
- `Assets/Scripts/GameLogic/Night/NightPlanValidator.cs` — ตรวจ spacing ด้วยค่ารายคืน
- `Assets/Scripts/GameLogic/Night/NightPlanRunner.cs` — ยัดความยาวคืนเข้า NightTimer

**ข้อมูล**
- `Assets/Settings/DifficultyProfile.asset` — ตารางจูน 5 คืน
- `Assets/Settings/HauntProfile.asset` — ปรับการปลดล็อก loop + อัตราเพิ่ม
- `Assets/Settings/Anomalies/*.asset` (8 ไฟล์) — ราคา, คืนที่ปลดล็อก, ปิด threat timeout
