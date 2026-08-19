# ผลการตรวจสอบ Performance — GamePlay Scene

## สรุปสาเหตุหลักที่เจอ

### 🔴 #1 วิดีโอ Transition เล่นวนตลอดเวลาโดยไม่มีใครดู (แก้แล้ว)

**นี่คือตัวปัญหาหลัก**

ใน GamePlay scene มี GameObject ชื่อ `VideoPlayerTransition` ตั้งค่าไว้ว่า:
- `Play On Awake = ✓` — เริ่มเล่นทันทีที่เข้าซีน
- `Looping = ✓` — เล่นวนไม่มีวันหยุด
- Render Mode = **Render Texture** ขนาด **1920×1080** (`ScreenTexture`)
- คลิป = `Transition.mp4`

แต่ตัวที่แสดงผล RenderTexture นี้คือ GameObject `ScreenTransition` ซึ่ง **ปิดอยู่** (`m_IsActive: 0`)
และจะเปิดแค่ตอนสลับห้องเท่านั้น (`GameManager.NextRoom/PreviousRoom` → `screen.SetActive(true)`
แล้ว `UnActiveSelf` จะปิดกลับเองหลังจากนั้นไม่กี่วินาที)

**แปลว่า:** เกมถอดรหัสวิดีโอ Full HD ทุกเฟรม ตลอดทั้งคืน ลง texture ขนาด 1920×1080
ที่ไม่มีอะไรเอาไปแสดงเลย — เสียเปล่า 100% ทั้ง CPU (decode) และ GPU (เขียน RT)

**วิธีแก้:** เพิ่ม component ใหม่ `VideoPlaybackGate` ที่ให้วิดีโอเล่นเฉพาะตอนที่ตัวแสดงผลเปิดอยู่จริง
- ปิด `Play On Awake` ใน scene แล้ว
- ตัว script บังคับ `playOnAwake = false` ซ้ำอีกชั้นตอน Awake กันพลาด
- ใช้ `Pause()` ไม่ใช่ `Stop()` เวลาซ่อน เพราะ Stop จะทำลาย decoder ทำให้ตอนโชว์ครั้งหน้าสะดุด

---

### 🟠 #2 Post-processing 6 ตัวเปิดพร้อมกัน (ยังไม่แก้ — เป็นเรื่องของภาพ)

`Assets/Settings/Global Volume Profile.asset` เปิดไว้ทั้งหมด:

| Effect | ค่าใช้จ่าย |
|---|---|
| **Bloom** (intensity 1, maxIterations 6) | **แพงสุด** — ทำ down/upsample หลายรอบแยกต่างหาก |
| PaniniProjection | ถูก (รวมใน UberPost pass เดียว) |
| LensDistortion | ถูก (รวมใน UberPost) |
| FilmGrain | ถูก (รวมใน UberPost) |
| ChromaticAberration | ถูก (รวมใน UberPost) |
| Vignette | ถูก (รวมใน UberPost) |

5 ตัวล่างรวมกันเป็น pass เดียวเลยไม่แพง แต่ **Bloom แยก pass ต่างหาก**

**ถ้ายังแลคอยู่ ลองปิด Bloom ก่อนเป็นอันดับแรก:** เปิด `Global Volume Profile.asset` →
ติ๊ก `Bloom` ออก → เทียบ FPS ดู ถ้าดีขึ้นชัดเจนแต่ยังอยากได้ bloom ให้ลด `Max Iterations` เหลือ 3-4
หรือตั้ง `Downscale` เป็น Quarter

---

### 🟡 #3 โมเดล Whisper 74 MB โหลดตอนเข้าซีน

`Assets/StreamingAssets/Models/ggml-tiny.bin` = 74 MB โหลดตอน `WhisperMicInput.Start()`
ทำให้**กระตุกครั้งเดียวตอนเข้าซีน** (ไม่ใช่แลคต่อเนื่อง) และกินแรม

ตัว Whisper เองไม่ได้รันตลอด — เป็น push-to-talk gate ไว้แล้ว (เล่นเสียงเฉพาะตอนกดปุ่มพูด)
ถ้าอาการคือ "ค้างแวบนึงตอนเข้าซีน" ต้นเหตุคือตรงนี้

---

## สิ่งที่ตรวจแล้ว "ไม่ใช่ปัญหา"

| ตรวจอะไร | ผล |
|---|---|
| ขนาด Texture/Sprite | รวมกันแค่ **3 MB** — ไม่ใช่ปัญหาเลย |
| จำนวน GameObject | 161 ตัว — ปกติมาก |
| URP Depth/Opaque Texture | ปิดทั้งคู่ ✓ (ถ้าเปิดจะมี full-screen copy ทุกเฟรม) |
| MSAA / Render Scale | MSAA ปิด, Render Scale 1.0 ✓ |
| SRP Batcher | เปิดอยู่ ✓ |
| Light2D (3 ดวง) | เปิด shadow ไว้ แต่**ไม่มี ShadowCaster2D สักตัวในซีน** เลยไม่ได้เสียค่าใช้จ่ายจริง |
| ContentSizeFitter | ไม่มีเลย ✓ (ตัวนี้คู่กับ LayoutGroup คือตัวทำ UI ช้าคลาสสิก) |
| Incident Report Window | ปิดอยู่ตอนเข้าซีน ✓ (67 UI graphics ไม่ได้ render) |
| `Active`/`Unactive`/`ActiveWhenUnactive` | เช็คสถานะก่อนเรียก SetActive แล้ว ✓ ไม่แพง |
| Physics2D | Simulation Mode ปกติ, collider เป็น static click zone |
| `Camera.main` ใน Update | ไม่มี ✓ |
| `GetComponent` ใน Update | ไม่มี ✓ |

---

## ที่แก้ไปแล้วรอบก่อนหน้า (ยังคงมีผล แต่ไม่ใช่ตัวการหลัก)

1. **`CameraFeedHud`** — เขียน TMP ใหม่ทุกเฟรม → แก้ให้เขียนเฉพาะตอนค่าเปลี่ยน
   ⚠️ **หมายเหตุ:** ตรวจเพิ่มพบว่า HUD ตัวนี้ถูกสร้างเฉพาะตอนมี camera glitch เท่านั้น
   (`CameraFeedController.PlayGlitch()` → `CameraFeedHud.Instance`) **ไม่ได้รันตอนเข้าซีน**
   เลยไม่ใช่สาเหตุของอาการแลคตั้งแต่แรกเข้า — แต่แก้ไว้ก็ยังดีตอนที่มัน active
2. **`MicAmplitudeMonitor`** — อ่าน PlayerPrefs (Registry บน Windows) ทุกเฟรม → cache แล้ว
   ทำงานเฉพาะตอน Silence Protocol haunt รันอยู่

---

## วิธีวัดผลเอง

1. เปิด **Window ▸ Analysis ▸ Profiler** (หรือกด Ctrl+7) ก่อนกด Play
2. ดูแถบ **CPU Usage** — คลิกเฟรมที่ค้าง แล้วดู Hierarchy ว่าอะไรกินเวลา
3. สลับไปดู **GPU Usage** ถ้า CPU ดูปกติแต่ยังช้า → แปลว่าติดที่ GPU (post-processing/overdraw)
4. ดู **GC Alloc** ในแถบ Memory — ถ้ามีค่าสูงทุกเฟรมแปลว่ามีการ allocate string/array ใน Update

> ⚠️ **ทดสอบใน Editor จะช้ากว่า build จริงเสมอ** โดยเฉพาะถ้าเปิด Profiler ค้างไว้
> ถ้าจะวัดจริงจังให้ Build แล้วรันไฟล์ .exe

---

# รอบที่ 2 — เจอต้นเหตุจริงแล้ว

## 🔴🔴 ตัวการหลัก: ไฟล์เสียง 79 MB โหลดแบบคลายทั้งก้อนลงแรม (แก้แล้ว)

`Assets/Audio/18.98hzSFX.mp3` — เสียง infrasound 18.98 Hz (ความถี่ที่ทำให้คนรู้สึกกลัว)

**ตั้งค่า import ไว้แบบนี้:**
| ช่อง | ค่าเดิม | ปัญหา |
|---|---|---|
| **Load Type** | `Decompress On Load` | คลาย MP3 **ทั้งไฟล์** เป็น PCM ดิบเก็บในแรม |
| **Load In Background** | `ปิด` | โหลดบน **main thread** = เกมค้างจนกว่าจะเสร็จ |
| ขนาดไฟล์ | **79 MB** | — |

**คำนวณคร่าวๆ:** MP3 79 MB ที่ ~128 kbps ≈ 82 นาที
→ PCM 44.1 kHz / 16-bit / stereo = 176,400 bytes/วินาที
→ **≈ 870 MB ในแรม** จากไฟล์เดียว

**ผลที่เกิด (ตรงกับอาการทุกข้อ):**
1. เข้าซีนแล้วค้างยาว เพราะคลายไฟล์บน main thread แบบ synchronous
2. กินแรมเกือบ 1 GB → ระบบต้อง swap ลงดิสก์ → **กระตุกตลอดเวลาไม่หาย**
3. ใช้อยู่ใน **ทั้ง 3 ซีน** (GamePlay / Result / StartScene) เลยแลคทุกที่

ตัวที่เล่นคือ GameObject `!898ZZHZSFX` ตั้ง **Play On Awake + Loop** เล่นตั้งแต่วินาทีแรก

**แก้เป็น:**
| ช่อง | ค่าใหม่ | เหตุผล |
|---|---|---|
| Load Type | **`Streaming`** | ถอดรหัสทีละนิดตอนเล่น ใช้แรมแค่ไม่กี่ KB |
| Load In Background | **`เปิด`** | ไม่บล็อก main thread |

แก้แบบเดียวกันให้ `AmbienceSFX.mp3` (3 MB) และ `VHSSFX.mp3` (1.8 MB) ด้วย
เพราะเป็น loop ยาวเล่นตลอดเหมือนกัน

**เสียง SFX สั้นๆ (whisper, click, jumpscare) ยังคงเป็น `Decompress On Load` ตามเดิม**
ซึ่ง**ถูกต้องแล้ว** — คลิปสั้นควรใช้แบบนี้เพื่อให้เล่นทันทีไม่มีดีเลย์

---

## กฎง่ายๆ สำหรับตั้งค่าไฟล์เสียงต่อไป

| ประเภทเสียง | Load Type ที่ควรใช้ |
|---|---|
| SFX สั้น < 200 KB (คลิก, เสียงพูดสั้น, jumpscare) | **Decompress On Load** |
| เสียงกลาง 200 KB - 1 MB | **Compressed In Memory** |
| **เพลง / ambience / loop ยาว** | **Streaming** + Load In Background ✓ |

> ⚠️ ห้ามใช้ Decompress On Load กับไฟล์ใหญ่เด็ดขาด — Unity จะคลายเป็น PCM ดิบ
> ซึ่งใหญ่กว่าไฟล์ต้นฉบับ **~10 เท่า**

---

## ❓ เรื่อง FindObjectOfType ที่สงสัย — ตรวจแล้ว "ไม่ใช่สาเหตุ"

ตรวจทุกจุดที่เรียก `FindObjectOfType` / `FindFirstObjectByType` แล้ว (34 จุด) พบว่า:

- **ทุกจุดอยู่ใน `Start()` หรือ `Awake()`** — รันครั้งเดียวตอนเข้าซีน ไม่ได้รันทุกเฟรม
- singleton getter ทุกตัว (`GameFlowManager`, `CameraFeedHud`, `HauntDirector`,
  `VoicePromptSystem`) ใช้แพทเทิร์น **cache-or-create** คือถ้าหาไม่เจอจะ*สร้างใหม่แล้วเก็บไว้*
  ไม่ใช่ค้นซ้ำทุกครั้ง
- **ไม่มี `FindObjectOfType` ใน `Update()` แม้แต่จุดเดียว**

สรุป: `FindObjectOfType` ในโปรเจกต์นี้ไม่ได้ทำให้แลคครับ (มันจะเป็นปัญหาก็ต่อเมื่ออยู่ใน Update
หรือ singleton getter ที่คืน null แล้วค้นใหม่ทุกเฟรม ซึ่งโปรเจกต์นี้ไม่มี)

---

## สรุปสิ่งที่แก้ไปทั้งหมด (เรียงตามผลกระทบ)

| # | ปัญหา | ผลกระทบ | สถานะ |
|---|---|---|---|
| 1 | เสียง 79 MB DecompressOnLoad + sync load | **~870 MB RAM + main thread block** | ✅ แก้แล้ว |
| 2 | AmbienceSFX / VHSSFX loop ยาว DecompressOnLoad | แรมเพิ่มโดยไม่จำเป็น | ✅ แก้แล้ว |
| 3 | วิดีโอ Transition วนตลอดลง RT 1920×1080 ที่ไม่มีใครดู | decode วิดีโอทุกเฟรมเปล่าๆ | ✅ แก้แล้ว |
| 4 | `CameraFeedHud` เขียน TMP ทุกเฟรม | รันเฉพาะตอนมี camera glitch | ✅ แก้แล้ว |
| 5 | `MicAmplitudeMonitor` อ่าน PlayerPrefs ทุกเฟรม | รันเฉพาะตอน Silence Protocol | ✅ แก้แล้ว |
| 6 | Post-processing 6 ตัว (Bloom แพงสุด) | ปกติของเกม ไม่ใช่ปัญหา | ไม่แตะ |

---

# รอบที่ 3 — เจอสาเหตุที่ตรงกับอาการ "แลคตอนขยับเมาส์" แล้ว

ผู้ใช้ยืนยันว่า Profiler ยังขึ้น CPU แดง (คอขวด) แม้แก้เสียง/วิดีโอไปแล้ว และรู้สึกแลค**ชัดเจนตอนขยับเมาส์**
— เบาะแสนี้ชี้ตรงไปที่อะไรก็ตามที่ผูกกับตำแหน่งเมาส์โดยเฉพาะ ไล่ตามนี้:

## 🔴 ต้นเหตุ: Light2D 3 ดวงเปิด Shadows ไว้ทั้งที่ไม่มีอะไรให้บังเลยสักตัว (แก้แล้ว)

ตรวจด้วย GUID ตรงๆ (ไม่ใช่เดา) พบว่า **ทั้งซีนและทุก prefab ไม่มี `ShadowCaster2D` แม้แต่ตัวเดียว**
แต่ Light2D ทั้ง 3 ดวงใน GamePlay ตั้งค่า `Shadows Enabled = true` ไว้:

| ไฟ | Shadows | Intensity | หมายเหตุ |
|---|---|---|---|
| **Flashlight** | ✓ เปิด | 4.38 | **ขยับตามเมาส์ทุกเฟรม** (`MouseManager.cs`) + มี Light Cookie Sprite |
| AreaLight2D | ✓ เปิด | 0.7 | อยู่นิ่ง |
| Sprite Light 2D | ✓ เปิด | 0.06 | อยู่นิ่ง |

**ทำไมถึงเสียเปล่า:** URP 2D Renderer จะรัน shadow pass เต็มรูปแบบ (สร้าง/bind/clear shadow
render texture) ให้ไฟทุกดวงที่เปิด shadow ไว้**ทุกเฟรม** ไม่ว่าจะมีอะไรให้บังหรือไม่ — ในเมื่อไม่มี
`ShadowCaster2D` เลย พาสนี้จึงทำงานไปเฉยๆ โดยไม่ได้ผลลัพธ์อะไรกลับมา

**ทำไมถึงรู้สึกแย่ตอนขยับเมาส์โดยเฉพาะ:** ไฟ **Flashlight** เป็นตัวเดียวที่ขยับ — ทุกครั้งที่ตำแหน่ง
เปลี่ยน 2D Renderer ต้องคำนวณใหม่ว่าไฟไปทับ sprite/sorting layer ไหนบ้าง (bounds เปลี่ยน) แถมยัง
sample Light Cookie Sprite ในพื้นที่ที่เปลี่ยนตลอดเวลา ผสมกับ shadow pass ที่รันอยู่แล้ว — ตอนเมาส์
นิ่งพื้นที่ที่ไฟกระทบเดิมๆ กัน แต่ตอนขยับพื้นที่เปลี่ยนทุกเฟรมจึงหนักขึ้นเห็นได้ชัด

**แก้แล้ว:** ปิด `Shadows Enabled` ทั้ง 3 ดวงใน `GamePlay.unity` — ไม่กระทบภาพเลยเพราะไม่มีอะไรถูกบังอยู่แล้ว
(นี่คือคำแนะนำมาตรฐานของ Unity เองด้วย: "ปิด shadow บนไฟที่ไม่ต้องการ" ในเอกสาร 2D Lighting performance)

> พบ pattern เดียวกันใน `Result.unity` และ `StartScene.unity` ด้วย (ซีนละ 1 ดวง) แต่รอบนี้แก้เฉพาะ
> `GamePlay.unity` ตามที่อาการชี้ไว้ก่อน ถ้าซีนอื่นแลคด้วยบอกได้ จะแก้ให้เหมือนกัน

## ✅ ตรวจแล้วไม่ใช่สาเหตุ

- ไม่มี `Physics2DRaycaster`/`PhysicsRaycaster` บนกล้อง → เมาส์ขยับไม่ได้ทำให้เกิด physics raycast เพิ่ม
- `InputSystemUIInputModule` raycast UI แค่ 67 ชิ้นที่ raycastable — น้อยเกินจะแลคเอง
- ไม่มี custom cursor เปลี่ยนทุกเฟรม (`Cursor.SetCursor` ไม่ถูกเรียกเลยในโปรเจกต์)
- ไม่มี `SpriteMask` ในซีน

---

## สรุปสะสมทั้ง 3 รอบ

| # | ปัญหา | ผลกระทบ | สถานะ |
|---|---|---|---|
| 1 | เสียง 79 MB DecompressOnLoad + sync load | ~870 MB RAM + main thread block | ✅ แก้แล้ว |
| 2 | AmbienceSFX / VHSSFX loop ยาว DecompressOnLoad | แรมเพิ่มโดยไม่จำเป็น | ✅ แก้แล้ว |
| 3 | วิดีโอ Transition วนตลอดลง RT ที่ไม่มีใครดู | decode วิดีโอทุกเฟรมเปล่าๆ | ✅ แก้แล้ว |
| **4** | **Light2D 3 ดวงเปิด shadow แต่ไม่มี ShadowCaster2D เลย** | **shadow pass เปล่าทุกเฟรม + หนักขึ้นตอนไฟขยับตามเมาส์** | ✅ แก้แล้ว |
| 5 | `CameraFeedHud` เขียน TMP ทุกเฟรม | รันเฉพาะตอนมี camera glitch | ✅ แก้แล้ว |
| 6 | `MicAmplitudeMonitor` อ่าน PlayerPrefs ทุกเฟรม | รันเฉพาะตอน Silence Protocol | ✅ แก้แล้ว |

---

# รอบที่ 4 — เจอตัวการจริง (ผู้ใช้ยืนยันว่า 3 รอบก่อนไม่ช่วยเลย)

ผู้ใช้บอกว่าแลค**เท่าเดิมทุกอย่าง**ทั้งที่แก้ไป 4 จุดในรอบก่อนแล้ว (เสียง 2 ชนิด, วิดีโอ, Light2D shadow)
— นี่คือสัญญาณว่าตัวที่แก้ไปไม่ใช่ตัวหลัก ต้องมีอะไรที่ **หนักกว่ามากจนกลบทุกอย่างที่แก้ไปจนหมด**
ไล่ตรวจใหม่ทั้งโปรเจกต์ (shader, physics, quality settings, DOTween, Whisper package internals,
infinite loop, texture import) จนเจอจุดนี้:

## 🔴🔴🔴 ตัวการจริง: `TypedInputFallback.forceEnabled` ถูกเปิดค้างไว้ในซีน

`Assets/Scripts/Whisper/TypedInputFallback.cs` เป็นกล่องพิมพ์ข้อความสำรอง (เผื่อผู้เล่นไม่มีไมค์)
ที่ปกติจะแสดงเฉพาะตอน **ไม่พบไมค์เท่านั้น** — แต่ตรวจในซีน `GamePlay.unity` แล้วพบว่ามันถูกตั้ง:

```
forceEnabled: 1
```

ซึ่งบังคับให้กล่องนี้ **แสดงตลอดเวลา แม้จะมีไมค์อยู่แล้วก็ตาม**

### ทำไมนี่ถึงหนักกว่าทุกอย่างที่แก้ไปก่อนหน้ารวมกัน

`TypedInputFallback` ใช้ `OnGUI()` ซึ่งเป็นระบบ **IMGUI แบบเก่า** (ไม่ใช่ uGUI/Canvas ที่ใช้ทั่วเกม)
และ Unity มีพฤติกรรมสำคัญที่คนไม่ค่อยรู้: **`OnGUI()` ไม่ได้ถูกเรียกแค่ 1 ครั้งต่อเฟรม**
แต่ถูกเรียกซ้ำสำหรับ**ทุก event ที่เกิดขึ้น** รวมถึง `EventType.Layout`, `EventType.Repaint`,
และที่สำคัญที่สุดคือ **`EventType.MouseMove`** — ทุกครั้งที่เมาส์ขยับ Unity จะเรียก `OnGUI()`
ซ้ำอีกรอบให้ทุก component ที่มี OnGUI ในซีนได้ประมวลผล event นั้น

บวกกับข้างในใช้ `GUILayout.BeginArea` / `GUILayout.BeginHorizontal` / `GUILayout.TextField`
ซึ่งเป็น **immediate-mode layout ที่คำนวณ layout ใหม่ทั้งหมดทุกครั้งที่ถูกเรียก** (ไม่มีการ cache
เหมือน uGUI) — TextField โดยเฉพาะยังต้องจัดการ keyboard focus/cursor state ทุกครั้งด้วย

**สรุปคือ:** ยิ่งขยับเมาส์เร็ว/บ่อย ยิ่งเรียก `OnGUI()` ถี่ ยิ่งคำนวณ layout ซ้ำเยอะ — ตรงกับอาการ
ทุกข้อที่บอกมาเป๊ะๆ:
| อาการที่สังเกต | คำอธิบาย |
|---|---|
| เข้าซีนก็แลคเลย | `forceEnabled: 1` ทำให้ทำงานตั้งแต่เฟรมแรก ไม่ต้องรอเงื่อนไขอะไร |
| แลคชัดตอนขยับเมาส์ | MouseMove event ทำให้ `OnGUI()` ถูกเรียกซ้ำมากกว่าปกติ |
| Profiler ขึ้น CPU แดง (ไม่ใช่ GPU) | IMGUI layout เป็นงาน CPU ล้วนๆ ไม่เกี่ยวกับ GPU/overdraw |
| 3 รอบก่อนแก้แล้วไม่ต่างเลย | เพราะสิ่งที่แก้ไปไม่ใช่ตัวที่หนักที่สุด ตัวนี้กลบไว้หมด |

**แก้แล้ว:** เปลี่ยน `forceEnabled: 0` ใน `GamePlay.unity` — กล่องพิมพ์ข้อความจะกลับไปแสดงเฉพาะตอน
ตรวจไม่พบไมค์จริงๆ ตามพฤติกรรมที่ตั้งใจออกแบบไว้แต่แรก (เช็คแล้วซีนอื่น StartScene/MainMenu/Result
ไม่มีปัญหานี้ มีแค่ GamePlay ซีนเดียว)

**นี่คือความผิดพลาดแบบ "ลืมปิดสวิตช์ debug"** ไม่ใช่บั๊กโค้ด — โค้ดของ `TypedInputFallback.cs`
เขียนถูกต้องอยู่แล้ว (`if (!_noMicDetected && !forceEnabled) return;`) เจตนาให้ `forceEnabled` เป็นออปชัน
"เผื่อผู้เล่นอยากพิมพ์เอง" แต่มีคนเปิดค้างไว้ในซีนตอนเทสต์แล้วไม่ได้ปิดกลับ
