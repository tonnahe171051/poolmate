# Hướng Dẫn Kiểm Tra CSV Export

## ✅ Đã Fix

### 1. **Lỗi Font Tiếng Việt**
- **Nguyên nhân:** Excel không nhận diện UTF-8
- **Giải pháp:** Thêm BOM character `\uFEFF` ở đầu file CSV
- **Kết quả:** Tiếng Việt hiển thị đúng khi mở bằng Excel

### 2. **Lỗi Nhảy Cột (Column Jumping)**
- **Nguyên nhân:** Dữ liệu chứa dấu phẩy (,) không được escape
- **Giải pháp:** Implement hàm `EscapeCsv()` theo chuẩn RFC 4180
- **Kết quả:** Dữ liệu có dấu phẩy được bọc trong dấu ngoặc kép

---

## 🧪 Các Trường Hợp Test

### Test Case 1: Tên Tiếng Việt
**Input:**
```
FullName: "Nguyễn Văn Hùng"
City: "Hà Nội"
Country: "Việt Nam"
```

**Expected Output trong CSV:**
```csv
"Nguyễn Văn Hùng","Hà Nội","Việt Nam"
```

**Kiểm tra Excel:**
- ✅ Các ký tự có dấu (ă, â, đ, ê, ô, ơ, ư) hiển thị đúng
- ✅ Không bị lỗi font garbled (NguyÃªn, Háº£i)

---

### Test Case 2: Địa Chỉ Có Dấu Phẩy
**Input:**
```
Address: "123 Đường ABC, Phường 1, Quận 2"
Email: "user@example.com"
```

**Expected Output trong CSV:**
```csv
"123 Đường ABC, Phường 1, Quận 2",user@example.com
```

**Kiểm tra Excel:**
- ✅ Địa chỉ vẫn nằm trong 1 cột (không bị tách thành 3 cột)
- ✅ Email nằm ở cột tiếp theo đúng vị trí

---

### Test Case 3: Tên Có Dấu Ngoặc Kép
**Input:**
```
Nickname: "Tèo "The King" Nguyễn"
```

**Expected Output trong CSV:**
```csv
"Tèo ""The King"" Nguyễn"
```

**Kiểm tra Excel:**
- ✅ Dấu ngoặc kép bên trong được nhân đôi
- ✅ Excel hiển thị đúng: Tèo "The King" Nguyễn

---

### Test Case 4: Dữ Liệu Có Xuống Dòng
**Input:**
```
Bio: "Line 1\nLine 2\nLine 3"
```

**Expected Output trong CSV:**
```csv
"Line 1 Line 2 Line 3"
```

**Kiểm tra Excel:**
- ✅ Xuống dòng được thay bằng khoảng trắng
- ✅ Không bị vỡ thành nhiều dòng trong Excel

---

### Test Case 5: Dữ Liệu Null/Empty
**Input:**
```
Phone: null
Country: ""
```

**Expected Output trong CSV:**
```csv
,,  (2 dấu phẩy liên tiếp = empty fields)
```

**Kiểm tra Excel:**
- ✅ Các ô trống không gây lỗi
- ✅ Các cột sau vẫn align đúng

---

## 🚀 Cách Test Trên Postman

### 1. Export CSV Cơ Bản
```http
GET https://localhost:7127/api/admin/players/export?format=csv&includeTournamentHistory=false
Authorization: Bearer {your_token}
```

**Kiểm tra Response:**
- Content-Type: `text/csv`
- File name: `players_list_YYYYMMDD_HHmmss.csv`
- Ký tự đầu file: `\uFEFF` (không hiển thị nhưng Excel sẽ detect)

---

### 2. Export CSV Với Lịch Sử
```http
GET https://localhost:7127/api/admin/players/export?format=csv&includeTournamentHistory=true
Authorization: Bearer {your_token}
```

**Kiểm tra Response:**
- File name: `players_history_YYYYMMDD_HHmmss.csv`
- Cột `TournamentHistory` có thể chứa dấu phẩy, dấu chấm phẩy → phải được escape đúng

---

## 📊 Kiểm Tra Trên Excel

### Bước 1: Tải File CSV
- Gọi API export từ Postman/Frontend
- Save file về máy

### Bước 2: Mở Bằng Excel
**Cách 1 (Khuyên dùng):**
1. Double-click file CSV
2. Excel sẽ tự động nhận diện UTF-8 nhờ BOM

**Cách 2 (Import thủ công):**
1. Mở Excel → Data → Get Data → From Text/CSV
2. Chọn file CSV
3. File Origin: **UTF-8**
4. Click **Load**

### Bước 3: Kiểm Tra
✅ **Các điểm cần check:**
- [ ] Tiêu đề cột hiển thị đúng (PlayerId, FullName, Email...)
- [ ] Tên tiếng Việt hiển thị đúng (không bị lỗi font)
- [ ] Các trường có dấu phẩy không bị tách cột
- [ ] Số điện thoại không bị chuyển thành số scientific (nếu có)
- [ ] Không có dòng trắng lạ ở giữa data

---

## 🔧 So Sánh Trước/Sau Fix

### ❌ TRƯỚC KHI FIX

**File CSV:**
```csv
1,Nguyễn Văn A,Ha Noi, Vietnam,0123456789
2,Trần Thị B,Ho Chi Minh,0987654321
```

**Excel hiển thị:**
| PlayerId | FullName | City | Extra | Phone |
|----------|----------|------|-------|-------|
| 1 | NguyÃªn... | Ha Noi | Vietnam | 0123456789 |
| 2 | Tráº§n... | Ho Chi Minh | | 0987654321 |

**Vấn đề:**
- ❌ Lỗi font: `NguyÃªn` thay vì `Nguyễn`
- ❌ Nhảy cột: "Vietnam" rơi sang cột riêng vì "Ha Noi, Vietnam" có dấu phẩy

---

### ✅ SAU KHI FIX

**File CSV:**
```csv
﻿1,"Nguyễn Văn A","Ha Noi, Vietnam",0123456789
2,"Trần Thị B","Ho Chi Minh",0987654321
```

**Excel hiển thị:**
| PlayerId | FullName | City | Phone |
|----------|----------|------|-------|
| 1 | Nguyễn Văn A | Ha Noi, Vietnam | 0123456789 |
| 2 | Trần Thị B | Ho Chi Minh | 0987654321 |

**Kết quả:**
- ✅ Font đúng: `Nguyễn` hiển thị hoàn hảo
- ✅ Không nhảy cột: "Ha Noi, Vietnam" nằm đúng 1 cột

---

## 📝 Checklist Test Toàn Diện

### Functional Testing
- [ ] Export không có tournament history
- [ ] Export có tournament history
- [ ] Export với filter (country, city, skillLevel)
- [ ] Export với search query
- [ ] Export empty result (0 players)
- [ ] Export với 1000+ players (performance test)

### Data Validation
- [ ] Tên tiếng Việt đầy đủ dấu
- [ ] Địa chỉ có nhiều dấu phẩy
- [ ] Email có ký tự đặc biệt
- [ ] Phone number bắt đầu bằng số 0
- [ ] Skill level: null, 1-10
- [ ] Created date format: yyyy-MM-dd HH:mm:ss

### Excel Compatibility
- [ ] Windows Excel 2016+
- [ ] Mac Excel 2019+
- [ ] Google Sheets import
- [ ] LibreOffice Calc
- [ ] Numbers (macOS)

### Edge Cases
- [ ] Player name = null
- [ ] Email = empty string
- [ ] Tournament history = rất dài (>1000 ký tự)
- [ ] Special characters: @#$%^&*()[]{}
- [ ] Emoji trong name (nếu có)

---

## 🐛 Troubleshooting

### Vấn đề: Vẫn bị lỗi font
**Giải pháp:**
1. Kiểm tra file CSV có bắt đầu bằng `\uFEFF` không
2. Thử mở bằng Notepad++ → Encoding → Verify UTF-8 BOM
3. Restart Excel

### Vấn đề: Vẫn bị nhảy cột
**Giải pháp:**
1. Kiểm tra field có dấu phẩy đã được bọc trong `"..."` chưa
2. Kiểm tra code có gọi `EscapeCsv()` cho tất cả string fields chưa
3. Debug: In ra CSV content và xem raw text

### Vấn đề: Số điện thoại bị format sai (0123 → 123)
**Giải pháp:**
- Trong Excel: Format cột Phone → Text
- Hoặc export với ký tự `'` ở đầu: `'0123456789`

---

## 📞 Support

Nếu gặp vấn đề:
1. Check file `CSV_EXPORT_FIX_SUMMARY.md` để hiểu logic
2. Xem logs trong API response
3. Test với tool: https://csvlint.io/

---

**Ngày cập nhật:** 19/12/2025  
**Tác giả:** GitHub Copilot  
**Trạng thái:** ✅ Đã hoàn thành

