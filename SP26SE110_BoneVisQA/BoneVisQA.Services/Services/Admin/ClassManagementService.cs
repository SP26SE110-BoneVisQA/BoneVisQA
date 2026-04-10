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
                    LecturerName = x.Class.Lecturer.FullName,
                    ExpertName = x.Class.Expert.FullName,
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
            var classEntity = await _unitOfWork.AcademicClassRepository .GetByIdAsync(dto.ClassId);

            if (classEntity == null) throw new Exception("Class not found");

            var users = await _unitOfWork.UserRepository
                .GetQueryable()
                .Include(x => x.UserRoles)
                .ThenInclude(x => x.Role)
                .Where(x =>
                    x.Id == dto.StudentId ||
                    x.Id == dto.LecturerId ||
                    x.Id == dto.ExpertId)
                .ToListAsync();


            var student = users.FirstOrDefault(x => x.Id == dto.StudentId);

            if (student == null ||
                !student.UserRoles.Any(r => r.Role.Name == "Student"))
            {
                throw new Exception("User is not Student");
            }


            var lecturer = users.FirstOrDefault(x => x.Id == dto.LecturerId);

            if (lecturer == null ||
                !lecturer.UserRoles.Any(r => r.Role.Name == "Lecturer"))
            {
                throw new Exception("User is not Lecturer");
            }


            var expert = users.FirstOrDefault(x => x.Id == dto.ExpertId);

            if (expert == null ||
                !expert.UserRoles.Any(r => r.Role.Name == "Expert"))
            {
                throw new Exception("User is not Expert");
            }


            // assign Lecturer nếu chưa có
            if (classEntity.LecturerId == null)
                classEntity.LecturerId = dto.LecturerId;


            // assign Expert nếu chưa có
            if (classEntity.ExpertId == null)
                classEntity.ExpertId = dto.ExpertId;

            _unitOfWork.AcademicClassRepository.Update(classEntity);


            // check student already enrolled chưa
            var existed = await _unitOfWork
                .ClassEnrollmentRepository
                .GetQueryable()
                .AnyAsync(x =>
                    x.ClassId == dto.ClassId &&
                    x.StudentId == dto.StudentId);

            if (existed) throw new Exception("Student already enrolled");


            var enrollment = new ClassEnrollment
            {
                Id = Guid.NewGuid(),
                ClassId = dto.ClassId,
                StudentId = dto.StudentId,
                EnrolledAt = DateTime.UtcNow
            };


            await _unitOfWork.ClassEnrollmentRepository.AddAsync(enrollment);

            await _unitOfWork.SaveAsync();


            dto.ClassName = enrollment.ClassName;

            return dto;
        }

        public async Task<AssignClassDTO> UpdateAssignClassAsync(AssignClassDTO dto)
        {
            var classEntity = await _unitOfWork .AcademicClassRepository.GetByIdAsync(dto.ClassId);

            if (classEntity == null) throw new Exception("Class not found");


            var enrollment = await _unitOfWork.ClassEnrollmentRepository
                .GetQueryable()
                .FirstOrDefaultAsync(x =>
                    x.ClassId == dto.ClassId &&
                    x.StudentId == dto.StudentId);

            if (enrollment == null) throw new Exception("Enrollment not found");


            var users = await _unitOfWork.UserRepository
                .GetQueryable()
                .Include(x => x.UserRoles)
                .ThenInclude(x => x.Role)
                .Where(x =>
                    x.Id == dto.StudentId ||
                    x.Id == dto.LecturerId ||
                    x.Id == dto.ExpertId)
                .ToListAsync();


            var student = users.FirstOrDefault(x => x.Id == dto.StudentId);

            if (student == null ||
                !student.UserRoles.Any(r => r.Role.Name == "Student"))
            {
                throw new Exception("User is not Student");
            }


            var lecturer = users.FirstOrDefault(x => x.Id == dto.LecturerId);

            if (lecturer == null ||
                !lecturer.UserRoles.Any(r => r.Role.Name == "Lecturer"))
            {
                throw new Exception("User is not Lecturer");
            }


            var expert = users.FirstOrDefault(x => x.Id == dto.ExpertId);

            if (expert == null ||
                !expert.UserRoles.Any(r => r.Role.Name == "Expert"))
            {
                throw new Exception("User is not Expert");
            }


            // assign Lecturer nếu class chưa có
            if (classEntity.LecturerId == null)
                classEntity.LecturerId = dto.LecturerId;


            // assign Expert nếu class chưa có
            if (classEntity.ExpertId == null)
                classEntity.ExpertId = dto.ExpertId;



            _unitOfWork.AcademicClassRepository.Update(classEntity);


            enrollment.EnrolledAt = DateTime.UtcNow;


            _unitOfWork.ClassEnrollmentRepository.Update(enrollment);


            await _unitOfWork.SaveAsync();


            dto.ClassName = enrollment.ClassName;

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
