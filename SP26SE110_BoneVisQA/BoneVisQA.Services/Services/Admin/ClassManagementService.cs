using BoneVisQA.Repositories.Models;
using BoneVisQA.Repositories.UnitOfWork;
using BoneVisQA.Services.Interfaces;
using BoneVisQA.Services.Interfaces.Admin;
using BoneVisQA.Services.Models.Admin;
using BoneVisQA.Services.Models.Expert;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BoneVisQA.Services.Services.Admin
{
    public class ClassManagementService : IClassManagementService
    {
        private readonly IUnitOfWork _unitOfWork;
        private readonly ILogger<ClassManagementService> _logger;

        public ClassManagementService(IUnitOfWork unitOfWork,ILogger<ClassManagementService> logger)
        {
            _unitOfWork = unitOfWork;
            _logger = logger;
        }

        public async Task<PagedResult<GetClassManagementDTO>> GetAcademicClassAsync(int pageIndex,int pageSize)
        {
            var query = _unitOfWork.AcademicClassRepository.GetQueryable();

            var totalCount = await query.CountAsync();

            var data = await query
                .OrderByDescending(x => x.CreatedAt)
                .Skip((pageIndex - 1) * pageSize)
                .Take(pageSize)
                .Select(x => new GetClassManagementDTO
                {
                    Id = x.Id,
                    ClassName = x.ClassName,
                    Semester = x.Semester,
                    CreatedAt = x.CreatedAt,
                    UpdatedAt = x.UpdatedAt,
                    LecturerId = x.LecturerId,
                    ExpertId = x.ExpertId,
                    LecturerName = x.Lecturer != null ? x.Lecturer.FullName : null,
                    ExpertName = x.Expert != null ? x.Expert.FullName : null,
                    LecturerEmail = x.Lecturer != null ? x.Lecturer.Email : null,
                    ExpertEmail = x.Expert != null ? x.Expert.Email : null,
                    StudentCount = x.ClassEnrollments.Count,
                    // Classification fields
                    ClassSpecialtyId = x.ClassSpecialtyId,
                    ClassSpecialtyName = x.ClassSpecialty != null ? x.ClassSpecialty.Name : null,
                    ClassSpecialtyCode = x.ClassSpecialty != null ? x.ClassSpecialty.Code : null,
                    FocusLevel = x.FocusLevel,
                    TargetStudentLevel = x.TargetStudentLevel,
                    TargetPathologyCategories = x.TargetPathologyCategories
                })
                .ToListAsync();

            return new PagedResult<GetClassManagementDTO>
            {
                Items = data,
                TotalCount = totalCount,
                PageIndex = pageIndex,
                PageSize = pageSize
            };
        }

        public async Task<GetClassManagementDTO?> GetAcademicClassByIdAsync(Guid id)
        {
            return await _unitOfWork.AcademicClassRepository
                .GetQueryable()
                .Where(x => x.Id == id)
                .Select(x => new GetClassManagementDTO
                {
                    Id = x.Id,
                    ClassName = x.ClassName,
                    Semester = x.Semester,
                    CreatedAt = x.CreatedAt,
                    UpdatedAt = x.UpdatedAt,
                    LecturerId = x.LecturerId,
                    ExpertId = x.ExpertId,
                    LecturerName = x.Lecturer != null ? x.Lecturer.FullName : null,
                    ExpertName = x.Expert != null ? x.Expert.FullName : null,
                    LecturerEmail = x.Lecturer != null ? x.Lecturer.Email : null,
                    ExpertEmail = x.Expert != null ? x.Expert.Email : null,
                    StudentCount = x.ClassEnrollments.Count,
                    // Classification fields
                    ClassSpecialtyId = x.ClassSpecialtyId,
                    ClassSpecialtyName = x.ClassSpecialty != null ? x.ClassSpecialty.Name : null,
                    ClassSpecialtyCode = x.ClassSpecialty != null ? x.ClassSpecialty.Code : null,
                    FocusLevel = x.FocusLevel,
                    TargetStudentLevel = x.TargetStudentLevel,
                    TargetPathologyCategories = x.TargetPathologyCategories
                })
                .FirstOrDefaultAsync();
        }

        public async Task<CreateClassManagementDTO> CreateAcademicClassAsync(CreateClassManagementDTO dto)
        {         

            var entity = new AcademicClass
            {
                Id = Guid.NewGuid(),
                ClassName = dto.ClassName,
                Semester = dto.Semester,
                CreatedAt = DateTime.UtcNow,
                // Classification fields
                ClassSpecialtyId = dto.ClassSpecialtyId,
                FocusLevel = dto.FocusLevel ?? "Basic",
                TargetStudentLevel = dto.TargetStudentLevel ?? "Beginner"
            };

            await _unitOfWork.AcademicClassRepository.AddAsync(entity);
            await _unitOfWork.SaveAsync();

            dto.Id = entity.Id;

            return dto;
        }

        public async Task<UpdateClassManagementDTO> UpdateAcademicClassAsync(UpdateClassManagementDTO dto)
        {
            var entity = await _unitOfWork .AcademicClassRepository.GetByIdAsync(dto.Id);

            if (entity == null)
                throw new Exception("Academic class not found");

            entity.ClassName = dto.ClassName;
            entity.Semester = dto.Semester;
            entity.UpdatedAt = DateTime.UtcNow;
            // Classification fields
            entity.ClassSpecialtyId = dto.ClassSpecialtyId;
            entity.FocusLevel = dto.FocusLevel;
            entity.TargetStudentLevel = dto.TargetStudentLevel;

            _unitOfWork.AcademicClassRepository.Update(entity);
            await _unitOfWork.SaveAsync();

            return dto;
        }

        public async Task<bool> DeleteAcademicClassAsync(Guid id)
        {
            var entity = await _unitOfWork
                .AcademicClassRepository
                .GetByIdAsync(id);

            if (entity == null)
                return false;

            _unitOfWork.AcademicClassRepository.Remove(entity);

            await _unitOfWork.SaveAsync();

            return true;
        }

        //=======================================================  ASSIGN CLASS  ===================================================
        public async Task<PagedResult<GetAssignClassDTO>> GetAssignClassAsync(int pageIndex,int pageSize, Guid? classId = null)
        {
            var query = _unitOfWork.ClassEnrollmentRepository
                .GetQueryable()
                .AsQueryable();

            if (classId.HasValue)
                query = query.Where(x => x.ClassId == classId.Value);

            var totalCount = await query.CountAsync();

            var data = await query
                .OrderByDescending(x => x.EnrolledAt)
                .Skip((pageIndex - 1) * pageSize)
                .Take(pageSize)
                .Select(x => new GetAssignClassDTO
                {
                    Id = x.Id,
                    ClassId = x.ClassId,
                    ClassName = x.Class.ClassName,
                    LecturerName = x.Class.Lecturer != null ? x.Class.Lecturer.FullName : null,
                    ExpertName = x.Class.Expert != null ? x.Class.Expert.FullName : null,
                    StudentName = x.Student.FullName,
                    EnrolledAt = x.EnrolledAt
                })
                .ToListAsync();


            return new PagedResult<GetAssignClassDTO>
            {
                Items = data,
                TotalCount = totalCount,
                PageIndex = pageIndex,
                PageSize = pageSize
            };
        }
       
        public async Task<AssignClassDTO> AssignClassAsync(AssignClassDTO dto)
        {
            if (!dto.LecturerId.HasValue && !dto.ExpertId.HasValue && !dto.StudentId.HasValue && !dto.RemoveExpert)
                throw new InvalidOperationException("At least one action is required: assign LecturerId, ExpertId, StudentId (enroll), or RemoveExpert.");

            if (dto.RemoveExpert && dto.ExpertId.HasValue)
                throw new InvalidOperationException("Cannot set RemoveExpert and ExpertId at the same time.");

            var classEntity = await _unitOfWork.AcademicClassRepository.GetByIdAsync(dto.ClassId)
                ?? throw new InvalidOperationException("Class not found");

            var userIds = new HashSet<Guid>();
            if (dto.StudentId.HasValue) userIds.Add(dto.StudentId.Value);
            if (dto.LecturerId.HasValue) userIds.Add(dto.LecturerId.Value);
            if (dto.ExpertId.HasValue) userIds.Add(dto.ExpertId.Value);

            var users = userIds.Count == 0
                ? new List<User>()
                : await _unitOfWork.UserRepository
                    .GetQueryable()
                    .Include(x => x.UserRoles)
                    .ThenInclude(x => x.Role)
                    .Where(x => userIds.Contains(x.Id))
                    .ToListAsync();

            static bool HasRole(User u, string roleName) =>
                u.UserRoles.Any(r => r.Role != null && r.Role.Name == roleName);

            if (dto.LecturerId.HasValue)
            {
                var lecturer = users.FirstOrDefault(x => x.Id == dto.LecturerId.Value)
                    ?? throw new InvalidOperationException("Lecturer not found.");
                if (!HasRole(lecturer, "Lecturer"))
                    throw new InvalidOperationException("User is not Lecturer.");
                classEntity.LecturerId = dto.LecturerId.Value;
            }

            if (dto.RemoveExpert)
                classEntity.ExpertId = null;
            else if (dto.ExpertId.HasValue)
            {
                var expert = users.FirstOrDefault(x => x.Id == dto.ExpertId.Value)
                    ?? throw new InvalidOperationException("Expert not found.");
                if (!HasRole(expert, "Expert"))
                    throw new InvalidOperationException("User is not Expert.");
                classEntity.ExpertId = dto.ExpertId.Value;
            }

            classEntity.UpdatedAt = DateTime.UtcNow;
            _unitOfWork.AcademicClassRepository.Update(classEntity);

            if (dto.StudentId.HasValue)
            {
                var student = users.FirstOrDefault(x => x.Id == dto.StudentId.Value)
                    ?? throw new InvalidOperationException("Student not found.");
                if (!HasRole(student, "Student"))
                    throw new InvalidOperationException("User is not Student.");

                var existed = await _unitOfWork.ClassEnrollmentRepository
                    .GetQueryable()
                    .AnyAsync(x => x.ClassId == dto.ClassId && x.StudentId == dto.StudentId.Value);
                if (existed)
                    throw new InvalidOperationException("Student already enrolled.");

                var enrollment = new ClassEnrollment
                {
                    Id = Guid.NewGuid(),
                    ClassId = dto.ClassId,
                    StudentId = dto.StudentId.Value,
                    ClassName = classEntity.ClassName,
                    EnrolledAt = DateTime.UtcNow
                };
                await _unitOfWork.ClassEnrollmentRepository.AddAsync(enrollment);
            }

            await _unitOfWork.SaveAsync();

            dto.ClassName = classEntity.ClassName;
            return dto;
        }

        public async Task<AssignClassDTO> UpdateAssignClassAsync(AssignClassDTO dto)
        {
            if (!dto.LecturerId.HasValue && !dto.ExpertId.HasValue && !dto.RemoveExpert && !dto.StudentId.HasValue)
                throw new InvalidOperationException("At least one field is required: StudentId (update enrollment), LecturerId, ExpertId, or RemoveExpert.");

            if (dto.RemoveExpert && dto.ExpertId.HasValue)
                throw new InvalidOperationException("Cannot set RemoveExpert and ExpertId at the same time.");

            var classEntity = await _unitOfWork.AcademicClassRepository.GetByIdAsync(dto.ClassId)
                ?? throw new InvalidOperationException("Class not found");

            ClassEnrollment? enrollment = null;
            if (dto.StudentId.HasValue)
            {
                enrollment = await _unitOfWork.ClassEnrollmentRepository
                    .GetQueryable()
                    .FirstOrDefaultAsync(x =>
                        x.ClassId == dto.ClassId &&
                        x.StudentId == dto.StudentId.Value);
                if (enrollment == null)
                    throw new InvalidOperationException("Enrollment not found.");
            }

            var userIds = new HashSet<Guid>();
            if (dto.StudentId.HasValue) userIds.Add(dto.StudentId.Value);
            if (dto.LecturerId.HasValue) userIds.Add(dto.LecturerId.Value);
            if (dto.ExpertId.HasValue) userIds.Add(dto.ExpertId.Value);

            var users = userIds.Count == 0
                ? new List<User>()
                : await _unitOfWork.UserRepository
                    .GetQueryable()
                    .Include(x => x.UserRoles)
                    .ThenInclude(x => x.Role)
                    .Where(x => userIds.Contains(x.Id))
                    .ToListAsync();

            static bool HasRole(User u, string roleName) =>
                u.UserRoles.Any(r => r.Role != null && r.Role.Name == roleName);

            if (dto.LecturerId.HasValue)
            {
                var lecturer = users.FirstOrDefault(x => x.Id == dto.LecturerId.Value)
                    ?? throw new InvalidOperationException("Lecturer not found.");
                if (!HasRole(lecturer, "Lecturer"))
                    throw new InvalidOperationException("User is not Lecturer.");
                classEntity.LecturerId = dto.LecturerId.Value;
            }

            if (dto.RemoveExpert)
                classEntity.ExpertId = null;
            else if (dto.ExpertId.HasValue)
            {
                var expert = users.FirstOrDefault(x => x.Id == dto.ExpertId.Value)
                    ?? throw new InvalidOperationException("Expert not found.");
                if (!HasRole(expert, "Expert"))
                    throw new InvalidOperationException("User is not Expert.");
                classEntity.ExpertId = dto.ExpertId.Value;
            }

            if (dto.StudentId.HasValue)
            {
                var student = users.FirstOrDefault(x => x.Id == dto.StudentId.Value)
                    ?? throw new InvalidOperationException("Student not found.");
                if (!HasRole(student, "Student"))
                    throw new InvalidOperationException("User is not Student.");
                enrollment!.EnrolledAt = DateTime.UtcNow;
                _unitOfWork.ClassEnrollmentRepository.Update(enrollment);
            }

            classEntity.UpdatedAt = DateTime.UtcNow;
            _unitOfWork.AcademicClassRepository.Update(classEntity);

            await _unitOfWork.SaveAsync();

            dto.ClassName = classEntity.ClassName;
            return dto;
        }
        public async Task<bool> DeleteAssignClassAsync(Guid id)
        {
            var enrollment = await _unitOfWork.ClassEnrollmentRepository.GetByIdAsync(id);

            if (enrollment == null) return false;


            _unitOfWork.ClassEnrollmentRepository.Remove(enrollment);

            await _unitOfWork.SaveAsync();

            return true;
        }
    }
}
