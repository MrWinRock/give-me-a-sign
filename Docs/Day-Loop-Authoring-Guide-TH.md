# คู่มือแก้ระบบวัน / VDO / Minigame ด้วยตัวเอง

เอกสารนี้บอกว่า **ถ้าอยากเพิ่ม/ลดอะไร ต้องไปแตะตรงไหน** ทุกอย่างแก้ได้จาก Inspector หรือจากไฟล์
Asset ไม่ต้องแก้โค้ด

---

## 0. ต้องวางอะไรในซีนบ้าง (ทำครั้งเดียว)

### ซีน `StartScene` (เมนู) และ `GamePlay`

| GameObject | Component ที่ต้องมี | หมายเหตุ |
|---|---|---|
| `GameFlowManager` | `GameFlowManager` | **ต้องอยู่บน GameObject เปล่าๆ ของตัวเอง** เพราะติ๊ก Persist Across Scenes ไว้ (มันจะ DontDestroyOnLoad ทั้งก้อน) |
| (ก้อนเดียวกันได้) | `RandomEventDirector` | ตัวสุ่ม event ท้ายวัน |
| (ก้อนเดียวกันได้) | `DayEventPlayer` | ตัวเล่นวิดีโอ |
| (ก้อนเดียวกันได้) | `EndingSequenceController` | ฉากจบวัน 7 (ตอนนี้เป็น stub) |

> วางไว้ที่ซีนไหนก็ได้ที่โหลดก่อน เพราะ `GameFlowManager` อยู่ข้ามซีนอยู่แล้ว
> ถ้าไม่อยากให้ข้ามซีน ให้ติ๊ก **Persist Across Scenes ออก** แล้ววางไว้ทุกซีนแทน

### ซีน `GamePlay` เพิ่มเติม

| GameObject | Component | หมายเหตุ |
|---|---|---|
| ก้อนไหนก็ได้ | `AnomalyOverloadWatcher` | เงื่อนไขแพ้แบบ anomaly ล้น ถ้าไม่วาง = ไม่มีเงื่อนไขนี้ |

---

## 1. เพิ่ม / ลด "วัน"

### เปลี่ยนจำนวนวันทั้งหมด (ตอนนี้ 7 วัน)

**2 ที่ ต้องแก้ให้ตรงกัน:**

1. **`GameFlowManager` → ช่อง `Final Day`** — วันสุดท้ายก่อนขึ้นฉากจบ
2. **`Assets/Settings/DifficultyProfile.asset` → list `Nights`** — เพิ่มแถวใหม่ให้ครบ

> ถ้า `Final Day` = 10 แต่ตาราง Nights มีแค่ 7 แถว → วัน 8-10 จะไม่พัง แต่จะไปใช้
> **สูตรเชิงเส้น** (base + growth) แทน ซึ่งจะตันที่เพดานเวลา แนะนำให้เพิ่มแถวให้ครบ

### เพิ่มวันที่ 8

1. เปิด `Assets/Settings/DifficultyProfile.asset`
2. กาง `Nights` → กด **+** → ตั้ง `Night Index` = 8
3. กรอกค่าที่ต้องการ (ดูตารางข้อ 2)
4. ไปที่ `GameFlowManager` → `Final Day` = 8

### ลบวัน

ลบแถวออกจาก `Nights` แล้วลด `Final Day` ตาม — **ห้ามลืมลด `Final Day`** ไม่งั้นวันที่ไม่มีแถวจะ
ตกไปใช้สูตรเชิงเส้นเงียบๆ

---

## 2. ปรับความยากแต่ละวัน

`Assets/Settings/DifficultyProfile.asset` → `Nights` → แถวของวันนั้น

| ช่อง | ความหมาย | ใส่ `0` หมายถึง |
|---|---|---|
| `Night Index` | วันที่เท่าไหร่ (1 = วันแรก) | — |
| `Notes` | โน้ตของคุณเอง เกมไม่อ่าน | — |
| `Night Duration Minutes` | คืนยาวกี่นาทีจริง (**คันโยกสำคัญสุด** — ยาวขึ้น = ใส่ anomaly ได้มากขึ้น) | ใช้ค่าใน NightTimer ของซีน |
| `Threat Budget` | งบซื้อ anomaly (แพงตามราคา `threatCost` ของแต่ละตัว) | ใช้สูตร base+growth |
| `Win Ratio` | ต้องเก็บกี่ % ถึงรอด (0.7 = 70%) | ใช้ค่ากลาง |
| `Minimum Spacing Seconds` | anomaly ห่างกันขั้นต่ำกี่วิ (น้อย = ถี่ = ยาก) | ใช้ค่ากลาง |
| `Glitch Count` | ฟอร์มโกหกกี่ครั้ง | **ต้องใส่ `-1`** ถ้าอยากใช้สูตร (`0` = ไม่มีเลย) |
| `Penalty Anomalies Per Wrong Report` | ตอบผิด 1 ครั้ง เกิด anomaly เพิ่มกี่ตัว | **ต้องใส่ `-1`** ถ้าอยากใช้ค่ากลาง (`0` = ตอบผิดฟรี) |
| `Demon Timeout Seconds` | ปีศาจโผล่แล้วมีเวลากี่วิก่อนแพ้ | ใช้ค่ากลาง |
| `Max Concurrent Anomalies` | มี anomaly ค้างได้กี่ตัว **เกินกว่านี้** เริ่มจับเวลา | ใช้ค่ากลาง |
| `Overload Duration Seconds` | ค้างเกินติดต่อกันกี่วิถึงแพ้ (**หล่นกลับมาเมื่อไหร่ รีเซ็ตทันที**) | ใช้ค่ากลาง |

> ⚠️ **ทำไม 2 ช่องใช้ `-1` ไม่ใช่ `0`** — เพราะ `0` เป็นค่าที่มีความหมายจริง
> (`glitchCount: 0` = ไม่มีกลิตช์เลย ซึ่งวัน 1 ต้องการ) ถ้าใช้ `0` เป็น "ไม่ได้ตั้ง" จะแยกไม่ออก

**เช็กผลลัพธ์:** คลิกที่ `DifficultyProfile.asset` → กดปุ่ม **Log Campaign Curve** ใน Inspector
→ Console จะพิมพ์ตารางทั้ง 7 วันออกมาว่าค่าจริงเป็นเท่าไหร่ และมาจากตารางหรือสูตร

### ค่าที่ตั้งไว้ตอนนี้

| วัน | ยาว | งบ | ต้องได้ | Glitch | โทษตอบผิด | Demon | Anomaly ค้างได้ | ล้นนานแค่ไหนถึงแพ้ |
|---|---|---|---|---|---|---|---|---|
| 1 สอนเล่น | 4 น. | 4 | 50% | 0 | 0 | 45s | 4 | 180s |
| 2 | 5 น. | 7 | 60% | 2 | 1 | 40s | 4 | 150s |
| 3 | 6 น. | 10 | 65% | 4 | 1 | 35s | 3 | 130s |
| 4 | 7 น. | 14 | 70% | 6 | 2 | 32s | 3 | 120s |
| 5 | 8 น. | 18 | 75% | 8 | 2 | 30s | 3 | 110s |
| 6 | 9 น. | 21 | 78% | 10 | 2 | 26s | 3 | 95s |
| 7 จบ | 10 น. | 24 | 80% | 12 | 3 | 22s | 3 | 80s |

---

## 3. เพิ่ม / ลด Short VDO

### VDO ที่มีอยู่แล้ว

ผมสร้าง Asset ให้จากวิดีโอใน `Assets/Sprites/VDO/` แล้ว 4 ตัว:

```
Assets/Settings/DayEvents/VDO_001.asset  ->  001.mp4
Assets/Settings/DayEvents/VDO_002.asset  ->  002.mp4
Assets/Settings/DayEvents/VDO_003.asset  ->  003.mp4
Assets/Settings/DayEvents/VDO_004.asset  ->  004.mp4
```

### เพิ่ม VDO ใหม่

1. เอาไฟล์ `.mp4` ใส่ใน `Assets/Sprites/VDO/` (หรือที่ไหนก็ได้)
2. คลิกขวาในโปรเจกต์ → **Create ▸ Give Me A Sign ▸ Day Event ▸ Short VDO**
3. เซฟไว้ที่ `Assets/Settings/DayEvents/`
4. กรอก:
   - `Event Id` — **คีย์ถาวร ห้ามซ้ำ ห้ามเปลี่ยนทีหลัง** (เซฟเก็บ id นี้ว่า "ดูไปแล้ว")
   - `Display Name` — ชื่อโชว์ใน log เฉยๆ
   - `Clip` — ลากไฟล์วิดีโอมาใส่
   - `Skippable After Seconds` — กี่วิถึงกดข้ามได้ (`0` = ข้ามไม่ได้)
5. ไปที่ `RandomEventDirector` ในซีน → **คลิกขวาที่หัว component → `Find All Events In Project`**
   (มันจะกวาดหา VDO/Minigame ทั้งโปรเจกต์มาใส่ list ให้เอง) หรือลากใส่ `Short Vdo Pool` เองก็ได้

### ลบ VDO

ลบ `.asset` ทิ้ง แล้วกด `Find All Events In Project` ใหม่ (หรือลบออกจาก list เอง)

### ⚠️ ถ้าช่อง Clip ขึ้นว่า `None`

ผมสร้างไฟล์ `.asset` ด้วยมือนอก Unity ถ้า Unity อ่าน reference วิดีโอไม่เจอ
ให้**ลากไฟล์ `.mp4` ใส่ช่อง `Clip` ใหม่เอง** ครั้งเดียว แล้วมันจะติดถาวร

---

## 4. เพิ่ม Minigame (ตอนนี้ยังไม่มี)

โครงพร้อมแล้ว รอแค่ตัวเกม

1. คลิกขวา → **Create ▸ Give Me A Sign ▸ Day Event ▸ Minigame**
2. กรอก:
   - `Event Id` — คีย์ถาวร ห้ามซ้ำ
   - `Display Name`
   - **`Prefab` หรือ `Scene Name` — ใส่ได้อย่างใดอย่างหนึ่งเท่านั้น** (ใส่ทั้งคู่หรือไม่ใส่เลย =
     ระบบจะข้ามตัวนี้ไป)
   - `Time Limit Seconds` — จำกัดเวลา (`0` = ไม่จำกัด)
3. กด `Find All Events In Project` ที่ `RandomEventDirector`

> **สิ่งที่ยังต้องเขียนโค้ดเพิ่ม:** ตัวรัน minigame จริง อยู่ใน
> `GameFlowManager.PlayDayEndEvent()` ตรงสาขา `DayEventType.Minigame` ตอนนี้แค่ log แล้วข้าม
> รูปแบบที่ต้องทำเหมือนสาขา VDO เป๊ะๆ คือ เล่น → รอจนจบ → `yield break`

---

## 5. ปรับการสุ่ม event ท้ายวัน

`RandomEventDirector` ในซีน

| ช่อง | ความหมาย |
|---|---|
| `Event Chance Per Day` | array ความน่าจะเป็น **index 0 = วัน 1** ค่า 0-1 (0.5 = 50%) วันที่เกินความยาว array จะใช้ค่าสุดท้าย |
| `Short Vdo Weight` | น้ำหนัก VDO เทียบ Minigame — `1` = VDO ล้วน, `0` = Minigame ล้วน, `0.5` = ครึ่งๆ |
| `Short Vdo Pool` | list ของ VDO ทั้งหมด |
| `Minigame Pool` | list ของ Minigame ทั้งหมด |
| `Always Roll Event` | (debug) บังคับให้ผ่านด่านสุ่มชั้นแรกเสมอ |
| `Show Debug Info` | log ผลการสุ่มลง Console |

**ค่าปัจจุบัน:** `{0, 0.5, 0.5, 0.6, 0.6, 0.7, 1}` → วัน 1 ไม่มี event, วัน 7 มีแน่นอน

### กฎการสุ่มที่ต้องรู้

1. สุ่มชั้นแรก: วันนี้มี event ไหม
2. ถ้ามี → สุ่มชั้นสอง: VDO หรือ Minigame (**ตัดสินใจแล้วไม่เปลี่ยน**)
3. หยิบแบบสุ่มจริงจากตัวที่ยังไม่เคยดู (ไม่ใช่เรียง 1,2,3,4)
4. **ถ้า pool ที่สุ่มได้หมดแล้ว → วันนั้นไม่มี event เลย** ไม่ย้ายไป pool อื่น ไม่เล่นซ้ำ
5. mark ว่า "ดูแล้ว" **ต่อเมื่อดูจบจริง** — ปิดเกมกลางคันไม่เสีย

**รีเซ็ตของที่ดูไปแล้ว:** คลิกขวาที่ `RandomEventDirector` → `Reset Consumed Events (save)`

---

## 6. ปรับหน้าจอเล่นวิดีโอ

`DayEventPlayer` ในซีน

| ช่อง | ความหมาย |
|---|---|
| `Canvas Sort Order` | ลำดับชั้น ต้องสูงกว่า HUD อื่น (default 900) |
| `Background Color` | สีขอบ/พื้นหลัง |
| `Video Resolution` | ความละเอียด render texture |
| `Preserve Aspect Ratio` | ติ๊ก = ใส่ขอบดำรักษาสัดส่วน, ไม่ติ๊ก = ยืดเต็มจอ |
| `Fade In / Out Seconds` | เวลาเฟด |
| `Skip Key` | ปุ่มข้าม (Escape / Space / Enter / AnyKey) |
| `Skip Prompt Format` | ข้อความ `{key}` จะถูกแทนด้วยชื่อปุ่ม |
| `Prompt Font` | ฟอนต์ (เว้นว่าง = ใช้ TMP default) — ใส่ Tahoma ได้ |
| `Skip Prompt Font Size / Color / Margin` | หน้าตาข้อความข้าม |
| `Apply Sfx Volume` | ให้เสียงวิดีโอตามสไลเดอร์ SFX |
| `Max Playback Seconds` | กันค้าง ถ้าวิดีโอเจ๊งจะบังคับจบที่เวลานี้ (`0` = ไม่จำกัด) |

---

## 7. เพิ่ม Anomaly ใหม่

ดู [Campaign-5-Nights-Guide-TH.md](Campaign-5-Nights-Guide-TH.md) หัวข้อ 5.1 (ยังใช้ได้เหมือนเดิม)
สรุปสั้นๆ:

1. ทำ prefab (copy จาก `Assets/Prefabs/Anomaly2Res1.prefab`)
2. คลิกขวา → **Create ▸ Give Me A Sign ▸ Anomaly Definition**
3. กรอก `anomalyId` / `correctKeywords` / `prefab` / `threatCost` / `minNightIndex`
4. **เพิ่มเข้า list `anomalies` ใน `Assets/Resources/NightContentLibrary.asset`** ← ลืมขั้นนี้ = ไม่มีวันโผล่

**ราคาและวันปลดล็อกตอนนี้:**

| Asset | ราคา | ปลดล็อกวัน |
|---|---|---|
| Anomaly2Res1 / Anomaly3Res1 | 1 | 1 |
| Anomaly5Res1 | 1 | 2 |
| Anomaly1Res2 | 2 | 2 |
| Anomaly4Res2 | 2 | 3 |
| **DemonAnomaly** | **4** | **3** |
| Anomaly6Res2 | 2 | 4 |
| Anomaly7Res2 | 3 | 5 |

---

## 8. ระบบเซฟ

ไฟล์อยู่ที่ `%USERPROFILE%\AppData\LocalLow\<Company>\<Product>\givemeasign.save.json`

เก็บ 3 อย่าง: `currentDay`, `consumedEventIds` (VDO/Minigame ที่ดูแล้ว), `readEmailIds`

| อยากทำอะไร | ทำยังไง |
|---|---|
| เริ่มใหม่หมด | ปุ่ม New Game (`DesktopAction.NewGame`) หรือลบไฟล์ save ทิ้ง |
| เทสต์วันใดวันหนึ่ง | `NightPlanRunner` → `Night Index Override` = เลขวัน (`0` = ใช้ค่าจากเซฟ) |
| เล่น seed เดิมซ้ำ | `NightPlanRunner` → `Seed Override` |
| รีเซ็ตแค่ VDO ที่ดูแล้ว | คลิกขวา `RandomEventDirector` → `Reset Consumed Events (save)` |

**เซฟตอนไหน:** ทุกครั้งที่ `AdvanceDay()` (คือตอนขึ้นวันใหม่) และตอน mark VDO ว่าดูจบ

> ⚠️ เซฟเก่าที่ใช้ PlayerPrefs `"UnlockedNight"` **เลิกใช้แล้ว** คนที่เคยเล่นจะเริ่มใหม่ที่วัน 1

---

## 9. ปุ่ม New Game (ใส่ให้แล้ว)

อยู่ที่ `MainMenuCanvas > StartMenu > Items > Item_NewGame` ต่อจาก Start Shift แล้ว
และผูกเข้า `StartMenuController.items` เรียบร้อย

- `StartShift` = เล่นต่อจากเซฟ
- `NewGame` = **ลบเซฟทันที** แล้วเริ่มวัน 1

แก้ข้อความได้ที่ component `XPMenuItem` บน `Item_NewGame`:
`Label String` = "New Game", `Subtitle String` = "Erase progress and start over"

> ⚠️ **ยังไม่มีหน้าต่างยืนยัน** — กดแล้วลบเซฟทันที ถ้าอยากให้ถามก่อน ให้เปลี่ยน
> `DesktopAction.NewGame` ไปเปิดหน้าต่างยืนยัน แล้วให้ปุ่ม OK ในหน้าต่างนั้นเรียก
> `DesktopManager.NewGame()` แทน

> ⚠️ **`DesktopAction` ห้ามแทรกค่าใหม่ตรงกลาง enum** — ค่าพวกนี้ถูกเซฟเป็นตัวเลขในไฟล์ซีน
> ถ้าแทรกตรงกลาง เมนูเดิมทุกอันจะเลื่อนไปชี้ผิดปุ่มหมด ต้อง **ต่อท้ายเท่านั้น**
> (`NewGame` เลยเป็นค่า 9 ไม่ใช่ 2)

---

## 9b. GameFlowManager ในซีน (ใส่ให้แล้ว)

อยู่เป็น root object ชื่อ `GameFlowManager` ใน `MainMenu.unity` มี 4 component:

| Component | ค่าที่ตั้งไว้ |
|---|---|
| `GameFlowManager` | Result/MainMenu/GamePlay scene names, Final Day = 7, Persist Across Scenes = ติ๊ก |
| `RandomEventDirector` | โอกาสต่อวัน `{0, .5, .5, .6, .6, .7, 1}`, VDO Weight = 1 (ยังไม่มี minigame), pool ใส่ VDO ครบ 4 ตัว |
| `DayEventPlayer` | ฟอนต์ Tahoma, ปุ่มข้าม = Escape, กันค้าง 300 วิ |
| `EndingSequenceController` | placeholder 2 วิ |

> **`Short Vdo Weight` ตั้งเป็น 1 ไว้ก่อน** = สุ่มได้ VDO อย่างเดียว เพราะยังไม่มี minigame
> ถ้าไม่ตั้ง 1 จะมีโอกาสสุ่มไปโดน pool minigame ที่ว่างเปล่า แล้ววันนั้นจะไม่มี event เลย
> **พอมี minigame แล้วค่อยลดเป็น 0.5**

---

## 10. ผัง event ที่ hook เข้าไปได้

`GameFlowManager` มี UnityEvent ให้ลาก UI/เสียงมาต่อได้ใน Inspector:

| Event | ยิงเมื่อไหร่ | ส่งค่า |
|---|---|---|
| `On Day Started` | เริ่มวัน (รวมเล่นซ้ำ) | เลขวัน |
| `On Day Ended` | รอดวัน | เลขวัน |
| `On Day Lost` | แพ้ ก่อนเริ่มใหม่ | เลขวัน |
| `On Day 7 Complete` | ผ่านวันสุดท้าย | — |
| `On Game Won` | ฉากจบเริ่มเล่น | — |

---

## 11. ลำดับการทำงานทั้งหมด

```
MainMenu (StartShift)
   └─> DayGameplay  ── แพ้ ──> Result ──> RestartCurrentDay (วันเดิม สุ่มใหม่หมด)
        │
        └─ รอด ──> Result ──> DayEndEvent
                                 ├─ สุ่มได้ VDO  ──> DayEventPlayer เล่นจนจบ ──> mark ว่าดูแล้ว
                                 ├─ สุ่มได้ Minigame ──> (ยังไม่มีตัวรัน)
                                 └─ ไม่มี/หมด pool ──> ข้าม
                                        │
                                        └─> AdvanceDay (เซฟ) ──> MainMenu วันถัดไป
                                                 │
                                                 └─ ถ้าเป็นวันสุดท้าย ──> Ending
                                                          └─> ResetAllSaveData ──> MainMenu วัน 1
```

**ทางแพ้มี 3 ทาง** ทุกทางวิ่งเข้า `EndNight()` เหมือนกันหมด:
1. ปีศาจหมดเวลา (`DemonAnomaly`)
2. Anomaly ล้นค้างนานเกิน (`AnomalyOverloadWatcher`)
3. Silence Protocol (ของเดิม)
