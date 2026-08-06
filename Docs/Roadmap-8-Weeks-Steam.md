# Give Me A Sign — Road-Map 8 สัปดาห์ สู่ Steam Release

**เขียนวันที่:** 2 สิงหาคม 2026
**เป้าหมาย:** ปล่อยเกมบน Steam
**กำลังพล:** 1 คน เต็มเวลา (~40 ชม./สัปดาห์ = ~320 ชม. รวม)
**Launch target:** ~27 กันยายน 2026 (สิ้นสุด Sprint 8)
**เอกสารอ้างอิง:** `Docs/Project-Status-Report.md`

---

## 0. อ่านตรงนี้ก่อน — ข้อจำกัดของ Steam ที่บีบ Roadmap

Steam มีเวลารอที่ **ตัดออกไม่ได้** และมันไม่ขนานกับงาน dev เสมอไป ต้องเริ่มพร้อมกันตั้งแต่วันแรก:

| ข้อกำหนด | ระยะเวลา | Deadline ในแผนนี้ |
|---|---|---|
| จ่ายค่า Steam Direct ($100) แล้วต้องรอ **30 วัน** ก่อนปล่อยเกมแรกได้ | 30 วัน | **จ่ายภายใน Sprint 1 วันที่ 1-3** |
| ส่ง store page ให้ Valve รีวิว (3-5 วันทำการ; แนะนำเผื่อ 7 วัน) | ~7 วัน | ส่งภายใน **สิ้น Sprint 4** |
| store page ต้อง live แบบ "Coming Soon" อย่างน้อย **14 วัน** ก่อนวันปล่อย | 14 วัน | live ภายใน **ต้น Sprint 6** |
| ส่ง build ให้รีวิว (1-5 วันทำการ) | ~5 วัน | ส่งภายใน **ต้น Sprint 8** |

**Critical path ที่ห้ามพลาด:**

```
วันที่ 1-3    ─ จ่าย Steam Direct fee  ────────────────┐ (นาฬิกา 30 วันเริ่มเดิน)
Sprint 4 จบ  ─ ส่ง store page รีวิว                    │
Sprint 5 กลาง─ store page ผ่าน → เปิด Coming Soon ───┐ │ (นาฬิกา 14 วันเริ่มเดิน)
Sprint 8 ต้น ─ ส่ง build รีวิว                        │ │
Sprint 8 จบ  ─ LAUNCH ◄──────────────────────────────┘─┘
```

> ⚠️ **นี่คือความเสี่ยงอันดับ 1 ของแผนนี้** ถ้าลืมจ่าย fee ในสัปดาห์แรก หรือ store page ช้าไป 1 สัปดาห์ วันปล่อยจะเลื่อนทันทีโดยที่งาน dev เสร็จแล้วก็ช่วยอะไรไม่ได้
>
> งาน Steam ops รวมทั้งหมด ~35 ชม. ถูกกระจายไว้ใน Sprint 1, 4, 5, 8 แล้วในแผนข้างล่าง

**คำแนะนำเพิ่มเติม:** ตั้งวันปล่อยไว้ที่ **วันอังคาร-พฤหัสบดี** และเผื่อ buffer 1 สัปดาห์ไว้ในใจ (Launch จริง = ต้นเดือนตุลาคม) เพราะ Steam build review อาจตีกลับได้

---

## 1. ตอบคำถามเรื่องกลไกไมค์ (แทนที่การสวดมนต์)

คุณบอกว่า "ไม่ต้องเป็นบทสวดก็ได้ ขอให้ใช้ไมค์ และขอให้ตื่นกลัว" — ผมเสนอให้ **เลิกคิดว่าไมค์คือ 'ปุ่มวิเศษที่ใช้แก้ปัญหา'** แล้วเปลี่ยนเป็น **'ช่องทางที่ผีใช้เข้าถึงคุณ'** แทน

นี่คือกลไก 2 ตัวที่ผมคิดว่าลงตัวกับเกมนี้ที่สุด:

### 🎙️ กลไกหลัก: **Silence Protocol** (โปรโตคอลความเงียบ)

> มี anomaly ชนิดหนึ่ง — **"The Listener"** — ที่ *ไม่มีตา* มันล่าด้วยเสียง
> เมื่อมันปรากฏ HUD จะขึ้น VU meter วัดเสียงจากไมค์ของคุณแบบเรียลไทม์
> **เสียงดังเกินเส้นแดงติดต่อกันเกิน 2 วินาที = มันหาคุณเจอ**

**ทำไมมันถึงน่ากลัวมาก:**

1. **มันดึงห้องจริงของผู้เล่นเข้ามาในเกม** — ไอ จาม เก้าอี้ดัง คนเดินผ่าน แมวร้อง = ตาย ไม่มีเกมไหนควบคุมสิ่งนี้ได้ นี่คือความกลัวที่แท้จริง
2. **มันขัดแย้งกับ core loop โดยตรง** — เกมนี้บังคับให้คุณ *พูด* เพื่อกรอกรายงาน แต่ Listener บังคับให้คุณ *เงียบ* → ผู้เล่นต้องเลือก: กรอกรายงานตอนนี้แล้วเสี่ยง หรือรอให้มันไปก่อนแล้วเสี่ยงหมดเวลา **นี่คือ dilemma ที่แท้จริง ไม่ใช่แค่ mechanic เพิ่ม**
3. **ทางออกคือ "กระซิบ"** — threshold ถูกตั้งไว้ให้เสียงกระซิบผ่านได้ ผู้เล่นจะพบเองว่าต้องเอาหน้าไปจ่อไมค์แล้วกระซิบชื่อ anomaly ใส่ระบบ **ท่าทางนั้นเองคือความสยองที่ผู้เล่นแสดงออกมาด้วยร่างกายจริง** — และเป็นคลิปที่ streamer จะเอาไปลงแน่นอน
4. **ถูกมากในเชิงเทคนิค** — ใช้แค่ `Microphone.GetPosition` + คำนวณ RMS amplitude ไม่ต้องรัน Whisper inference เลย ไม่กิน CPU ไม่ต้องรอโมเดล

**สิ่งที่ต้องมี:** หน้าจอ calibrate ไมค์ตอนเริ่มเกม (วัด noise floor ของห้องผู้เล่น) + accessibility toggle สำหรับคนที่เล่นในที่เสียงดัง

### 📻 กลไกรอง: **Radio Check / Roll Call**

> ทุก ~60-90 วินาที วิทยุจาก HQ ดังขึ้น: *"SEC-04, radio check."*
> คุณมีเวลา 8 วินาทีที่จะตอบด้วยเสียงว่า **"SEC-04, copy"**
> ไม่ตอบ = ถูกบันทึกว่า "ละทิ้งหน้าที่" (สะสมโทษ)

**ตัวบิดที่ทำให้มันเป็น horror:**

- บางครั้ง **เสียงที่เรียกคุณคือเสียงของคุณเอง** — เกมอัดเสียงที่คุณเคยตอบไว้ แล้วเล่นกลับมาทางวิทยุ (ทำได้จริง: เก็บ `AudioClip` จาก mic buffer ตอนตอบครั้งก่อน แล้ว playback)
- บางครั้ง HQ เรียก **ID ที่ไม่ใช่ของคุณ** ("SEC-03, radio check") — ถ้าคุณตอบไป = คุณยอมรับว่าคุณเป็นคนอื่น (ผูกกับ story ในอนาคตได้เต็มๆ)
- ระหว่าง **Silence Protocol** วิทยุก็ยังเรียก → บังคับให้ต้องเลือกอีกครั้ง

**นี่คือกลไกที่ทำให้ไมค์ 'มีชีวิต' ตลอดทั้งคืน** ไม่ใช่แค่ตอนเปิดฟอร์ม

### สิ่งที่จะเกิดกับโค้ดสวดมนต์เดิม

**ไม่ทิ้ง — เปลี่ยนชื่อและ generalize:**

| เดิม | ใหม่ | เหตุผล |
|---|---|---|
| `PhraseMatcher` | คงเดิม | fuzzy matching ใช้ต่อได้เลย ทั้ง Roll Call และ report |
| `VoiceCommandRouter` | → `VoicePromptSystem` | ระบบกลาง: "เกมขอให้พูดอะไรบางอย่าง แล้วเช็คว่าพูดถูกไหม" ใช้ได้ทั้ง Roll Call, report, และ event อื่นในอนาคต |
| `PrayUiManager` | → `VoicePromptUI` | UI ที่แสดงว่า "ตอนนี้ระบบรอฟังอะไรอยู่" |
| `SignRequestSystem` | → รวมเข้า `VoicePromptSystem` เป็น prompt ชนิดหนึ่ง | "Give me a sign" กลับเข้าเกมในฐานะ **คำขอ hint ที่มีราคา** (ดู HL-7) |

ได้ประโยชน์: โค้ดที่เขียนไว้แล้วทั้งหมดถูกใช้งาน, ลบ dead code, และได้ระบบกลางที่รองรับ story ในอนาคต

---

## 2. Haunt Loop ทั้งหมด (ปัจจุบัน 2 → เป้าหมาย 7)

**นิยาม "Haunt Loop":** sub-loop ที่ (ก) สร้างความกลัว (ข) บังคับให้ผู้เล่นตอบสนอง (ค) มีสำเร็จ/ล้มเหลว
ทุกตัวต้องถูก **สุ่มเลือกและวางเวลาโดย NightPlanGenerator** ไม่ใช่ hardcode

| ID | ชื่อ | สถานะ | ผู้เล่นต้องทำอะไร | ใช้ไมค์ | Sprint |
|---|---|---|---|---|---|
| **HL-1** | **Form Betrayal** | ✅ มีแล้ว | ตัดสินว่าจะเชื่อฟอร์มไหม | – | ปรับ S2 |
| **HL-2** | **Demon Lock-in** | ✅ มีแล้ว | ติดในห้อง ต้องรายงานให้ถูกใน 30 วิ | ✔ | ปรับ S6 |
| **HL-3** | **Silence Protocol** | 🆕 | **เงียบ / กระซิบ** ไม่งั้นตาย | ✔✔ | **S4** |
| **HL-4** | **Radio Check** | 🆕 | ตอบวิทยุด้วยเสียงใน 8 วิ | ✔✔ | **S5** |
| **HL-5** | **Camera Betrayal** | 🆕 | จับผิดว่าภาพจากกล้อง "ไม่สด" | – | **S5** |
| **HL-6** | **Impostor Case** | 🆕 | เจอรายงานที่ตัวเองไม่ได้ยื่น | – | **S6** |
| **HL-7** | **Give Me A Sign** | 🔁 กู้คืน | ขอ hint ได้ แต่มันได้ยินคุณ | ✔ | **S6** |

### รายละเอียดตัวใหม่

#### HL-3 — Silence Protocol
- **Trigger:** anomaly ชนิด `The Listener` spawn (tier สูง, ไม่มาก่อนนาทีที่ 2)
- **สัญญาณเตือน:** ไฟทั้งชั้นดับ + HUD ขึ้น VU meter สีแดง + เพลงเงียบสนิท (ตัด ambient ทั้งหมด = ความเงียบเป็นสัญญาณ)
- **กติกา:** RMS > threshold ติดต่อกัน 2 วิ → มันเข้ามาหา (3 strikes = ตาย) / กระซิบผ่านได้
- **ทางออก:** อยู่เงียบจนครบ 20-30 วิ **หรือ** กระซิบชื่อมันใส่ฟอร์มให้สำเร็จ (เร็วกว่า แต่เสี่ยง)
- **ตัวคูณความกลัว:** ถ้า **HL-4 Radio Check** ยิงตอนนี้พอดี → ต้องเลือกว่าจะตอบวิทยุ (ตาย) หรือไม่ตอบ (โดนโทษ)

#### HL-4 — Radio Check
- **Trigger:** ทุก 60-90 วิ (สุ่มโดย generator)
- **กติกา:** พูด "SEC-04, copy" ใน 8 วิ → ผ่าน / ไม่ตอบ → +1 negligence strike
- **Variants (สุ่ม):** เสียงปกติ / **เสียงตัวเอง** / เรียก ID ผิด / เรียกตอน Silence Protocol / เรียกซ้อนกัน 2 เสียงพร้อมกัน
- **3 negligence strikes** → HQ "ส่งคนมาตรวจ" → เกิด haunt event บังคับ

#### HL-5 — Camera Betrayal
ขยายแนวคิด "สิ่งที่คุณเห็นเชื่อไม่ได้" จากฟอร์มไปที่กล้อง — reuse pattern เดียวกับ `GlitchDirector`/`FormGlitchController`

| Variant | อาการ | วิธีจับผิด |
|---|---|---|
| **Loop** | ฟีดห้องนั้นเป็นภาพย้อนหลัง 40 วิ (anomaly อยู่ตอนนี้แต่มองไม่เห็น) | timestamp มุมจอไม่เดิน |
| **Ghost Room** | มีกล้องตัวที่ 4 โผล่ในตัวสลับ ทั้งที่ไม่มีอยู่จริง | เข้าไปแล้วเป็นห้องที่ไม่มีในคู่มือ |
| **Frozen** | ภาพนิ่งสนิท ไม่มี noise ไม่มี flicker | ไม่มี grain เคลื่อนไหว |
| **Mirror** | กล้องแสดงห้องรปภ.เอง — ห้องที่คุณนั่งอยู่ | มีเงาคนนั่งอยู่ในภาพ |
| **Blackout** | กล้องดับ 1 ห้อง ต้องตัดสินใจโดยไม่เห็น | – |

#### HL-6 — Impostor Case
- เลข case กระโดดไป 2-3 เลขทั้งที่ไม่ได้ยื่น
- เปิดฟอร์มมาแล้วเจอ case เก่าที่ **กรอกไว้แล้ว** — location = "Security Office", type = ชื่อผู้เล่น
- ช่อง Officer เปลี่ยนจาก SEC-04 → SEC-03 ชั่วขณะ
- **ต่อยอด story ในอนาคตได้โดยตรง** — นี่คือ seed ของเนื้อเรื่องที่คุณจะใส่ทีหลัง

#### HL-7 — Give Me A Sign (กู้ระบบเดิมกลับมา)
- ผู้เล่นพูด **"Give me a sign"** ได้ทุกเมื่อ → ระบบชี้ห้องที่มี anomaly อยู่ (hint)
- **ราคา:** glitch intensity +50% ไปตลอดคืน / เพิ่มโอกาสเกิด HL-5 / บาง anomaly ตอบกลับ **จริงๆ**
- ใช้ได้จำกัด 3 ครั้ง/คืน — เป็น risk/reward ที่ผู้เล่นเลือกเอง
- **กลไกนี้คือชื่อเกม** — ต้องมีในบิลด์ที่ปล่อยแน่นอน

---

## 3. ระบบสุ่ม (Procedural) — สถาปัตยกรรมสำหรับ Replayability

### 3.1 แนวคิดหลัก: NightPlan

**หัวใจคือ: สุ่มครั้งเดียวตอนเริ่มคืน แล้วได้แผนที่กำหนดตายตัว** — ไม่ใช่สุ่มทีละเหตุการณ์ระหว่างเล่น
ข้อดี: reproducible จาก seed, debug ได้, balance ได้, และ **ผู้เล่นแชร์ seed กันได้**

```
NightSeed (int)  ─→  NightPlanGenerator  ─→  NightPlan
                            │                    ├── List<AnomalyPlacement>  {definition, roomId, minute}
                     DifficultyProfile           ├── List<GlitchBeat>        {type, minute, overrideText}
                     (จาก night index)           ├── List<HauntBeat>         {hauntId, minute, params}
                                                 ├── GlitchProfile           {baseChance, weights, curve}
                                                 └── int requiredScore       (คำนวณจากแผน — ไม่ hardcode)
                                                          │
                    ┌─────────────────────┬───────────────┴──────┐
                    ▼                     ▼                      ▼
            AnomalyScheduler       GlitchScheduler         HauntDirector
```

> ⭐ `requiredScore` ถูก **คำนวณจากแผนที่สุ่มได้** (เช่น 75% ของ anomaly ทั้งหมด) → **บั๊ก blocker §4.1 ในรายงานจะหายไปถาวรและเกิดซ้ำไม่ได้อีกเลย**

### 3.2 Constraint — สิ่งที่ทำให้ "สุ่มดี" ต่างจาก "สุ่มมั่ว"

การสุ่มแบบไม่มีกฎจะทำให้บางคืนง่ายเกินและบางคืนเป็นไปไม่ได้ Generator ต้องบังคับกฎเหล่านี้:

| กฎ | รายละเอียด | ป้องกันอะไร |
|---|---|---|
| **Threat Budget** | แต่ละคืนมีงบภัยคุกคาม แต่ละ anomaly/haunt มี cost ต่างกัน สุ่มจนงบหมด | คืนที่ยากเกินหรือง่ายเกิน |
| **Minimum Spacing** | เหตุการณ์ 2 อันห่างกันอย่างน้อย 25 วินาที | เหตุการณ์กระจุก (ปัญหาปัจจุบัน) |
| **No Overlap Rule** | Active-type ห้ามซ้อนกัน เว้นแต่ budget สูง (คืนท้ายๆ) | สถานการณ์ที่แก้ไม่ได้ |
| **Room Spread** | ห้ามใช้ห้องเดิม 2 ครั้งติด, ทุกห้องต้องถูกใช้อย่างน้อย 1 ครั้ง | ผู้เล่นจ้องห้องเดียว |
| **Type Spread** | ห้าม type ซ้ำ 2 ครั้งติด | ตอบคำเดิมซ้ำ (ปัญหาปัจจุบัน) |
| **Onboarding Window** | 20% แรกของคืนสะอาด ไม่มี glitch ไม่มี haunt | ผู้เล่นเปิดเกมมาเจอ overload |
| **Climax Rule** | 1 เหตุการณ์ tier สูงสุด บังคับให้อยู่ใน 25% ท้ายคืน | คืนที่จืดตอนจบ |
| **Solvability Check** | หลังสุ่มเสร็จ จำลองว่า "ผู้เล่นเก่งสุด" ทำได้กี่แต้ม ถ้าต่ำกว่า requiredScore → สุ่มใหม่ | คืนที่ชนะไม่ได้ |

### 3.3 Data Model ที่ต้องสร้าง (แทนที่ string ใน prefab)

```
AnomalyDefinition (ScriptableObject)          RoomDefinition (ScriptableObject)
├── displayName        "The Listener"         ├── roomId          "basement"
├── correctKeywords[]  {Listener, Ears}       ├── displayName     "Basement"
├── prefab                                    ├── cameraX         36.12
├── respondType                               ├── spawnPoints[]
├── tier / threatCost  3                      └── manualEntry
├── allowedRooms[]
├── minNightIndex      2      ← ไม่โผล่คืนแรก
├── fieldManualEntry   (ภาพ + คำบรรยาย)
└── linkedHauntLoop    HL-3
```

**สิ่งที่หายไปพร้อมกัน:** `GameManager.CameraPositionsX` ที่ hardcode, `correctAnomalyType`/`correctLocationName` ที่เป็น string ใน prefab, และ `AnomalyOptionsCatalog` ที่ต้อง sync มือ

### 3.4 Seed UI (ของแถมที่ขายได้บน Steam)

- แสดง seed ของคืนที่เล่นอยู่บนหน้า Result
- ปุ่ม **"Replay this seed"** และ **"Custom seed"**
- ผู้เล่นแชร์ seed โหดๆ กันในฟอรัม/Discord = user-generated content ฟรี
- ต้นทุนพัฒนา ~4 ชม. ผลตอบแทนด้าน retention สูงมาก

---

## 4. Road-Map 8 Sprint (โครงสร้างพร้อมใส่ Jira)

**โครงสร้างที่แนะนำใน Jira:**
`Epic` = ตารางข้างล่างแต่ละแถวใหญ่ | `Story` = แต่ละบรรทัดงาน | `Sub-task` = แตกเอง
Label ที่ควรมี: `steam-ops` `procedural` `haunt-loop` `content` `polish` `blocker`

---

### 🔴 Sprint 1 (สัปดาห์ 1) — Unblock & Foundation
**เป้าหมาย Sprint:** เกมเล่นจนชนะได้ + วางฐานข้อมูลใหม่ + นาฬิกา Steam เริ่มเดิน

| Epic | Story | ชม. | Acceptance Criteria |
|---|---|---|---|
| **EP-01 Steam Ops** | S-101 สมัคร Steamworks + จ่าย Steam Direct fee | 3 | ได้รับ App ID, นาฬิกา 30 วันเริ่มเดิน |
| | S-102 กรอกข้อมูลภาษี/ธนาคาร (ใช้เวลา verify) | 2 | สถานะ payee ผ่าน |
| **EP-02 Critical Fixes** | S-103 แก้ `winThreshold` ให้เกมชนะได้ (ชั่วคราว) | 1 | เล่นเก่งสุด → WIN |
| | S-104 คืนค่า GlitchScheduler ที่เป็นค่าเทสต์ | 1 | glitch กระจายทั้งคืน |
| | S-105 กระจาย anomaly timeline ใหม่ | 2 | ไม่มีเหตุการณ์ห่างกัน < 25 วิ |
| | S-106 เรียก `RegisterReportResult()` ใน `SubmitReport()` | 1 | `ConsecutiveFailures` เปลี่ยนค่าจริง |
| | S-107 แก้ `GlitchStateSource.ReportCount` ให้นับรายงานที่ยื่น | 1 | scripted beat ยิงตามที่ตั้ง |
| | S-108 ปิด `debugHotkeys`, ลบ scene ซ้ำใน Build Settings | 1 | F1-F6 ไม่ทำงานในบิลด์ |
| | S-109 **commit งานค้าง 919 ไฟล์ + ตั้ง .gitignore ให้ถูก** | 2 | working tree สะอาด |
| **EP-03 Data Model** | S-110 สร้าง `AnomalyDefinition` ScriptableObject + migrate 7 ตัว | 6 | prefab ไม่มี string ค้างแล้ว |
| | S-111 สร้าง `RoomDefinition` SO + `RoomRegistry` | 5 | `CameraPositionsX` ถูกลบ |
| | S-112 `GameFlowManager` — ย้าย scene loading ออกจาก Anomaly/Demon | 6 | ไม่มี `LoadScene` ใน `Anomaly.cs` |
| | S-113 Refactor `Anomaly.cs` แยกเป็น movement/presentation/state | 8 | แต่ละ class < 150 บรรทัด |
| | **รวม** | **39** | |

---

### 🟠 Sprint 2 (สัปดาห์ 2) — Procedural Core ⭐
**เป้าหมาย Sprint:** ทุกคืนไม่เหมือนกัน และ reproducible จาก seed

| Epic | Story | ชม. | Acceptance Criteria |
|---|---|---|---|
| **EP-04 NightPlan** | S-201 โครงสร้าง `NightPlan` + `NightSeed` | 4 | serialize/deserialize ได้ |
| | S-202 `NightPlanGenerator` — สุ่ม anomaly (type + ห้อง + เวลา) | 10 | seed เดิม → แผนเดิม 100% |
| | S-203 Constraint solver (budget/spacing/spread/climax/onboarding) | 8 | กฎทั้ง 8 ข้อใน §3.2 ผ่าน unit test |
| | S-204 Solvability check + auto re-roll | 4 | สุ่ม 1000 seed ไม่มีคืนที่ชนะไม่ได้ |
| | S-205 `requiredScore` คำนวณจากแผน (ลบ winThreshold ทิ้ง) | 2 | ปรับ schedule แล้ว threshold ตามอัตโนมัติ |
| **EP-05 Wiring** | S-206 `AnomalyScheduler` รับ NightPlan (คงโหมด manual ไว้ debug) | 4 | โหมดเดิมยังใช้เทสต์ได้ |
| | S-207 `GlitchScheduler` + `GlitchProfile` รับ NightPlan | 4 | น้ำหนัก glitch ต่างกันตามคืน |
| | S-208 ปรับ blackout rule (แก้ปัญหา §4.8 — Passive บล็อกเกือบตลอด) | 2 | ambient glitch ยิงจริงตอนกรอกฟอร์ม |
| | S-209 Debug: `Dump Night Plan` + Gizmo timeline | 3 | ดูแผนทั้งคืนได้โดยไม่ต้องเล่น |
| | **รวม** | **41** | |

---

### 🟡 Sprint 3 (สัปดาห์ 3) — Anomaly Content & Field Manual ⭐
**เป้าหมาย Sprint:** เกมมี "ความรู้ที่ต้องเรียน" ไม่ใช่แค่ปฏิกิริยา

| Epic | Story | ชม. | Acceptance Criteria |
|---|---|---|---|
| **EP-06 Anomaly Types** | S-301 ออกแบบ anomaly 8 ชนิด (สเปค + วิธีแยกด้วยสายตา) | 5 | เอกสารสเปคครบ 8 ตัว |
| | S-302 ผลิต/ดัดแปลง sprite + animation 8 ตัว | 14 | แยกจากกันได้ในภาพ 1 เฟรม |
| | S-303 กรอก `AnomalyDefinition` ครบ 8 (keyword ไม่ซ้ำ) | 3 | ไม่มีตัวไหนใช้คำว่า "Shadow" ซ้ำ |
| **EP-07 Rooms** | S-304 เพิ่มห้องเป็น 5 ห้อง (จาก 3) + ตำแหน่งกล้อง | 8 | สลับกล้องได้ 5 ห้อง |
| | S-305 เปิด `requireCorrectLocation` + validation ใหม่ | 3 | เลือกห้องผิด = รายงานผิด |
| **EP-08 Field Manual** | S-306 UI คู่มือในเกม (เปิดได้ระหว่างเล่น, ไม่ pause) | 8 | กด TAB เปิด/ปิดได้ |
| | S-307 เนื้อหาคู่มือ 8 หน้า (ภาพ + วิธีสังเกต + คำที่ต้องพูด) | 4 | ผู้เล่นใหม่แยก anomaly ได้ |
| | **รวม** | **45** ⚠️ | เกินงบ 5 ชม. — ดู Cut list |

> ⚠️ **Sprint นี้เสี่ยงที่สุด** เพราะเป็นงาน art ซึ่งประเมินยากสำหรับ programmer solo
> **แผนสำรอง:** ลดเหลือ 6 anomaly types และ 4 ห้อง → ลด ~10 ชม. ทันที (คุณภาพเกมแทบไม่ต่าง)

---

### 🟣 Sprint 4 (สัปดาห์ 4) — Haunt Framework + Silence Protocol + Store Page
**เป้าหมาย Sprint:** กลไกไมค์ตัวเอกทำงาน + store page ส่งรีวิวทัน deadline

| Epic | Story | ชม. | Acceptance Criteria |
|---|---|---|---|
| **EP-09 Haunt Framework** | S-401 `HauntDirector` + `HauntBeat` (โครงเลียนแบบ GlitchDirector) | 8 | เพิ่ม haunt ใหม่ได้โดยไม่แตะ core |
| | S-402 `VoicePromptSystem` (generalize VoiceCommandRouter เดิม) | 5 | Roll Call + report ใช้ระบบเดียวกัน |
| **EP-10 HL-3 Silence** | S-403 `MicAmplitudeMonitor` — RMS แบบ realtime ไม่ผ่าน Whisper | 5 | ค่า RMS ถูกต้อง, ไม่กิน CPU |
| | S-404 หน้า calibrate ไมค์ (วัด noise floor ห้องผู้เล่น) | 4 | ห้องเงียบ/ห้องดัง ตั้งค่าได้ทั้งคู่ |
| | S-405 The Listener: anomaly + VU meter HUD + 3-strike + ไฟดับ | 8 | เสียงดัง 2 วิ = strike, กระซิบผ่าน |
| **EP-01 Steam Ops** | S-406 Capsule art ทุกขนาด + screenshot 5 ใบ | 6 | ผ่านสเปคของ Valve |
| | S-407 เขียนคำบรรยาย/แท็ก/ระบบเรตติ้ง + **ส่ง store page รีวิว** | 4 | 🚩 **ส่งภายในสิ้นสัปดาห์นี้** |
| | **รวม** | **40** | |

---

### 🔵 Sprint 5 (สัปดาห์ 5) — Radio Check + Camera Betrayal
**เป้าหมาย Sprint:** ไมค์มีชีวิตตลอดคืน + ความไม่น่าเชื่อถือลามจากฟอร์มไปที่กล้อง

| Epic | Story | ชม. | Acceptance Criteria |
|---|---|---|---|
| **EP-11 HL-4 Radio** | S-501 Radio Check core (ping + timer 8 วิ + STT ตรวจคำตอบ) | 6 | ตอบทัน = ผ่าน, ไม่ตอบ = strike |
| | S-502 บันทึกเสียงผู้เล่น + playback (variant "เสียงตัวเอง") | 6 | ได้ยินเสียงตัวเองเรียกตัวเอง |
| | S-503 Variants อีก 3 แบบ + negligence strike system | 4 | 3 strikes → haunt event บังคับ |
| **EP-12 HL-5 Camera** | S-504 `CameraFeedController` (โครง executor แบบ FormGlitchController) | 6 | revert ได้เสมอ ไม่ค้าง |
| | S-505 Variant: Loop, Frozen, Blackout | 6 | จับผิดได้จาก timestamp/grain |
| | S-506 Variant: Ghost Room, Mirror | 6 | กล้องตัวที่ 6 โผล่แล้วหายได้ |
| **EP-01 Steam Ops** | S-507 ตัด trailer 60 วิ + อัปโหลด | 5 | 🚩 **store page live ภายในสัปดาห์นี้** |
| | **รวม** | **39** | |

---

### 🟢 Sprint 6 (สัปดาห์ 6) — Impostor Case + Meta Layer
**เป้าหมาย Sprint:** เกมมีคืนหลายคืนและมีเหตุผลให้เล่นซ้ำ

| Epic | Story | ชม. | Acceptance Criteria |
|---|---|---|---|
| **EP-13 HL-6/HL-7** | S-601 Impostor Case (case กระโดด / ฟอร์มกรอกไว้แล้ว / เปลี่ยน officer) | 7 | ยิงจาก HauntDirector ได้ |
| | S-602 กู้ "Give Me A Sign" เป็น hint ที่มีราคา (3 ครั้ง/คืน) | 6 | พูดแล้วได้ hint + intensity ขึ้น |
| **EP-14 Progression** | S-603 ระบบคืน 1-5 + `DifficultyProfile` ต่อคืน | 8 | คืนหลังยากขึ้นจริง วัดได้ |
| | S-604 คืนที่ 1 = tutorial (ใช้ `SetFlag("tutorial")` ที่มีอยู่แล้ว) | 4 | ผู้เล่นใหม่ผ่านคืน 1 ได้ |
| | S-605 Save progression (คืนที่ปลดล็อกแล้ว) | 3 | ปิดเกมแล้วเปิดใหม่ไม่หาย |
| **EP-15 UX** | S-606 Death sequence (fade + jumpscare + บอกสาเหตุการตาย) | 5 | ไม่ตัดจอทันทีอีกต่อไป |
| | S-607 Result breakdown + seed + ปุ่ม Replay seed | 4 | เห็นว่าพลาดตรงไหน |
| | S-608 Pause + Options (bind AudioManager) + เลือกไมค์ | 6 | ปรับเสียง/ไมค์ได้ในเกม |
| | **รวม** | **43** ⚠️ | ล้น 3 ชม. — ดู Cut list |

---

### 🟤 Sprint 7 (สัปดาห์ 7) — Polish, Audio/VFX, Playtest
**เป้าหมาย Sprint:** เกมรู้สึกเหมือนของจริง ไม่ใช่ prototype

| Epic | Story | ชม. | Acceptance Criteria |
|---|---|---|---|
| **EP-16 Playtest** | S-701 **Playtest กับคนนอก 5-8 คน** (บันทึกหน้าจอ + เสียง) | 8 | ได้ note ทุกคน |
| | S-702 Balance pass จากข้อมูล playtest | 6 | win rate ~40-60% ในคืน 1-2 |
| **EP-17 Audio** | S-703 Ambient horror layer (ใช้ whisper WAV 7 ไฟล์ที่มีอยู่) | 6 | มีเสียงตลอดคืน ไม่เงียบเก้อ |
| | S-704 เสียงต่อ haunt loop + mix pass | 5 | ทุก haunt มีเสียงประจำตัว |
| **EP-18 Visual** | S-705 VHS post-processing + transition (มี video asset แล้ว) | 6 | ภาพรวมเป็น VHS horror |
| **EP-19 Voice** | S-706 อัปเกรด Whisper เป็น `ggml-base.en` + จูน threshold | 5 | ความแม่นยำสูงขึ้นวัดได้ |
| | S-707 Fallback ไม่มีไมค์ / ไม่ได้ permission → โหมดพิมพ์ | 4 | เล่นจบได้โดยไม่มีไมค์ |
| | **รวม** | **40** | |

---

### ⚫ Sprint 8 (สัปดาห์ 8) — Ship
**เป้าหมาย Sprint:** เกมอยู่บน Steam

| Epic | Story | ชม. | Acceptance Criteria |
|---|---|---|---|
| **EP-01 Steam Ops** | S-801 Steamworks SDK + build depot + branch setup | 6 | อัปโหลด build ผ่าน |
| | S-802 **ส่ง build ให้ Valve รีวิว** | 2 | 🚩 **ส่งวันจันทร์ต้นสัปดาห์** |
| | S-803 Steam achievements 10-15 อัน (optional แต่คุ้ม) | 5 | ปลดล็อกได้จริง |
| | S-804 แก้ตามที่ Valve ตีกลับ | 4 | ผ่านรีวิว |
| **EP-20 Ship** | S-805 Bugfix จาก playtest (คิว P0/P1) | 12 | ไม่มีบั๊ก P0 ค้าง |
| | S-806 Build final + smoke test บนเครื่องสะอาด | 4 | ติดตั้งแล้วเล่นได้ทันที |
| | S-807 หน้า store final + วันปล่อย + ประกาศ | 3 | 🚀 **LAUNCH** |
| | Buffer สำรอง | 4 | |
| | **รวม** | **40** | |

---

## 5. งบประมาณเวลารวม

| Sprint | ธีม | ชม. | สะสม |
|---|---|---|---|
| 1 | Unblock & Foundation | 39 | 39 |
| 2 | Procedural Core ⭐ | 41 | 80 |
| 3 | Anomaly Content ⭐ | 45 ⚠️ | 125 |
| 4 | Haunt Framework + Silence | 40 | 165 |
| 5 | Radio + Camera Betrayal | 39 | 204 |
| 6 | Impostor + Meta Layer | 43 ⚠️ | 247 |
| 7 | Polish & Playtest | 40 | 287 |
| 8 | Ship | 40 | 327 |

**รวม 327 ชม. / งบ 320 ชม. → เกิน 7 ชม. (2%)**

แผนนี้ **ตึงมากและไม่มี buffer จริง** งาน solo ที่ประเมินแบบนี้มักบานปลาย 20-30% ดังนั้นให้ถือ Cut list ข้างล่างเป็นส่วนหนึ่งของแผน ไม่ใช่แผนสำรอง

---

## 6. Cut List (เรียงลำดับการตัด — ตัดจากบนลงล่าง)

ถ้าถึงสิ้น Sprint 5 แล้วช้ากว่าแผน ให้ตัดตามลำดับนี้ทันที **อย่าลังเล**

| ลำดับ | ตัดอะไร | ประหยัด | ผลกระทบ |
|---|---|---|---|
| 1 | Steam achievements (S-803) | 5 ชม. | เพิ่มทีหลังผ่าน patch ได้ |
| 2 | ลด anomaly จาก 8 → 6 ชนิด | 6 ชม. | แทบไม่รู้สึก |
| 3 | ลดห้องจาก 5 → 4 ห้อง | 4 ชม. | แทบไม่รู้สึก |
| 4 | HL-5 ตัด variant Ghost Room + Mirror | 6 ชม. | เหลือ 3 variant ก็ยังดี |
| 5 | ลดคืนจาก 5 → 3 คืน | 6 ชม. | ยังมี progression |
| 6 | **HL-6 Impostor Case ทั้งอัน** | 7 ชม. | เก็บไว้เป็น content update ตอนใส่ story |
| 7 | โหมดพิมพ์แทนไมค์ (S-707) | 4 ชม. | ⚠️ กระทบ accessibility — ตัดเป็นอันสุดท้าย |

**ห้ามตัดเด็ดขาด:**
- 🔒 EP-04/05 (Procedural) — คือเหตุผลที่เกมมี replayability
- 🔒 HL-3 Silence Protocol — คือ hook ที่ทำให้เกมนี้ต่างจากเกมอื่นบน Steam
- 🔒 HL-7 Give Me A Sign — คือชื่อเกม
- 🔒 EP-08 Field Manual — ถ้าไม่มี ระบบ anomaly type ทั้งหมดจะไร้ความหมายเหมือนตอนนี้
- 🔒 S-701 Playtest — ปล่อยเกมโดยไม่มีคนนอกเล่นคือการฆ่าตัวตายบน Steam

---

## 7. ความเสี่ยงหลักและแผนรับมือ

| # | ความเสี่ยง | ระดับ | แผนรับมือ |
|---|---|---|---|
| 1 | **พลาด deadline ของ Steam** (fee/store page) | 🔴 สูงสุด | จ่าย fee **วันแรก** ตั้งเตือนปฏิทิน 3 จุด: จ่าย fee / ส่ง store page / ส่ง build |
| 2 | **Sprint 3 (งาน art) บานปลาย** | 🔴 สูง | เตรียม cut ลงเหลือ 6 types ไว้ล่วงหน้า; พิจารณาจ้าง freelance sprite 1 ชุด |
| 3 | **Whisper แม่นยำไม่พอ** ในสภาพจริง | 🟠 กลาง | S-706 อัปเกรดโมเดล + S-707 โหมดพิมพ์เป็นตาข่ายนิรภัย |
| 4 | **Silence Protocol ใช้ไม่ได้จริง** (ห้องผู้เล่นเสียงดัง) | 🟠 กลาง | S-404 calibration บังคับ + accessibility toggle + ทดสอบใน playtest ก่อนใคร |
| 5 | Valve ตีกลับ build | 🟠 กลาง | ส่ง build **ต้น** Sprint 8 ไม่ใช่ปลาย เผื่อรอบแก้ 1 รอบ |
| 6 | Procedural สร้างคืนที่ไม่สนุก | 🟡 ต่ำ | S-204 solvability check + สุ่ม 1000 seed ตรวจสถิติอัตโนมัติ |
| 7 | Scope creep จากไอเดีย story | 🟡 ต่ำ | **story = หลัง launch** เขียนไอเดียลง backlog ห้ามแตะใน 8 สัปดาห์นี้ |
| 8 | หมดไฟ (solo 8 สัปดาห์เต็ม) | 🟠 กลาง | Sprint 7 มี playtest = ได้เห็นคนเล่นจริง ใช้เป็นเชื้อเพลิง; หยุด 1 วันเต็มทุกสัปดาห์ |

---

## 8. Milestone ที่ใช้วัดผลได้จริง

| สิ้นสุด | Milestone | เกณฑ์ผ่าน (ทดสอบได้) |
|---|---|---|
| Sprint 1 | **Playable & Winnable** | เล่นเก่งสุด → เห็นหน้า WIN; working tree สะอาด; Steam App ID มาแล้ว |
| Sprint 2 | **Every Night Is Different** | เล่น 3 คืนติด ได้แผนต่างกัน 3 แบบ; ใส่ seed เดิม → ได้คืนเดิมเป๊ะ |
| Sprint 3 | **Knowledge Game** | คนที่ไม่เคยเล่นมาก่อน เปิดคู่มือแล้วแยก anomaly ได้ 6/8 ตัว |
| Sprint 4 | **The Hook Works** | ให้คนนอกลอง Silence Protocol 1 คน แล้วเขา *กระซิบจริงๆ* |
| Sprint 5 | **Content Complete (loops)** | Haunt loop ทั้ง 5-6 ตัวยิงได้จาก NightPlan |
| Sprint 6 | **Feature Complete** | เล่นได้ตั้งแต่เมนู → คืน 1-5 → จบเกม โดยไม่ต้องแตะ Inspector |
| Sprint 7 | **Release Candidate** | คนนอก 5+ คนเล่นจบ ไม่มีบั๊ก P0; store page live ครบ 14 วันแล้ว |
| Sprint 8 | **Shipped** | 🚀 |

---

## 9. สรุปสั้นที่สุด

1. **จ่าย Steam Direct fee ภายใน 3 วันแรก** — ทุกอย่างอื่นแก้ทีหลังได้ แต่นาฬิกา 30 วันของ Valve แก้ไม่ได้
2. **หัวใจของ 2 เดือนนี้คือ Sprint 2-4** — Procedural (replayability), Anomaly types + Field Manual (ความลึก), Silence Protocol (จุดขาย) สามอย่างนี้คือเกมที่คุณกำลังจะขาย ที่เหลือคือส่วนประกอบ
3. **แทนที่การสวดมนต์ด้วย Silence Protocol + Radio Check** — เปลี่ยนไมค์จาก "ปุ่มแก้ปัญหา" เป็น "ช่องทางที่ผีเข้าถึงคุณ" โค้ดสวดมนต์เดิมไม่ทิ้ง แต่ generalize เป็น `VoicePromptSystem`
4. **แผนนี้เกินงบ 2% และไม่มี buffer** — ให้ถือ Cut list ใน §6 เป็นส่วนหนึ่งของแผน ตัดตั้งแต่เห็นสัญญาณ อย่ารอให้สายเกินไป
5. **Story = หลัง launch** — เขียนไอเดียลง backlog ได้ แต่ห้ามแตะใน 8 สัปดาห์นี้ HL-6 Impostor Case ถูกออกแบบมาให้เป็นสะพานเชื่อมไปหา story อยู่แล้ว

---

**Sources (ข้อมูล Steam):**
- [Onboarding — Steamworks Documentation](https://partner.steamgames.com/doc/gettingstarted/onboarding)
- [Release Process — Steamworks Documentation](https://partner.steamgames.com/doc/store/releasing)
- [Coming Soon — Steamworks Documentation](https://partner.steamgames.com/doc/store/coming_soon)
- [Review Process — Steamworks Documentation](https://partner.steamgames.com/doc/store/review_process)
- [How to Publish Your Game on Steam in 2026](https://www.thegamemarketer.com/insight-posts/how-to-publish-your-game-on-steam-guide)
