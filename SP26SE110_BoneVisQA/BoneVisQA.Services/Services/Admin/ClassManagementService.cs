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
            var query = _unitOfWork.AcademicClassRepository
                .GetQueryable()
                .Include(x => x.Lecturer)
                .Include(x => x.Expert);

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
                    UpdatedAt = x.UpdatedAt
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

        public async Task<CreateClassManagementDTO> CreateAcademicClassAsync(CreateClassManagementDTO dto)
        {         

            var entity = new AcademicClass
            {
                Id = Guid.NewGuid(),
                ClassName = dto.ClassName,
                Semester = dto.Semester,
                CreatedAt = DateTime.UtcNow
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
        public async Task<PagedResult<GetAssignClassDTO>> GetAssignClassAsync(int pageIndex,int pageSize)
        {
            var query = _unitOfWork.ClassEnrollmentRepository
                .GetQueryable()
                .Include(x => x.Class)
                .ThenInclude(x => x.Lecturer)
                .Include(x => x.Class)
                .ThenInclude(x => x.Expert)
                .Include(x => x.Student);


            var totalCount = await query.CountAsync();


            var data = await query
                .OrderByDescending(x => x.EnrolledAt)
                .Skip((pageIndex - 1) * pageSize)
                .Take(pageSize)
                .Select(x => new GetAssignClassDTO
                {
                    Id = x.Id,
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
                throw new InvalidOperationException("Cần ít nhất một thao tác: gán LecturerId, ExpertId, StudentId (enroll), hoặc RemoveExpert.");

            if (dto.RemoveExpert && dto.ExpertId.HasValue)
                throw new InvalidOperationException("Không thể vừa RemoveExpert vừa gán ExpertId.");

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
                throw new InvalidOperationException("Cần ít nhất một trường: StudentId (cập nhật enrollment), LecturerId, ExpertId, hoặc RemoveExpert.");

            if (dto.RemoveExpert && dto.ExpertId.HasValue)
                throw new InvalidOperationException("Không thể vừa RemoveExpert vừa gán ExpertId.");

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
