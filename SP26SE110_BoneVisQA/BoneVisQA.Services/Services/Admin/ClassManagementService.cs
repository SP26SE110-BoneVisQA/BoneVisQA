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
            var query = _unitOfWork
                .ClassEnrollmentRepository
                .GetQueryable()
                .Include(x => x.Class)
                .ThenInclude(x => x.Lecturer)
                .Include(x => x.Class)
                .ThenInclude(x => x.Expert)
                .Include(x => x.Student);


            var groupedQuery = query
                .GroupBy(x => x.ClassId)
                .Select(g => new GetAssignClassDTO
                {
                    ClassId = g.Key,

                    ClassName = g.First().Class.ClassName,

                    LecturerName = g.First().Class.Lecturer != null
                        ? g.First().Class.Lecturer.FullName
                        : null,

                    ExpertName = g.First().Class.Expert != null
                        ? g.First().Class.Expert.FullName
                        : null,

                    Students = g
                        .Select(x => x.Student.FullName)
                        .ToList(),

                    LastEnrollment = g
                        .Max(x => x.EnrolledAt)
                });


            var totalCount = await groupedQuery.CountAsync();


            var data = await groupedQuery
                .OrderByDescending(x => x.LastEnrollment)
                .Skip((pageIndex - 1) * pageSize)
                .Take(pageSize)
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
            if (dto.StudentIds == null || !dto.StudentIds.Any()) throw new Exception("Must assign at least 1 student into class");


            var classEntity = await _unitOfWork.AcademicClassRepository.GetByIdAsync(dto.ClassId);

            if (classEntity == null) throw new Exception("Class not found");


            var users = await _unitOfWork.UserRepository
                .GetQueryable()
                .Include(x => x.UserRoles)
                .ThenInclude(x => x.Role)
                .Where(x =>
                    dto.StudentIds.Contains(x.Id) ||
                    (dto.LecturerId != null && x.Id == dto.LecturerId) ||
                    (dto.ExpertId != null && x.Id == dto.ExpertId))
                .ToListAsync();


            // validate students
            var students = users
                .Where(x => dto.StudentIds.Contains(x.Id))
                .ToList();

            if (students.Count != dto.StudentIds.Count) throw new Exception("Some students not found");


            foreach (var student in students)
            {
                if (!student.UserRoles.Any(r => r.Role.Name == "Student"))
                    throw new Exception($"User {student.FullName} is not Student");
            }


            // assign Lecturer nếu class chưa có
            if (classEntity.LecturerId == null)
            {
                if (dto.LecturerId == null)
                    throw new Exception("Lecturer is required for this class");

                var lecturer = users.FirstOrDefault(x => x.Id == dto.LecturerId);

                if (lecturer == null ||
                    !lecturer.UserRoles.Any(r => r.Role.Name == "Lecturer"))
                {
                    throw new Exception("User is not Lecturer");
                }

                classEntity.LecturerId = dto.LecturerId;
            }


            // assign Expert nếu class chưa có
            if (classEntity.ExpertId == null)
            {
                if (dto.ExpertId == null)
                    throw new Exception("Expert is required for this class");

                var expert = users.FirstOrDefault(x => x.Id == dto.ExpertId);

                if (expert == null ||!expert.UserRoles.Any(r => r.Role.Name == "Expert"))
                {
                    throw new Exception("User is not Expert");
                }

                classEntity.ExpertId = dto.ExpertId;
            }


            _unitOfWork.AcademicClassRepository.Update(classEntity);


            // check existed students
            var existedStudents = await _unitOfWork
                .ClassEnrollmentRepository
                .GetQueryable()
                .Where(x =>
                    x.ClassId == dto.ClassId &&
                    dto.StudentIds.Contains(x.StudentId))
                .Select(x => x.StudentId)
                .ToListAsync();


            var newStudents = dto.StudentIds
                .Except(existedStudents)
                .ToList();


            if (!newStudents.Any())
                throw new Exception("Student already enrolled");


            var enrollments = newStudents.Select(studentId =>
                new ClassEnrollment
                {
                    Id = Guid.NewGuid(),
                    ClassId = dto.ClassId,
                    StudentId = studentId,
                    EnrolledAt = DateTime.UtcNow,   
                });


            await _unitOfWork.ClassEnrollmentRepository.AddRangeAsync(enrollments);


            await _unitOfWork.SaveAsync();

            dto.ClassName = classEntity.ClassName;

            return dto;
        }

        public async Task<UpdateAssignClassDTO> UpdateAssignClassAsync(UpdateAssignClassDTO dto)
        {
            var classEntity = await _unitOfWork
                .AcademicClassRepository
                .GetByIdAsync(dto.ClassId);

            if (classEntity == null)
                throw new Exception("Class not found");


            var userIds = new List<Guid>();

            if (dto.LecturerId.HasValue)
                userIds.Add(dto.LecturerId.Value);

            if (dto.ExpertId.HasValue)
                userIds.Add(dto.ExpertId.Value);


            var users = await _unitOfWork.UserRepository
                .GetQueryable()
                .Include(x => x.UserRoles)
                .ThenInclude(x => x.Role)
                .Where(x => userIds.Contains(x.Id))
                .ToListAsync();


            // update Lecturer nếu truyền vào
            if (dto.LecturerId == null)
            {
                var lecturer = users
                    .FirstOrDefault(x => x.Id == dto.LecturerId);

                if (lecturer == null ||!lecturer.UserRoles.Any(r => r.Role.Name == "Lecturer"))
                {
                    throw new Exception("User is not Lecturer");
                }

                classEntity.LecturerId = dto.LecturerId;
            }


            // update Expert nếu truyền vào
            if (dto.ExpertId == null)
            {
                var expert = users
                    .FirstOrDefault(x => x.Id == dto.ExpertId);

                if (expert == null || !expert.UserRoles.Any(r => r.Role.Name == "Expert"))
                {
                    throw new Exception("User is not Expert");
                }

                classEntity.ExpertId = dto.ExpertId;
            }


            _unitOfWork
                .AcademicClassRepository
                .Update(classEntity);


            await _unitOfWork.SaveAsync();

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
