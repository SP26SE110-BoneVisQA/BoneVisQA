using BoneVisQA.Services.Models.Admin;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace BoneVisQA.Services.Interfaces.Admin
{
    /// <summary>
    /// Interface cho service phân loại chuyên sâu Manager Class bên Admin
    /// </summary>
    public interface IClassClassificationService
    {
        #region ==================== CORE CLASSIFICATION ====================

        /// <summary>
        /// Phân loại một lớp học theo chuyên môn
        /// </summary>
        /// <param name="request">Yêu cầu phân loại</param>
        /// <returns>Kết quả phân loại chi tiết</returns>
        Task<ClassClassificationResultDto> ClassifyClassAsync(ClassClassificationRequestDto request);

        /// <summary>
        /// Lấy thông tin phân loại hiện tại của lớp học
        /// </summary>
        /// <param name="classId">ID lớp học</param>
        /// <returns>Kết quả phân loại</returns>
        Task<ClassClassificationResultDto?> GetClassClassificationAsync(Guid classId);

        /// <summary>
        /// Cập nhật phân loại lớp học
        /// </summary>
        /// <param name="request">Yêu cầu cập nhật</param>
        /// <returns>Kết quả phân loại mới</returns>
        Task<ClassClassificationResultDto> UpdateClassClassificationAsync(ClassClassificationRequestDto request);

        #endregion

        #region ==================== BULK CLASSIFICATION ====================

        /// <summary>
        /// Phân loại hàng loạt nhiều lớp học
        /// </summary>
        /// <param name="request">Yêu cầu phân loại hàng loạt</param>
        /// <returns>Kết quả phân loại hàng loạt</returns>
        Task<BulkClassificationResultDto> BulkClassifyAsync(BulkClassificationRequestDto request);

        #endregion

        #region ==================== AUTO CLASSIFICATION ====================

        /// <summary>
        /// Gợi ý phân loại tự động cho một lớp học
        /// </summary>
        /// <param name="classId">ID lớp học</param>
        /// <returns>Gợi ý phân loại</returns>
        Task<AutoClassificationSuggestionDto?> GetAutoSuggestionAsync(Guid classId);

        /// <summary>
        /// Gợi ý phân loại tự động cho tất cả lớp học chưa được phân loại
        /// </summary>
        /// <returns>Danh sách gợi ý</returns>
        Task<List<AutoClassificationSuggestionDto>> GetAutoSuggestionsForAllAsync();

        /// <summary>
        /// Áp dụng gợi ý tự động cho một lớp học
        /// </summary>
        /// <param name="classId">ID lớp học</param>
        /// <param name="apply">Có áp dụng hay không</param>
        /// <returns>Kết quả phân loại</returns>
        Task<ClassClassificationResultDto?> ApplyAutoSuggestionAsync(Guid classId, bool apply = true);

        #endregion

        #region ==================== EXPERT MATCHING ====================

        /// <summary>
        /// Tìm Expert phù hợp nhất cho lớp học
        /// </summary>
        /// <param name="classId">ID lớp học</param>
        /// <returns>Danh sách Expert phù hợp</returns>
        Task<List<ExpertMatchClassificationDto>> FindMatchingExpertsAsync(Guid classId);

        /// <summary>
        /// Tính điểm phù hợp của Expert với lớp học
        /// </summary>
        /// <param name="classId">ID lớp học</param>
        /// <param name="expertId">ID Expert</param>
        /// <returns>Kết quả ghép</returns>
        Task<ExpertMatchClassificationDto?> CalculateExpertMatchAsync(Guid classId, Guid expertId);

        #endregion

        #region ==================== DASHBOARD & SUMMARY ====================

        /// <summary>
        /// Lấy tổng hợp phân loại cho dashboard
        /// </summary>
        /// <returns>Tổng hợp phân loại</returns>
        Task<ClassificationSummaryDto> GetClassificationSummaryAsync();

        /// <summary>
        /// Lấy danh sách lớp học với bộ lọc phân loại
        /// </summary>
        /// <param name="filter">Bộ lọc</param>
        /// <param name="pageIndex">Trang hiện tại</param>
        /// <param name="pageSize">Số item/trang</param>
        /// <returns>Kết quả phân trang</returns>
        Task<FilteredClassificationResultDto> GetFilteredClassificationsAsync(
            ClassificationFilterDto filter, 
            int pageIndex = 1, 
            int pageSize = 10);

        #endregion

        #region ==================== VALIDATION ====================

        /// <summary>
        /// Validate phân loại của lớp học
        /// </summary>
        /// <param name="classId">ID lớp học</param>
        /// <returns>Kết quả validation</returns>
        Task<ClassificationValidationDto> ValidateClassClassificationAsync(Guid classId);

        /// <summary>
        /// Validate tất cả phân loại
        /// </summary>
        /// <returns>Danh sách validation</returns>
        Task<List<ClassificationValidationDto>> ValidateAllClassificationsAsync();

        #endregion

        #region ==================== PATHOLOGY MANAGEMENT ====================

        /// <summary>
        /// Lấy danh sách Pathology Categories theo Bone Specialty
        /// </summary>
        /// <param name="boneSpecialtyId">ID Bone Specialty</param>
        /// <returns>Danh sách Pathology</returns>
        Task<List<PathologyClassificationDto>> GetPathologiesBySpecialtyAsync(Guid boneSpecialtyId);

        /// <summary>
        /// Cập nhật danh sách Pathology cho lớp học
        /// </summary>
        /// <param name="classId">ID lớp học</param>
        /// <param name="pathologyIds">Danh sách ID Pathology</param>
        /// <returns>Kết quả</returns>
        Task<ClassClassificationResultDto> UpdateClassPathologiesAsync(Guid classId, List<Guid> pathologyIds);

        #endregion

        #region ==================== STUDENT LEVEL MANAGEMENT ====================

        /// <summary>
        /// Lấy thông tin cấp độ sinh viên
        /// </summary>
        /// <param name="level">Cấp độ</param>
        /// <returns>Thông tin cấp độ</returns>
        Task<StudentLevelClassificationDto?> GetStudentLevelInfoAsync(string level);

        /// <summary>
        /// Lấy tất cả cấp độ sinh viên
        /// </summary>
        /// <returns>Danh sách cấp độ</returns>
        Task<List<StudentLevelClassificationDto>> GetAllStudentLevelsAsync();

        #endregion

        #region ==================== FOCUS LEVEL MANAGEMENT ====================

        /// <summary>
        /// Lấy thông tin cấp độ tập trung
        /// </summary>
        /// <param name="level">Cấp độ</param>
        /// <returns>Thông tin cấp độ</returns>
        Task<FocusLevelClassificationDto?> GetFocusLevelInfoAsync(string level);

        /// <summary>
        /// Lấy tất cả cấp độ tập trung
        /// </summary>
        /// <returns>Danh sách cấp độ</returns>
        Task<List<FocusLevelClassificationDto>> GetAllFocusLevelsAsync();

        #endregion
    }
}
