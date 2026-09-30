# Voice Gameplay Redesign — "พูดทุกครั้งมีราคา"

> แผนเปลี่ยนบทบาทของไมค์จาก "ปุ่มตอบชื่อ anomaly" เป็นช่องทางที่ทั้งจำเป็นและอันตราย
> สถานะ: **Phase 1 กำลังทำ** (Observation Report + Noise Meter) — Phase 2-4 ยังเป็นแผน

---

## 1. ปัญหาของระบบเดิม

| ปัญหา | ผลต่อเกม |
|---|---|
| ผู้เล่นต้องพูด "ชื่อ" anomaly (`correctKeywords`) | ต้องรู้ชื่อที่เกมตั้งไว้ ไม่ได้ใช้การสังเกต |
| anomaly ทั้ง 7 ตัวชื่อ `"Shadow"` เหมือนกันหมด | ไม่มีความต่าง แยกไม่ออก |
| ชื่อที่แต่งขึ้นเอง Whisper จับไม่แม่น | รายงานพลาดเพราะ speech-to-text ไม่ใช่เพราะผู้เล่นผิด |
| พูดแล้วไม่มีผลเสีย | ไมค์ไม่มีความตึงเครียด เป็นแค่ปุ่มส่งคำตอบ |
| ไมค์เปิดได้เฉพาะในฟอร์ม Incident Report | Radio Check (`VoicePromptSystem.Expect`) ฟังคำตอบได้เฉพาะตอนฟอร์มเปิดอยู่และกำลังอัด |

---

## 2. แกนกลาง: Noise Meter ⭐ (Phase 1)

มาตรวัด 0-100 ตัวเดียวทั้งคืน

- **พูดแล้วค่าขึ้น** — คิดจากความดังจริงของไมค์ (RMS เทียบกับ noise floor ที่ calibrate ไว้)
  - กระซิบ → ขึ้นช้า (×0.3)
  - พูดปกติ → ขึ้นตามอัตราฐาน
  - ตะโกน → ขึ้นเร็ว (×2.5)
- **เงียบแล้วค่าลด** — หลังเงียบไปสักพัก (drain delay) ค่อยๆ ลดลง
- **เต็ม 100 → Listener มา** — เรียก Silence Protocol (HL-3) ทันทีผ่าน `HauntDirector.TriggerNow`
  แล้วค่าตกลงมาเหลือราว 35 และมี cooldown กันเรียกซ้ำติดๆ
- **คืนที่ 1 (tutorial)** — `HauntDirector` ข้าม haunt ทุกตัวในคืนแรกอยู่แล้ว มาตรยังขึ้นให้เห็นและเตือนว่า "มันเกือบได้ยินแล้ว"
  แต่ยังไม่มี Listener มา ผู้เล่นได้เรียนกลไกก่อนโดนจริง
- ระหว่าง Silence Protocol ทำงาน มาตรหยุดขึ้น เพราะ encounter นั้นลงโทษเสียงดังเองอยู่แล้ว

ผลที่ต้องการ: ผู้เล่นต้องคิดก่อนพูดทุกครั้ง พูดสั้นๆ พูดเบาๆ รายงานให้ถูกตั้งแต่ครั้งแรก
(ทุกรายงานที่ผิดต้องพูดใหม่ ซึ่งแปลว่าเสียงเพิ่มขึ้นอีก)

### ค่าที่ปรับได้ใน Inspector (`NoiseMeter` บน GameObject `NoiseMeter` ในซีน GamePlay)
`speechNoisePerSecond`, `whisperCostMultiplier`, `shoutCostMultiplier`, `whisperBandMultiplier`,
`shoutBandMultiplier`, `drainPerSecond`, `drainDelaySeconds`, `triggerThreshold`, `levelAfterTrigger`, `triggerCooldownSeconds`

---

## 3. ไอเดียแทนการพูดชื่อ Anomaly

### #1 Observation Report — รายงานสิ่งที่เห็น ⭐ (Phase 1)

ผู้เล่นไม่ต้องรู้ชื่อ แต่ต้องบอกว่า **อะไรเปลี่ยนไป** ในห้องไหน
- ห้อง → dropdown LOCATION เดิม
- สิ่งที่เปลี่ยน → พูดใส่ไมค์ เช่น *"person"*, *"door open"*, *"chair moved"*

**ข้อมูล**
- `ObservationType` (enum) — หมวดของสิ่งที่เปลี่ยน:
  `Intruder`, `Shadow`, `ObjectMoved`, `ExtraObject`, `MissingObject`, `Door`, `Light`, `Picture`, `Demon`
- `ObservationVocabulary` (ScriptableObject, `Resources/ObservationVocabulary.asset`) — คำพูดที่ยอมรับในแต่ละหมวด
  แก้ได้ที่ asset เดียว ไม่ต้องไล่แก้ทีละ anomaly
- `AnomalyDefinition.observation` — anomaly แต่ละชนิดบอกแค่ว่ามันอยู่หมวดไหน
- `AnomalyDefinition.correctKeywords` — ยังอยู่ แต่เปลี่ยนเป็น "คำเสริม" ที่รับเพิ่ม (ไม่บังคับกรอกแล้ว)

**การตรวจรายงาน (`IncidentReportManager.SubmitReport`)**
1. แปลงคำที่พูดเป็นชุด `ObservationType` ที่ถูกพูดถึง
2. หา anomaly ที่ยัง active **ทุกตัว** (ไม่ใช่แค่ตัวแรกเหมือนเดิม) ที่หมวดตรงกับที่พูด
   และถ้าเปิด `requireCorrectLocation` ต้องอยู่ในห้องที่เลือกด้วย
3. ตัวที่ฟอร์มเปิดมาให้ (ถ้าตรงเงื่อนไข) ได้สิทธิ์ก่อน ไม่งั้นเอาตัวแรกที่ตรง
4. ตรง → banish ตัวนั้น + ได้คะแนน / ไม่ตรง → รายงานผิดเหมือนเดิม (`Respond()` + penalty)

**Matcher ใหม่เข้มกว่า `PhraseMatcher`** — คำสั้น (< 4 ตัวอักษร) ต้องตรงเป๊ะ
เพราะ `PhraseMatcher.WordsMatch` ใช้ `Contains` สองทาง ทำให้คำอย่าง `"a"`, `"i"` ไป match ได้แทบทุกคำ
(ดูข้อ 7 ความเสี่ยง)

**หมวดของ anomaly ที่มีตอนนี้** (ดูจาก sprite จริง — แก้ได้ใน Inspector):

| Prefab | ภาพ | observation |
|---|---|---|
| Anomaly1Res2 | ร่างคลุมผ้า เอนตัว | Intruder |
| Anomaly2Res1 | ก้อนเงาดำ | Shadow |
| Anomaly3Res1 | เบาะ/ของตกแต่งผิดที่ | ObjectMoved |
| Anomaly4Res2 | ประตูเปิดมีร่างยืนอยู่ | Door (+คำเสริม person/figure) |
| Anomaly5Res1 | บานประตู/ตู้สีดำที่ไม่ควรมี | ExtraObject |
| Anomaly6Res2 | คนยืนทั้งตัว | Intruder |
| Anomaly7Res2 | ร่างห้อยจากเพดาน | Intruder (+คำเสริม hanging/ceiling) |
| DemonAnomaly | jumpscare เต็มจอ | Demon |

> **แนะนำ:** เปิด `requireCorrectLocation` บน `IncidentReportManager` ควบคู่กันไป เพื่อให้ "ห้องไหน + อะไรเปลี่ยน"
> เป็นรายงานเต็มรูปแบบ (เจเป็นคนตัดสินใจเปิดเอง — Phase 1 ไม่ได้แตะค่านี้)

### #2 ตอบด้วยระดับเสียง (Phase 3)
แต่ละ anomaly ต้องรับมือด้วยเสียงคนละแบบ — กระซิบ / ตะโกน *"GET OUT"* / เงียบสนิท
ใช้ช่วง whisper/danger เดียวกับ Silence Protocol และ Noise Meter ต่อยอดได้ทันที
ต้องทำ: `AnomalyDefinition.voiceResponse` (Whisper/Shout/Silence) + UI บอกว่ากำลังรับมือแบบไหน

### #3 "Give me a sign" เป็นเครื่องมือสืบสวน (Phase 4)
พูด *"Give me a sign"* → ผีตอบด้วยไฟกะพริบ/เสียงเคาะ/กล้องกระตุก ชี้ห้องที่มีของจริง หรือบอกว่าตัวไหนเป็นตัวหลอก
ทุกครั้งที่ถาม → noise +20 และ glitch intensity ขึ้น
ต้องทำ: ต่อ `SignRequestSystem` เข้ากับ `GlitchDirector` + `NoiseMeter.AddNoise`

### #4 เสียงที่ไว้ใจไม่ได้ (Phase 3)
ขยาย Radio Check variant `OwnVoice` / `WrongId` — ตัวที่เลียนเสียง HQ สั่งให้พูด *"Confirm all clear"*
ถ้าตอบ = เชิญเข้ามา ผู้เล่นต้องเช็คกล้องก่อนตอบ

---

## 3b. Field Manual (S-306/307) ✅

คู่มือที่ผู้เล่นเปิดดูได้ตลอดระหว่างเล่น แก้ปัญหา "รู้ว่าต้องรายงานสิ่งที่เห็น แต่ไม่รู้จะพูดคำไหน"
โดยไม่ต้องพึ่งการจำจากคัทซีนอย่างเดียว

- `FieldManualController.cs` — กด **TAB** เปิด/ปิด ไม่แตะ `Time.timeScale` เลย (ภัยยังมาได้ระหว่างเปิดอ่าน ตรงตามสเปค S-306)
  ปิดอัตโนมัติถ้า Incident Report เปิดขึ้นมา / คัทซีนกำลังเล่น / Demon reveal / เกม pause อยู่แล้ว
- `FieldManualHud.cs` — runtime-built, ไม่ต้อง wire ใน Editor, 8 แท็บแนวตั้งฝั่งซ้าย (คลิกเลือกได้ตรงๆ ไม่ใช่ Prev/Next) เรียงตาม `minNightIndex`
  แต่ละหน้าโชว์: รูป (ใช้ sprite เดิมของ anomaly นั้น ไม่ต้องวาดใหม่), "HOW TO SPOT IT" (`howToSpot`),
  และ "SAY, e.g.:" ดึงตัวอย่างคำจาก `ObservationVocabulary.SamplePhrases` ของหมวดนั้นโดยตรง — คำในคู่มือกับคำที่เกมยอมรับจริงเป็นชุดเดียวกัน ไม่มีทางไม่ตรงกัน
- เนื้อหาคู่มือกรอกครบทั้ง 8 (7 anomaly + Demon) แล้วในสคริปต์นี้ ไม่ใช่แค่โครง UI เปล่า
- **ธีม Windows XP** — ใช้สี/ฟอนต์เดียวกับ MainMenu (`Docs/MainMenu-XP-Desktop.md`) เป๊ะ ผ่าน 2 ไฟล์ใหม่ที่ตั้งใจให้ UI
  gameplay-scene อื่นๆ ต่อยอดได้ (ไม่ผูกกับ `DesktopManager` แบบ `XPWindowController` เดิมที่ใช้ได้แค่ใน MainMenu):
  - `XPTheme.cs` (SO, `Resources/UI/XPTheme.asset`) — สี titlebar/body/border/ปุ่มปิด/แท็บ + ฟอนต์ Tahoma SDF จุดเดียว
  - `XPWindowBuilder.cs` — builder runtime: titlebar gradient + ปุ่มปิดแดง + แท็บแนวตั้ง ให้หน้าต่างใหม่เรียกใช้แทนเขียนเอง

## 3c. CCTV overlay + rollout ไปยัง UI อื่นในซีน Gameplay ✅

ทุกหน้าต่างในซีน Gameplay คือ "ฟีดที่ขึ้นจอมอนิเตอร์ SEC-04" ไม่ใช่หน้าต่าง OS จริง — ธีม XP เดิมใส่ `cctv: true` เพิ่ม:
- Scanline overlay (ลายเส้นแนวนอนจางๆ, generate texture 1×2px รันไทม์ ไม่ต้องมี asset ใหม่)
- REC dot สีแดงกระพริบที่ titlebar (เฉพาะ `XPWindowBuilder.Build(..., cctv: true)`)
- Tint จางๆ ทับสีเขียว-ขาวแบบจอ CRT

**หมายเหตุ:** post-process filter จริงของกล้อง (Global Volume) render บนภาพ 3D เท่านั้น ไม่แตะ Canvas
ScreenSpaceOverlay เลย (คนละ pass) เลยต้อง "ปลอม" ลุค CCTV ที่ตัว UI เองแทน ไม่ใช่พึ่ง Volume

**สิ่งที่รีดีไซน์แล้ว (ตรวจสด ผ่านทุกจุด):**
- **Field Manual** — เต็มรูปแบบ (ดูหัวข้อ 3b) titlebar+แท็บ+scanline+REC dot
- **Pause Menu** (`PauseMenuController.cs`) — เขียนใหม่ทั้ง `BuildUi()` ด้วย `XPWindowBuilder` แทนโค้ด panel มือเดิม
  titlebar "System Paused.exe", ปุ่ม X = Resume, Master/Music slider คงการทำงานเดิมทุกอย่าง (ทดสอบ nudge +/- แล้วค่าเปลี่ยนจริง)
- **Silence Protocol HUD** (`SilenceProtocolHud.cs`) — เปลี่ยนฟอนต์เป็น Tahoma + เพิ่ม scanline overlay
  (ไม่ใส่ titlebar/ปุ่มปิด เพราะเป็น full-screen encounter ที่ผู้เล่นปิดเองไม่ได้ ไม่ใช่ "หน้าต่าง")
- **Noise Meter HUD** (`NoiseMeterHud.cs`) — เปลี่ยนฟอนต์เป็น Tahoma (เป็นแค่แถบมุมจอ ไม่ใส่ titlebar)
- **Incident Report Window** — retrofit แบบ additive เท่านั้น (prefab เดิมเสี่ยงเกินจะรื้อทั้งยวง):
  `IncidentReportUI.Awake()` กวาด TMP label ทั้งหมด (28 ตัว) ไปใช้ Tahoma + วาง scanline overlay ทับ
  สีเดิมในพรีแฟบ (`#ECE9D8` ฯลฯ) ตรงกับ `XPTheme` อยู่แล้วก่อนหน้านี้ เลยไม่ต้องแก้สี ทดสอบยื่นรายงานจริงหลังแก้แล้วยังทำงานถูกต้อง

**ยังไม่แตะ:** MainMenu (เป็นต้นฉบับของธีมอยู่แล้ว), `XPWindowController`/`DesktopManager` (ใช้ได้เฉพาะ MainMenu โดยดีไซน์)

## 3d. Layout redesign — อิง MailWindow ✅

ดูตัวอย่างจริงจาก `MailWindow.cs`/`.prefab` (Mail), `TextContentWindow.cs` (READ ME.txt / Recycle Bin) และ
Incident Report Window แล้วปรับ Field Manual ให้ไปทางเดียวกัน — ที่ใกล้เคียงที่สุดคือ MailWindow
(list ซ้าย + reading pane ขวา คือโครงเดียวกับ Field Manual เป๊ะ) เลยดึงค่าจริงจาก component นั้นมาใช้ตรงๆ
แทนที่จะคิดสีเอง:

- **แถบเลือก (list row)** — เปลี่ยนจากปุ่มแท็บสีน้ำตาล/เทาเดิม เป็นสไตล์ inbox ของ Mail จริง:
  เลือกอยู่ = ฟ้า `#316AC5 @35%` + ตัวหนังสือขาวตัวหนา, ไม่ได้เลือก = โปร่งใส + ตัวหนังสือดำปกติ
  (`XPTheme.listSelected/listNormal/listSelectedText/listNormalText`)
- **เส้นแบ่ง (Divider)** — เพิ่มเส้นแนวตั้งคั่นระหว่างคอลัมน์ list กับ reading pane สีเดียวกับ Divider จริงใน
  MailWindow prefab (`#D5D8C4`) — ของเดิมไม่มีเส้นนี้เลย
  ความกว้างคอลัมน์ list ปรับเป็น 220px ให้เท่า MailWindow's inbox column
- **Reading pane** จัดโครงใหม่ให้มีจังหวะแบบ subject/sender/date ของอีเมลจริง: Title (หัวเรื่องตัวหนา)
  → เส้นแบ่งบางๆ ใต้หัวเรื่อง → "REPORT AS: หมวด" (บรรทัด meta สีฟ้า accent เหมือนวันที่/ผู้ส่ง) → label
  "HOW TO SPOT IT" → เนื้อหา
- **"SAY, e.g.:"** ย้ายจากข้อความเปล่าในคอลัมน์แคบ เป็น **callout เต็มความกว้าง** ใต้รูปภาพ — กล่องพื้นหลัง
  สีฟ้าอ่อนจางๆ ทับ (`accentText @8%`) ให้คำที่ต้องพูดเด่นแยกจากส่วนอื่นชัดเจน ไม่ต้องแย่งพื้นที่กับข้อความ
  บรรยายในคอลัมน์แคบเหมือนก่อน

ทดสอบสดยืนยันสี/ตำแหน่งตรงตาม MailWindow เป๊ะ (`RGBA(0.192, 0.416, 0.773, 0.35)` ตรงกับ `#316AC5@35%`),
คลิกสลับหน้าแล้ว highlight ย้ายถูกต้อง ไม่มี error

## 3e. Rebuild เป็น Prefab จริง (ตาม spec ของเจ) ✅

เจส่ง spec ละเอียดมาให้ทำ Field Manual ใหม่เป็น **Prefab จริง** (ไม่ใช่ runtime-built C#) วางลงใน Canvas
เดียวกับ UI อื่นของ Gameplay scene — รื้อของเดิม (`FieldManualHud.cs` runtime-built) ทิ้งทั้งหมด แล้วสร้างใหม่ตาม spec

**Already existed (reuse, ไม่สร้างซ้ำ):**
- Data: `AnomalyDefinition.cs` มีครบทุกฟิลด์ที่ spec ขอ (`displayName`, `observation`→reportAs, `manualImage`→previewSprite,
  `howToSpot`→spotDescription, `correctKeywords`) — ไม่สร้าง `AnomalyEntryData` ใหม่ตามที่ spec อนุญาตให้ reuse
- คำที่ใช้ตรวจ report (`ObservationVocabulary.SamplePhrases` + `correctKeywords`) คือ**แหล่งเดียว**ที่ทั้ง
  `IncidentReportManager` และ Field Manual อ่าน — ไม่มีชุดคำซ้ำ ตรงตาม hard requirement
- Tahoma SDF font, `XPPalette`/`UIGradient` (ของเดิมจาก MainMenu theme)
- `MainMenu.XPWindowController` — Field Manual **extends** คลาสนี้ตรงๆ (เหมือน `MailWindow`/`TextContentWindow`)
  แทนที่จะสร้าง window manager ใหม่ — ใน MainMenu ใช้กับ `DesktopManager` ได้ปกติ, ใน Gameplay scene ไม่มี
  Desktop ผูกอยู่ก็ใช้ Show()/Hide() ตรงๆ ได้เลย (โค้ด `Desktop?.` null-check ไว้อยู่แล้ว)
- `HelpItem` ใน MenuBar ของ Incident Report window มีอยู่แล้ว (แค่ text เฉยๆ ยังไม่มี Button)

**Added / changed:**
- `FieldManualUI.cs` (extends XPWindowController) — sidebar 8 แถว + reading pane, `Open()`/`Open(AnomalyDefinition)`/
  `Close()`/`ToggleOpen()`, ปุ่ม Up/Down เปลี่ยนแถว, จำ index ที่เลือกไว้ตลอด session (ไม่เซฟลงดิสก์)
- `FieldManualEntryButton.cs` — 1 แถวใน sidebar, 3 สถานะ (normal/hover/selected) สีตรงตาม spec เป๊ะ
- `FieldManualController.cs` — ปรับให้เรียก `FieldManualUI.ToggleOpen()` แทนที่จะสร้าง HUD เอง (ของเดิมจาก session ก่อนยังใช้ได้เกือบทั้งหมด)
- Prefab: `Assets/Prefabs/Gameplay/FieldManualEntryButton.prefab`, `FieldManualWindow.prefab` — ตาม layout
  spec เป๊ะ (560×367, titlebar 26px gradient `#1A5FB0→#0A3C8A`, sidebar 130px, preview 148×148, SayBox `#E2DFCE`)
- วางอินสแตนซ์ไว้ใน **`GamePlay.unity` > `=== UI === > Canvas`** (Canvas เดียวกับ `IncidentReportWindow`) ตามที่เจสั่ง

**ทดสอบสดครบ:** เปิด/ปิด, สลับแถว (สีตรง `#C5D5EE`/`#316AC5@35%` เป๊ะ), `Time.timeScale` ไม่ขยับตอนเปิด,
**เปิด Field Manual ขณะฟอร์ม Incident Report เปิดค้างอยู่ (มีคำที่พูดไปแล้ว) ไม่กระทบฟอร์มเลย** ปิด manual แล้ว submit
report ต่อได้ปกติ (ยืนยัน hard requirement "no UI action may penalize correct player input")

**Assumptions ที่ต้องแฟลกให้เจรู้:**
1. **Min/Max ปุ่ม** — มีแค่ภาพ ไม่มีพฤติกรรมจริง เพราะ `XPWindowController` (base class ที่ reuse) รองรับแค่ Close
   ถ้าต้องการ minimize/maximize จริงต้องขยาย base class ก่อน (กระทบ MailWindow/TextContentWindow ด้วย)
2. **`useLockedEntries`** — ปิดไว้ (false) และ `IsDiscovered()` return true เสมอ (stub) เพราะ `SaveData` ยังไม่มี
   field เก็บว่า anomaly ไหนเคยเจอมาก่อน — ตรงตาม spec ที่บอกให้ stub ถ้ายังไม่มี save record
3. **Desktop icon "Field Manual.exe" บน MainMenu + Help item เปิด Field Manual** — ✅ ทำแล้ว:
   `DesktopAction.OpenFieldManual` + ช่อง `fieldManualWindowPrefab` ใน `DesktopManager`, ไอคอนใหม่ `Icon_FieldManual`
   (ใช้รูปเดียวกับ Mail ชั่วคราว ต้องเปลี่ยนเป็นรูปจริง), `HelpItem` ใน Incident Report มี `OpenFieldManualOnClick`
   (ใน `GamePlay.unity` window ถูก unpack จาก prefab แล้ว จึงต้องแก้ใน scene ตรงๆ ไม่ใช่ที่ prefab)
   และแก้ `windowWidth` ใน prefab เป็น 560 (ค่าเดิม 275 ทำให้หน้าต่างบีบเมื่อสร้างจาก MainMenu)
   **เปลี่ยนพฤติกรรม:** Field Manual ไม่ถูกบล็อก/ปิดเองตอนฟอร์ม Incident Report เปิดอยู่แล้ว (เพื่อให้เปิดอ่านระหว่างเขียน report ได้)
4. **XP scrollbar** — ไม่มี asset "XP scrollbar" สำเร็จรูปในโปรเจกต์ให้ reuse ใช้ `ScrollRect` เปล่าๆ (ลาก mouse
   wheel ได้) ยังไม่มี Scrollbar handle ที่เห็นเป็นแท่งเลื่อน (8 แถว×24px=192px ยังพอดีกับความสูง sidebar 341px
   อยู่แล้วในตอนนี้ เลยไม่จำเป็นต้องสกอลล์จริงๆ)

## 3f. Bug หลัง rebuild เป็น Prefab — "เพี้ยนมากตอนกด Play" ✅

เจรายงานว่าหลัง rebuild เป็น prefab (3e) แล้ว window เพี้ยนมาก — ไม่เห็น sidebar, รูปเพี้ยนตำแหน่ง, SayBox
กว้างเต็มจอ — และย้ำว่า**เกิดเฉพาะตอนกด Play เท่านั้น** (ใน Editor ปกติ layout ถูกหมด) ไล่เจอ 4 สาเหตุจริงซ้อนกัน:

1. **`Mask` + Image โปร่งใสสนิทบน `SidebarViewport`** — `Mask` component ต้องมี Graphic บน GameObject
   เดียวกัน แต่ Image ที่ใส่ไว้ alpha=0 ทำให้ CanvasRenderer culling ทิ้งทั้ง masking graphic และลูกๆ
   ข้างใน (sidebar ทั้งก้อนหายไป) ทั้งที่ค่า C# ทุกตัวอ่านถูกหมด (active/enabled/position ปกติ) — เกิดเฉพาะ
   Play mode เพราะ CanvasRenderer culling ทำงานตอน runtime เท่านั้น ไม่ใช่ตอน edit scene view
   → **แก้:** ถอด `Mask`+`Image` ทิ้ง ใส่ `RectMask2D` แทน (ไม่ต้องมี Graphic เลย)
2. **Scene instance หลุดการเชื่อมกับ prefab asset** — หลัง fix ข้อ 1 ที่ prefab แล้ว instance ใน `GamePlay.unity`
   ไม่รับการเปลี่ยนแปลงเลยแม้ exit/re-enter Play หลายรอบ (`PrefabUtility.GetPrefabInstanceStatus()` คืน
   `NotAPrefab`) → **แก้:** ลบ instance เดิมทิ้งแล้ว `PrefabUtility.InstantiatePrefab()` ใหม่จาก asset ล่าสุด
3. **`Instantiate()` ไม่รับประกันว่า `Awake()` รันไปแล้วก่อน caller จะแตะ instance ได้** — `FieldManualEntryButton.Setup()`
   โยน NullReferenceException ที่ `Button.onClick.RemoveAllListeners()` เพราะ `Button` (set ใน `Awake()`) ยัง
   null — ยืนยันด้วยการทดสอบตรงๆ ว่า `Object.Instantiate(prefab)` ตามด้วยเช็ค field ที่ set ใน `Awake()` ทันที
   คืน null ทั้งที่ `GetComponent<Button>()` บน instance เดียวกันเจอปกติ
   → **แก้:** เพิ่ม `EnsureButton()` lazy-resolve เรียกทั้งจาก `Awake()` และต้น `Setup()` (`FieldManualEntryButton.cs`)
4. **Window active อยู่โดย default ตั้งแต่ scene load — ไม่มีใครสั่งปิด** — ต่างจาก MainMenu ที่ `DesktopManager`
   เรียก `InitializeHidden()` ให้ทุก window ตอน boot, ใน Gameplay scene ไม่มี Desktop ผูกอยู่เลย ไม่มีใครเรียก
   `InitializeHidden()` ให้ Field Manual — ยืนยันสดว่า `FieldManualUI.IsOpen == true` ทันทีหลัง scene load สด
   (ก่อน toggle ใดๆ) แปลว่า window เพี้ยนที่เจเห็นคือ**หน้าต่างที่ยังไม่เคยถูกเรียก `Show()`/`OnShown()` เลย
   ก็ค้างเปิดอยู่กลางจอตั้งแต่ต้นเกม** — sidebar ว่าง (`EnsureRows()` ไม่เคยรัน), รูป/ข้อความยังไม่ถูก set
   → **แก้:** `FieldManualController.Awake()` เรียก `fieldManual.InitializeHidden()` ทันทีตอน boot (เหมือน
   pattern ที่ `DesktopManager` ทำใน MainMenu)

**ทดสอบสดยืนยันครบ (ผ่าน `FieldManualController.Toggle()` จริง ไม่ใช้ workaround ในสคริปต์ทดสอบ):**
fresh scene load → `IsOpen=false` แต่ต้น, กด toggle ครั้งแรก → sidebar 8 แถวตำแหน่งถูกต้อง (ห่างกัน 24px ตาม
สเปค), ไม่มี NRE, detail pane/preview sprite/report-as text ขึ้นถูกทุกช่อง, toggle ซ้ำปิดได้ปกติ

**บั๊กข้อ 5 (แก้แล้ว): แถว sidebar เกิน 1 แถว (9 แทน 8)** — เดิมสรุปผิดว่าเป็น testing artifact จริงๆ คือมี
GameObject `FieldManualEntryButton(Clone)` ตัวหนึ่งถูก **save ค้างอยู่ใน `GamePlay.unity`** (จากการทดสอบในโหมด Edit)
เลยมี 1 แถวก่อน `EnsureRows()` ทำงานเสมอ → ลบออกจากซีนแล้ว ยืนยันสด: ก่อนเปิด rows=0, หลัง TAB rows=8
**ข้อควรระวัง:** ห้ามรัน `Instantiate`/`Setup` ทดสอบกับ instance ในซีนตอน Edit mode แล้ว save ซีน

---

## 4. Global Push-to-Talk (Phase 2 — ต้องทำก่อน #2-#4)

ตอนนี้ไมค์เปิดได้เฉพาะปุ่ม "Hold to Speak" ในฟอร์ม Incident Report
→ Radio Check, #2, #3, #4 ทำงานจริงไม่ได้ถ้าไม่มีปุ่มพูดนอกฟอร์ม
- เพิ่มปุ่ม **กดค้าง V** = พูดได้ทุกที่ (Spacebar ใช้เปิด/ปิดฟอร์มอยู่แล้ว)
- ใช้ `WhisperMicInput.BeginPushToTalk/EndPushToTalk` ตัวเดิม ทุกคำที่พูดผ่าน Noise Meter เหมือนกัน
- ไอคอนไมค์มุมจอบอกสถานะ

---

## 5. Rollout ตามโครงเรื่อง 3 องก์

| องก์ | ผู้เล่นได้เรียน |
|---|---|
| Act 1 (คืน 1-2) | Observation Report (#1) + Noise Meter (คืน 1 เตือนอย่างเดียว คืน 2 Listener มาจริง) + ตอบวิทยุ |
| Act 2 (คืน 3-5) | เสียงปลอม (#4) + ตัวที่ต้องกระซิบ/ตะโกน (#2) |
| Act 3 (คืน 6-7) | "Give me a sign" (#3) เป็นทางเดียวที่แยกของจริงกับของปลอมได้ |

---

## 6. Phase Plan

| Phase | งาน | สถานะ |
|---|---|---|
| 1 | Observation Report (#1) + Noise Meter + HUD | 🟡 กำลังทำ |
| 2 | Global Push-to-Talk (V) + ต่อ Radio Check ให้ฟังได้นอกฟอร์ม | ⬜ |
| 3 | #2 Voice Response + #4 Voice Mimic | ⬜ |
| 4 | #3 Give Me A Sign investigation | ⬜ |
| — | Content: art ของ anomaly ให้ "สิ่งที่เปลี่ยน" เห็นชัดในแต่ละห้อง | ⬜ |
| — | Field Manual (S-306/307) — คู่มือกด TAB เปิด/ปิดได้ตลอด ไม่ pause | ✅ |

---

## 7. ความเสี่ยง / สิ่งที่ต้องระวัง

- **`PhraseMatcher.WordsMatch` หลวมเกินไป** — `recognizedWord.Contains(targetWord) || targetWord.Contains(recognizedWord)`
  ทำให้คำพูดสั้นอย่าง `"a"`, `"i"`, `"in"` match คำเป้าหมายได้เกือบทุกคำ กระทบ Radio Check (ต้องการแค่ 2 คำ)
  Phase 1 ใช้ matcher ใหม่ของตัวเอง ยังไม่ได้แก้ `PhraseMatcher` — ควรแก้ใน Phase 2
- **ภาษาไทย** — Whisper ตั้ง `language = "en"` คำศัพท์ใน `ObservationVocabulary` เป็นอังกฤษทั้งหมด
  ถ้าจะรองรับไทยต้องเพิ่มคำไทยใน asset + ตั้ง language เป็น `auto`/`th`
- **ไมค์ไม่มี / ยังไม่ calibrate** — Noise Meter ใช้ `MicCalibration.NoiseFloor` ถ้ายังไม่เคย calibrate ใช้ค่า fallback
  ถ้าไม่มีไมค์เลย มาตรจะไม่ขึ้น (พิมพ์ผ่าน `TypedInputFallback` ไม่คิดเสียง)
- **โค้ด legacy ที่ไม่ได้ใช้** — `AnomalyMovement.MoveToTarget`, `AnomalyThreatTimer.Begin`, `_canPrayDisappear`,
  `VoiceCommandRouter` (prayer) ไม่มีใครเรียก ไม่ได้แตะใน Phase นี้ ควรตัดสินใจลบหรือต่อสายใหม่ทีหลัง
