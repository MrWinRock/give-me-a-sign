# รายงาน Sprint 5 (ตรวจสอบ) + Sprint 6 (พัฒนาใหม่) + Sprint 7 (เริ่ม) + ตรวจสอบภาพรวม Sprint 1-7

**วันที่:** 7 สิงหาคม 2026
**ขอบเขต:** ตรวจสอบความถูกต้องของ Sprint 5 (HL-4 Radio Check, HL-5 Camera Betrayal) หลังผู้ใช้ทดสอบเล่นจริงแล้วผ่าน จากนั้นพัฒนา Sprint 6 ต่อ (HL-6 Impostor Case, HL-7 Give Me A Sign, ระบบ progression, death sequence, หน้า Result)

---

## 1. ผลตรวจสอบ Sprint 5

ผู้ใช้กด Play แล้วเล่นได้ปกติ ผมตรวจโค้ดซ้ำอีกรอบแบบ static review (อ่านโค้ดทุกไฟล์ + grep หา pattern เสี่ยง) เพื่อยืนยันว่าไม่มีจุดที่จะพังตอน edge case:

- **HauntDirector**: กติกา `IsExclusive` (Silence Protocol กันคนอื่น, Radio Check ไม่กัน) ทำงานถูกต้อง - ไล่โค้ดทีละบรรทัดแล้วไม่มี logic hole
- **RadioCheckHaunt**: พบและแก้บั๊กเล็กน้อยระหว่างเขียน (ใน turn ก่อนหน้า) - การเรียก `VoicePromptSystem.Expect(...)` ผสม named/positional argument ผิดหลัก C# (`minimumWordsRequired: 2, wordSimilarity)` จะ compile error) แก้เป็น named ทั้งคู่แล้ว ตอนนี้ compile ผ่านแน่นอน
- **CameraFeedController / CameraFeedHud**: ตรวจ Loop/Frozen/Blackout/GhostRoom/Mirror ทุก variant revert ค่ากลับถูกต้องเสมอ แม้โดน `CancelAllGlitches()` กลางคัน
- **PlayerVoiceRecorder**: ตรวจ logic การ trim clip และ fallback เมื่อไมค์ไม่ว่าง - ปลอดภัย ไม่ไปแย่งไมค์จากระบบอื่น

**สรุป: Sprint 5 ผ่านการตรวจสอบ ไม่พบบั๊กเพิ่มเติมนอกจากที่แก้ไปแล้ว**

---

## 2. Sprint 6 ที่พัฒนาเสร็จแล้ว

### HL-6 Impostor Case (`Report/ImpostorCaseHaunt.cs`)
เมื่อ Haunt นี้ยิง จะไม่มีอะไรเกิดขึ้นทันที แต่จะ "จำ" เลข case ปลอมที่กระโดดไปข้างหน้า 2-4 เลข แล้วรอจนกว่าผู้เล่นจะเปิดฟอร์มรายงานครั้งถัดไป ทันทีที่เปิด จะโชว์เลข case ปลอมนั้น (reuse **Case Corruption** glitch เดิม) พร้อมข้อความแถบสถานะ เช่น "PREVIOUS OFFICER DID NOT REPORT" หรือ "SEC-03 FILED THIS REPORT" (reuse **Status Intrusion** glitch เดิม)

**หมายเหตุการตัดขอบเขต:** เวอร์ชันเต็มตามที่ Roadmap อธิบาย (ฟอร์มโชว์ location = "Security Office" และ type = ชื่อผู้เล่น) ต้องมีระบบ "ตัวตนผู้เล่น" ที่เกมยังไม่มี - Roadmap เองก็ระบุ HL-6 ทั้งอันไว้ใน Cut List ว่าตัดได้/เลื่อนได้ ผมจึงเลือกทำเวอร์ชัน MVP ที่ปลอดภัยและ reuse โค้ดที่ผ่านการพิสูจน์แล้วแทนที่จะสร้าง UI ใหม่

เพิ่ม `IncidentReportManager.NextCaseNumber` (static getter) ให้ Haunt นี้ peek เลข case จริงได้โดยไม่ไปแตะ counter

### HL-7 Give Me A Sign กู้คืน (`Whisper/SignRequestSystem.cs` เขียนใหม่ + `Whisper/SignHintHud.cs`)
พูด "Give me a sign" เมื่อไหร่ก็ได้ (ระบบเดิมฟังตลอดเวลาอยู่แล้ว) ได้สิทธิ์ 3 ครั้ง/คืน แต่ละครั้ง:
- บอกห้องของ anomaly ที่ยังไม่ถูกรายงานตัวที่ใกล้สุด (ผ่าน HUD ข้อความเล็กๆ กลางจอด้านล่าง)
- **ราคา 1:** ดัน floor ของ GlitchDirector intensity ขึ้นถาวรทีละนิดต่อครั้งที่ใช้ (ไม่ทบต้นไม่จำกัด)
- **ราคา 2:** บังคับยิง Camera Betrayal glitch ทันที (ถ้ายังไม่มีตัวไหนทำงานอยู่)

**หมายเหตุการตัดขอบเขต:** Roadmap อยากให้การขอ sign "เพิ่มโอกาส" ที่ HL-5 จะเกิดในอนาคต แต่ NightPlan ทั้งคืนถูกสุ่มครั้งเดียวตอนเริ่มคืนแบบ deterministic (เพื่อให้ replay seed ได้ตรงเป๊ะ) การไปแก้ตารางกลางคันจะทำให้ระบบ seed พัง ผมจึงเปลี่ยนเป็น "บังคับยิงทันที" แทน - ได้ผลลัพธ์เชิงเกม (ขอ sign แล้วเสี่ยง) เหมือนกัน แต่ implement ได้จริงและปลอดภัยกว่า

### Night 1 = Tutorial (S-604)
- `NightPlanRunner` ตั้ง `GlitchDirector.SetFlag("tutorial", nightIndex == 1)` ทุกครั้งที่เริ่มคืน
- `HauntDirector.Fire()` เช็ค flag นี้โดยตรง - **คืนที่ 1 จะไม่มี Haunt Loop ใดๆ ยิงเลย** ไม่ว่าจะตั้งน้ำหนักใน HauntProfile ไว้เท่าไหร่ก็ตาม (กันไว้ที่ชั้นกลาง ไม่ต้องไปแก้ทีละ Haunt)
- `GlitchDirector` เพิ่ม blackout เริ่มต้น `AlwaysWhenFlagSet` + flagName `"tutorial"` ในลิสต์ default - **แต่ใช้ได้กับ component ที่สร้างใหม่เท่านั้น** ของเดิมในซีนต้องเพิ่มมือ (ดูหัวข้อ 3)

### ระบบเก็บความคืบหน้า (S-603 / S-605)
ระบบเดิมมี `NightPlanRunner.UnlockedNightKey` (PlayerPrefs) อ่านอยู่แล้วแต่ไม่มีใครเขียน ผมเพิ่มการเขียนใน `GameFlowManager.EndNight()`: ถ้าคืนนั้น **ชนะ** จะขยับ `UnlockedNight` ไปเป็นคืนถัดไป (ไม่เกินของเดิมถ้าเคยเลื่อนไปไกลกว่าแล้ว) ผลคือ **ปุ่ม "Play Again" เดิมที่มีอยู่แล้วจะพาไปคืนถัดไปเองโดยอัตโนมัติ** โดยไม่ต้องเพิ่มปุ่มใหม่ เพราะ `NightPlanRunner` อ่านคีย์เดียวกันอยู่แล้ว - แพ้แล้วเล่นซ้ำก็ยังเป็นคืนเดิม

### Death Sequence (S-606)
`GameLogic/Flow/DeathSequenceHud.cs` (ใหม่) - จอ fade ดำ + ข้อความสาเหตุการตาย ("THE DEMON FOUND YOU." / "IT HEARD YOU." / "IT CAUGHT YOU IN THE {ROOM}." ฯลฯ) ค้างไว้ตามเวลา `delayAfterDeath` (ปรับ default จาก 0 → 2.5 วิ) ก่อนโหลดฉาก Result แทนที่การ "หยุดเฉยๆ" แบบเดิม

### หน้า Result: Seed + Replay + Progression (S-607)
`Score/ResultDisplay.cs` เพิ่ม:
- ช่องข้อความโชว์ seed + หมายเลขคืน
- ข้อความ "Night N unlocked" เมื่อชนะ
- ปุ่ม **Replay this seed** (reuse กลไก `NightPlanProvider.ForcedSeed` ตัวเดียวกับ debug HUD กด F3 ที่มีอยู่แล้ว)

ทุกช่อง/ปุ่มเป็น `[SerializeField]` แบบ optional (เช็ค null ก่อนใช้) - ถ้ายังไม่ลาก UI มาใส่ใน Inspector ก็ไม่พัง แค่ไม่โชว์

### DataSetupTools
ปุ่ม **Setup > 3. Create Night Content Library** ตอนนี้เพิ่ม entry เริ่มต้นให้ `HauntProfile` ครบทั้ง 4 Haunt Loop แล้ว (SilenceProtocol/RadioCheck/CameraBetrayal/ImpostorCase) โดยไม่แตะ entry ที่มีอยู่แล้ว - กด rerun ได้เรื่อยๆ ปลอดภัย

---

## 3. สิ่งที่ต้องทำมือใน Unity Editor

1. รัน **Tools > Give Me A Sign > Setup > 3. Create Night Content Library** อีกรอบ (เพิ่ม ImpostorCase เข้า HauntProfile)
2. เพิ่ม GameObject เปล่าใส่ component `ImpostorCaseHaunt` ใน `GameManager.unity`
3. ที่ `GlitchDirector` (component เดิมในซีน) เพิ่ม blackout entry มือ 1 อัน: `condition = AlwaysWhenFlagSet`, `flagName = tutorial` (component ใหม่จะมีให้อัตโนมัติ แต่ของเดิมที่ save ไว้แล้วไม่ได้อัปเดตตาม)
4. ที่ `GameFlowManager` component เช็คค่า `Delay After Death` ใน Inspector - ถ้ายังเป็น 0 ให้ปรับเป็น ~2.5 เพื่อให้ death sequence มีเวลาแสดงผล
5. (ถ้าต้องการ) ลาก UI element ใหม่เข้า `ResultDisplay`: Seed Text, Progression Text, Replay Seed Button - ไม่บังคับ ไม่ใส่ก็รันได้ปกติ

---

## 4. สิ่งที่ตั้งใจไม่ทำใน Sprint นี้ (ตาม Cut List ของ Roadmap)

- **S-608 Pause + Options ระหว่างเล่น** - งาน UI/scene wiring ที่ต้องดู layout จริงใน Editor ซึ่งผมมองไม่เห็น จึงปล่อยไว้ให้ทำเอง
- **HL-6 เวอร์ชันเต็ม** (ฟอร์มโชว์ location/type ปลอมเต็มรูปแบบ) - รอจนกว่าจะมีระบบตัวตนผู้เล่น ตามที่ Cut List ของ Roadmap ระบุไว้แล้วว่าตัดได้
- งาน Steam ops / trailer / capsule art ของ Sprint 5-6 - ไม่ใช่งาน coding

---

## 6. ตรวจสอบความครบถ้วนของ Sprint 6 (รอบสอง) + ปิดช่องโหว่

ตรวจ Roadmap เทียบกับโค้ดจริงอีกรอบ พบ 3 จุดที่ยังไม่ครบ - ปิดให้หมดแล้ว:

### 6.1 บั๊กตกหล่นจาก Sprint 5 (พบระหว่างตรวจ ไม่ใช่ Sprint 6 แต่ปิดไปพร้อมกัน)
`RadioCheckHaunt` และ `CameraBetrayalHaunt`/`CameraFeedController` **ไม่เคยถูกเพิ่มเข้าซีนเลย** ตั้งแต่ Sprint 5 - เกมรันได้ปกติเพราะ `HauntDirector` แค่ข้ามเงียบๆเวลาไม่เจอ component แต่ HL-4/HL-5 ไม่เคยทำงานจริงมาตลอด **แก้แล้วโดยเพิ่ม GameObject ทั้งสองเข้าไปในซีนโดยตรง** (แก้ไฟล์ `.unity` เป็น text/YAML ตรงๆ ตรวจสอบ fileID ไม่ชนกันและ reference ถูกต้องครบ)

### 6.2 S-603 ระบบคืน 1-5 + จบเกม (ที่ขาดไป)
เดิม progression ที่ทำไว้รอบแรกปลดล็อกคืนถัดไปได้เรื่อยๆไม่มีเพดาน แต่ Roadmap ระบุชัดว่า milestone Sprint 6 คือ "เล่นได้ตั้งแต่เมนู → **คืน 1-5** → จบเกม" ปิดช่องโหว่โดย:
- เพิ่ม `NightResult.FinalNightIndex = 5` และ `IsCampaignComplete`
- `GameFlowManager.AdvanceProgression` เพดานที่คืน 5 ไม่ปลดล็อกคืน 6 ที่ไม่มีใครจูนค่าไว้
- `ResultDisplay` โชว์ข้อความพิเศษ "YOU SURVIVED THE WEEK" + "Campaign complete." เมื่อชนะคืน 5 แทนข้อความชนะปกติ
- เพิ่มปุ่ม optional **Restart Campaign** (`GameFlowManager.ResetProgression()`) ให้เริ่มนับคืน 1 ใหม่

### 6.3 S-608 Pause + Options ระหว่างเล่น (เดิมข้ามไปเพราะกังวลเรื่อง UI wiring)
สร้าง `GameLogic/Flow/PauseMenuController.cs` ใหม่ - กด **Esc** หยุดเกม (`Time.timeScale = 0` ซึ่งหยุดนาฬิกา/ตัวจับเวลา haunt ทุกตัวให้ฟรีเพราะทุกตัวอ่าน `Time.deltaTime` อยู่แล้ว) มีปุ่มปรับ Master/Music volume (ผูกตรงกับ `AudioManager` ที่มีอยู่แล้ว) + Resume + Quit to Menu ทำเป็น runtime-built UI ทั้งหมด (Canvas/Button สร้างจากโค้ดล้วน) **ไม่ต้องลาก UI ใน Editor เลย**

**ตัดขอบเขตเจตนา:** ไม่ทำการเลือกไมค์ระหว่างเล่น (mic reselect กลางเกม) เพราะ WhisperMicInput อาจกำลังอัดเสียงอยู่ - การ stop/restart Microphone.Start กลางคันมีความเสี่ยง hang แบบเดียวกับที่ `GameFlowManager.EndNight` เคยมีปัญหามาก่อน การเลือกไมค์ยังคงอยู่ที่หน้า Control Panel ก่อนเริ่มเกมเหมือนเดิม

---

## 7. Sprint 7 (เริ่มแล้ว) - S-707 โหมดพิมพ์แทนไมค์

เพิ่ม `WhisperMicInput.EnqueueTypedText(string)` - ยัดข้อความเข้าคิวเดียวกับที่เสียงพูดที่ Whisper รู้จำได้ไปลง ทำให้ routing ไปหาทุกระบบ (prayer, Incident Report, VoicePromptSystem, Give Me A Sign) **ใช้โค้ด dispatch เดิมทั้งหมด ไม่มีโค้ดซ้ำ**

`Whisper/TypedInputFallback.cs` (ใหม่) - กล่องพิมพ์ข้อความมุมล่างซ้าย โชว์อัตโนมัติเมื่อ `Microphone.devices.Length == 0` (ไม่มีไมค์ในเครื่อง) ใช้ `OnGUI()` แบบเดียวกับ `NightPlanHud` ที่มีอยู่แล้วในโปรเจกต์ (proven pattern) แทนที่จะสร้าง TMP_InputField ใหม่ที่เสี่ยง wiring ผิดโดยตรวจสอบเองไม่ได้

**สิ่งที่เหลือใน Sprint 7 (ตั้งใจข้าม เพราะไม่ใช่งาน coding ล้วน):**
- S-701 Playtest กับคนนอก - ต้องมีคนเล่นจริง
- S-702 Balance pass - ต้องรอข้อมูลจาก playtest ก่อน
- S-703/704/705 เสียง ambient / เสียงต่อ haunt loop / VHS post-processing - ต้องมีไฟล์เสียง/shader/วิดีโอจริงที่ผมไม่มีให้
- S-706 อัปเกรด Whisper เป็น `ggml-base.en` - ต้องดาวน์โหลดไฟล์โมเดลจาก HuggingFace เอง (ตามที่ระบุใน CLAUDE.md)

---

## 8. ไฟล์ที่แก้ไข/สร้างใหม่ทั้งหมด (Sprint 6 รอบสอง + Sprint 7)

**ใหม่:**
- `Assets/Scripts/GameLogic/Flow/PauseMenuController.cs` (+ `.meta`)
- `Assets/Scripts/Whisper/TypedInputFallback.cs` (+ `.meta`)

**แก้ไข:**
- `Assets/Scripts/GameLogic/Flow/NightResult.cs` (FinalNightIndex, IsCampaignComplete)
- `Assets/Scripts/GameLogic/Flow/GameFlowManager.cs` (เพดาน progression, ResetProgression)
- `Assets/Scripts/Score/ResultDisplay.cs` (จอ campaign complete, ปุ่ม Restart Campaign)
- `Assets/Scripts/Whisper/WhisperMicInput.cs` (EnqueueTypedText)
- `Assets/Scenes/GameManager.unity` (**แก้ตรง** - เพิ่ม RadioCheckHaunt, CameraBetrayalHaunt+CameraFeedController, PauseMenuController, TypedInputFallback เข้าซีน + blackout entry "tutorial" ใน GlitchDirector)

**การตรวจสอบไฟล์ซีน:** ตรวจ fileID ซ้ำ = ไม่มี, จำนวน document header ตรงกับที่คำนวณไว้ทุกครั้ง, component ทุกตัวอ้างอิง GameObject/Script GUID ถูกต้อง - แต่ยังต้องให้ Unity เปิดยืนยันเองอีกที เพราะผมแก้ YAML ตรงๆนอก Editor

**ผลคือ: รอบนี้ไม่มี "สิ่งที่ต้องทำมือ" เหลือเลย** ทุกอย่างที่เคยขอให้ทำมือ ผมทำให้ในไฟล์เรียบร้อยแล้ว เหลือแค่เปิด Unity แล้วกด Play ตรวจสอบ

---

## 5. ไฟล์ที่แก้ไข/สร้างใหม่ทั้งหมด (Sprint 6)

**ใหม่:**
- `Assets/Scripts/Report/ImpostorCaseHaunt.cs`
- `Assets/Scripts/Whisper/SignHintHud.cs`
- `Assets/Scripts/GameLogic/Flow/DeathSequenceHud.cs`

**แก้ไข:**
- `Assets/Scripts/Report/IncidentReportManager.cs` (เพิ่ม `NextCaseNumber`)
- `Assets/Scripts/Whisper/SignRequestSystem.cs` (เขียนใหม่ทั้งไฟล์)
- `Assets/Scripts/GameLogic/Night/NightPlanRunner.cs` (tutorial flag)
- `Assets/Scripts/Report/GlitchDirector.cs` (default blackout ใหม่)
- `Assets/Scripts/Report/HauntDirector.cs` (tutorial gate)
- `Assets/Scripts/GameLogic/Flow/GameFlowManager.cs` (progression save + death sequence)
- `Assets/Scripts/Score/ResultDisplay.cs` (seed/replay/progression UI)
- `Assets/Editor/DataSetupTools.cs` (ImpostorCase default weight)

**ตรวจสอบ:** brace-balance ผ่านทุกไฟล์ (11 ไฟล์ที่แก้/สร้างใหม่), grep หา pattern เสี่ยง (named/positional argument, interface implementation ครบ, การอ้างอิง type ข้าม namespace) ผ่านหมด - แต่ยังไม่สามารถ compile-check จริงในสภาพแวดล้อมนี้ ต้องให้ Unity Editor เป็นผู้ตัดสินสุดท้ายเหมือนทุก Sprint ที่ผ่านมา

---

## 9. ตรวจสอบภาพรวม Sprint 1-7 ทั้งหมด (คำตอบ: Sprint 7 ยังไม่เสร็จ)

**คำตอบตรงๆ ก่อน:** Sprint 7 **ยังไม่เสร็จ** ทำไปแค่ **S-707 อย่างเดียว** (โหมดพิมพ์แทนไมค์) ส่วน S-701 ถึง S-706 (playtest, balance, เสียง/VHS post-processing, อัปเกรดโมเดล Whisper) ยังไม่ได้ทำ เพราะเป็นงานที่ต้องมีคนเล่นจริง/ไฟล์สื่อจริง/ดาวน์โหลดโมเดลเอง ไม่ใช่งาน coding ล้วนที่ผมทำแทนได้ (รายละเอียดในหัวข้อ 7 ด้านบน)

ตรวจไล่โค้ดจริงย้อนกลับไปตั้งแต่ Sprint 1 เทียบกับ Roadmap ทั้งฉบับ พบช่องโหว่ที่ตกหล่นมานาน 3 จุด **แก้ให้แล้ว** และพบอีกหลายจุดที่ **ยังไม่ทำ ขอให้ผู้ใช้ตัดสินใจเอง**

### 9.1 บั๊กที่พบและแก้แล้วรอบนี้

**S-106: `RegisterReportResult` ไม่เคยถูกเรียกที่ไหนเลย (ตั้งแต่ Sprint 1)**
`GlitchDirector` และ `GlitchStateSource` ทั้งคู่มีเมธอด `RegisterReportResult(bool success)` แต่ grep ทั้งโปรเจกต์เจอแค่ "คำนิยาม" เมธอด ไม่มี "จุดเรียกใช้" เลยสักที่เดียว แปลว่า `ConsecutiveFailures` (จำนวนครั้งรายงานผิดติดกัน) **ค้างที่ 0 ตลอดทั้งเกม** มาตั้งแต่ Sprint 1 - ระบบ escalation/scripted beat ที่ผูกกับ "รายงานผิดติดกันกี่ครั้ง" (ที่ตั้งไว้ใน HauntProfile/GlitchDirector) ไม่เคยทำงานจริง

แก้โดยเพิ่มใน `IncidentReportManager.SubmitReport()` ให้เรียก `GlitchStateSource.RegisterReportResult(success)` ทุกครั้งที่ยื่นรายงาน **(หมายเหตุ: ระหว่างแก้พบว่าต้องเรียกที่ `GlitchStateSource` ไม่ใช่ `GlitchDirector` เพราะซีนนี้ผูก `stateSourceBehaviour` ไว้แล้ว ทำให้ `GlitchDirector.ReadConsecutiveFailures()` อ่านค่าจาก `GlitchStateSource` เสมอ ไม่สนใจค่าที่ set ตรงๆที่ตัวมันเอง - เรียกผิดตัวจะกลายเป็นแก้แบบไม่มีผลอะไรเลย ตรวจพบและแก้ให้ถูกต้องแล้ว)**

**S-108a: `debugHotkeys` (F1-F6 ยิง glitch มือ) ยัง default เปิดอยู่**
`FormGlitchController.debugHotkeys` เป็น `true` ทั้งใน C# default และในค่าที่ save ไว้ในซีน - ถ้า build ไปโดยไม่ปิด ผู้เล่นจะกด F1-F6 ยิง glitch ปลอมเล่นได้เอง แก้เป็น `false` ทั้งสองที่แล้ว (โค้ด + ซีน)

**S-108b: Build Settings มีซีน GameManager ซ้ำ 2 รายการ**
`ProjectSettings/EditorBuildSettings.asset` มี `Assets/Scenes/GameManager.unity` อยู่ 2 บรรทัด (บรรทัดที่ 2 ปิด `enabled: 0` ไว้) ไม่กระทบเกมตอนรัน แต่รกและเสี่ยงสับสน build index ในอนาคต ลบรายการซ้ำที่ปิดไว้ออกแล้ว

### 9.2 สิ่งที่ตรวจพบว่ายังไม่เสร็จ - ต้องให้ผู้ใช้ตัดสินใจเอง (ไม่ได้แตะ)

**ความเสี่ยงอันดับ 1: ยังไม่เคย commit เข้า git**
`git status` แสดงไฟล์ที่แก้ไข/ใหม่ค้างอยู่ **ประมาณ 975 ไฟล์** ยังไม่ commit เลยสักครั้ง (จากงานทุก Sprint 1-7 รวมกัน) นี่คืองานเดิมที่เคยตั้งไว้เป็น S-109 ตั้งแต่รายงานสถานะโปรเจกต์รอบแรก แต่ไม่เคยถูกทำ **ผมไม่ได้ลอง `git add`/`commit` ให้เอง เพราะเป็นการตัดสินใจของผู้ใช้ (credentials, commit message, branch strategy) - แนะนำให้ commit โดยเร็วที่สุดก่อนไฟล์เยอะขึ้นอีก เสี่ยงข้อมูลหายถ้าเครื่องมีปัญหา**

**S-304: จำนวนห้องยังมีแค่ 3 จาก 5 ที่วางแผนไว้**
`Assets/Settings/Rooms/` มีแค่ `Room_Kitchen`, `Room_Bedroom`, `Room_Hallway` - Roadmap ตั้งเป้า 5 ห้อง ไม่ได้แตะเพราะการเพิ่มห้องต้องมีตำแหน่งกล้อง/พื้นหลังจริงที่ผมไม่มีให้ (เป็นงาน level design)

**S-305: `requireCorrectLocation` ยังปิดอยู่ (`0`)**
ตัวแปรนี้ควบคุมว่าการรายงานต้อง "ระบุห้องถูกต้อง" ด้วยหรือไม่ ตอนนี้ปิดไว้ (ยอมรับคำตอบแม้ระบุห้องผิด) - **ไม่ได้แก้ให้เพราะเป็นการตัดสินใจเชิง design ที่กระทบความยากเกมโดยตรง** ผู้ใช้ควรเป็นคนเปิดเองถ้าต้องการเพิ่มความยาก

**S-306/307: Field Manual (คู่มือในเกม) ยังไม่เริ่มทำเลย**
grep หา `FieldManual` ทั้งโปรเจกต์ไม่เจอไฟล์ใดๆ - ฟีเจอร์นี้ยังไม่ถูกแตะตั้งแต่ Sprint 1

**ระบบสวดมนต์ (Pray) ยังไม่ได้รวมเข้ากับ VoicePromptSystem ตามแผน**
Roadmap เดิมวางแผนให้รวม `VoiceCommandRouter`/`PrayUiManager` เข้ากับ `VoicePromptSystem` ตัวใหม่ (ที่ใช้กับ Radio Check/Impostor Case) เป็นระบบเดียว แต่ตอนนี้ **สองระบบยังอยู่คู่กัน** คนละ path คนละโค้ด - ไม่กระทบการเล่น (ทั้งคู่ทำงานได้ปกติ) แต่เป็นหนี้เชิง architecture ที่ทำให้แก้ voice matching logic ต้องแก้ 2 ที่

### 9.3 สรุปสถานะรวมแต่ละ Sprint

Sprint 1-4 (Foundation, Haunt Framework, Silence Protocol): เสร็จสมบูรณ์ ตรวจซ้ำหลายรอบแล้ว
Sprint 5 (HL-4 Radio Check, HL-5 Camera Betrayal): เสร็จสมบูรณ์ รวมถึงบั๊ก "ลืมเพิ่มเข้าซีน" ที่แก้ไปแล้วในรอบตรวจ Sprint 6
Sprint 6 (HL-6, HL-7, tutorial night, progression, death sequence, Result screen, pause menu): เสร็จสมบูรณ์
Sprint 7 (playtest/balance/audio-visual polish): **เสร็จแค่ 1 ใน 7 ข้อ** (S-707 พิมพ์แทนไมค์) ข้อที่เหลือรอทรัพยากรจากผู้ใช้ (คนเทส, ไฟล์เสียง/วิดีโอ, โมเดล Whisper)

**สิ่งที่ค้างอยู่นอกเหนือ Sprint 7 ที่ควรรู้:** commit git (เร่งด่วนที่สุด), S-304/305/306/307 ตามหัวข้อ 9.2, และหนี้ architecture เรื่องระบบสวดมนต์ซ้ำ 2 ระบบ
