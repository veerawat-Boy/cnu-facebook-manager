# คู่มือ Deploy ขึ้น Railway

โปรเจคนี้มี 3 ส่วนที่ต้อง deploy เป็น 3 services แยกกันใน Railway project เดียว:

1. **SQL Server** — database (Docker image, ไม่ต้องเขียนโค้ดเพิ่ม)
2. **Backend** (`CnuFacebookAPI`) — Web API
3. **Frontend** (`CnuFacebookBlazor`) — Blazor Server UI

Railway แต่ละ service ในโปรเจคเดียวกันคุยกันผ่าน private network ได้ (เช่น `sqlserver.railway.internal`) และแต่ละ service เปิด public domain แยกกันได้

---

## ขั้นตอนที่ 0 — เตรียม Repo

Railway deploy จาก Git repo ได้ดีที่สุด แนะนำ push โค้ดนี้ขึ้น GitHub ก่อน (private repo ก็ได้)
ไฟล์ `appsettings*.json` ถูก `.gitignore` ไว้แล้ว — secret จะไม่ติดไปกับ repo (ปลอดภัย) แต่หมายความว่า **ต้องตั้งค่าทุกอย่างผ่าน Environment Variables บน Railway**

---

## ขั้นตอนที่ 1 — สร้าง Service: SQL Server

1. Railway → New Project → **Deploy a Docker Image**
2. ใส่ image: `mcr.microsoft.com/mssql/server:2022-latest`
3. ตั้ง Environment Variables:
   - `ACCEPT_EULA` = `Y`
   - `MSSQL_SA_PASSWORD` = (ตั้งรหัสผ่านที่แข็งแรง เช่น `Cnu#2026Strong!`)
4. ไปที่ **Settings → Volumes** → เพิ่ม volume mount path `/var/opt/mssql` (กัน data หายตอน redeploy)
5. ตั้งชื่อ service เป็น `sqlserver` (ชื่อนี้จะถูกใช้เป็น internal hostname: `sqlserver.railway.internal`)
6. **ไม่ต้อง** generate public domain ให้ service นี้ (ปลอดภัยกว่า ใช้ private network พอ)

### รัน setup.sql ครั้งแรก
ใช้ Railway **"Connect"** บน service `sqlserver` เพื่อดู connection command (หรือเปิด TCP proxy ชั่วคราวจาก Settings → Networking → Public Networking)
แล้วใช้ SSMS / Azure Data Studio / `sqlcmd` เชื่อมต่อแล้วรัน `Database/setup.sql` หนึ่งครั้งเพื่อสร้างตาราง

---

## ขั้นตอนที่ 2 — สร้าง Service: Backend (CnuFacebookAPI)

1. New Service → **Deploy from GitHub repo** → เลือก repo นี้
2. **Settings → Root Directory** = `BackendFb/CnuFacebookAPI/CnuFacebookAPI`
   (Railway จะเจอ `Dockerfile` ที่สร้างไว้ในโฟลเดอร์นี้แล้ว build ให้อัตโนมัติ)
3. ตั้ง Environment Variables:

   | Key | Value |
   |---|---|
   | `ConnectionStrings__EMS` | `Server=sqlserver.railway.internal,1433;Database=CnuFacebookDB;User Id=sa;Password=<รหัสจาก step 1>;TrustServerCertificate=True;Encrypt=True;` |
   | `FacebookApp__AppId` | (App ID จาก Meta for Developers) |
   | `FacebookApp__AppSecret` | (App Secret จาก Meta for Developers) |
   | `FacebookApp__RedirectUri` | `https://<frontend-domain>/api/CnuFacebook/FacebookCallback` |
   | `FacebookApp__FrontendSelectPageUrl` | `https://<frontend-domain>/select-pages` |
   | `AllowedOrigins__0` | `https://<frontend-domain>` |
   | `AI__GeminiToken` | (Gemini API key) |

   ⚠️ `<frontend-domain>` จะรู้ค่าหลังทำขั้นตอนที่ 3 (generate domain ของ Frontend) — กลับมาแก้ตรงนี้อีกครั้งได้
4. **Settings → Networking → Generate Domain** เพื่อให้ Backend มี public URL (ต้องใช้ตั้งเป็น Webhook URL ของ Facebook)

---

## ขั้นตอนที่ 3 — สร้าง Service: Frontend (CnuFacebookBlazor)

1. New Service → Deploy from GitHub repo (เดียวกัน) อีกครั้ง
2. **Settings → Root Directory** = `Frontend/CnuFacebookBlazor/CnuFacebookBlazor`
3. ตั้ง Environment Variables:

   | Key | Value |
   |---|---|
   | `BackendApi__BaseUrl` | `https://<backend-domain>/` (จาก Generate Domain ของ Backend ใน step 2.4) |

4. **Settings → Networking → Generate Domain** เพื่อได้ public URL ของ Frontend
5. กลับไปที่ Backend service → แก้ `FacebookApp__RedirectUri`, `FacebookApp__FrontendSelectPageUrl`, `AllowedOrigins__0` ให้ตรงกับ domain จริงที่ได้ตอนนี้ → redeploy Backend

---

## ขั้นตอนที่ 4 — ตั้งค่าใน Meta for Developers

ไปที่ App ของ Meta for Developers แล้วตั้งค่า:

| ตำแหน่ง | ค่า |
|---|---|
| Settings → Basic → App Domains | `<frontend-domain>`, `<backend-domain>` |
| Facebook Login → Settings → Valid OAuth Redirect URIs | `https://<frontend-domain>/api/CnuFacebook/FacebookCallback` |
| Messenger → Settings → Webhook → Callback URL | `https://<backend-domain>/api/cnufacebook` |
| Messenger → Settings → Webhook → Verify Token | `CNU2025` (ตรงกับ `VERIFY_TOKEN` ใน `CnuFacebookController.cs`) |
| Settings → Basic → Privacy Policy URL | `https://<frontend-domain>/privacy-policy` |
| Settings → Basic → Terms of Service URL | `https://<frontend-domain>/terms-of-service` |
| Settings → Basic → User Data Deletion | `https://<frontend-domain>/data-deletion` |

---

## ขั้นตอนที่ 5 — ทดสอบ

1. เปิด `https://<frontend-domain>/connect` → กด Login with Facebook
2. เลือกเพจ → บันทึก → เช็คว่าไปหน้า Dashboard ได้
3. ทดสอบส่งข้อความเข้า Page Messenger → เช็คว่า AI ตอบกลับ (ดู log ของ Backend service บน Railway)

ถ้าผ่านหมด แอปพร้อม submit App Review สำหรับ `pages_messaging` แล้วครับ (อย่าลืมอัดวิดีโอ demo การใช้งานไว้แนบตอนยื่นด้วย)
