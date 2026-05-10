using BoneVisQA.Services.Models.Admin;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace BoneVisQA.Services.Interfaces.Admin
{
    /// <summary>
    /// Interface cho Admin Class Dashboard Service
    /// </summary>
    public interface IAdminClassDashboardService
    {
        #region Dashboard Summary
        /// <summary>
        /// Lấy tổng hợp Dashboard - Thống kê toàn hệ thống
        /// </summary>
        Task<AdminDashboardSummaryDto> GetDashboardSummaryAsync();
        #endregion

        #region Class List with Expert Specialties
        /// <summary>
        /// Lấy danh sách Class với thông tin Expert Specialty đầy đủ
        /// </summary>
        Task<AdminPagedResult<ClassDashboardDto>> GetClassesDashboardAsync(
            int pageIndex = 1,
            int pageSize = 10,
            string? search = null,
            Guid? lecturerId = null,
            Guid? expertId = null);
        #endregion

        #region Class Detail
        /// <summary>
        /// Lấy chi tiết một Class với đầy đủ thông tin
        /// </summary>
        Task<ClassDetailDto?> GetClassDetailAsync(Guid classId);
        #endregion

        #region Dropdowns
        /// <summary>
        /// Lấy danh sách Lecturers cho dropdown
        /// </summary>
        Task<List<LecturerDropdownDto>> GetLecturersAsync();

        /// <summary>
        /// Lấy danh sách Experts cho dropdown - có kèm Specialties
        /// </summary>
        Task<List<ExpertDropdownDto>> GetExpertsAsync();
        #endregion

        #region Class CRUD
        /// <summary>
        /// Tạo Class mới
        /// </summary>
        Task<ClassDashboardDto> CreateClassAsync(CreateClassRequestDto request);

        /// <summary>
        /// Cập nhật Class
        /// </summary>
        Task<ClassDetailDto?> UpdateClassAsync(UpdateClassRequestDto request);

        /// <summary>
        /// Cập nhật Specialty cho Class
        /// </summary>
        Task<ClassDetailDto?> UpdateClassSpecialtyAsync(UpdateClassSpecialtyRequestDto request);

        /// <summary>
        /// Xóa Class
        /// </summary>
        Task<bool> DeleteClassAsync(Guid classId);
        #endregion

        #region Assign Users
        /// <summary>
        /// Gán Lecturer/Expert vào Class
        /// </summary>
        Task<ClassDetailDto?> AssignUserToClassAsync(AssignUserToClassRequestDto request);

        /// <summary>
        /// Xóa Expert khỏi Class
        /// </summary>
        Task<bool> RemoveExpertFromClassAsync(Guid classId);

        /// <summary>
        /// Xóa Lecturer khỏi Class
        /// </summary>
        Task<bool> RemoveLecturerFromClassAsync(Guid classId);

        /// <summary>
        /// Xóa Student khỏi Class (xóa Enrollment)
        /// </summary>
        Task<bool> RemoveStudentFromClassAsync(Guid classId, Guid studentId);
        #endregion
    }
}
