# คู่มือ Debug Tools ทั้งหมดในโปรเจกต์

## 🆕 ทางลัด: กด F12 เปิดแผงเดียวจบ

**`DebugMasterPanel`** — กด **F12** ระหว่างเล่น เปิด/ปิดแผง debug ที่รวมทุกอย่างไว้ที่เดียว:

- **สแกนหา `[ContextMenu]` และ Gaskellgames `[Button]` ทุกตัวในซีนที่โหลดอยู่โดยอัตโนมัติ**
  (ไม่ได้ hardcode ปุ่มไว้ตายตัว — เพิ่ม `[ContextMenu]` ใหม่ที่ไหนในโปรเจกต์ กด **↻ Refresh**
  ในแผงก็โผล่มาเองทันที ไม่ต้องมาแก้ไฟล์นี้)
- **กล่องพิมพ์ข้อความแทนพูดอยู่ด้านบนสุดตลอด** — พิมพ์แล้ว Enter หรือกด Send ส่งเข้าคิว
  recognized-text เดียวกับที่ไมค์จริงส่ง ใช้แทนไมค์ได้ทุกระบบที่ต้องพูด (สวดมนต์, Incident Report,
  Radio Check, ...)
- ใช้ได้ทั้งจากซีน `MainMenu` และ `GamePlay` (วางไว้ทั้งสองซีนแล้ว, ตั้ง Persist Across Scenes
  ไว้ด้วยเลยติดตามไปทุกซีนหลังจากนั้น)
- สร้างด้วย **uGUI ล้วนๆ ไม่ใช้ `OnGUI()`** เจตนา — `OnGUI` คือสิ่งที่เพิ่งทำให้เกมแลคหนักไปก่อน
  หน้านี้ (ดู [Performance-Findings-TH.md](Performance-Findings-TH.md) รอบที่ 4) เพราะมันถูกเรียกซ้ำ
  ทุกครั้งที่เมาส์ขยับ แผงนี้จึงไม่มีต้นทุนใดๆ ตอนปิดอยู่ และไม่เพิ่มต้นทุนพิเศษตอนเปิดนอกจาก UI ปกติ

**ปรับได้ใน Inspector** (เลือก GameObject `DebugMasterPanel` ในซีน):
- `Toggle Key` — เปลี่ยนจาก F12 เป็น F10 / F9 / BackQuote (`) ได้ถ้าชนกับโปรแกรมอื่น
- `Panel Size`, `Canvas Sort Order` — ปรับขนาด/ความสูงของ layer

ส่วนด้านล่างนี้ยังเก็บไว้เป็นรายละเอียดอ้างอิง (ปุ่ม/คำสั่งแต่ละตัวทำอะไรจริงๆ) เผื่ออยากรู้ว่ากด
ปุ่มไหนในแผงแล้วมันเรียกอะไรอยู่เบื้องหลัง

---

## 1. คลิกขวาใน Inspector (`[ContextMenu]` / ปุ่ม `Button`)

วิธีใช้ทั่วไป: เลือก GameObject ที่มี component นั้นอยู่ในซีน → คลิกขวาที่**ชื่อ component**
(ที่หัว Inspector ของ component นั้น ไม่ใช่คลิกขวาที่ GameObject) → เมนูจะโผล่ขึ้นมา
**ต้องอยู่ใน Play Mode** ส่วนใหญ่ถึงจะมีผล (ยกเว้นที่ระบุว่าใช้ตอน Edit Mode ได้)

### ระบบคืน / วัน (Night & Flow)

| เมนู | อยู่บน Component | ทำอะไร |
|---|---|---|
| **Dump Night Plan** | `NightPlanRunner` | พิมพ์ตารางคืนทั้งคืนลง Console (anomaly/glitch/haunt ทุกตัว + เวลา) |
| **Force Spawn Next** | `AnomalyScheduler` | เร่งสปอน anomaly ตัวถัดไปในคิวทันที ไม่ต้องรอเวลา |
| **Force Spawn All Remaining** | `AnomalyScheduler` | สปอน anomaly ที่เหลือทั้งหมดรวดเดียว |
| **Sort Entries By Time** | `AnomalyScheduler`, `GlitchScheduler` | (Edit Mode ก็ได้) เรียงลำดับ list ตามเวลาให้ดูง่าย |
| **Force Fire Next** | `GlitchScheduler`, `HauntDirector` | ยิง glitch/haunt ตัวถัดไปในคิวทันที |
| **Dump Glitch Plan** | `GlitchDirector` | พิมพ์แผนกลิตช์ทั้งหมดของคืนนี้ + เหตุผลการตัดสินใจล่าสุด |
| **Reset Fired Beats** | `GlitchDirector` | ล้างประวัติ "ยิงไปแล้ว" ของกลิตช์แบบสคริปต์ (ใช้ตอนเทสต์รีสตาร์ทวัน) |
| **End Night Now** | `NightTimer` | บังคับให้คืนจบทันที (นับเป็น Survived ถ้าคะแนนถึงเกณฑ์) |
| **End Night Now (survived)** / **Test Win Condition** | `ScoreManager` | บังคับจบคืนแบบรอด/ทดสอบเงื่อนไขชนะ |
| **Add Test Score** | `ScoreManager` | เพิ่มแต้มปลอมเพื่อเทสต์ |
| **Log Campaign Curve** | `DifficultyProfile.asset` | **ไม่ต้อง Play Mode** พิมพ์ตารางความยากทั้ง 7 วันจากค่าจริงตอนนี้ (คันโยกทุกช่อง) |

### ระบบ Day-End Event (VDO / Minigame)

| เมนู | อยู่บน Component | ทำอะไร |
|---|---|---|
| **Find All Events In Project** | `RandomEventDirector` | **Editor-only** กวาดหา `ShortVDOData`/`MinigameData` ทั้งโปรเจกต์มาใส่ pool ให้อัตโนมัติ |
| **Reset Consumed Events (save)** | `RandomEventDirector` | ล้างประวัติ "ดูแล้ว" ในไฟล์เซฟ ให้ VDO/Minigame กลับมาสุ่มได้ใหม่ทั้งหมด |
| **Log Pool State** | `RandomEventDirector` | พิมพ์จำนวน VDO/Minigame ที่เหลือ (ยังไม่ถูกดู) ลง Console |

### ระบบ Result / คะแนน (`ResultDisplay`, `SampleSceneManager` — ต้องเปิดซีน Result เอง)

| เมนู | ทำอะไร |
|---|---|
| **Test Win Result** / **Test Normal Result** | จำลองหน้าจอผลชนะ |
| **Test Lose Result** | จำลองหน้าจอผลแพ้ (คะแนนไม่ถึง) |
| **Test Anomaly Defeat** | จำลองหน้าจอ "โดน Anomaly จับได้" |
| **Test Demon Defeat** | จำลองหน้าจอ "โดนปีศาจจับได้" |

> ⚠️ ตั้งแต่แก้ flow แล้ว **ซีน Result ไม่ได้ถูกเรียกอัตโนมัติอีกต่อไป** เมนูพวกนี้มีไว้เทสต์หน้าจอ
> Result ด้วยตัวเองเท่านั้น (เปิดซีนนั้นตรงๆ ใน Editor แล้วกด Play)

### เมนู Main Menu / กลิตช์ฟอร์ม (สำหรับเทสต์ไว)

| เมนู | อยู่บน Component | ทำอะไร |
|---|---|---|
| **Debug/Start Shift**, **Debug/Open My Reports** ฯลฯ | `DesktopManager` | เปิดหน้าต่างนั้นๆ ตรงๆ โดยไม่ต้องคลิก icon จริง |
| **Play Boot Sequence** / **Play Shutdown Sequence** | `ShutdownSequence` | เล่นแอนิเมชันเปิด/ปิดเครื่องซ้ำโดยไม่ต้องกดปุ่มจริง |
| **Glitch/A - Phantom Dropdown** ... **Glitch/E - Clock Desync** | `FormGlitchController` | ยิงกลิตช์แต่ละแบบทีละตัวเพื่อดูหน้าตา |
| **Glitch/RUN DEMO (C, D, E - 4s each)** | `FormGlitchController` | โชว์กลิตช์ 3 แบบต่อกันแบบ auto-play |
| **Glitch/Cancel All** | `FormGlitchController` | ยกเลิกกลิตช์ที่กำลังเล่นอยู่ทั้งหมด |
| **Trigger Scene Transition** | `Cutscene/SceneTransition` | ทดสอบทรานสิชันเปลี่ยนซีน |
| **Refresh** | `UI/XPGroupBoxTitle` | (Edit Mode) รีเฟรชเส้นกรอบ/หัวข้อ XP-style ใน Inspector |

---

## 2. ปุ่มลัดคีย์บอร์ดตอนเล่น (Play Mode)

| ปุ่ม | ทำอะไร | เปิด/ปิดยังไง |
|---|---|---|
| **F3** | เปิด/ปิด HUD แผนคืน (`NightPlanHud`) — โชว์ anomaly/glitch/haunt ทั้งหมดของคืนนี้พร้อมเวลา + ปุ่มเล่นซ้ำ seed เดิม | ค่าเริ่มต้นเปิดใช้ได้เลย ปรับปุ่มได้ที่ `NightPlanHud.Toggle Key` |
| **Spacebar** | เปิด/ปิดฟอร์ม Incident Report (ของจริง ไม่ใช่ debug) | เป็นปุ่มเกมปกติ |
| **F1-F6** | ยิงกลิตช์แต่ละแบบตรงๆ ระหว่างเล่น (เหมือนเมนู Glitch/A-E แต่กดสดได้) | **ปิดอยู่โดย default** ต้องไปติ๊ก `FormGlitchController.Debug Hotkeys` ก่อน |

> ⚠️ **F3 ชนกัน:** `NightPlanHud` ใช้ F3 toggle HUD และ `FormGlitchController` (ถ้าเปิด Debug
> Hotkeys) ก็ใช้ F3 ยิง "Case Corruption" glitch เหมือนกัน ถ้าเปิดทั้งคู่พร้อมกันปุ่มจะชนกัน
> แนะนำให้เปิดใช้ทีละตัว หรือเปลี่ยน `NightPlanHud.Toggle Key` เป็นปุ่มอื่น

---

## 3. หน้าจอ Debug บนจอ (OnGUI, Play Mode เท่านั้น)

| อะไร | โผล่ตอนไหน | เนื้อหา |
|---|---|---|
| **Night Plan HUD** (`NightPlanHud`) | กด F3 | ตารางคืนทั้งคืน, ช่องกรอก seed, ปุ่ม "Replay THIS seed" |
| **Typed Input Fallback** (`TypedInputFallback`) | อัตโนมัติเมื่อ**ไม่พบไมค์** (หรือติ๊ก `Force Enabled`) | กล่องพิมพ์ข้อความแทนการพูด ไม่ใช่ debug tool จริงๆ แต่เป็น accessibility fallback ที่ใช้ OnGUI เหมือนกัน |

---

## 4. Console Log (`Show Debug Info` ใน Inspector)

Component เกือบทุกตัวมีช่องติ๊ก **`Show Debug Info`** ใน Header "Debug" ของตัวเอง
ติ๊กแล้วจะ log สถานะ/การตัดสินใจของระบบนั้นๆ ลง Console ระหว่างเล่น ที่ใช้บ่อยที่สุด:

| Component | Log อะไร |
|---|---|
| `GameFlowManager` | ทุกครั้งที่วันเริ่ม/จบ/แพ้/เลื่อนวัน พร้อมเหตุผล |
| `RandomEventDirector` | ผลการสุ่ม event ท้ายวันแต่ละครั้ง (สุ่มได้อะไร/ทำไมไม่มี) |
| `DayEventPlayer` | ตอนข้ามวิดีโอ |
| `AnomalyOverloadWatcher` | ตอน anomaly ล้นแล้วรีเซ็ต / ตอนแพ้ |
| `AnomalyScheduler` | ตอนสปอน anomaly แต่ละตัว รวมถึงตัวที่สปอนเป็นบทลงโทษ (`PENALTY: ...`) |
| `IncidentReportManager` | ตอนกด Spacebar, สถานะฟอร์ม |
| `GlitchDirector` | ติ๊ก `Log Director Decisions` แยกต่างหาก (ไม่ใช่ Show Debug Info) — log เหตุผลทุกครั้งที่ตัดสินใจยิง/ไม่ยิงกลิตช์ |

**ค่าเริ่มต้นตอนนี้ในซีน `MainMenu.unity`:** `GameFlowManager`, `RandomEventDirector`,
`DayEventPlayer`, `EndingSequenceController` เปิด `Show Debug Info` ไว้ให้แล้ว (เห็น log
ทันทีโดยไม่ต้องไปติ๊กเอง) ปิดได้จากช่องนี้เมื่อพร้อม build จริง

---

## 5. ตัวช่วยดู Scene View (Gizmos, ไม่ต้อง Play)

| Component | Gizmo อะไร |
|---|---|
| `AnomalyScheduler` | จุดสปอนแต่ละจุด (ManualList mode) พร้อมป้ายเวลา — ปิด/สีได้ที่ `Show Gizmos`/`Gizmo Color` |
| `NightPlanRunner` | ตำแหน่ง anomaly ที่วางแผนไว้ของคืนนี้ (ต้อง Play + เลือก object ก่อนถึงเห็น เพราะห้องสุ่มตอนรันจริง) |

---

## สรุปเร็ว: อยากเทสต์อะไร ไปที่ไหน

| อยากรู้/เทสต์ | ทำยังไง |
|---|---|
| คืนนี้จะมี anomaly/glitch อะไรบ้าง | กด **F3** ระหว่างเล่น |
| อยากดูตารางความยากทั้ง 7 วันตอนนี้ | เลือก `DifficultyProfile.asset` → **Log Campaign Curve** |
| อยากข้ามไปดู anomaly ตัวถัดไปเลย | `AnomalyScheduler` → **Force Spawn Next** |
| อยากดูกลิตช์แต่ละแบบหน้าตายังไง | `FormGlitchController` → เมนู **Glitch/A-E** |
| อยากรีเซ็ต VDO ที่ดูไปแล้ว | `RandomEventDirector` → **Reset Consumed Events (save)** |
| อยากดูว่าทำไม event ท้ายวันไม่มา | ติ๊ก `RandomEventDirector.Show Debug Info` แล้วดู Console |
| อยากเล่น seed เดิมซ้ำ | กด F3 → กรอก seed → **Replay THIS seed** |
