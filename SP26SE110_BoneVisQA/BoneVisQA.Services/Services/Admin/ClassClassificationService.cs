using BoneVisQA.Repositories.Models;
using BoneVisQA.Repositories.UnitOfWork;
using BoneVisQA.Services.Interfaces.Admin;
using BoneVisQA.Services.Models.Admin;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;

namespace BoneVisQA.Services.Services.Admin
{
    /// <summary>
    /// Service phân loại chuyên sâu Manager Class bên Admin
    /// </summary>
    public class ClassClassificationService : IClassClassificationService
    {
        private readonly IUnitOfWork _unitOfWork;
        private readonly ILogger<ClassClassificationService> _logger;

        #region ==================== FOCUS LEVEL DATA ====================
        
        private static readonly Dictionary<string, FocusLevelClassificationDto> FocusLevelData = new()
        {
            ["Basic"] = new FocusLevelClassificationDto
            {
                Level = "Basic",
                DisplayName = "Cơ bản",
                Description = "Giới thiệu kiến thức nền tảng về cơ-xương-khớp",
                Order = 1,
                Prerequisites = new List<string>(),
                LearningOutcomes = new List<string> 
                { 
                    "Hiểu cấu trúc cơ bản của xương",
                    "Nhận biết các loại khớp thông dụng",
                    "Phân biệt được các loại gãy xương cơ bản"
                },
                RecommendedActivities = new List<string>
                {
                    "Học lý thuyết qua hình ảnh",
                    "Xem video minh hoạ",
                    "Làm quiz cơ bản"
                }
            },
            ["Intermediate"] = new FocusLevelClassificationDto
            {
                Level = "Intermediate",
                DisplayName = "Trung gian",
                Description = "Mở rộng và áp dụng kiến thức về bệnh lý cơ-xương-khớp",
                Order = 2,
                Prerequisites = new List<string> { "Basic" },
                LearningOutcomes = new List<string>
                {
                    "Phân tích được hình ảnh X-quang thông thường",
                    "Chẩn đoán được các bệnh lý phổ biến",
                    "Đề xuất được phương pháp điều trị cơ bản"
                },
                RecommendedActivities = new List<string>
                {
                    "Phân tích case study",
                    "Thảo luận nhóm",
                    "Làm quiz nâng cao"
                }
            },
            ["Advanced"] = new FocusLevelClassificationDto
            {
                Level = "Advanced",
                DisplayName = "Nâng cao",
                Description = "Chuyên sâu về chẩn đoán và điều trị bệnh lý phức tạp",
                Order = 3,
                Prerequisites = new List<string> { "Basic", "Intermediate" },
                LearningOutcomes = new List<string>
                {
                    "Chẩn đoán được các ca khó",
                    "Phân tích hình ảnh CT/MRI",
                    "Lập kế hoạch điều trị toàn diện"
                },
                RecommendedActivities = new List<string>
                {
                    "Giải quyết ca lâm sàng phức tạp",
                    "Nghiên cứu tài liệu chuyên ngành",
                    "Thuyết trình ca bệnh"
                }
            },
            ["Specialized"] = new FocusLevelClassificationDto
            {
                Level = "Specialized",
                DisplayName = "Chuyên ngành",
                Description = "Nghiên cứu chuyên sâu một lĩnh vực cụ thể",
                Order = 4,
                Prerequisites = new List<string> { "Basic", "Intermediate", "Advanced" },
                LearningOutcomes = new List<string>
                {
                    "Thành thạo kỹ thuật chuyên biệt",
                    "Nghiên cứu và cập nhật kiến thức mới",
                    "Đào tạo và hướng dẫn chuyên môn"
                },
                RecommendedActivities = new List<string>
                {
                    "Nghiên cứu học thuật",
                    "Tham gia hội nghị chuyên đề",
                    "Viết bài báo khoa học"
                }
            }
        };

        #endregion

        #region ==================== STUDENT LEVEL DATA ====================
        
        private static readonly Dictionary<string, StudentLevelClassificationDto> StudentLevelData = new()
        {
            ["Beginner"] = new StudentLevelClassificationDto
            {
                Level = "Beginner",
                DisplayName = "Mới bắt đầu",
                Description = "Sinh viên chưa có kinh nghiệm về cơ-xương-khớp",
                MinCasesRequired = 5,
                MinQuizzesRequired = 3,
                RecommendedPathologyCategories = new List<string> { "Gãy xương cơ bản", "Viêm khớp đơn giản" },
                ProgressInfo = new StudentLevelProgressDto
                {
                    CasesStudied = 0,
                    QuizzesTaken = 0,
                    AverageScore = 0,
                    CurrentLevel = "Beginner",
                    SuggestedNextLevel = "Intermediate"
                }
            },
            ["Intermediate"] = new StudentLevelClassificationDto
            {
                Level = "Intermediate",
                DisplayName = "Trung gian",
                Description = "Sinh viên có kiến thức cơ bản và một số kinh nghiệm",
                MinCasesRequired = 15,
                MinQuizzesRequired = 10,
                RecommendedPathologyCategories = new List<string> { "Thoái hóa khớp", "Chấn thương thể thao" },
                ProgressInfo = new StudentLevelProgressDto
                {
                    CasesStudied = 0,
                    QuizzesTaken = 0,
                    AverageScore = 0,
                    CurrentLevel = "Intermediate",
                    SuggestedNextLevel = "Advanced"
                }
            },
            ["Advanced"] = new StudentLevelClassificationDto
            {
                Level = "Advanced",
                DisplayName = "Nâng cao",
                Description = "Sinh viên có kiến thức vững và kinh nghiệm tốt",
                MinCasesRequired = 30,
                MinQuizzesRequired = 20,
                RecommendedPathologyCategories = new List<string> { "Khối u xương", "Dị dạng bẩm sinh" },
                ProgressInfo = new StudentLevelProgressDto
                {
                    CasesStudied = 0,
                    QuizzesTaken = 0,
                    AverageScore = 0,
                    CurrentLevel = "Advanced",
                    SuggestedNextLevel = "Expert"
                }
            },
            ["Expert"] = new StudentLevelClassificationDto
            {
                Level = "Expert",
                DisplayName = "Chuyên gia",
                Description = "Chuyên gia có kiến thức sâu và kinh nghiệm phong phú",
                MinCasesRequired = 50,
                MinQuizzesRequired = 30,
                RecommendedPathologyCategories = new List<string> { "Phẫu thuật phức tạp", "Nghiên cứu lâm sàng" },
                ProgressInfo = new StudentLevelProgressDto
                {
                    CasesStudied = 0,
                    QuizzesTaken = 0,
                    AverageScore = 0,
                    CurrentLevel = "Expert",
                    SuggestedNextLevel = "Expert"
                }
            }
        };

        #endregion

        #region ==================== CONSTRUCTOR ====================

        public ClassClassificationService(IUnitOfWork unitOfWork, ILogger<ClassClassificationService> logger)
        {
            _unitOfWork = unitOfWork;
            _logger = logger;
        }

        #endregion

        #region ==================== CORE CLASSIFICATION ====================

        /// <summary>
        /// Phân loại một lớp học theo chuyên môn
        /// </summary>
        public async Task<ClassClassificationResultDto> ClassifyClassAsync(ClassClassificationRequestDto request)
        {
            try
            {
                var academicClass = await _unitOfWork.AcademicClassRepository
                    .GetQueryable()
                    .Include(c => c.ClassSpecialty)
                        .ThenInclude(s => s.Parent)
                    .Include(c => c.Expert)
                        .ThenInclude(e => e.ExpertSpecialties)
                            .ThenInclude(es => es.BoneSpecialty)
                    .Include(c => c.Expert)
                        .ThenInclude(e => e.ExpertSpecialties)
                            .ThenInclude(es => es.PathologyCategory)
                    .FirstOrDefaultAsync(c => c.Id == request.ClassId);

                if (academicClass == null)
                    throw new InvalidOperationException("Class not found.");

                // Update classification fields
                if (request.BoneSpecialtyId.HasValue)
                    academicClass.ClassSpecialtyId = request.BoneSpecialtyId.Value;

                academicClass.FocusLevel = request.FocusLevel ?? academicClass.FocusLevel ?? "Basic";
                academicClass.TargetStudentLevel = request.TargetStudentLevel ?? academicClass.TargetStudentLevel ?? "Beginner";

                if (request.PathologyCategoryIds != null && request.PathologyCategoryIds.Any())
                {
                    academicClass.TargetPathologyCategories = JsonSerializer.Serialize(request.PathologyCategoryIds);
                }

                academicClass.UpdatedAt = DateTime.UtcNow;
                _unitOfWork.AcademicClassRepository.Update(academicClass);
                await _unitOfWork.SaveAsync();

                return await BuildClassificationResultAsync(academicClass);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error classifying class {ClassId}", request.ClassId);
                throw;
            }
        }

        /// <summary>
        /// Lấy thông tin phân loại hiện tại của lớp học
        /// </summary>
        public async Task<ClassClassificationResultDto?> GetClassClassificationAsync(Guid classId)
        {
            var academicClass = await _unitOfWork.AcademicClassRepository
                .GetQueryable()
                .Include(c => c.ClassSpecialty)
                    .ThenInclude(s => s.Parent)
                .Include(c => c.Expert)
                    .ThenInclude(e => e.ExpertSpecialties)
                        .ThenInclude(es => es.BoneSpecialty)
                .Include(c => c.Expert)
                    .ThenInclude(e => e.ExpertSpecialties)
                        .ThenInclude(es => es.PathologyCategory)
                .FirstOrDefaultAsync(c => c.Id == classId);

            if (academicClass == null)
                return null;

            return await BuildClassificationResultAsync(academicClass);
        }

        /// <summary>
        /// Cập nhật phân loại lớp học
        /// </summary>
        public async Task<ClassClassificationResultDto> UpdateClassClassificationAsync(ClassClassificationRequestDto request)
        {
            return await ClassifyClassAsync(request);
        }

        #endregion

        #region ==================== BULK CLASSIFICATION ====================

        /// <summary>
        /// Phân loại hàng loạt nhiều lớp học
        /// </summary>
        public async Task<BulkClassificationResultDto> BulkClassifyAsync(BulkClassificationRequestDto request)
        {
            var result = new BulkClassificationResultDto
            {
                TotalClasses = request.ClassIds.Count
            };

            foreach (var classId in request.ClassIds)
            {
                try
                {
                    var classifyRequest = new ClassClassificationRequestDto
                    {
                        ClassId = classId,
                        BoneSpecialtyId = request.BoneSpecialtyId,
                        FocusLevel = request.FocusLevel,
                        TargetStudentLevel = request.TargetStudentLevel,
                        AutoSuggest = request.AutoMatchExperts
                    };

                    var classificationResult = await ClassifyClassAsync(classifyRequest);
                    result.Results.Add(classificationResult);

                    if (classificationResult.IsClassified)
                        result.SuccessCount++;
                    else
                        result.WarningCount++;
                }
                catch (Exception ex)
                {
                    result.ErrorCount++;
                    result.Errors.Add($"Class {classId}: {ex.Message}");
                    _logger.LogError(ex, "Error bulk classifying class {ClassId}", classId);
                }
            }

            return result;
        }

        #endregion

        #region ==================== AUTO CLASSIFICATION ====================

        /// <summary>
        /// Gợi ý phân loại tự động cho một lớp học
        /// </summary>
        public async Task<AutoClassificationSuggestionDto?> GetAutoSuggestionAsync(Guid classId)
        {
            var academicClass = await _unitOfWork.AcademicClassRepository
                .GetQueryable()
                .Include(c => c.ClassEnrollments)
                .Include(c => c.Expert)
                    .ThenInclude(e => e.ExpertSpecialties)
                        .ThenInclude(es => es.BoneSpecialty)
                .FirstOrDefaultAsync(c => c.Id == classId);

            if (academicClass == null)
                return null;

            var suggestion = new AutoClassificationSuggestionDto
            {
                ClassId = classId,
                ClassName = academicClass.ClassName,
                ConfidenceScore = 0,
                Reasoning = ""
            };

            // Auto-suggest based on expert's specialties
            if (academicClass.Expert != null && academicClass.Expert.ExpertSpecialties.Any())
            {
                var primarySpecialty = academicClass.Expert.ExpertSpecialties
                    .Where(es => es.IsPrimary && es.IsActive)
                    .FirstOrDefault();

                if (primarySpecialty != null)
                {
                    suggestion.SuggestedBoneSpecialtyId = primarySpecialty.BoneSpecialtyId;
                    suggestion.SuggestedBoneSpecialtyName = primarySpecialty.BoneSpecialty?.Name;
                    suggestion.ConfidenceScore = primarySpecialty.ProficiencyLevel * 20;
                    suggestion.Reasoning = $"Dựa trên chuyên môn chính của Expert {academicClass.Expert.FullName}";
                }
            }

            // Suggest based on student count
            var studentCount = academicClass.ClassEnrollments.Count;
            if (studentCount > 0)
            {
                if (studentCount < 10)
                    suggestion.SuggestedStudentLevel = "Beginner";
                else if (studentCount < 30)
                    suggestion.SuggestedStudentLevel = "Intermediate";
                else
                    suggestion.SuggestedStudentLevel = "Advanced";
            }

            // Suggest focus level based on existing data
            suggestion.SuggestedFocusLevel = academicClass.FocusLevel ?? "Basic";

            // Find matching experts
            if (suggestion.SuggestedBoneSpecialtyId.HasValue)
            {
                suggestion.SuggestedExperts = await FindSuggestedExpertsAsync(suggestion.SuggestedBoneSpecialtyId.Value);
            }

            return suggestion;
        }

        /// <summary>
        /// Gợi ý phân loại tự động cho tất cả lớp học chưa được phân loại
        /// </summary>
        public async Task<List<AutoClassificationSuggestionDto>> GetAutoSuggestionsForAllAsync()
        {
            var unclassifiedClasses = await _unitOfWork.AcademicClassRepository
                .GetQueryable()
                .Where(c => c.ClassSpecialtyId == null || c.FocusLevel == null)
                .ToListAsync();

            var suggestions = new List<AutoClassificationSuggestionDto>();

            foreach (var classEntity in unclassifiedClasses)
            {
                var suggestion = await GetAutoSuggestionAsync(classEntity.Id);
                if (suggestion != null)
                    suggestions.Add(suggestion);
            }

            return suggestions;
        }

        /// <summary>
        /// Áp dụng gợi ý tự động cho một lớp học
        /// </summary>
        public async Task<ClassClassificationResultDto?> ApplyAutoSuggestionAsync(Guid classId, bool apply = true)
        {
            if (!apply)
                return await GetClassClassificationAsync(classId);

            var suggestion = await GetAutoSuggestionAsync(classId);
            if (suggestion == null)
                return null;

            var request = new ClassClassificationRequestDto
            {
                ClassId = classId,
                BoneSpecialtyId = suggestion.SuggestedBoneSpecialtyId,
                FocusLevel = suggestion.SuggestedFocusLevel,
                TargetStudentLevel = suggestion.SuggestedStudentLevel,
                PathologyCategoryIds = suggestion.SuggestedPathologyIds,
                AutoSuggest = false
            };

            return await ClassifyClassAsync(request);
        }

        #endregion

        #region ==================== EXPERT MATCHING ====================

        /// <summary>
        /// Tìm Expert phù hợp nhất cho lớp học
        /// </summary>
        public async Task<List<ExpertMatchClassificationDto>> FindMatchingExpertsAsync(Guid classId)
        {
            var academicClass = await _unitOfWork.AcademicClassRepository
                .GetQueryable()
                .Include(c => c.ClassSpecialty)
                .FirstOrDefaultAsync(c => c.Id == classId);

            if (academicClass == null)
                return new List<ExpertMatchClassificationDto>();

            if (!academicClass.ClassSpecialtyId.HasValue)
                return new List<ExpertMatchClassificationDto>();

            var experts = await _unitOfWork.ExpertSpecialtyRepository
                .GetQueryable()
                .Include(es => es.Expert)
                .Include(es => es.BoneSpecialty)
                .Include(es => es.PathologyCategory)
                .Where(es => es.BoneSpecialtyId == academicClass.ClassSpecialtyId.Value && es.IsActive)
                .ToListAsync();

            return experts
                .GroupBy(es => es.ExpertId)
                .Select(g => new ExpertMatchClassificationDto
                {
                    ExpertId = g.Key,
                    ExpertName = g.First().Expert?.FullName ?? "Unknown",
                    Email = g.First().Expert?.Email,
                    MatchScore = CalculateMatchScore(g.ToList()),
                    MatchLevel = GetMatchLevel(CalculateMatchScore(g.ToList())),
                    MatchingSpecialties = g.Select(es => new ExpertSpecialtyMatchDto
                    {
                        SpecialtyId = es.BoneSpecialtyId,
                        SpecialtyName = es.BoneSpecialty?.Name ?? "Unknown",
                        PathologyId = es.PathologyCategoryId,
                        PathologyName = es.PathologyCategory?.Name,
                        ProficiencyLevel = es.ProficiencyLevel,
                        IsPrimary = es.IsPrimary,
                        IsMatch = true
                    }).ToList(),
                    ProficiencyLevel = g.Max(es => es.ProficiencyLevel),
                    YearsExperience = g.Max(es => es.YearsExperience ?? 0)
                })
                .OrderByDescending(e => e.MatchScore)
                .ToList();
        }

        /// <summary>
        /// Tính điểm phù hợp của Expert với lớp học
        /// </summary>
        public async Task<ExpertMatchClassificationDto?> CalculateExpertMatchAsync(Guid classId, Guid expertId)
        {
            var academicClass = await _unitOfWork.AcademicClassRepository
                .GetQueryable()
                .Include(c => c.ClassSpecialty)
                .Include(c => c.ClassEnrollments)
                .FirstOrDefaultAsync(c => c.Id == classId);

            if (academicClass == null)
                return null;

            var expert = await _unitOfWork.UserRepository
                .GetQueryable()
                .Include(u => u.ExpertSpecialties)
                    .ThenInclude(es => es.BoneSpecialty)
                .Include(u => u.ExpertSpecialties)
                    .ThenInclude(es => es.PathologyCategory)
                .FirstOrDefaultAsync(u => u.Id == expertId);

            if (expert == null)
                return null;

            var expertSpecialties = expert.ExpertSpecialties.Where(es => es.IsActive).ToList();

            return new ExpertMatchClassificationDto
            {
                ExpertId = expertId,
                ExpertName = expert.FullName,
                Email = expert.Email,
                MatchScore = CalculateMatchScore(expertSpecialties),
                MatchLevel = GetMatchLevel(CalculateMatchScore(expertSpecialties)),
                MatchingSpecialties = expertSpecialties.Select(es => new ExpertSpecialtyMatchDto
                {
                    SpecialtyId = es.BoneSpecialtyId,
                    SpecialtyName = es.BoneSpecialty?.Name ?? "Unknown",
                    PathologyId = es.PathologyCategoryId,
                    PathologyName = es.PathologyCategory?.Name,
                    ProficiencyLevel = es.ProficiencyLevel,
                    IsPrimary = es.IsPrimary,
                    IsMatch = academicClass.ClassSpecialtyId == es.BoneSpecialtyId
                }).ToList(),
                ProficiencyLevel = expertSpecialties.Max(es => es.ProficiencyLevel),
                YearsExperience = expertSpecialties.Max(es => es.YearsExperience ?? 0)
            };
        }

        #endregion

        #region ==================== DASHBOARD & SUMMARY ====================

        /// <summary>
        /// Lấy tổng hợp phân loại cho dashboard
        /// </summary>
        public async Task<ClassificationSummaryDto> GetClassificationSummaryAsync()
        {
            var allClasses = await _unitOfWork.AcademicClassRepository
                .GetQueryable()
                .Include(c => c.ClassSpecialty)
                .Include(c => c.Expert)
                .Include(c => c.Lecturer)
                .ToListAsync();

            var summary = new ClassificationSummaryDto
            {
                TotalClasses = allClasses.Count,
                ClassifiedClasses = allClasses.Count(c => c.ClassSpecialtyId.HasValue),
                UnclassifiedClasses = allClasses.Count(c => !c.ClassSpecialtyId.HasValue)
            };

            // Distribution by Specialty
            summary.DistributionBySpecialty = allClasses
                .Where(c => c.ClassSpecialty != null)
                .GroupBy(c => c.ClassSpecialty!.Name)
                .ToDictionary(g => g.Key, g => g.Count());

            // Distribution by Focus Level
            summary.DistributionByFocusLevel = allClasses
                .Where(c => !string.IsNullOrEmpty(c.FocusLevel))
                .GroupBy(c => c.FocusLevel!)
                .ToDictionary(g => g.Key, g => g.Count());

            // Distribution by Student Level
            summary.DistributionByStudentLevel = allClasses
                .Where(c => !string.IsNullOrEmpty(c.TargetStudentLevel))
                .GroupBy(c => c.TargetStudentLevel!)
                .ToDictionary(g => g.Key, g => g.Count());

            // Classes without Expert
            summary.ClassesWithoutExpert = allClasses
                .Where(c => c.ExpertId == null)
                .GroupBy(c => c.ClassSpecialty?.Name ?? "Chưa phân loại")
                .ToDictionary(g => g.Key, g => g.Count());

            // Classes needing attention
            summary.ClassesNeedingAttention = allClasses
                .Where(c => c.ClassSpecialtyId == null || c.ExpertId == null)
                .Select(c => new ClassNeedsAttentionDto
                {
                    ClassId = c.Id,
                    ClassName = c.ClassName,
                    Issue = c.ClassSpecialtyId == null 
                        ? "Chưa phân loại chuyên môn" 
                        : "Chưa có Expert",
                    Severity = c.ClassSpecialtyId == null ? "High" : "Medium",
                    SuggestedAction = c.ClassSpecialtyId == null
                        ? "Vui lòng phân loại lớp học theo Bone Specialty"
                        : "Vui lòng gán Expert cho lớp học"
                })
                .ToList();

            return summary;
        }

        /// <summary>
        /// Lấy danh sách lớp học với bộ lọc phân loại
        /// </summary>
        public async Task<FilteredClassificationResultDto> GetFilteredClassificationsAsync(
            ClassificationFilterDto filter, 
            int pageIndex = 1, 
            int pageSize = 10)
        {
            var query = _unitOfWork.AcademicClassRepository
                .GetQueryable()
                .Include(c => c.ClassSpecialty)
                    .ThenInclude(s => s.Parent)
                .Include(c => c.Expert)
                    .ThenInclude(e => e.ExpertSpecialties)
                        .ThenInclude(es => es.BoneSpecialty)
                .AsQueryable();

            // Apply filters
            if (filter.BoneSpecialtyId.HasValue)
                query = query.Where(c => c.ClassSpecialtyId == filter.BoneSpecialtyId.Value);

            if (!string.IsNullOrEmpty(filter.FocusLevel))
                query = query.Where(c => c.FocusLevel == filter.FocusLevel);

            if (!string.IsNullOrEmpty(filter.TargetStudentLevel))
                query = query.Where(c => c.TargetStudentLevel == filter.TargetStudentLevel);

            if (filter.ExpertId.HasValue)
                query = query.Where(c => c.ExpertId == filter.ExpertId.Value);

            if (filter.LecturerId.HasValue)
                query = query.Where(c => c.LecturerId == filter.LecturerId.Value);

            if (filter.IsClassified.HasValue)
            {
                if (filter.IsClassified.Value)
                    query = query.Where(c => c.ClassSpecialtyId != null);
                else
                    query = query.Where(c => c.ClassSpecialtyId == null);
            }

            if (!string.IsNullOrEmpty(filter.Search))
            {
                var search = filter.Search.ToLower();
                query = query.Where(c => 
                    c.ClassName.ToLower().Contains(search) ||
                    c.Semester.ToLower().Contains(search));
            }

            var totalCount = await query.CountAsync();
            var totalPages = (int)Math.Ceiling(totalCount / (double)pageSize);

            var classes = await query
                .OrderByDescending(c => c.CreatedAt)
                .Skip((pageIndex - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

            var results = new List<ClassClassificationResultDto>();
            foreach (var c in classes)
            {
                results.Add(await BuildClassificationResultAsync(c));
            }

            return new FilteredClassificationResultDto
            {
                AppliedFilter = filter,
                TotalCount = totalCount,
                PageIndex = pageIndex,
                PageSize = pageSize,
                TotalPages = totalPages,
                Items = results
            };
        }

        #endregion

        #region ==================== VALIDATION ====================

        /// <summary>
        /// Validate phân loại của lớp học
        /// </summary>
        public async Task<ClassificationValidationDto> ValidateClassClassificationAsync(Guid classId)
        {
            var validation = new ClassificationValidationDto { IsValid = true };

            var academicClass = await _unitOfWork.AcademicClassRepository
                .GetQueryable()
                .Include(c => c.ClassSpecialty)
                    .ThenInclude(s => s.Parent)
                .Include(c => c.Expert)
                    .ThenInclude(e => e.ExpertSpecialties)
                .Include(c => c.ClassEnrollments)
                .FirstOrDefaultAsync(c => c.Id == classId);

            if (academicClass == null)
            {
                validation.IsValid = false;
                validation.Errors.Add("Class not found");
                return validation;
            }

            // Check if classified
            if (!academicClass.ClassSpecialtyId.HasValue)
            {
                validation.Warnings.Add("Class is not classified by Bone Specialty");
                validation.Health.Issues.Add("Missing Bone Specialty classification");
            }

            // Check expert match
            if (academicClass.ExpertId.HasValue && academicClass.ClassSpecialtyId.HasValue)
            {
                var expertSpecialties = academicClass.Expert?.ExpertSpecialties
                    .Where(es => es.IsActive)
                    .ToList() ?? new List<ExpertSpecialty>();

                var hasMatchingSpecialty = expertSpecialties
                    .Any(es => es.BoneSpecialtyId == academicClass.ClassSpecialtyId.Value);

                if (!hasMatchingSpecialty)
                {
                    validation.Warnings.Add("Expert does not have matching specialty with class");
                    validation.Health.Issues.Add("Expert-Specialty mismatch");
                }
            }
            else if (academicClass.ClassSpecialtyId.HasValue && !academicClass.ExpertId.HasValue)
            {
                validation.Warnings.Add("Class has specialty but no expert assigned");
            }

            // Check student count
            if (!academicClass.ClassEnrollments.Any())
            {
                validation.Info.Add("Class has no enrolled students");
            }

            // Calculate health score
            validation.Health.Score = CalculateHealthScore(validation);
            validation.Health.Status = GetHealthStatus(validation.Health.Score);
            validation.Health.Recommendations = GetRecommendations(validation);

            return validation;
        }

        /// <summary>
        /// Validate tất cả phân loại
        /// </summary>
        public async Task<List<ClassificationValidationDto>> ValidateAllClassificationsAsync()
        {
            var classes = await _unitOfWork.AcademicClassRepository
                .GetQueryable()
                .Select(c => c.Id)
                .ToListAsync();

            var validations = new List<ClassificationValidationDto>();
            foreach (var classId in classes)
            {
                validations.Add(await ValidateClassClassificationAsync(classId));
            }

            return validations;
        }

        #endregion

        #region ==================== PATHOLOGY MANAGEMENT ====================

        /// <summary>
        /// Lấy danh sách Pathology Categories theo Bone Specialty
        /// </summary>
        public async Task<List<PathologyClassificationDto>> GetPathologiesBySpecialtyAsync(Guid boneSpecialtyId)
        {
            var pathologies = await _unitOfWork.PathologyCategoryRepository
                .GetQueryable()
                .Where(p => p.BoneSpecialtyId == boneSpecialtyId && p.IsActive)
                .ToListAsync();

            return pathologies.Select(p => new PathologyClassificationDto
            {
                Id = p.Id,
                Code = p.Code,
                Name = p.Name,
                ParentBoneSpecialtyId = p.BoneSpecialtyId,
                DisplayOrder = p.DisplayOrder,
                IsActive = p.IsActive,
                UsageCount = 0
            }).ToList();
        }

        /// <summary>
        /// Cập nhật danh sách Pathology cho lớp học
        /// </summary>
        public async Task<ClassClassificationResultDto> UpdateClassPathologiesAsync(Guid classId, List<Guid> pathologyIds)
        {
            var academicClass = await _unitOfWork.AcademicClassRepository
                .GetByIdAsync(classId);

            if (academicClass == null)
                throw new InvalidOperationException("Class not found");

            academicClass.TargetPathologyCategories = JsonSerializer.Serialize(pathologyIds);
            academicClass.UpdatedAt = DateTime.UtcNow;

            _unitOfWork.AcademicClassRepository.Update(academicClass);
            await _unitOfWork.SaveAsync();

            return await GetClassClassificationAsync(classId) 
                ?? throw new InvalidOperationException("Error getting classification after update");
        }

        #endregion

        #region ==================== STUDENT LEVEL MANAGEMENT ====================

        /// <summary>
        /// Lấy thông tin cấp độ sinh viên
        /// </summary>
        public Task<StudentLevelClassificationDto?> GetStudentLevelInfoAsync(string level)
        {
            StudentLevelData.TryGetValue(level, out var info);
            return Task.FromResult(info);
        }

        /// <summary>
        /// Lấy tất cả cấp độ sinh viên
        /// </summary>
        public Task<List<StudentLevelClassificationDto>> GetAllStudentLevelsAsync()
        {
            return Task.FromResult(StudentLevelData.Values.ToList());
        }

        #endregion

        #region ==================== FOCUS LEVEL MANAGEMENT ====================

        /// <summary>
        /// Lấy thông tin cấp độ tập trung
        /// </summary>
        public Task<FocusLevelClassificationDto?> GetFocusLevelInfoAsync(string level)
        {
            FocusLevelData.TryGetValue(level, out var info);
            return Task.FromResult(info);
        }

        /// <summary>
        /// Lấy tất cả cấp độ tập trung
        /// </summary>
        public Task<List<FocusLevelClassificationDto>> GetAllFocusLevelsAsync()
        {
            return Task.FromResult(FocusLevelData.Values.ToList());
        }

        #endregion

        #region ==================== HELPER METHODS ====================

        private async Task<ClassClassificationResultDto> BuildClassificationResultAsync(AcademicClass academicClass)
        {
            var result = new ClassClassificationResultDto
            {
                ClassId = academicClass.Id,
                ClassName = academicClass.ClassName,
                IsClassified = academicClass.ClassSpecialtyId.HasValue,
                ClassificationType = academicClass.ClassSpecialtyId.HasValue 
                    ? ClassificationType.ByBoneSpecialty 
                    : ClassificationType.ByBoneSpecialty,
                ClassificationLevel = academicClass.FocusLevel ?? "Basic",
                ConfidenceScore = CalculateConfidenceScore(academicClass)
            };

            // Build details
            var details = new ClassificationDetailDto();

            if (academicClass.ClassSpecialty != null)
            {
                details.BoneSpecialtyId = academicClass.ClassSpecialty.Id;
                details.BoneSpecialtyName = academicClass.ClassSpecialty.Name;
                details.BoneSpecialtyCode = academicClass.ClassSpecialty.Code;
                details.SpecialtyLevel = await CalculateSpecialtyLevelAsync(academicClass.ClassSpecialty);

                if (academicClass.ClassSpecialty.Parent != null)
                {
                    details.ParentSpecialtyId = academicClass.ClassSpecialty.Parent.Id;
                    details.ParentSpecialtyName = academicClass.ClassSpecialty.Parent.Name;
                }
            }

            // Student level info
            var studentLevel = academicClass.TargetStudentLevel ?? "Beginner";
            if (StudentLevelData.TryGetValue(studentLevel, out var studentLevelInfo))
            {
                details.StudentLevelInfo = studentLevelInfo;
            }

            // Focus level info
            var focusLevel = academicClass.FocusLevel ?? "Basic";
            if (FocusLevelData.TryGetValue(focusLevel, out var focusLevelInfo))
            {
                details.FocusLevelInfo = focusLevelInfo;
            }

            // Expert match
            if (academicClass.Expert != null && academicClass.Expert.ExpertSpecialties.Any())
            {
                var expertSpecialties = academicClass.Expert.ExpertSpecialties
                    .Where(es => es.IsActive)
                    .ToList();

                var matchScore = CalculateMatchScore(expertSpecialties);
                details.ExpertMatch = new ExpertMatchClassificationDto
                {
                    ExpertId = academicClass.Expert.Id,
                    ExpertName = academicClass.Expert.FullName,
                    Email = academicClass.Expert.Email,
                    MatchScore = matchScore,
                    MatchLevel = GetMatchLevel(matchScore),
                    ProficiencyLevel = expertSpecialties.Max(es => es.ProficiencyLevel),
                    YearsExperience = expertSpecialties.Max(es => es.YearsExperience ?? 0)
                };
            }

            // Warnings
            if (!academicClass.ClassSpecialtyId.HasValue)
                result.Warnings.Add("Class is not classified by Bone Specialty");

            if (!academicClass.ExpertId.HasValue)
                result.Warnings.Add("Class has no assigned Expert");

            if (string.IsNullOrEmpty(academicClass.FocusLevel))
                result.Warnings.Add("Focus level is not set");

            result.Details = details;

            return result;
        }

        private async Task<int> CalculateSpecialtyLevelAsync(BoneSpecialty specialty)
        {
            int level = 0;
            var current = specialty;

            while (current.ParentId.HasValue)
            {
                level++;
                var parent = await _unitOfWork.BoneSpecialtyRepository.GetByIdAsync(current.ParentId.Value);
                if (parent == null) break;
                current = parent;
            }

            return level;
        }

        private int CalculateConfidenceScore(AcademicClass academicClass)
        {
            int score = 0;

            if (academicClass.ClassSpecialtyId.HasValue) score += 30;
            if (academicClass.ExpertId.HasValue) score += 25;
            if (!string.IsNullOrEmpty(academicClass.FocusLevel)) score += 20;
            if (!string.IsNullOrEmpty(academicClass.TargetStudentLevel)) score += 15;
            if (!string.IsNullOrEmpty(academicClass.TargetPathologyCategories)) score += 10;

            return Math.Min(score, 100);
        }

        private int CalculateMatchScore(List<ExpertSpecialty> specialties)
        {
            if (!specialties.Any()) return 0;

            var primaryCount = specialties.Count(es => es.IsPrimary);
            var avgProficiency = specialties.Average(es => es.ProficiencyLevel);
            var baseScore = (primaryCount * 20) + (avgProficiency * 10);

            return Math.Min((int)baseScore, 100);
        }

        private string GetMatchLevel(int score)
        {
            return score switch
            {
                >= 80 => "Excellent",
                >= 60 => "Good",
                >= 40 => "Fair",
                _ => "Poor"
            };
        }

        private async Task<List<ExpertMatchSuggestionDto>> FindSuggestedExpertsAsync(Guid specialtyId)
        {
            var expertSpecialties = await _unitOfWork.ExpertSpecialtyRepository
                .GetQueryable()
                .Include(es => es.Expert)
                .Where(es => es.BoneSpecialtyId == specialtyId && es.IsActive)
                .ToListAsync();

            return expertSpecialties
                .GroupBy(es => es.ExpertId)
                .Select(g =>
                {
                    var primary = g.FirstOrDefault(es => es.IsPrimary);
                    return new ExpertMatchSuggestionDto
                    {
                        ExpertId = g.Key,
                        ExpertName = g.First().Expert?.FullName ?? "Unknown",
                        MatchScore = g.Sum(es => es.ProficiencyLevel * 10),
                        ProficiencyLevel = primary?.ProficiencyLevel ?? 0,
                        MatchingSpecialties = g.Select(es => es.BoneSpecialty?.Name ?? "").ToList()
                    };
                })
                .OrderByDescending(e => e.MatchScore)
                .Take(5)
                .ToList();
        }

        private int CalculateHealthScore(ClassificationValidationDto validation)
        {
            int score = 100;
            score -= validation.Errors.Count * 25;
            score -= validation.Warnings.Count * 10;
            return Math.Max(score, 0);
        }

        private string GetHealthStatus(int score)
        {
            return score switch
            {
                >= 80 => "Healthy",
                >= 50 => "Needs Attention",
                _ => "Critical"
            };
        }

        private List<string> GetRecommendations(ClassificationValidationDto validation)
        {
            var recommendations = new List<string>();

            if (validation.Warnings.Any(w => w.Contains("not classified")))
                recommendations.Add("Vui lòng phân loại lớp học theo Bone Specialty");

            if (validation.Warnings.Any(w => w.Contains("no expert")))
                recommendations.Add("Vui lòng gán Expert cho lớp học");

            if (validation.Warnings.Any(w => w.Contains("mismatch")))
                recommendations.Add("Cân nhắc gán Expert có chuyên môn phù hợp với lớp học");

            if (!recommendations.Any())
                recommendations.Add("Phân loại lớp học đang tốt");

            return recommendations;
        }

        #endregion
    }
}
