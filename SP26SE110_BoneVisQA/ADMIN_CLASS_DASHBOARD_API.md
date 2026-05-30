# Admin Class Dashboard API Documentation

## Overview

Trang **Admin Class Dashboard** cung cấp giao diện quản lý Class toàn diện cho Admin, bao gồm:
- Xem danh sách Class với đầy đủ thông tin
- Gán/Remove Lecturer vào Class
- Gán/Remove Expert vào Class
- **Hiển thị chuyên môn của Expert** (BoneSpecialty, PathologyCategory, ProficiencyLevel)

---

## Base URL

```
/api/admin/class-dashboard
```

---

## Authentication

Tất cả endpoints yêu cầu:
- Header: `Authorization: Bearer <admin-token>`
- Role: `Admin`

---

## API Endpoints

### 1. Dashboard Summary

**GET** `/api/admin/class-dashboard/summary`

Lấy tổng hợp thống kê toàn hệ thống.

**Response:**
```json
{
    "totalClasses": 15,
    "totalLecturers": 8,
    "totalExperts": 12,
    "totalStudents": 245,
    "classesWithoutLecturer": 2,
    "classesWithoutExpert": 3
}
```

---

### 2. Get Classes List (with Expert Specialties)

**GET** `/api/admin/class-dashboard`

Lấy danh sách Class với thông tin đầy đủ về Expert Specialties.

**Query Parameters:**
| Parameter | Type | Default | Description |
|-----------|------|---------|-------------|
| `pageIndex` | int | 1 | Trang hiện tại |
| `pageSize` | int | 10 | Số item/trang (max: 50) |
| `search` | string | null | Tìm kiếm theo tên Class, Semester, Lecturer, Expert |
| `lecturerId` | Guid | null | Lọc theo Lecturer cụ thể |
| `expertId` | Guid | null | Lọc theo Expert cụ thể |

**Response:**
```json
{
    "items": [
        {
            "id": "guid",
            "className": "Y6A - Chi dưới",
            "semester": "2026 Spring",
            "createdAt": "2026-01-15T00:00:00Z",
            "updatedAt": "2026-02-20T00:00:00Z",

            "lecturerId": "guid",
            "lecturerName": "Dr. Nguyễn Văn A",
            "lecturerEmail": "lecturer@example.com",

            "expertId": "guid",
            "expertName": "Dr. Trần Văn B",
            "expertEmail": "expert@example.com",

            "expertSpecialties": [
                {
                    "id": "guid",
                    "boneSpecialtyId": "guid",
                    "boneSpecialtyName": "Chi dưới",
                    "boneSpecialtyCode": "LOWER_LIMB",
                    "pathologyCategoryId": "guid",
                    "pathologyCategoryName": "Gãy xương",
                    "proficiencyLevel": 5,
                    "yearsExperience": 15,
                    "certifications": "[\"AO Trauma Fellowship\"]",
                    "isPrimary": true
                },
                {
                    "id": "guid",
                    "boneSpecialtyId": "guid",
                    "boneSpecialtyName": "Cột sống",
                    "boneSpecialtyCode": "SPINE",
                    "pathologyCategoryId": null,
                    "pathologyCategoryName": null,
                    "proficiencyLevel": 3,
                    "yearsExperience": 8,
                    "isPrimary": false
                }
            ],

            "studentCount": 25,
            "totalCases": 10,
            "totalQuizzes": 5
        }
    ],
    "totalCount": 15,
    "pageIndex": 1,
    "pageSize": 10,
    "totalPages": 2,
    "hasPreviousPage": false,
    "hasNextPage": true
}
```

---

### 3. Get Class Detail

**GET** `/api/admin/class-dashboard/{classId}`

Lấy chi tiết một Class với đầy đủ thông tin.

**Response:**
```json
{
    "id": "guid",
    "className": "Y6A - Chi dưới",
    "semester": "2026 Spring",
    "createdAt": "2026-01-15T00:00:00Z",
    "updatedAt": "2026-02-20T00:00:00Z",

    "lecturer": {
        "id": "guid",
        "fullName": "Dr. Nguyễn Văn A",
        "email": "lecturer@example.com"
    },

    "expert": {
        "id": "guid",
        "fullName": "Dr. Trần Văn B",
        "email": "expert@example.com",
        "specialties": [
            {
                "id": "guid",
                "boneSpecialtyId": "guid",
                "boneSpecialtyName": "Chi dưới",
                "boneSpecialtyCode": "LOWER_LIMB",
                "pathologyCategoryId": "guid",
                "pathologyCategoryName": "Gãy xương",
                "proficiencyLevel": 5,
                "yearsExperience": 15,
                "certifications": "[\"AO Trauma Fellowship\"]",
                "isPrimary": true
            }
        ]
    },

    "students": [
        {
            "id": "guid",
            "fullName": "Nguyễn Văn Sinh",
            "email": "student1@example.com",
            "enrolledAt": "2026-01-20T00:00:00Z"
        }
    ],

    "stats": {
        "totalStudents": 25,
        "totalCasesAssigned": 10,
        "totalQuizzesAssigned": 5,
        "totalAnnouncements": 3
    }
}
```

---

### 4. Get Lecturers for Dropdown

**GET** `/api/admin/class-dashboard/dropdowns/lecturers`

Lấy danh sách Lecturers để hiển thị trong dropdown.

**Response:**
```json
[
    {
        "id": "guid",
        "fullName": "Dr. Nguyễn Văn A",
        "email": "lecturer@example.com"
    }
]
```

---

### 5. Get Experts for Dropdown (with Specialties)

**GET** `/api/admin/class-dashboard/dropdowns/experts`

**QUAN TRỌNG:** Lấy danh sách Experts kèm Specialties để Admin biết Expert đó thuộc chuyên môn nào.

**Response:**
```json
[
    {
        "id": "guid",
        "fullName": "Dr. Trần Văn B",
        "email": "expert@example.com",
        "specialties": [
            {
                "boneSpecialtyId": "guid",
                "boneSpecialtyName": "Chi dưới",
                "pathologyCategoryName": "Gãy xương",
                "proficiencyLevel": 5,
                "isPrimary": true
            },
            {
                "boneSpecialtyId": "guid",
                "boneSpecialtyName": "Cột sống",
                "pathologyCategoryName": null,
                "proficiencyLevel": 3,
                "isPrimary": false
            }
        ]
    }
]
```

**Frontend Usage:**
```javascript
// Khi Admin chọn Expert từ dropdown, hiển thị thông tin Specialties
const expertSelect = document.getElementById('expert-select');
expertSelect.addEventListener('change', async (e) => {
    const expertId = e.target.value;
    const response = await fetch('/api/admin/class-dashboard/dropdowns/experts');
    const experts = await response.json();
    const selected = experts.find(ex => ex.id === expertId);

    if (selected) {
        console.log('Expert:', selected.fullName);
        console.log('Specialties:', selected.specialties);
        // Hiển thị: "Dr. B - Chi dưới (Gãy xương) - Level 5"
    }
});
```

---

### 6. Create Class

**POST** `/api/admin/class-dashboard`

Tạo Class mới.

**Request:**
```json
{
    "className": "Y6B - Cột sống",
    "semester": "2026 Spring"
}
```

**Response:**
```json
{
    "id": "guid",
    "className": "Y6B - Cột sống",
    "semester": "2026 Spring",
    "createdAt": "2026-05-08T00:00:00Z",
    "studentCount": 0,
    "totalCases": 0,
    "totalQuizzes": 0
}
```

---

### 7. Update Class

**PUT** `/api/admin/class-dashboard`

Cập nhật thông tin Class.

**Request:**
```json
{
    "id": "guid",
    "className": "Y6B - Cột sống (Updated)",
    "semester": "2026 Fall"
}
```

---

### 8. Delete Class

**DELETE** `/api/admin/class-dashboard/{classId}`

Xóa Class.

**Response:**
```json
{
    "deleted": true,
    "message": "Class deleted successfully."
}
```

---

### 9. Assign Lecturer to Class

**POST** `/api/admin/class-dashboard/{classId}/assign-lecturer?lecturerId={lecturerId}`

Gán Lecturer vào Class.

**Query Parameters:**
| Parameter | Type | Description |
|-----------|------|-------------|
| `lecturerId` | Guid | ID của Lecturer cần gán |

**Response:** Trả về ClassDashboardDto với thông tin đã cập nhật.

---

### 10. Assign Expert to Class

**POST** `/api/admin/class-dashboard/{classId}/assign-expert?expertId={expertId}`

Gán Expert vào Class.

**Query Parameters:**
| Parameter | Type | Description |
|-----------|------|-------------|
| `expertId` | Guid | ID của Expert cần gán |

**Response:** Trả về ClassDashboardDto với thông tin Expert và Specialties.

---

### 11. Assign Both Lecturer and Expert

**POST** `/api/admin/class-dashboard/{classId}/assign-users`

Gán cả Lecturer và Expert vào Class cùng lúc.

**Request:**
```json
{
    "classId": "guid",
    "lecturerId": "guid",
    "expertId": "guid"
}
```

---

### 12. Remove Expert from Class

**POST** `/api/admin/class-dashboard/{classId}/remove-expert`

Xóa Expert khỏi Class (không xóa Lecturer).

**Response:**
```json
{
    "message": "Expert removed from class successfully."
}
```

---

## Frontend Integration Example

### React Component

```tsx
import React, { useState, useEffect } from 'react';

const AdminClassDashboard = () => {
    const [classes, setClasses] = useState([]);
    const [experts, setExperts] = useState([]);
    const [lecturers, setLecturers] = useState([]);
    const [summary, setSummary] = useState({});
    const [loading, setLoading] = useState(true);

    useEffect(() => {
        fetchDashboardSummary();
        fetchExperts(); // Lấy Experts với Specialties
        fetchLecturers();
        fetchClasses();
    }, []);

    const fetchExperts = async () => {
        const response = await fetch('/api/admin/class-dashboard/dropdowns/experts');
        const data = await response.json();
        setExperts(data);
    };

    const handleAssignExpert = async (classId, expertId) => {
        await fetch(`/api/admin/class-dashboard/${classId}/assign-expert?expertId=${expertId}`, {
            method: 'POST'
        });
        fetchClasses(); // Refresh list
    };

    return (
        <div className="admin-dashboard">
            {/* Summary Cards */}
            <div className="summary-cards">
                <div className="card">Total Classes: {summary.totalClasses}</div>
                <div className="card">Without Lecturer: {summary.classesWithoutLecturer}</div>
                <div className="card">Without Expert: {summary.classesWithoutExpert}</div>
            </div>

            {/* Classes Table */}
            <table>
                <thead>
                    <tr>
                        <th>Class Name</th>
                        <th>Semester</th>
                        <th>Lecturer</th>
                        <th>Expert</th>
                        <th>Expert Specialties</th>
                        <th>Students</th>
                        <th>Actions</th>
                    </tr>
                </thead>
                <tbody>
                    {classes.map(cls => (
                        <tr key={cls.id}>
                            <td>{cls.className}</td>
                            <td>{cls.semester}</td>
                            <td>{cls.lecturerName || '-'}</td>
                            <td>{cls.expertName || '-'}</td>
                            <td>
                                {/* Hiển thị Expert Specialties */}
                                {cls.expertSpecialties.length > 0 ? (
                                    <div className="specialties-list">
                                        {cls.expertSpecialties.map((spec, idx) => (
                                            <span key={idx} className={`badge ${spec.isPrimary ? 'primary' : ''}`}>
                                                {spec.boneSpecialtyName}
                                                {spec.pathologyCategoryName && ` - ${spec.pathologyCategoryName}`}
                                                (Lv.{spec.proficiencyLevel})
                                            </span>
                                        ))}
                                    </div>
                                ) : (
                                    <span className="no-specialty">No specialties</span>
                                )}
                            </td>
                            <td>{cls.studentCount}</td>
                            <td>
                                <button onClick={() => openAssignModal(cls)}>
                                    Assign Users
                                </button>
                            </td>
                        </tr>
                    ))}
                </tbody>
            </table>

            {/* Expert Selection Modal */}
            <Modal open={showAssignModal}>
                <h3>Assign Expert to Class</h3>

                {/* Expert Dropdown với Specialties Preview */}
                <select onChange={(e) => handleExpertSelect(e.target.value)}>
                    <option value="">-- Select Expert --</option>
                    {experts.map(expert => (
                        <option key={expert.id} value={expert.id}>
                            {expert.fullName}
                            {expert.specialties.length > 0 && (
                                ` - ${expert.specialties[0].boneSpecialtyName}`
                            )}
                            {expert.specialties.length > 0 && expert.specialties[0].isPrimary && ' ⭐'}
                        </option>
                    ))}
                </select>

                {/* Hiển thị Specialties khi chọn */}
                {selectedExpert && (
                    <div className="expert-specialties-detail">
                        <h4>Chuyên môn của {selectedExpert.fullName}:</h4>
                        {selectedExpert.specialties.map((spec, idx) => (
                            <div key={idx} className={`specialty-card ${spec.isPrimary ? 'primary' : ''}`}>
                                <div className="specialty-name">
                                    {spec.boneSpecialtyName}
                                    {spec.isPrimary && <span className="badge">Primary</span>}
                                </div>
                                <div className="specialty-detail">
                                    {spec.pathologyCategoryName && (
                                        <span>Danh mục: {spec.pathologyCategoryName}</span>
                                    )}
                                    <span>Level: {spec.proficiencyLevel}/5</span>
                                </div>
                            </div>
                        ))}
                    </div>
                )}
            </Modal>
        </div>
    );
};
```

### CSS Styles

```css
.specialties-list {
    display: flex;
    flex-wrap: wrap;
    gap: 4px;
}

.badge {
    background: #e0e0e0;
    padding: 2px 8px;
    border-radius: 12px;
    font-size: 12px;
}

.badge.primary {
    background: #4CAF50;
    color: white;
}

.specialty-card {
    border: 1px solid #ddd;
    padding: 10px;
    margin: 5px 0;
    border-radius: 8px;
}

.specialty-card.primary {
    border-color: #4CAF50;
    background: #f1f8f1;
}
```

---

## Error Responses

| Status Code | Description |
|-------------|-------------|
| 400 | Bad Request - Invalid input |
| 401 | Unauthorized - Missing or invalid token |
| 403 | Forbidden - User is not Admin |
| 404 | Not Found - Class/Expert/Lecturer not found |
| 500 | Internal Server Error |

**Example Error:**
```json
{
    "message": "Expert not found."
}
```

---

## Notes

1. **Expert Specialty Display:** Khi Admin chọn Expert từ dropdown, Frontend nên:
   - Gọi API `GET /dropdowns/experts` để lấy danh sách với Specialties
   - Hiển thị thông tin Specialties cho Admin biết Expert đó giỏi về mảng nào
   - Sắp xếp theo `isPrimary` (chuyên môn chính) và `proficiencyLevel`

2. **Filtering:** Có thể lọc Class theo:
   - Lecturer cụ thể
   - Expert cụ thể
   - Tìm kiếm text (tên Class, Semester, tên người)

3. **Pagination:** Mặc định 10 items/trang, tối đa 50 items/trang
