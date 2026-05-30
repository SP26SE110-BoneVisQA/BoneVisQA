using BoneVisQA.Services.Interfaces.Admin;
using BoneVisQA.Services.Models.Admin;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System;
using System.Threading.Tasks;

namespace BoneVisQA.API.Controllers.Admin
{
    /// <summary>
    /// Admin Dashboard Controller - Trang quản lý Class toàn diện
    /// Cho phép Admin:
    /// - Xem danh sách Class với đầy đủ thông tin Expert và Specialties
    /// - Gán Lecturer/Expert vào Class
    /// - Thấy được Expert đó thuộc chuyên môn nào (BoneSpecialty, Pathology, Proficiency)
    /// </summary>
    [ApiController]
    [Route("api/admin/class-dashboard")]
    [Tags("Admin - Class Dashboard")]
    [Authorize(Roles = "Admin")]
    public class AdminClassDashboardController : ControllerBase
    {
        private readonly IAdminClassDashboardService _dashboardService;

        public AdminClassDashboardController(IAdminClassDashboardService dashboardService)
        {
            _dashboardService = dashboardService;
        }

        #region Dashboard Summary

        /// <summary>
        /// Lấy tổng hợp Dashboard - Thống kê toàn hệ thống
        /// </summary>
        /// <returns>Tổng số Class, Lecturer, Expert, Student và các cảnh báo</returns>
        [HttpGet("summary")]
        public async Task<IActionResult> GetDashboardSummary()
        {
            var result = await _dashboardService.GetDashboardSummaryAsync();
            return Ok(result);
        }

        #endregion

        #region Class List

        /// <summary>
        /// Lấy danh sách Class với thông tin đầy đủ
        /// Bao gồm: Lecturer, Expert, Expert's Specialties, Student Count
        /// </summary>
        /// <param name="pageIndex">Trang hiện tại (mặc định: 1)</param>
        /// <param name="pageSize">Số item/trang (mặc định: 10, tối đa: 50)</param>
        /// <param name="search">Tìm kiếm theo tên Class, Semester, Lecturer, Expert</param>
        /// <param name="lecturerId">Lọc theo Lecturer cụ thể</param>
        /// <param name="expertId">Lọc theo Expert cụ thể</param>
        [HttpGet]
        public async Task<IActionResult> GetClasses(
            [FromQuery] int pageIndex = 1,
            [FromQuery] int pageSize = 10,
            [FromQuery] string? search = null,
            [FromQuery] Guid? lecturerId = null,
            [FromQuery] Guid? expertId = null)
        {
            if (pageIndex < 1) pageIndex = 1;
            if (pageSize < 1) pageSize = 10;
            if (pageSize > 50) pageSize = 50;

            var result = await _dashboardService.GetClassesDashboardAsync(
                pageIndex, pageSize, search, lecturerId, expertId);

            return Ok(result);
        }

        /// <summary>
        /// Lấy chi tiết một Class
        /// </summary>
        /// <param name="classId">ID của Class</param>
        [HttpGet("{classId:guid}")]
        public async Task<IActionResult> GetClassDetail(Guid classId)
        {
            var result = await _dashboardService.GetClassDetailAsync(classId);
            if (result == null)
                return NotFound(new { message = "Class not found." });

            return Ok(result);
        }

        #endregion

        #region Dropdowns

        /// <summary>
        /// Lấy danh sách Lecturers cho dropdown
        /// </summary>
        [HttpGet("dropdowns/lecturers")]
        public async Task<IActionResult> GetLecturers()
        {
            var result = await _dashboardService.GetLecturersAsync();
            return Ok(result);
        }

        /// <summary>
        /// Lấy danh sách Experts cho dropdown - có kèm Specialties
        /// </summary>
        /// <remarks>
        /// Response bao gồm:
        /// - ExpertId, FullName, Email
        /// - Danh sách Specialties: BoneSpecialtyName, PathologyCategoryName, ProficiencyLevel, IsPrimary
        /// </remarks>
        [HttpGet("dropdowns/experts")]
        public async Task<IActionResult> GetExperts()
        {
            var result = await _dashboardService.GetExpertsAsync();
            return Ok(result);
        }

        #endregion

        #region Class CRUD

        /// <summary>
        /// Tạo Class mới
        /// </summary>
        [HttpPost]
        public async Task<IActionResult> CreateClass([FromBody] CreateClassRequestDto request)
        {
            if (string.IsNullOrWhiteSpace(request.ClassName))
                return BadRequest(new { message = "ClassName is required." });

            if (string.IsNullOrWhiteSpace(request.Semester))
                return BadRequest(new { message = "Semester is required." });

            var result = await _dashboardService.CreateClassAsync(request);
            return Ok(result);
        }

        /// <summary>
        /// Cập nhật Class
        /// </summary>
        [HttpPut]
        public async Task<IActionResult> UpdateClass([FromBody] UpdateClassRequestDto request)
        {
            if (request.Id == Guid.Empty)
                return BadRequest(new { message = "Class Id is required." });

            if (string.IsNullOrWhiteSpace(request.ClassName))
                return BadRequest(new { message = "ClassName is required." });

            if (string.IsNullOrWhiteSpace(request.Semester))
                return BadRequest(new { message = "Semester is required." });

            var result = await _dashboardService.UpdateClassAsync(request);
            if (result == null)
                return NotFound(new { message = "Class not found." });

            return Ok(result);
        }

        /// <summary>
        /// Cập nhật Specialty cho Class
        /// </summary>
        [HttpPut("{classId:guid}/specialty")]
        public async Task<IActionResult> UpdateClassSpecialty(Guid classId, [FromBody] UpdateClassSpecialtyRequestDto request)
        {
            if (classId == Guid.Empty)
                return BadRequest(new { message = "Class Id is required." });

            request.ClassId = classId;

            var result = await _dashboardService.UpdateClassSpecialtyAsync(request);
            if (result == null)
                return NotFound(new { message = "Class not found." });

            return Ok(result);
        }

        /// <summary>
        /// Xóa Class
        /// </summary>
        [HttpDelete("{classId:guid}")]
        public async Task<IActionResult> DeleteClass(Guid classId)
        {
            var result = await _dashboardService.DeleteClassAsync(classId);
            if (!result)
                return NotFound(new { message = "Class not found." });

            return Ok(new { deleted = true, message = "Class deleted successfully." });
        }

        #endregion

        #region Assign Users to Class

        /// <summary>
        /// Gán Lecturer vào Class
        /// </summary>
        /// <param name="classId">ID của Class</param>
        /// <param name="lecturerId">ID của Lecturer cần gán</param>
        [HttpPost("{classId:guid}/assign-lecturer")]
        public async Task<IActionResult> AssignLecturer(Guid classId, [FromQuery] Guid lecturerId)
        {
            if (lecturerId == Guid.Empty)
                return BadRequest(new { message = "LecturerId is required." });

            try
            {
                var request = new AssignUserToClassRequestDto
                {
                    ClassId = classId,
                    LecturerId = lecturerId
                };

                var result = await _dashboardService.AssignUserToClassAsync(request);
                if (result == null)
                    return NotFound(new { message = "Class not found." });

                return Ok(result);
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }

        /// <summary>
        /// Gán Expert vào Class - Hiển thị đầy đủ chuyên môn của Expert
        /// </summary>
        /// <remarks>
        /// Khi chọn Expert từ dropdown, FE nên gọi GET /api/admin/class-dashboard/dropdowns/experts 
        /// để lấy danh sách Expert kèm Specialties để hiển thị cho Admin biết Expert đó thuộc chuyên môn nào.
        /// </remarks>
        /// <param name="classId">ID của Class</param>
        /// <param name="expertId">ID của Expert cần gán</param>
        [HttpPost("{classId:guid}/assign-expert")]
        public async Task<IActionResult> AssignExpert(Guid classId, [FromQuery] Guid expertId)
        {
            if (expertId == Guid.Empty)
                return BadRequest(new { message = "ExpertId is required." });

            try
            {
                var request = new AssignUserToClassRequestDto
                {
                    ClassId = classId,
                    ExpertId = expertId
                };

                var result = await _dashboardService.AssignUserToClassAsync(request);
                if (result == null)
                    return NotFound(new { message = "Class not found." });

                return Ok(result);
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }

        /// <summary>
        /// Gán cả Lecturer và Expert vào Class cùng lúc
        /// </summary>
        [HttpPost("{classId:guid}/assign-users")]
        public async Task<IActionResult> AssignUsers(Guid classId, [FromBody] AssignUserToClassRequestDto request)
        {
            if (classId == Guid.Empty)
                return BadRequest(new { message = "ClassId is required." });

            request.ClassId = classId;

            try
            {
                var result = await _dashboardService.AssignUserToClassAsync(request);
                if (result == null)
                    return NotFound(new { message = "Class not found." });

                return Ok(result);
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }

        /// <summary>
        /// Xóa Expert khỏi Class (không xóa Lecturer)
        /// </summary>
        [HttpPost("{classId:guid}/remove-expert")]
        public async Task<IActionResult> RemoveExpert(Guid classId)
        {
            var result = await _dashboardService.RemoveExpertFromClassAsync(classId);
            if (!result)
                return NotFound(new { message = "Class not found." });

            return Ok(new { message = "Expert removed from class successfully." });
        }

        /// <summary>
        /// Xóa Lecturer khỏi Class (không xóa Expert)
        /// </summary>
        [HttpPost("{classId:guid}/remove-lecturer")]
        public async Task<IActionResult> RemoveLecturer(Guid classId)
        {
            var result = await _dashboardService.RemoveLecturerFromClassAsync(classId);
            if (!result)
                return NotFound(new { message = "Class not found." });

            return Ok(new { message = "Lecturer removed from class successfully." });
        }

        #endregion

        #region Class Enrollment Management

        /// <summary>
        /// Xóa Student khỏi Class (xóa Enrollment)
        /// </summary>
        [HttpPost("{classId:guid}/remove-student/{studentId:guid}")]
        public async Task<IActionResult> RemoveStudentFromClass(Guid classId, Guid studentId)
        {
            var result = await _dashboardService.RemoveStudentFromClassAsync(classId, studentId);
            if (!result)
                return NotFound(new { message = "Enrollment not found." });

            return Ok(new { message = "Student removed from class successfully." });
        }

        #endregion
    }
}
