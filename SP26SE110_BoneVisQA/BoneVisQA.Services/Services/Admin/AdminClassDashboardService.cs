using BoneVisQA.Repositories.UnitOfWork;
using BoneVisQA.Services.Interfaces.Admin;
using BoneVisQA.Services.Models.Admin;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace BoneVisQA.Services.Services.Admin
{
    /// <summary>
    /// Service cho Admin Dashboard - Quản lý Class toàn diện
    /// </summary>
    public class AdminClassDashboardService : IAdminClassDashboardService
    {
        private readonly IUnitOfWork _unitOfWork;
        private readonly ILogger<AdminClassDashboardService> _logger;

        public AdminClassDashboardService(IUnitOfWork unitOfWork, ILogger<AdminClassDashboardService> logger)
        {
            _unitOfWork = unitOfWork;
            _logger = logger;
        }

        #region Dashboard Summary

        /// <summary>
        /// Lấy tổng hợp Dashboard - Thống kê toàn hệ thống
        /// </summary>
        public async Task<AdminDashboardSummaryDto> GetDashboardSummaryAsync()
        {
            var totalClasses = await _unitOfWork.AcademicClassRepository.GetQueryable().CountAsync();
            var totalLecturers = await _unitOfWork.UserRepository.GetQueryable()
                .Include(u => u.UserRoles)
                .ThenInclude(ur => ur.Role)
                .CountAsync(u => u.UserRoles.Any(ur => ur.Role != null && ur.Role.Name == "Lecturer"));

            var totalExperts = await _unitOfWork.UserRepository.GetQueryable()
                .Include(u => u.UserRoles)
                .ThenInclude(ur => ur.Role)
                .CountAsync(u => u.UserRoles.Any(ur => ur.Role != null && ur.Role.Name == "Expert"));

            var totalStudents = await _unitOfWork.UserRepository.GetQueryable()
                .Include(u => u.UserRoles)
                .ThenInclude(ur => ur.Role)
                .CountAsync(u => u.UserRoles.Any(ur => ur.Role != null && ur.Role.Name == "Student"));

            var classesWithoutLecturer = await _unitOfWork.AcademicClassRepository.GetQueryable()
                .CountAsync(c => c.LecturerId == null);

            var classesWithoutExpert = await _unitOfWork.AcademicClassRepository.GetQueryable()
                .CountAsync(c => c.ExpertId == null);

            return new AdminDashboardSummaryDto
            {
                TotalClasses = totalClasses,
                TotalLecturers = totalLecturers,
                TotalExperts = totalExperts,
                TotalStudents = totalStudents,
                ClassesWithoutLecturer = classesWithoutLecturer,
                ClassesWithoutExpert = classesWithoutExpert
            };
        }

        #endregion

        #region Class List with Expert Specialties

        /// <summary>
        /// Lấy danh sách Class với thông tin Expert Specialty đầy đủ
        /// </summary>
        public async Task<AdminPagedResult<ClassDashboardDto>> GetClassesDashboardAsync(
            int pageIndex = 1,
            int pageSize = 10,
            string? search = null,
            Guid? lecturerId = null,
            Guid? expertId = null)
        {
            var query = _unitOfWork.AcademicClassRepository.GetQueryable()
                .Include(c => c.Lecturer)
                .Include(c => c.Expert)
                    .ThenInclude(e => e.ExpertSpecialties)
                        .ThenInclude(es => es.BoneSpecialty)
                .Include(c => c.Expert)
                    .ThenInclude(e => e.ExpertSpecialties)
                        .ThenInclude(es => es.PathologyCategory)
                .Include(c => c.ClassSpecialty)
                .Include(c => c.ClassEnrollments)
                .AsQueryable();

            // Apply filters
            if (!string.IsNullOrWhiteSpace(search))
            {
                search = search.ToLower();
                query = query.Where(c =>
                    c.ClassName.ToLower().Contains(search) ||
                    c.Semester.ToLower().Contains(search) ||
                    (c.Lecturer != null && c.Lecturer.FullName.ToLower().Contains(search)) ||
                    (c.Expert != null && c.Expert.FullName.ToLower().Contains(search)));
            }

            if (lecturerId.HasValue)
            {
                query = query.Where(c => c.LecturerId == lecturerId.Value);
            }

            if (expertId.HasValue)
            {
                query = query.Where(c => c.ExpertId == expertId.Value);
            }

            var totalCount = await query.CountAsync();

            var data = await query
                .OrderByDescending(c => c.CreatedAt)
                .Skip((pageIndex - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

            var result = data.Select(c => new ClassDashboardDto
            {
                Id = c.Id,
                ClassName = c.ClassName,
                Semester = c.Semester,
                CreatedAt = c.CreatedAt,
                UpdatedAt = c.UpdatedAt,

                // Classification - Bone Specialty
                ClassSpecialtyId = c.ClassSpecialtyId,
                ClassSpecialtyName = c.ClassSpecialty?.Name,
                ClassSpecialtyCode = c.ClassSpecialty?.Code,
                FocusLevel = c.FocusLevel,
                TargetStudentLevel = c.TargetStudentLevel,

                // Lecturer
                LecturerId = c.LecturerId,
                LecturerName = c.Lecturer?.FullName,
                LecturerEmail = c.Lecturer?.Email,

                // Expert
                ExpertId = c.ExpertId,
                ExpertName = c.Expert?.FullName,
                ExpertEmail = c.Expert?.Email,

                // Expert Specialties - FULL DETAIL
                ExpertSpecialties = c.Expert?.ExpertSpecialties
                    .Where(es => es.IsActive)
                    .OrderByDescending(es => es.IsPrimary)
                    .ThenByDescending(es => es.ProficiencyLevel)
                    .Select(es => new ExpertSpecialtyInfoDto
                    {
                        Id = es.Id,
                        BoneSpecialtyId = es.BoneSpecialtyId,
                        BoneSpecialtyName = es.BoneSpecialty?.Name,
                        BoneSpecialtyCode = es.BoneSpecialty?.Code,
                        PathologyCategoryId = es.PathologyCategoryId,
                        PathologyCategoryName = es.PathologyCategory?.Name,
                        ProficiencyLevel = es.ProficiencyLevel,
                        YearsExperience = es.YearsExperience,
                        Certifications = es.Certifications,
                        IsPrimary = es.IsPrimary
                    }).ToList() ?? new List<ExpertSpecialtyInfoDto>(),

                // Stats
                StudentCount = c.ClassEnrollments.Count,
                TotalCases = c.ClassCases?.Count ?? 0,
                TotalQuizzes = c.ClassQuizSessions?.Count ?? 0
            }).ToList();

            return new AdminPagedResult<ClassDashboardDto>
            {
                Items = result,
                TotalCount = totalCount,
                PageIndex = pageIndex,
                PageSize = pageSize
            };
        }

        #endregion

        #region Class Detail

        /// <summary>
        /// Lấy chi tiết một Class với đầy đủ thông tin
        /// </summary>
        public async Task<ClassDetailDto?> GetClassDetailAsync(Guid classId)
        {
            var c = await _unitOfWork.AcademicClassRepository.GetQueryable()
                .Include(cl => cl.Lecturer)
                .Include(cl => cl.Expert)
                    .ThenInclude(e => e.ExpertSpecialties)
                        .ThenInclude(es => es.BoneSpecialty)
                .Include(cl => cl.Expert)
                    .ThenInclude(e => e.ExpertSpecialties)
                        .ThenInclude(es => es.PathologyCategory)
                .Include(cl => cl.ClassEnrollments)
                    .ThenInclude(en => en.Student)
                .Include(cl => cl.ClassCases)
                .Include(cl => cl.ClassQuizSessions)
                .Include(cl => cl.Announcements)
                .FirstOrDefaultAsync(cl => cl.Id == classId);

            if (c == null) return null;

            return new ClassDetailDto
            {
                Id = c.Id,
                ClassName = c.ClassName,
                Semester = c.Semester,
                CreatedAt = c.CreatedAt,
                UpdatedAt = c.UpdatedAt,

                // Lecturer
                Lecturer = c.Lecturer != null ? new LecturerInfoDto
                {
                    Id = c.Lecturer.Id,
                    FullName = c.Lecturer.FullName,
                    Email = c.Lecturer.Email
                } : null,

                // Expert với Specialties
                Expert = c.Expert != null ? new ExpertInfoDto
                {
                    Id = c.Expert.Id,
                    FullName = c.Expert.FullName,
                    Email = c.Expert.Email,
                    Specialties = c.Expert.ExpertSpecialties
                        .Where(es => es.IsActive)
                        .OrderByDescending(es => es.IsPrimary)
                        .Select(es => new ExpertSpecialtyInfoDto
                        {
                            Id = es.Id,
                            BoneSpecialtyId = es.BoneSpecialtyId,
                            BoneSpecialtyName = es.BoneSpecialty?.Name,
                            BoneSpecialtyCode = es.BoneSpecialty?.Code,
                            PathologyCategoryId = es.PathologyCategoryId,
                            PathologyCategoryName = es.PathologyCategory?.Name,
                            ProficiencyLevel = es.ProficiencyLevel,
                            YearsExperience = es.YearsExperience,
                            Certifications = es.Certifications,
                            IsPrimary = es.IsPrimary
                        }).ToList()
                } : null,

                // Students
                Students = c.ClassEnrollments.Select(en => new StudentInfoDto
                {
                    Id = en.StudentId,
                    FullName = en.Student?.FullName ?? "Unknown",
                    Email = en.Student?.Email,
                    EnrolledAt = en.EnrolledAt
                }).ToList(),

                // Stats
                Stats = new ClassStatsDetailDto
                {
                    TotalStudents = c.ClassEnrollments.Count,
                    TotalCasesAssigned = c.ClassCases?.Count ?? 0,
                    TotalQuizzesAssigned = c.ClassQuizSessions?.Count ?? 0,
                    TotalAnnouncements = c.Announcements?.Count ?? 0
                }
            };
        }

        #endregion

        #region Dropdowns

        /// <summary>
        /// Lấy danh sách Lecturers cho dropdown
        /// </summary>
        public async Task<List<LecturerDropdownDto>> GetLecturersAsync()
        {
            return await _unitOfWork.UserRepository.GetQueryable()
                .Include(u => u.UserRoles)
                .ThenInclude(ur => ur.Role)
                .Where(u => u.UserRoles.Any(ur => ur.Role != null && ur.Role.Name == "Lecturer"))
                .Select(u => new LecturerDropdownDto
                {
                    Id = u.Id,
                    FullName = u.FullName,
                    Email = u.Email
                })
                .OrderBy(u => u.FullName)
                .ToListAsync();
        }

        /// <summary>
        /// Lấy danh sách Experts cho dropdown - có kèm Specialties
        /// </summary>
        public async Task<List<ExpertDropdownDto>> GetExpertsAsync()
        {
            var experts = await _unitOfWork.UserRepository.GetQueryable()
                .Include(u => u.UserRoles)
                .ThenInclude(ur => ur.Role)
                .Include(u => u.ExpertSpecialties)
                    .ThenInclude(es => es.BoneSpecialty)
                .Include(u => u.ExpertSpecialties)
                    .ThenInclude(es => es.PathologyCategory)
                .Where(u => u.UserRoles.Any(ur => ur.Role != null && ur.Role.Name == "Expert"))
                .ToListAsync();

            return experts.Select(e => new ExpertDropdownDto
            {
                Id = e.Id,
                FullName = e.FullName,
                Email = e.Email,
                Specialties = e.ExpertSpecialties
                    .Where(es => es.IsActive)
                    .OrderByDescending(es => es.IsPrimary)
                    .ThenByDescending(es => es.ProficiencyLevel)
                    .Select(es => new ExpertSpecialtyBriefDto
                    {
                        BoneSpecialtyId = es.BoneSpecialtyId,
                        BoneSpecialtyName = es.BoneSpecialty?.Name,
                        PathologyCategoryName = es.PathologyCategory?.Name,
                        ProficiencyLevel = es.ProficiencyLevel,
                        IsPrimary = es.IsPrimary
                    }).ToList()
            }).OrderBy(e => e.FullName).ToList();
        }

        #endregion

        #region Class CRUD

        /// <summary>
        /// Tạo Class mới
        /// </summary>
        public async Task<ClassDashboardDto> CreateClassAsync(CreateClassRequestDto request)
        {
            var entity = new Repositories.Models.AcademicClass
            {
                Id = Guid.NewGuid(),
                ClassName = request.ClassName,
                Semester = request.Semester,
                ClassSpecialtyId = request.ClassSpecialtyId,
                FocusLevel = request.FocusLevel ?? "Basic",
                TargetStudentLevel = request.TargetStudentLevel ?? "Beginner",
                TargetPathologyCategories = request.TargetPathologyCategories != null
                    ? System.Text.Json.JsonSerializer.Serialize(request.TargetPathologyCategories)
                    : null,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };

            await _unitOfWork.AcademicClassRepository.AddAsync(entity);
            await _unitOfWork.SaveAsync();

            return new ClassDashboardDto
            {
                Id = entity.Id,
                ClassName = entity.ClassName,
                Semester = entity.Semester,
                ClassSpecialtyId = entity.ClassSpecialtyId,
                FocusLevel = entity.FocusLevel,
                TargetStudentLevel = entity.TargetStudentLevel,
                CreatedAt = entity.CreatedAt,
                StudentCount = 0,
                TotalCases = 0,
                TotalQuizzes = 0
            };
        }

        /// <summary>
        /// Cập nhật Class
        /// </summary>
        public async Task<ClassDetailDto?> UpdateClassAsync(UpdateClassRequestDto request)
        {
            var entity = await _unitOfWork.AcademicClassRepository.GetByIdAsync(request.Id);
            if (entity == null) return null;

            entity.ClassName = request.ClassName;
            entity.Semester = request.Semester;
            entity.ClassSpecialtyId = request.ClassSpecialtyId;
            entity.FocusLevel = request.FocusLevel ?? "Basic";
            entity.TargetStudentLevel = request.TargetStudentLevel ?? "Beginner";
            entity.TargetPathologyCategories = request.TargetPathologyCategories != null
                ? System.Text.Json.JsonSerializer.Serialize(request.TargetPathologyCategories)
                : null;
            entity.UpdatedAt = DateTime.UtcNow;

            _unitOfWork.AcademicClassRepository.Update(entity);
            await _unitOfWork.SaveAsync();

            // Reload to get updated data with full details
            return await GetClassDetailAsync(entity.Id);
        }

        /// <summary>
        /// Cập nhật Specialty cho Class
        /// </summary>
        public async Task<ClassDetailDto?> UpdateClassSpecialtyAsync(UpdateClassSpecialtyRequestDto request)
        {
            var entity = await _unitOfWork.AcademicClassRepository.GetByIdAsync(request.ClassId);
            if (entity == null) return null;

            entity.ClassSpecialtyId = request.ClassSpecialtyId;
            entity.FocusLevel = request.FocusLevel;
            entity.TargetStudentLevel = request.TargetStudentLevel;
            entity.TargetPathologyCategories = request.TargetPathologyCategories != null
                ? System.Text.Json.JsonSerializer.Serialize(request.TargetPathologyCategories)
                : null;
            entity.UpdatedAt = DateTime.UtcNow;

            _unitOfWork.AcademicClassRepository.Update(entity);
            await _unitOfWork.SaveAsync();

            return await GetClassDetailAsync(entity.Id);
        }

        /// <summary>
        /// Xóa Class
        /// </summary>
        public async Task<bool> DeleteClassAsync(Guid classId)
        {
            var entity = await _unitOfWork.AcademicClassRepository.GetByIdAsync(classId);
            if (entity == null) return false;

            _unitOfWork.AcademicClassRepository.Remove(entity);
            await _unitOfWork.SaveAsync();

            return true;
        }

        #endregion

        #region Assign Users to Class

        /// <summary>
        /// Gán Lecturer/Expert vào Class
        /// </summary>
        public async Task<ClassDetailDto?> AssignUserToClassAsync(AssignUserToClassRequestDto request)
        {
            var entity = await _unitOfWork.AcademicClassRepository.GetQueryable()
                .Include(c => c.Lecturer)
                .Include(c => c.Expert)
                    .ThenInclude(e => e.ExpertSpecialties)
                        .ThenInclude(es => es.BoneSpecialty)
                .Include(c => c.Expert)
                    .ThenInclude(e => e.ExpertSpecialties)
                        .ThenInclude(es => es.PathologyCategory)
                .Include(c => c.ClassEnrollments)
                .FirstOrDefaultAsync(c => c.Id == request.ClassId);

            if (entity == null) return null;

            // Validate Lecturer
            if (request.LecturerId.HasValue)
            {
                var lecturer = await _unitOfWork.UserRepository.GetQueryable()
                    .Include(u => u.UserRoles)
                    .ThenInclude(ur => ur.Role)
                    .FirstOrDefaultAsync(u => u.Id == request.LecturerId.Value);

                if (lecturer == null)
                    throw new InvalidOperationException("Lecturer not found.");
                if (!lecturer.UserRoles.Any(ur => ur.Role != null && ur.Role.Name == "Lecturer"))
                    throw new InvalidOperationException("User is not a Lecturer.");

                entity.LecturerId = request.LecturerId.Value;
            }

            // Validate Expert
            if (request.ExpertId.HasValue)
            {
                var expert = await _unitOfWork.UserRepository.GetQueryable()
                    .Include(u => u.UserRoles)
                    .ThenInclude(ur => ur.Role)
                    .FirstOrDefaultAsync(u => u.Id == request.ExpertId.Value);

                if (expert == null)
                    throw new InvalidOperationException("Expert not found.");
                if (!expert.UserRoles.Any(ur => ur.Role != null && ur.Role.Name == "Expert"))
                    throw new InvalidOperationException("User is not an Expert.");

                entity.ExpertId = request.ExpertId.Value;
            }

            entity.UpdatedAt = DateTime.UtcNow;
            _unitOfWork.AcademicClassRepository.Update(entity);
            await _unitOfWork.SaveAsync();

            // Reload to get updated data
            return await GetClassDetailAsync(entity.Id);
        }

        /// <summary>
        /// Xóa Expert khỏi Class
        /// </summary>
        public async Task<bool> RemoveExpertFromClassAsync(Guid classId)
        {
            var entity = await _unitOfWork.AcademicClassRepository.GetByIdAsync(classId);
            if (entity == null) return false;

            entity.ExpertId = null;
            entity.UpdatedAt = DateTime.UtcNow;

            _unitOfWork.AcademicClassRepository.Update(entity);
            await _unitOfWork.SaveAsync();

            return true;
        }

        /// <summary>
        /// Xóa Lecturer khỏi Class
        /// </summary>
        public async Task<bool> RemoveLecturerFromClassAsync(Guid classId)
        {
            var entity = await _unitOfWork.AcademicClassRepository.GetByIdAsync(classId);
            if (entity == null) return false;

            entity.LecturerId = null;
            entity.UpdatedAt = DateTime.UtcNow;

            _unitOfWork.AcademicClassRepository.Update(entity);
            await _unitOfWork.SaveAsync();

            return true;
        }

        /// <summary>
        /// Xóa Student khỏi Class (xóa Enrollment)
        /// </summary>
        public async Task<bool> RemoveStudentFromClassAsync(Guid classId, Guid studentId)
        {
            var enrollment = await _unitOfWork.ClassEnrollmentRepository.GetQueryable()
                .FirstOrDefaultAsync(e => e.ClassId == classId && e.StudentId == studentId);

            if (enrollment == null) return false;

            _unitOfWork.ClassEnrollmentRepository.Remove(enrollment);
            await _unitOfWork.SaveAsync();

            return true;
        }

        #endregion
    }
}
