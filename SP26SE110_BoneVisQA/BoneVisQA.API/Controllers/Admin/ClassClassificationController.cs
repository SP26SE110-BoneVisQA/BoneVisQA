using BoneVisQA.Services.Interfaces.Admin;
using BoneVisQA.Services.Models.Admin;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace BoneVisQA.API.Controllers.Admin
{
    /// <summary>
    /// Controller phân loại chuyên sâu Manager Class bên Admin
    /// Cung cấp API để phân loại, quản lý và theo dõi phân loại của các lớp học
    /// </summary>
    [ApiController]
    [Route("api/admin/class-classification")]
    [Tags("Admin - Class Classification")]
    [Authorize(Roles = "Admin")]
    public class ClassClassificationController : ControllerBase
    {
        private readonly IClassClassificationService _classificationService;

        public ClassClassificationController(IClassClassificationService classificationService)
        {
            _classificationService = classificationService;
        }

        #region ==================== CORE CLASSIFICATION ====================

        /// <summary>
        /// Phân loại một lớp học theo chuyên môn
        /// </summary>
        /// <param name="request">Yêu cầu phân loại với Bone Specialty, Focus Level, Student Level, Pathology Categories</param>
        /// <returns>Kết quả phân loại chi tiết</returns>
        [HttpPost]
        public async Task<IActionResult> ClassifyClass([FromBody] ClassClassificationRequestDto request)
        {
            if (request.ClassId == Guid.Empty)
                return BadRequest(new { message = "ClassId is required." });

            try
            {
                var result = await _classificationService.ClassifyClassAsync(request);
                return Ok(result);
            }
            catch (InvalidOperationException ex)
            {
                return NotFound(new { message = ex.Message });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "An error occurred while classifying the class.", error = ex.Message });
            }
        }

        /// <summary>
        /// Lấy thông tin phân loại hiện tại của một lớp học
        /// </summary>
        /// <param name="classId">ID lớp học</param>
        /// <returns>Kết quả phân loại</returns>
        [HttpGet("{classId:guid}")]
        public async Task<IActionResult> GetClassClassification(Guid classId)
        {
            if (classId == Guid.Empty)
                return BadRequest(new { message = "ClassId is required." });

            try
            {
                var result = await _classificationService.GetClassClassificationAsync(classId);
                if (result == null)
                    return NotFound(new { message = "Class not found." });

                return Ok(result);
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "An error occurred.", error = ex.Message });
            }
        }

        /// <summary>
        /// Cập nhật phân loại lớp học
        /// </summary>
        /// <param name="request">Yêu cầu cập nhật phân loại</param>
        /// <returns>Kết quả phân loại mới</returns>
        [HttpPut]
        public async Task<IActionResult> UpdateClassification([FromBody] ClassClassificationRequestDto request)
        {
            if (request.ClassId == Guid.Empty)
                return BadRequest(new { message = "ClassId is required." });

            try
            {
                var result = await _classificationService.UpdateClassClassificationAsync(request);
                return Ok(result);
            }
            catch (InvalidOperationException ex)
            {
                return NotFound(new { message = ex.Message });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "An error occurred.", error = ex.Message });
            }
        }

        #endregion

        #region ==================== BULK CLASSIFICATION ====================

        /// <summary>
        /// Phân loại hàng loạt nhiều lớp học
        /// </summary>
        /// <param name="request">Yêu cầu phân loại hàng loạt với danh sách Class IDs</param>
        /// <returns>Kết quả phân loại hàng loạt</returns>
        [HttpPost("bulk")]
        public async Task<IActionResult> BulkClassify([FromBody] BulkClassificationRequestDto request)
        {
            if (request.ClassIds == null || !request.ClassIds.Any())
                return BadRequest(new { message = "At least one ClassId is required." });

            try
            {
                var result = await _classificationService.BulkClassifyAsync(request);
                return Ok(result);
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "An error occurred during bulk classification.", error = ex.Message });
            }
        }

        #endregion

        #region ==================== AUTO CLASSIFICATION ====================

        /// <summary>
        /// Gợi ý phân loại tự động cho một lớp học
        /// </summary>
        /// <param name="classId">ID lớp học</param>
        /// <returns>Gợi ý phân loại dựa trên Expert và dữ liệu hiện có</returns>
        [HttpGet("{classId:guid}/auto-suggestion")]
        public async Task<IActionResult> GetAutoSuggestion(Guid classId)
        {
            if (classId == Guid.Empty)
                return BadRequest(new { message = "ClassId is required." });

            try
            {
                var result = await _classificationService.GetAutoSuggestionAsync(classId);
                if (result == null)
                    return NotFound(new { message = "Class not found." });

                return Ok(result);
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "An error occurred.", error = ex.Message });
            }
        }

        /// <summary>
        /// Gợi ý phân loại tự động cho tất cả lớp học chưa được phân loại
        /// </summary>
        /// <returns>Danh sách gợi ý cho các lớp chưa phân loại</returns>
        [HttpGet("auto-suggestions/all")]
        public async Task<IActionResult> GetAllAutoSuggestions()
        {
            try
            {
                var result = await _classificationService.GetAutoSuggestionsForAllAsync();
                return Ok(result);
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "An error occurred.", error = ex.Message });
            }
        }

        /// <summary>
        /// Áp dụng gợi ý tự động cho một lớp học
        /// </summary>
        /// <param name="classId">ID lớp học</param>
        /// <param name="apply">Có áp dụng gợi ý hay không (mặc định: true)</param>
        /// <returns>Kết quả phân loại</returns>
        [HttpPost("{classId:guid}/apply-suggestion")]
        public async Task<IActionResult> ApplyAutoSuggestion(Guid classId, [FromQuery] bool apply = true)
        {
            if (classId == Guid.Empty)
                return BadRequest(new { message = "ClassId is required." });

            try
            {
                var result = await _classificationService.ApplyAutoSuggestionAsync(classId, apply);
                if (result == null)
                    return NotFound(new { message = "Class not found." });

                return Ok(result);
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "An error occurred.", error = ex.Message });
            }
        }

        #endregion

        #region ==================== EXPERT MATCHING ====================

        /// <summary>
        /// Tìm Expert phù hợp nhất cho lớp học
        /// </summary>
        /// <param name="classId">ID lớp học</param>
        /// <returns>Danh sách Expert được xếp hạng theo độ phù hợp</returns>
        [HttpGet("{classId:guid}/matching-experts")]
        public async Task<IActionResult> FindMatchingExperts(Guid classId)
        {
            if (classId == Guid.Empty)
                return BadRequest(new { message = "ClassId is required." });

            try
            {
                var result = await _classificationService.FindMatchingExpertsAsync(classId);
                return Ok(result);
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "An error occurred.", error = ex.Message });
            }
        }

        /// <summary>
        /// Tính điểm phù hợp của Expert với lớp học
        /// </summary>
        /// <param name="classId">ID lớp học</param>
        /// <param name="expertId">ID Expert</param>
        /// <returns>Kết quả ghép Expert với lớp học</returns>
        [HttpGet("{classId:guid}/expert-match/{expertId:guid}")]
        public async Task<IActionResult> CalculateExpertMatch(Guid classId, Guid expertId)
        {
            if (classId == Guid.Empty || expertId == Guid.Empty)
                return BadRequest(new { message = "ClassId and ExpertId are required." });

            try
            {
                var result = await _classificationService.CalculateExpertMatchAsync(classId, expertId);
                if (result == null)
                    return NotFound(new { message = "Class or Expert not found." });

                return Ok(result);
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "An error occurred.", error = ex.Message });
            }
        }

        #endregion

        #region ==================== DASHBOARD & SUMMARY ====================

        /// <summary>
        /// Lấy tổng hợp phân loại cho dashboard
        /// </summary>
        /// <returns>Thống kê tổng hợp phân loại</returns>
        [HttpGet("summary")]
        public async Task<IActionResult> GetClassificationSummary()
        {
            try
            {
                var result = await _classificationService.GetClassificationSummaryAsync();
                return Ok(result);
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "An error occurred.", error = ex.Message });
            }
        }

        /// <summary>
        /// Lấy danh sách lớp học với bộ lọc phân loại
        /// </summary>
        /// <param name="boneSpecialtyId">Lọc theo Bone Specialty</param>
        /// <param name="focusLevel">Lọc theo Focus Level</param>
        /// <param name="targetStudentLevel">Lọc theo Student Level</param>
        /// <param name="expertId">Lọc theo Expert</param>
        /// <param name="lecturerId">Lọc theo Lecturer</param>
        /// <param name="isClassified">Lọc theo trạng thái phân loại</param>
        /// <param name="search">Tìm kiếm theo tên</param>
        /// <param name="pageIndex">Trang hiện tại</param>
        /// <param name="pageSize">Số item/trang</param>
        /// <returns>Danh sách lớp học đã phân loại</returns>
        [HttpGet("filtered")]
        public async Task<IActionResult> GetFilteredClassifications(
            [FromQuery] Guid? boneSpecialtyId = null,
            [FromQuery] string? focusLevel = null,
            [FromQuery] string? targetStudentLevel = null,
            [FromQuery] Guid? expertId = null,
            [FromQuery] Guid? lecturerId = null,
            [FromQuery] bool? isClassified = null,
            [FromQuery] string? search = null,
            [FromQuery] int pageIndex = 1,
            [FromQuery] int pageSize = 10)
        {
            var filter = new ClassificationFilterDto
            {
                BoneSpecialtyId = boneSpecialtyId,
                FocusLevel = focusLevel,
                TargetStudentLevel = targetStudentLevel,
                ExpertId = expertId,
                LecturerId = lecturerId,
                IsClassified = isClassified,
                Search = search
            };

            try
            {
                var result = await _classificationService.GetFilteredClassificationsAsync(filter, pageIndex, pageSize);
                return Ok(result);
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "An error occurred.", error = ex.Message });
            }
        }

        #endregion

        #region ==================== VALIDATION ====================

        /// <summary>
        /// Validate phân loại của một lớp học
        /// </summary>
        /// <param name="classId">ID lớp học</param>
        /// <returns>Kết quả validation</returns>
        [HttpGet("{classId:guid}/validation")]
        public async Task<IActionResult> ValidateClassification(Guid classId)
        {
            if (classId == Guid.Empty)
                return BadRequest(new { message = "ClassId is required." });

            try
            {
                var result = await _classificationService.ValidateClassClassificationAsync(classId);
                return Ok(result);
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "An error occurred.", error = ex.Message });
            }
        }

        /// <summary>
        /// Validate tất cả phân loại
        /// </summary>
        /// <returns>Danh sách kết quả validation cho tất cả lớp học</returns>
        [HttpGet("validation/all")]
        public async Task<IActionResult> ValidateAllClassifications()
        {
            try
            {
                var result = await _classificationService.ValidateAllClassificationsAsync();
                return Ok(result);
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "An error occurred.", error = ex.Message });
            }
        }

        #endregion

        #region ==================== PATHOLOGY MANAGEMENT ====================

        /// <summary>
        /// Lấy danh sách Pathology Categories theo Bone Specialty
        /// </summary>
        /// <param name="boneSpecialtyId">ID Bone Specialty</param>
        /// <returns>Danh sách Pathology</returns>
        [HttpGet("pathologies/{boneSpecialtyId:guid}")]
        public async Task<IActionResult> GetPathologiesBySpecialty(Guid boneSpecialtyId)
        {
            if (boneSpecialtyId == Guid.Empty)
                return BadRequest(new { message = "BoneSpecialtyId is required." });

            try
            {
                var result = await _classificationService.GetPathologiesBySpecialtyAsync(boneSpecialtyId);
                return Ok(result);
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "An error occurred.", error = ex.Message });
            }
        }

        /// <summary>
        /// Cập nhật danh sách Pathology cho lớp học
        /// </summary>
        /// <param name="classId">ID lớp học</param>
        /// <param name="pathologyIds">Danh sách ID Pathology</param>
        /// <returns>Kết quả phân loại</returns>
        [HttpPut("{classId:guid}/pathologies")]
        public async Task<IActionResult> UpdateClassPathologies(Guid classId, [FromBody] List<Guid> pathologyIds)
        {
            if (classId == Guid.Empty)
                return BadRequest(new { message = "ClassId is required." });

            try
            {
                var result = await _classificationService.UpdateClassPathologiesAsync(classId, pathologyIds);
                return Ok(result);
            }
            catch (InvalidOperationException ex)
            {
                return NotFound(new { message = ex.Message });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "An error occurred.", error = ex.Message });
            }
        }

        #endregion

        #region ==================== STUDENT LEVEL MANAGEMENT ====================

        /// <summary>
        /// Lấy thông tin cấp độ sinh viên
        /// </summary>
        /// <param name="level">Cấp độ (Beginner, Intermediate, Advanced, Expert)</param>
        /// <returns>Thông tin chi tiết cấp độ</returns>
        [HttpGet("student-levels/{level}")]
        public async Task<IActionResult> GetStudentLevelInfo(string level)
        {
            if (string.IsNullOrWhiteSpace(level))
                return BadRequest(new { message = "Level is required." });

            try
            {
                var result = await _classificationService.GetStudentLevelInfoAsync(level);
                if (result == null)
                    return NotFound(new { message = "Level not found." });

                return Ok(result);
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "An error occurred.", error = ex.Message });
            }
        }

        /// <summary>
        /// Lấy tất cả cấp độ sinh viên
        /// </summary>
        /// <returns>Danh sách tất cả cấp độ sinh viên</returns>
        [HttpGet("student-levels")]
        public async Task<IActionResult> GetAllStudentLevels()
        {
            try
            {
                var result = await _classificationService.GetAllStudentLevelsAsync();
                return Ok(result);
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "An error occurred.", error = ex.Message });
            }
        }

        #endregion

        #region ==================== FOCUS LEVEL MANAGEMENT ====================

        /// <summary>
        /// Lấy thông tin cấp độ tập trung
        /// </summary>
        /// <param name="level">Cấp độ (Basic, Intermediate, Advanced, Specialized)</param>
        /// <returns>Thông tin chi tiết cấp độ</returns>
        [HttpGet("focus-levels/{level}")]
        public async Task<IActionResult> GetFocusLevelInfo(string level)
        {
            if (string.IsNullOrWhiteSpace(level))
                return BadRequest(new { message = "Level is required." });

            try
            {
                var result = await _classificationService.GetFocusLevelInfoAsync(level);
                if (result == null)
                    return NotFound(new { message = "Level not found." });

                return Ok(result);
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "An error occurred.", error = ex.Message });
            }
        }

        /// <summary>
        /// Lấy tất cả cấp độ tập trung
        /// </summary>
        /// <returns>Danh sách tất cả cấp độ tập trung</returns>
        [HttpGet("focus-levels")]
        public async Task<IActionResult> GetAllFocusLevels()
        {
            try
            {
                var result = await _classificationService.GetAllFocusLevelsAsync();
                return Ok(result);
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "An error occurred.", error = ex.Message });
            }
        }

        #endregion
    }
}
