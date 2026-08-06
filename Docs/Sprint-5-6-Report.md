# รายงาน Sprint 5 (ตรวจสอบ) + Sprint 6 (พัฒนาใหม่)

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
