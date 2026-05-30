using System.ComponentModel.DataAnnotations;

namespace BoneVisQA.Services.Models.Admin
{
    #region ==================== FOCUS LEVEL ENUMS ====================
    
    /// <summary>
    /// Cấp độ tập trung của lớp học (theo mức độ chuyên sâu)
    /// </summary>
    public enum ClassFocusLevel
    {
        /// <summary>
        /// Cơ bản - Giới thiệu kiến thức nền tảng
        /// </summary>
        [Display(Name = "Cơ bản", Description = "Giới thiệu kiến thức nền tảng")]
        Basic = 1,
        
        /// <summary>
        /// Trung gian - Mở rộng và áp dụng kiến thức
        /// </summary>
        [Display(Name = "Trung gian", Description = "Mở rộng và áp dụng kiến thức")]
        Intermediate = 2,
        
        /// <summary>
        /// Nâng cao - Chuyên sâu và chuyên biệt
        /// </summary>
        [Display(Name = "Nâng cao", Description = "Chuyên sâu và chuyên biệt")]
        Advanced = 3,
        
        /// <summary>
        /// Chuyên ngành - Nghiên cứu chuyên sâu
        /// </summary>
        [Display(Name = "Chuyên ngành", Description = "Nghiên cứu chuyên sâu")]
        Specialized = 4
    }

    #endregion

    #region ==================== STUDENT LEVEL ENUMS ====================
    
    /// <summary>
    /// Cấp độ sinh viên mục tiêu
    /// </summary>
    public enum TargetStudentLevel
    {
        /// <summary>
        /// Mới bắt đầu - Chưa có kinh nghiệm
        /// </summary>
        [Display(Name = "Mới bắt đầu", Description = "Chưa có kinh nghiệm")]
        Beginner = 1,
        
        /// <summary>
        /// Trung gian - Có kinh nghiệm cơ bản
        /// </summary>
        [Display(Name = "Trung gian", Description = "Có kinh nghiệm cơ bản")]
        Intermediate = 2,
        
        /// <summary>
        /// Nâng cao - Có kinh nghiệm tốt
        /// </summary>
        [Display(Name = "Nâng cao", Description = "Có kinh nghiệm tốt")]
        Advanced = 3,
        
        /// <summary>
        /// Chuyên gia - Có chuyên môn sâu
        /// </summary>
        [Display(Name = "Chuyên gia", Description = "Có chuyên môn sâu")]
        Expert = 4
    }

    #endregion

    #region ==================== CLASS STATUS ENUMS ====================
    
    /// <summary>
    /// Trạng thái lớp học
    /// </summary>
    public enum ClassStatus
    {
        /// <summary>
        /// Đang chờ - Chưa bắt đầu
        /// </summary>
        [Display(Name = "Đang chờ", Description = "Lớp học đang chờ bắt đầu")]
        Pending = 1,
        
        /// <summary>
        /// Đang hoạt động - Đang diễn ra
        /// </summary>
        [Display(Name = "Đang hoạt động", Description = "Lớp học đang diễn ra")]
        Active = 2,
        
        /// <summary>
        /// Đã kết thúc - Hoàn thành
        /// </summary>
        [Display(Name = "Đã kết thúc", Description = "Lớp học đã hoàn thành")]
        Completed = 3,
        
        /// <summary>
        /// Tạm dừng - Tạm ngưng
        /// </summary>
        [Display(Name = "Tạm dừng", Description = "Lớp học tạm ngưng")]
        Suspended = 4
    }

    #endregion

    #region ==================== CLASSIFICATION TYPE ENUMS ====================
    
    /// <summary>
    /// Loại phân loại chuyên môn
    /// </summary>
    public enum ClassificationType
    {
        /// <summary>
        /// Theo Bộ xương - Cơ quan
        /// </summary>
        [Display(Name = "Theo Bộ xương", Description = "Phân loại theo Bone Specialty")]
        ByBoneSpecialty = 1,
        
        /// <summary>
        /// Theo Bệnh lý
        /// </summary>
        [Display(Name = "Theo Bệnh lý", Description = "Phân loại theo Pathology Category")]
        ByPathology = 2,
        
        /// <summary>
        /// Theo Cấp độ học tập
        /// </summary>
        [Display(Name = "Theo Cấp độ", Description = "Phân loại theo Student Level")]
        ByStudentLevel = 3,
        
        /// <summary>
        /// Theo Chuyên gia
        /// </summary>
        [Display(Name = "Theo Chuyên gia", Description = "Phân loại theo Expert")]
        ByExpert = 4,
        
        /// <summary>
        /// Theo Giảng viên
        /// </summary>
        [Display(Name = "Theo Giảng viên", Description = "Phân loại theo Lecturer")]
        ByLecturer = 5
    }

    #endregion

    #region ==================== CLASSIFICATION RESULT ====================
    
    /// <summary>
    /// Kết quả phân loại
    /// </summary>
    public enum ClassificationResult
    {
        /// <summary>
        /// Thành công
        /// </summary>
        [Display(Name = "Thành công", Description = "Phân loại thành công")]
        Success = 1,
        
        /// <summary>
        /// Cảnh báo - Cần xem xét
        /// </summary>
        [Display(Name = "Cảnh báo", Description = "Có vấn đề cần xem xét")]
        Warning = 2,
        
        /// <summary>
        /// Lỗi - Không hợp lệ
        /// </summary>
        [Display(Name = "Lỗi", Description = "Phân loại không hợp lệ")]
        Error = 3
    }

    #endregion
}
