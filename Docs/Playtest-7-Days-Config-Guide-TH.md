# คู่มือเทสยาว 7 วัน + ทุกค่าที่ตั้งได้ (core-gameplay)

> สถานะ ณ 2026-10-05 (branch `new-gameplay`, commit `b2e38f9`)
> ค่าที่เขียนในวงเล็บคือ "ค่าปัจจุบัน" - ถ้าแก้ในโปรเจกต์แล้ว ให้ถือค่าใน Inspector เป็นหลัก

---

## 1. ต้องทำก่อนเริ่มเล่น

- [ ] **เริ่ม New Game** - เซฟเดิมค้างอยู่วันที่ 6 (Field Manual ปลดล็อกครบ, cutscene เล่นไปแล้ว)
      ใน MainMenu: Start Menu → **New Game** จะล้างทุกอย่างกลับวันที่ 1
- [ ] **กด Play จากซีน MainMenu เท่านั้น** - `GameFlowManager` (ตัวนับวัน, จบวัน, ending) อยู่ใน MainMenu
      ถ้า Play จาก GamePlay ตรงๆ วันจะไม่เดิน
- [ ] **เซฟซีน `GamePlay.unity`** - มีการแก้ที่ยังไม่ได้ commit (เช่น call sign `Section 4`, สาย Mimic)
- [ ] **ไมค์** - ปรับเทียบแล้ว (noise floor 0.005) ถ้าเปลี่ยนห้อง/ไมค์ กด F12 → ปรับเทียบไมค์ใหม่
- [ ] **โมเดลเสียง** - ตอนนี้ English (tiny) สลับได้ใน F12

## 2. บั๊กที่แก้ไปแล้วระหว่างตรวจ

- สาย Mimic ("Who are you?", "Where are you?") ตั้ง `Answer All Of` ว่างไว้ → เดิม **ไม่มีวันนับว่าตอบ** สายหลอกจึงไม่เคยลงโทษ
- แก้แล้ว (`b2e38f9`): รายการว่าง = **ตอบอะไรก็ได้นับว่าตอบ** (ไม่นับแท็กเสียงรบกวนของ Whisper เช่น `[BLANK_AUDIO]`)

## 3. ข้อควรรู้ / ยังไม่แก้

| เรื่อง | ผลต่อการเล่น | วิธีแก้ถ้าต้องการ |
|---|---|---|
| ทุก RoomAnchor มี `Spawn Points` = 0 | anomaly ในห้องเดียวกันเกิดจุดเดียว → **ทับกัน** | เพิ่ม Transform ลง `Spawn Points` (ดูข้อ 5.2) |
| ฟอร์ม Incident Report ปิดอยู่ | Form Glitch (`Glitch Count` คืน 2-7) และ haunt **Impostor Case** (คืน 4+) ไม่มีผลบนจอ แต่ Impostor ยังกินโควตา haunt | ปิด `Enabled` ของ ImpostorCase ใน HauntProfile |
| Wrong ID hint เขียน `say: "{wrong}, copy"` | hint ชวนให้ตอบ ซึ่งการตอบ = โดนลงโทษ | ถ้าตั้งใจเป็นกับดัก ใช้ได้เลย |
| `Delay After Death` ในซีน MainMenu = 2.5 | ค่าในซีนชนะค่าในโค้ด (1.8) | แก้ที่ GameFlowManager ใน MainMenu |
| ไม่มีเสียง `RadioCall` / `RadioMissed` ใน Sound Library | สาย Radio Check เงียบ เห็นแค่การ์ด | เพิ่มชื่อนี้ใน `Assets/Resources/AudioManager` prefab |
| Cutscene มีวิดีโอจริงแค่ Training คืน 2 | ที่เหลือเป็นจอดำ + ข้อความ 3 วิ, Mail มี 1 ฉบับ | ใส่คลิปใน Story/Event assets |

---

## 4. ภาพรวม 7 คืน (ค่าปัจจุบัน)

| คืน | ยาว (นาทีจริง) | งบ threat | ต้องจัดการ | Demon timeout | เพดานล้นฉาก | ปลดล็อกใหม่ |
|---|---|---|---|---|---|---|
| 1 (สอน) | 4 | 4 | 50% | 45 วิ | 4 ตัว | Shadow Blob, Moved Furniture |
| 2 | 5 | 7 | 60% | 40 วิ | 4 | Hooded Figure (ต้องเงียบ), Extra Wardrobe |
| 3 | 6 | 10 | 65% | 35 วิ | 3 | Open Door, **Demon** |
| 4 | 7 | 14 | 70% | 32 วิ | 3 | Standing Figure |
| 5 | 8 | 18 | 75% | 30 วิ | 3 | Hanging Figure |
| 6 | 9 | 21 | 78% | 26 วิ | 3 | - |
| 7 (สุดท้าย) | 10 | 24 | 80% | 22 วิ | 3 | - |

- **ล้นฉาก:** เกินเพดานต่อเนื่อง **60 วิ** = แพ้ (Override ทับตารางทุกคืน)
- **Haunt:** คืน 1 ไม่มี, คืน 2+ Radio Check, คืน 3+ Camera Betrayal, คืน 4+ Impostor Case
- **Story:** opening ก่อนคืน 1 (ครั้งเดียว), เปลี่ยนองก์คืน 3 และ 6 (ครั้งเดียว), training คืน 2-5 (ทุกครั้งที่เริ่มคืนนั้น รวมตอน retry)
- **จบวัน:** overlay → สุ่ม Short VDO (โอกาสต่อวัน 0/50/50/60/60/70/100%, คลัง 4 คลิป แต่ละคลิปครั้งเดียว) → MainMenu
- **แพ้:** Demon jumpscare → Result (Play Again = เล่นวันเดิมซ้ำ)
- **รอดคืน 7:** เล่น ending แล้ว **ล้างเซฟ**

### เงื่อนไขแพ้ทั้งหมด
1. หมดคืนแต่จัดการไม่ถึง `Win Ratio`
2. Demon ไม่ถูกตะโกนไล่ภายใน `Demon Timeout Seconds`
3. ส่งเสียงดังกว่ากระซิบตอนเจอ Hooded Figure
4. anomaly ค้างเกิน `Max Concurrent` นาน 60 วิ (Overload)
5. anomaly ค้างจนหมด `Threat Timeout Seconds` ของตัวมันเอง (ถ้าตั้งไว้)

---

## 5. คู่มือการตั้งค่า

### 5.1 กำหนด anomaly แต่ละวัน

แต่ละคืน **สุ่มจาก seed** (NightPlanGenerator): ใช้งบ threat ซื้อ anomaly ที่ปลดล็อกแล้วแบบสุ่ม,
ไม่ซ้ำชนิดติดกัน, ตัวแพงสุดไว้ท้ายคืน, ห้องสุ่มจากถุงที่สับแล้ว
→ ไม่มีช่อง "คืน 3 ต้องมีตัว A" ตรงๆ ปรับทางอ้อมได้ดังนี้

| ที่อยู่ | ฟิลด์ | ผล |
|---|---|---|
| `Assets/Settings/Anomalies/Anomaly_*.asset` | `Min Night Index` | คืนแรกที่ตัวนั้นเริ่มออก |
| 〃 | `Threat Cost` | ราคา - แพงขึ้น = ออกน้อยลง |
| `Assets/Settings/DifficultyProfile.asset` → `Nights` | `Threat Budget` | งบรวมต่อคืน = จำนวนตัว |
| `Resources/NightContentLibrary` → `Anomalies` | (รายการ) | เอาออก = ตัวนั้นไม่ออกเลย |
| `NightPlanRunner` (GamePlay) | `Seed Override` | ล็อกให้คืนออกเหมือนเดิมทุกครั้ง (seed ดูจาก log `=== Night N \| seed ...`) |
| 〃 | `Night Index Override` | บังคับเล่นคืนที่กำหนด (0 = ตามเซฟ) |
| `AnomalyScheduler` (GamePlay) | `Source = Manual List` | ใช้ลิสต์เขียนเอง แต่ **ลิสต์เดียวทุกคืน** และไม่มีห้อง (ไม่ตรวจห้องตอนรายงาน) |

ค่าปัจจุบันของแต่ละตัว (`Allowed Rooms` ว่างทุกตัว):

| Anomaly | Observation | Cost | Min Night | หมายเหตุ |
|---|---|---|---|---|
| Shadow Blob | Shadow | 1 | 1 | |
| Moved Furniture | ObjectMoved | 1 | 1 | |
| Extra Wardrobe | ExtraObject | 1 | 2 | |
| Hooded Figure | Intruder | 2 | 2 | Silence |
| Open Door | Door | 2 | 3 | |
| Demon | Demon | 4 | 3 | Shout |
| Standing Figure | Intruder | 2 | 4 | |
| Hanging Figure | Intruder | 3 | 5 | |

> อยากล็อกแบบ "คืน X มีตัว A, B ที่นาทีนี้" ต้องเพิ่มฟีเจอร์ใหม่ (ยังไม่มี)

### 5.2 จุดเกิดและเวลาเกิด

**ห้อง/จุด**
- `RoomAnchor` ใน GamePlay → `Spawn Points`: ลาก Transform ใส่ได้หลายจุด ระบบสุ่มตาม seed (ตอนนี้ว่างทุกห้อง)
  - Hallway กล้อง X 0, Bedroom 17.73, Kitchen 36.12
- `Allowed Rooms` ใน AnomalyDefinition: จำกัดห้องต่อชนิด (ว่าง = ทุกห้อง)
- `Assets/Settings/Rooms/Room_*.asset`
  - `Display Name`: ชื่อที่ผู้เล่นต้องพูดตอนรายงาน
  - `Camera X`: ตำแหน่งกล้องของห้อง

**เวลา (DifficultyProfile)**

| ฟิลด์ | ค่า | ความหมาย |
|---|---|---|
| `Night Duration Minutes` | 4 → 10 | ความยาวคืน (นาทีจริง) |
| `First Spawn Minute` | 0.25 | ก่อนนาทีนี้ยังไม่เกิด |
| `Last Spawn Fraction` | 0.9 | หลังสัดส่วนนี้ของคืนไม่เกิดเพิ่ม |
| `Minimum Spacing Seconds` | 35 → 16 | ระยะห่างขั้นต่ำระหว่างตัว |
| `Onboarding Quiet Fraction` | 0.2 | ช่วงต้นคืนที่เบาลง |
| `Climax Fraction` | 0.25 | ช่วงท้ายคืนที่ตัวแพงสุดต้องเกิด |
| `Handle Cost Seconds` | 10 | เวลาที่คาดว่าผู้เล่นใช้จัดการ 1 ตัว (ใช้คำนวณความหนาแน่น) |

### 5.3 ค่าในแต่ละ AnomalyDefinition

| ฟิลด์ | ความหมาย |
|---|---|
| `Observation` | หมวดคำที่ใช้รายงาน (Shadow, Door, Intruder, ...) |
| `Correct Keywords` | คำเสริมเฉพาะตัว (นอกจากคำในหมวด) |
| `Voice Response` | None = ระดับไหนก็ได้ / Shout = ต้องตะโกน / Silence = stealth (หาด้วยเมาส์ก่อน แล้วกระซิบหรือเงียบ) |
| `Respond Type`, `Move Speed` | พฤติกรรมตอนถูกรายงานผิด/หมดเวลา |
| `Threat Timeout Seconds` | ค้างนานเท่านี้ = แพ้ (0 = ไม่มี) |
| `Display Name`, `Manual Image`, `How To Spot` | เนื้อหาใน Field Manual |

### 5.4 แพ้/ชนะ (DifficultyProfile → `Nights`)

| ฟิลด์ | ค่า (คืน 1 → 7) |
|---|---|
| `Win Ratio` | 0.5 → 0.8 |
| `Penalty Anomalies Per Wrong Report` | 0/1/1/2/2/2/3 (ใช้ทั้งรายงานผิดและ Radio Check พลาด) |
| `Demon Timeout Seconds` | 45 → 22 |
| `Max Concurrent Anomalies` | 4/4/3/3/3/3/3 |
| `Overload Duration Seconds` | 180 → 80 แต่ถูกทับด้วย `AnomalyOverloadWatcher → Overload Duration Override` = 60 (ใส่ 0 = ใช้ค่าตามคืน) |
| `Glitch Count` | 0 → 12 (ไม่มีผลตอนฟอร์มปิด) |

GameFlowManager (ซีน MainMenu): `Delay After Death` 2.5, `Delay After Demon Death` 0.8, `Final Day` 7

### 5.5 การหลอกผู้เล่น

**Radio Check** (`RadioCheckHaunt` ใน GamePlay)

| ฟิลด์ | ค่า | ความหมาย |
|---|---|---|
| `Radio Id` | Section 4 | call sign ของผู้เล่น |
| `Wrong Ids` | Section 1/2/3 | call sign ปลอมของสาย Wrong ID |
| `Normal Scripts` | "Section 4, radio check." → ตอบ `[Section 4, copy]` | สายจริง ต้องตอบ (Own Voice ก็ใช้ชุดนี้) |
| `Wrong Id Scripts` | "{wrong}, radio check." → `[copy]` | สายหลอก ตอบ = พลาด |
| `Mimic Scripts` | "Who are you?", "Where are you?" → ว่าง | สายหลอก ตอบอะไรก็ = พลาด |
| `Response Window Seconds` | 8 | เวลาตอบ |
| `Outcome Display Seconds` | 2.5 | ผล PASS/FAIL ค้างบนจอ |
| `Echo Grace Seconds` | 0.4 | ไม่ฟังเสียงช่วงนี้หลังเสียงสายจบ (กันไมค์รับเสียงสายตัวเอง) |
| น้ำหนัก Normal / Own Voice / Wrong Id / Mimic | 3 / 1.5 / 1.5 / 1 | โอกาสแต่ละแบบ |
| `Mimic Min Night` | 3 | Mimic เริ่มคืนนี้ |
| `Decoy Intensity Floor` | 1.25 | ระดับ glitch หลังโดนหลอก |

- ตัวแปรในข้อความ: `{id}` = Radio Id, `{wrong}` = Wrong Id ที่สุ่มได้
- `Answer All Of`: **ทุกรายการ** ต้องได้ยิน, `{id}` = call sign (รับ "four"/"4", "zero"/"o"/"oh") / **ว่าง = ตอบอะไรก็นับ**
- Own Voice / Mimic เล่นเสียงผู้เล่นที่อัดไว้จากการตอบสายจริงครั้งก่อน
- พลาด (ไม่ตอบสายจริง หรือ ตอบสายหลอก) → เพิ่ม anomaly ตาม `Penalty Anomalies`

**Haunt ทั้งหมด** (`Assets/Settings/HauntProfile.asset`)
- แต่ละ loop: `Enabled`, `Weight`, `Min Night Index`
  - RadioCheck: คืน 2+, weight 1.5
  - CameraBetrayal: คืน 3+, weight 1 (รายละเอียดอยู่ที่ `Variants` บน CameraBetrayalHaunt)
  - ImpostorCase: คืน 4+, weight 0.8 (ไม่มีผลตอนฟอร์มปิด)
- จำนวนต่อคืน: `Base Haunt Count` 1 + `Growth` 0.75 ต่อคืน, สูงสุด `Max` 4

### 5.6 Hooded Figure (เงียบ) และ Demon (ตะโกน)

**QuietResponse**

| ฟิลด์ | ค่า | ความหมาย |
|---|---|---|
| `Quiet Seconds` | 8 | เงียบครบเท่านี้ มันหายไป |
| `Tolerance Seconds` | 0.3 | ดังเกินกระซิบนานเท่านี้ = แพ้ |
| `Grace Seconds` | 1 | ผ่อนผันหลังไมค์ถูกบังคับเปิด |

- เริ่มเมื่อเมาส์ชี้โดนตัวมันเท่านั้น → ไมค์ถูกบังคับเปิด → กระซิบรายงาน หรือเงียบจนครบ

**NoiseMeter** (สไลเดอร์ความดัง)

| ฟิลด์ | ค่า | ความหมาย |
|---|---|---|
| `Whisper Band Multiplier` | 3 | เกิน noise floor × 3 = ไม่ใช่กระซิบแล้ว |
| `Shout Band Multiplier` | 8 | เกิน noise floor × 8 = ตะโกน |
| `Slider Max Multiplier` | 16 | ปลายสไลเดอร์ |

**DemonAnomaly prefab** (`Assets/Prefabs/DemonAnomaly.prefab`)
- `Reveal Distance`: กล้องใกล้แค่ไหน Demon ถึงเปิดตัว
- `Jumpscare Video`: ใส่แล้วใช้ทั้งตอนเปิดตัวและฉากแพ้
- `Glitch Intensity While Active`

### 5.7 การตรวจคำที่ผ่อนปรน (รวมทุกจุด)

**`Assets/Resources/ObservationVocabulary.asset`** - ค่ายิ่งต่ำยิ่งผ่อน, 1 = ต้องตรงเป๊ะ

| ฟิลด์ | ค่า | ใช้กับ |
|---|---|---|
| `Word Similarity` | 0.65 | คำ anomaly (shadow, door, ...) |
| `Room Similarity` | 0.6 | ชื่อห้อง |
| `Phrase Similarity` | 0.65 | คำตอบ Radio Check |
| `Use Sound Alike` | เปิด | นับคำที่ตัวแรกและโครงพยัญชนะเหมือนกัน เช่น "kichen" = kitchen |
| `Min Fuzzy Word Length` | 4 | คำสั้นกว่านี้ต้องตรงเป๊ะ (กัน "do" กลายเป็น "door") |
| `Entries` | - | แต่ละหมวดเพิ่มคำ/คำที่ Whisper ฟังพลาดบ่อยได้ เช่น ใส่ "chadow" ในหมวด Shadow |

**จุดอื่นที่เกี่ยว**
- `Correct Keywords` ใน AnomalyDefinition: คำเสริมเฉพาะตัว
- `Display Name` ในไฟล์ห้อง: ชื่อห้องพูดยาก → เปลี่ยนชื่อได้
- `IncidentReportManager` → `Partial Report Memory Seconds` (20): จำครึ่งรายงาน (พูดแค่ห้อง หรือแค่ตัว) ไว้นานเท่าไร
- การรายงานทางวิทยุ **ตรวจห้องเสมอ** (`Require Correct Location` บน IncidentReportManager มีผลแค่กับฟอร์มเก่าที่ปิดอยู่)
- `WhisperMicInput`
  - `English Model` / `Thai Model`: โมเดลใหญ่ = แม่นขึ้นแต่ช้าลง
  - `Window Seconds` (6), `Hop Sec` (0.8), `Dispatch Cooldown Sec` (0.25)
- `GlobalPushToTalk`
  - `Finalize Grace Seconds` (1): รอประโยคสุดท้ายหลังปล่อย V
  - `Forced File Gap Seconds` (1.2): ตอนไมค์ถูกบังคับเปิด เงียบนานเท่านี้ = ส่งรายงาน

### 5.8 ข้อความ, Field Manual, เสียง

- `Assets/Resources/PlayerMessages.asset`: ทุกข้อความที่ขึ้นจอ แก้คำและติ๊ก `Blink` ได้ (`{0}` `{1}` = ค่าที่เกมเติม)
- `FieldManualWindow`: `Use Locked Entries` (ปลดล็อกเมื่อเจอตัวนั้นครั้งแรก) และลำดับ `Entries`
- `Assets/Resources/AudioManager` prefab → `Sound Library`: JumpScare, MicOpenAndClose, MicHold, RadioCall/RadioMissed (ยังไม่มี)
- PauseMenu (ESC): ปรับ Master / Music / SFX ระหว่างเล่น

---

## 6. เครื่องมือทดสอบ

**Debug panel (F12)**
- สร้าง anomaly ทีละชนิด (รวม Demon, Hooded Figure)
- เรียก haunt / สาย Radio Check แต่ละแบบ (Normal, Own Voice, Wrong Id, Mimic) ทันที
- พิมพ์ข้อความแทนเสียงพูด (ทดสอบรายงานโดยไม่ใช้ไมค์)
- ปรับเทียบไมค์, สลับโมเดลเสียง, ล็อก/ปลดล็อก Field Manual
- QA checklist 10 ข้อ

**GameFlowManager → `Debug/Skip Night`**: ข้ามคืนพร้อมกำหนดคะแนน, mail และ event จบวัน

**ทางลัดเทสเร็ว**
- ลด `Night Duration Minutes` ในตารางคืน (นาทีเกิดของ anomaly ย่อตามอัตโนมัติ)
- `Night Index Override` ข้ามไปคืนที่อยากเทส
- `Seed Override` เล่นคืนเดิมซ้ำเพื่อเทียบผล
