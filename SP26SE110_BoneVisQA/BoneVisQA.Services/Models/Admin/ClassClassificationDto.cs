using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace BoneVisQA.Services.Models.Admin
{
    #region ==================== CLASSIFICATION REQUEST/RESPONSE ====================

    /// <summary>
    /// DTO cho yêu cầu phân loại lớp học
    /// </summary>
    public class ClassClassificationRequestDto
    {
        /// <summary>
        /// ID lớp học cần phân loại
        /// </summary>
        [Required]
        public Guid ClassId { get; set; }

        /// <summary>
        /// ID Chuyên khoa xương (Bone Specialty)
        /// </summary>
        public Guid? BoneSpecialtyId { get; set; }

        /// <summary>
        /// Cấp độ tập trung (Basic, Intermediate, Advanced, Specialized)
        /// </summary>
        public string? FocusLevel { get; set; }

        /// <summary>
        /// Cấp độ sinh viên mục tiêu (Beginner, Intermediate, Advanced, Expert)
        /// </summary>
        public string? TargetStudentLevel { get; set; }

        /// <summary>
        /// Danh sách ID các danh mục bệnh lý (Pathology Categories)
        /// </summary>
        public List<Guid>? PathologyCategoryIds { get; set; }

        /// <summary>
        /// Cho phép gợi ý tự động
        /// </summary>
        public bool AutoSuggest { get; set; } = true;
    }

    /// <summary>
    /// DTO cho kết quả phân loại lớp học
    /// </summary>
    public class ClassClassificationResultDto
    {
        public Guid ClassId { get; set; }
        public string ClassName { get; set; } = null!;
        public bool IsClassified { get; set; }
        public ClassificationType ClassificationType { get; set; }
        public string ClassificationLevel { get; set; } = null!;
        public int ConfidenceScore { get; set; }
        public List<string> Warnings { get; set; } = new();
        public List<string> Suggestions { get; set; } = new();
        public ClassificationDetailDto Details { get; set; } = new();
    }

    /// <summary>
    /// Chi tiết phân loại
    /// </summary>
    public class ClassificationDetailDto
    {
        public Guid? BoneSpecialtyId { get; set; }
        public string? BoneSpecialtyName { get; set; }
        public string? BoneSpecialtyCode { get; set; }
        public int SpecialtyLevel { get; set; }
        public Guid? ParentSpecialtyId { get; set; }
        public string? ParentSpecialtyName { get; set; }

        public List<PathologyClassificationDto> PathologyCategories { get; set; } = new();
        public StudentLevelClassificationDto StudentLevelInfo { get; set; } = new();
        public FocusLevelClassificationDto FocusLevelInfo { get; set; } = new();
        public ExpertMatchClassificationDto? ExpertMatch { get; set; }
    }

    #endregion

    #region ==================== PATHOLOGY CLASSIFICATION ====================

    /// <summary>
    /// Phân loại theo bệnh lý
    /// </summary>
    public class PathologyClassificationDto
    {
        public Guid Id { get; set; }
        public string Code { get; set; } = null!;
        public string Name { get; set; } = null!;
        public Guid? ParentBoneSpecialtyId { get; set; }
        public string? ParentBoneSpecialtyName { get; set; }
        public int DisplayOrder { get; set; }
        public bool IsActive { get; set; }
        public int UsageCount { get; set; }
    }

    #endregion

    #region ==================== STUDENT LEVEL CLASSIFICATION ====================

    /// <summary>
    /// Thông tin phân loại cấp độ sinh viên
    /// </summary>
    public class StudentLevelClassificationDto
    {
        public string Level { get; set; } = null!;
        public string DisplayName { get; set; } = null!;
        public string Description { get; set; } = null!;
        public int MinCasesRequired { get; set; }
        public int MinQuizzesRequired { get; set; }
        public List<string> RecommendedPathologyCategories { get; set; } = new();
        public StudentLevelProgressDto ProgressInfo { get; set; } = new();
    }

    /// <summary>
    /// Tiến độ học tập theo cấp độ
    /// </summary>
    public class StudentLevelProgressDto
    {
        public int CasesStudied { get; set; }
        public int QuizzesTaken { get; set; }
        public double AverageScore { get; set; }
        public string CurrentLevel { get; set; } = null!;
        public string SuggestedNextLevel { get; set; } = null!;
    }

    #endregion

    #region ==================== FOCUS LEVEL CLASSIFICATION ====================

    /// <summary>
    /// Thông tin phân loại cấp độ tập trung
    /// </summary>
    public class FocusLevelClassificationDto
    {
        public string Level { get; set; } = null!;
        public string DisplayName { get; set; } = null!;
        public string Description { get; set; } = null!;
        public int Order { get; set; }
        public List<string> Prerequisites { get; set; } = new();
        public List<string> LearningOutcomes { get; set; } = new();
        public List<string> RecommendedActivities { get; set; } = new();
    }

    #endregion

    #region ==================== EXPERT MATCH CLASSIFICATION ====================

    /// <summary>
    /// Kết quả ghép Expert với lớp học
    /// </summary>
    public class ExpertMatchClassificationDto
    {
        public Guid ExpertId { get; set; }
        public string ExpertName { get; set; } = null!;
        public string? Email { get; set; }
        public int MatchScore { get; set; }
        public string MatchLevel { get; set; } = null!;
        public List<ExpertSpecialtyMatchDto> MatchingSpecialties { get; set; } = new();
        public int ProficiencyLevel { get; set; }
        public int YearsExperience { get; set; }
    }

    /// <summary>
    /// Ghép chuyên môn Expert
    /// </summary>
    public class ExpertSpecialtyMatchDto
    {
        public Guid SpecialtyId { get; set; }
        public string SpecialtyName { get; set; } = null!;
        public Guid? PathologyId { get; set; }
        public string? PathologyName { get; set; }
        public int ProficiencyLevel { get; set; }
        public bool IsPrimary { get; set; }
        public bool IsMatch { get; set; }
    }

    #endregion

    #region ==================== BULK CLASSIFICATION ====================

    /// <summary>
    /// Yêu cầu phân loại hàng loạt
    /// </summary>
    public class BulkClassificationRequestDto
    {
        public List<Guid> ClassIds { get; set; } = new();
        public Guid? BoneSpecialtyId { get; set; }
        public string? FocusLevel { get; set; }
        public string? TargetStudentLevel { get; set; }
        public bool AutoMatchExperts { get; set; } = true;
    }

    /// <summary>
    /// Kết quả phân loại hàng loạt
    /// </summary>
    public class BulkClassificationResultDto
    {
        public int TotalClasses { get; set; }
        public int SuccessCount { get; set; }
        public int WarningCount { get; set; }
        public int ErrorCount { get; set; }
        public List<ClassClassificationResultDto> Results { get; set; } = new();
        public List<string> Errors { get; set; } = new();
    }

    #endregion

    #region ==================== CLASSIFICATION SUMMARY ====================

    /// <summary>
    /// Tổng hợp phân loại cho dashboard
    /// </summary>
    public class ClassificationSummaryDto
    {
        public int TotalClasses { get; set; }
        public int ClassifiedClasses { get; set; }
        public int UnclassifiedClasses { get; set; }
        public Dictionary<string, int> DistributionBySpecialty { get; set; } = new();
        public Dictionary<string, int> DistributionByFocusLevel { get; set; } = new();
        public Dictionary<string, int> DistributionByStudentLevel { get; set; } = new();
        public Dictionary<string, int> ClassesWithoutExpert { get; set; } = new();
        public List<ClassNeedsAttentionDto> ClassesNeedingAttention { get; set; } = new();
    }

    /// <summary>
    /// Lớp cần chú ý
    /// </summary>
    public class ClassNeedsAttentionDto
    {
        public Guid ClassId { get; set; }
        public string ClassName { get; set; } = null!;
        public string Issue { get; set; } = null!;
        public string Severity { get; set; } = null!;
        public string? SuggestedAction { get; set; }
    }

    #endregion

    #region ==================== CLASSIFICATION FILTER ====================

    /// <summary>
    /// Bộ lọc phân loại
    /// </summary>
    public class ClassificationFilterDto
    {
        public Guid? BoneSpecialtyId { get; set; }
        public string? FocusLevel { get; set; }
        public string? TargetStudentLevel { get; set; }
        public Guid? ExpertId { get; set; }
        public Guid? LecturerId { get; set; }
        public bool? IsClassified { get; set; }
        public string? Search { get; set; }
    }

    /// <summary>
    /// Kết quả phân loại với bộ lọc
    /// </summary>
    public class FilteredClassificationResultDto
    {
        public ClassificationFilterDto AppliedFilter { get; set; } = new();
        public int TotalCount { get; set; }
        public int PageIndex { get; set; }
        public int PageSize { get; set; }
        public int TotalPages { get; set; }
        public List<ClassClassificationResultDto> Items { get; set; } = new();
    }

    #endregion

    #region ==================== AUTO CLASSIFICATION ====================

    /// <summary>
    /// Gợi ý phân loại tự động
    /// </summary>
    public class AutoClassificationSuggestionDto
    {
        public Guid ClassId { get; set; }
        public string ClassName { get; set; } = null!;
        public Guid? SuggestedBoneSpecialtyId { get; set; }
        public string? SuggestedBoneSpecialtyName { get; set; }
        public string? SuggestedFocusLevel { get; set; }
        public string? SuggestedStudentLevel { get; set; }
        public List<Guid>? SuggestedPathologyIds { get; set; }
        public int ConfidenceScore { get; set; }
        public string Reasoning { get; set; } = null!;
        public List<ExpertMatchSuggestionDto> SuggestedExperts { get; set; } = new();
    }

    /// <summary>
    /// Gợi ý Expert phù hợp
    /// </summary>
    public class ExpertMatchSuggestionDto
    {
        public Guid ExpertId { get; set; }
        public string ExpertName { get; set; } = null!;
        public int MatchScore { get; set; }
        public int ProficiencyLevel { get; set; }
        public List<string> MatchingSpecialties { get; set; } = new();
    }

    #endregion

    #region ==================== VALIDATION ====================

    /// <summary>
    /// Kết quả validation phân loại
    /// </summary>
    public class ClassificationValidationDto
    {
        public bool IsValid { get; set; }
        public List<string> Errors { get; set; } = new();
        public List<string> Warnings { get; set; } = new();
        public List<string> Info { get; set; } = new();
        public ClassificationHealthDto Health { get; set; } = new();
    }

    /// <summary>
    /// Tình trạng sức khỏe phân loại
    /// </summary>
    public class ClassificationHealthDto
    {
        public int Score { get; set; }
        public string Status { get; set; } = null!;
        public List<string> Issues { get; set; } = new();
        public List<string> Recommendations { get; set; } = new();
    }

    #endregion
}
