using BoneVisQA.Repositories.Models;
using BoneVisQA.Repositories.UnitOfWork;
using BoneVisQA.Services.Interfaces.Expert;
using BoneVisQA.Services.Models.Expert;
using BoneVisQA.Services.Models.Student;
using DocumentFormat.OpenXml.Office2016.Excel;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BoneVisQA.Services.Services.Expert
{
    public class MedicalCaseService : IMedicalCaseService
    {
        private readonly IUnitOfWork _unitOfWork;
        private readonly IWebHostEnvironment _env;
        private readonly IHttpContextAccessor _httpContextAccessor;

        public MedicalCaseService(IUnitOfWork unitOfWork, IWebHostEnvironment env, IHttpContextAccessor httpContextAccessor)
        {
            _unitOfWork = unitOfWork;
            _env = env;
            _httpContextAccessor = httpContextAccessor;
        }
        private async Task<string> SaveImageAsync(IFormFile file)
        {
            var uploadFolder = Path.Combine(_env.ContentRootPath, "uploads", "images");
            if (!Directory.Exists(uploadFolder))
                Directory.CreateDirectory(uploadFolder);

            var extension = Path.GetExtension(file.FileName);
            var originalName = Path.GetFileNameWithoutExtension(file.FileName);

            // Rút ngắn tên nếu quá dài
            if (originalName.Length > 50)
                originalName = originalName.Substring(0, 50);

            var fileName = $"{originalName}_{DateTime.UtcNow:yyyyMMddHHmmss}{extension}";
            var filePath = Path.Combine(uploadFolder, fileName);

            using var stream = new FileStream(filePath, FileMode.Create);
            await file.CopyToAsync(stream);

            return $"/uploads/images/{fileName}";
        }
        public async Task<PagedResult<GetMedicalCaseDTO>> GetAllMedicalCasesAsync(int pageIndex,int pageSize)
        {
            var query = _unitOfWork.MedicalCaseRepository.GetQueryable();

            var totalCount = await query.CountAsync();

            var medicalCases = await query
                .OrderByDescending(x => x.CreatedAt)
                .Skip((pageIndex - 1) * pageSize)
                .Take(pageSize)
                .Select(x => new GetMedicalCaseDTO
                {
                    Id = x.Id,
                    Title = x.Title,
                    CreatedByExpertId = x.CreatedByExpertId,
                    ExpertName = x.CreatedByExpert!.FullName,
                    Description = x.Description,
                    Difficulty = x.Difficulty,
                    CategoryId = x.CategoryId,
                    CategoryName = x.Category!.Name, 
                    IsApproved = x.IsApproved,
                    IsActive = x.IsActive,
                    SuggestedDiagnosis = x.SuggestedDiagnosis,
                    ReflectiveQuestions = x.ReflectiveQuestions,
                    KeyFindings = x.KeyFindings,
                    CreatedAt = x.CreatedAt
                })
                .ToListAsync();

            return new PagedResult<GetMedicalCaseDTO>
            {
                Items = medicalCases,
                TotalCount = totalCount,
                PageIndex = pageIndex,
                PageSize = pageSize
            };
        }
        public async Task<CreateMedicalCaseResponseDTO> CreateMedicalCaseAsync(CreateMedicalCaseRequestDTO dto)
        {
            var categoryname = await _unitOfWork.CategoryRepository.GetByIdAsync(dto.CategoryId ?? Guid.Empty);
          
            User ? expert = null;

            if (dto.CreatedByExpertId.HasValue)
            {
                expert = await _unitOfWork.UserRepository.GetByIdAsync(dto.CreatedByExpertId.Value);
            }

            var medicalCase = new MedicalCase
            {
                Id = Guid.NewGuid(),
                CreatedByExpertId = dto.CreatedByExpertId,  
                Title = dto.Title,
                Description = dto.Description,
                Difficulty = dto.Difficulty,
                CategoryId = dto.CategoryId,
                SuggestedDiagnosis = dto.SuggestedDiagnosis,
                KeyFindings = dto.KeyFindings,
                ReflectiveQuestions = dto.ReflectiveQuestions,
                IsApproved = true,
                IsActive = true,
                CreatedAt = DateTime.UtcNow,
            };

            await _unitOfWork.MedicalCaseRepository.AddAsync(medicalCase);
            await _unitOfWork.SaveAsync();

            return new CreateMedicalCaseResponseDTO
            {
                Id = medicalCase.Id,
                ExpertName = expert?.FullName,  
                Title = medicalCase.Title,
                Description = medicalCase.Description,
                Difficulty = medicalCase.Difficulty,
                CategoryName = categoryname?.Name,
                IsApproved = medicalCase.IsApproved,
                IsActive = medicalCase.IsActive,
                SuggestedDiagnosis = medicalCase.SuggestedDiagnosis,
                ReflectiveQuestions = medicalCase.ReflectiveQuestions,  
                KeyFindings = medicalCase.KeyFindings,
                CreatedAt = medicalCase.CreatedAt,
            };
        }

        public async Task<UpdateMedicalCaseResponseDTO?> UpdateMedicalCaseAsync(Guid id,UpdateMedicalCaseDTORequest request)
        {
            var medicalCase = await _unitOfWork.MedicalCaseRepository.GetByIdAsync(id);

            if (medicalCase == null)
                return null;

            // Update fields
            medicalCase.Title = request.Title;
            medicalCase.Description = request.Description;
            medicalCase.Difficulty = request.Difficulty;
            medicalCase.CategoryId = request.CategoryId;
            medicalCase.IsApproved = request.IsApproved;
            medicalCase.IsActive = request.IsActive;
            medicalCase.SuggestedDiagnosis = request.SuggestedDiagnosis;
            medicalCase.ReflectiveQuestions = request.ReflectiveQuestions;
            medicalCase.KeyFindings = request.KeyFindings;
            medicalCase.CreatedByExpertId = request.CreatedByExpertId;
            medicalCase.UpdatedAt = DateTime.UtcNow;

            _unitOfWork.MedicalCaseRepository.Update(medicalCase);
            await _unitOfWork.SaveAsync();

            // lấy thêm dữ liệu liên quan
            var expert = await _unitOfWork.UserRepository
                .GetByIdAsync(medicalCase.CreatedByExpertId ?? Guid.Empty);

            var category = await _unitOfWork.CategoryRepository
                .GetByIdAsync(medicalCase.CategoryId ?? Guid.Empty);

            return new UpdateMedicalCaseResponseDTO
            {
                Id = medicalCase.Id,
                ExpertName = expert?.FullName,
                Title = medicalCase.Title,
                Description = medicalCase.Description,
                Difficulty = medicalCase.Difficulty,
                CategoryName = category?.Name,
                IsApproved = medicalCase.IsApproved,
                IsActive = medicalCase.IsActive,
                SuggestedDiagnosis = medicalCase.SuggestedDiagnosis,
                ReflectiveQuestions = medicalCase.ReflectiveQuestions,
                KeyFindings = medicalCase.KeyFindings,
                UpdatedAt = medicalCase.UpdatedAt
            };
        }
        public async Task<bool> DeleteMedicalCaseAsync(Guid id)
        {
            var medicalCase = await _unitOfWork.MedicalCaseRepository
                .GetByIdAsync(id);

            if (medicalCase == null)
                return false;

            _ = _unitOfWork.MedicalCaseRepository.RemoveAsync(medicalCase);

            await _unitOfWork.SaveAsync();

            return true;
        }

        // Thêm image cho case
        public async Task<AddMedicalImageDTO> AddImageAsync(AddMedicalImageDTOResponse dto)
        {
            var medicalCase = await _unitOfWork.MedicalCaseRepository.GetByIdAsync(dto.CaseId)
                ?? throw new KeyNotFoundException("Không tìm thấy ca bệnh.");

            var imageUrl = await SaveImageAsync(dto.Image);

            var image = new MedicalImage
            {
                Id = Guid.NewGuid(),
                CaseId = dto.CaseId,
                ImageUrl = imageUrl,
                Modality = dto.Modality,
                CreatedAt = DateTime.UtcNow
            };

            await _unitOfWork.MedicalImageRepository.AddAsync(image);
            await _unitOfWork.SaveAsync();

            return new AddMedicalImageDTO
            {
                Id = image.Id,
                ImageUrl = image.ImageUrl,
                Modality = image.Modality,
                CaseTitle = medicalCase.Title,
                Annotations = new List<AddAnnotationDTO>()
            };
        }

        // Thêm annotation cho image
        public async Task<AddAnnotationDTO> AddAnnotationAsync(AddAnnotationDTOResponse dto)
        {
            var image = await _unitOfWork.MedicalImageRepository.GetByIdAsync(dto.ImageId)
                ?? throw new KeyNotFoundException("Không tìm thấy ảnh.");

            var annotation = new CaseAnnotation
            {
                Id = Guid.NewGuid(),
                ImageId = dto.ImageId,
                Label = dto.Label,
                Coordinates = dto.Coordinates,
                CreatedAt = DateTime.UtcNow
            };

            await _unitOfWork.CaseAnnotationRepository.AddAsync(annotation);
            await _unitOfWork.SaveAsync();

            return new AddAnnotationDTO
            {
                Id = annotation.Id,
                Label = annotation.Label,
                Coordinates = annotation.Coordinates
            };
        }
<<<<<<< Updated upstream

=======
        public async Task<PagedResult<GetAllAnnotationDTO>> GetAllAnnotation(int pageIndex, int pageSize)
        {
            var request = _httpContextAccessor.HttpContext.Request;

            var baseUrl = $"{request.Scheme}://{request.Host}";

            var query = _unitOfWork.CaseAnnotationRepository
                .GetQueryable();

            var totalCount = await query.CountAsync();

            var annotations = await query
                .OrderByDescending(x => x.Id)
                .Skip((pageIndex - 1) * pageSize)
                .Take(pageSize)
                .Select(x => new GetAllAnnotationDTO
                {
                    Id = x.Id,
                    ImageUrl = baseUrl + x.Image.ImageUrl,
                    Label = x.Label,
                    Coordinates = x.Coordinates
                })
                .ToListAsync();

            return new PagedResult<GetAllAnnotationDTO>
            {
                Items = annotations,
                TotalCount = totalCount,
                PageIndex = pageIndex,
                PageSize = pageSize
            };
        }
>>>>>>> Stashed changes
        public async Task<PagedResult<GetCategoryDTO>> GetAllCategory(int pageIndex, int pageSize)
        {
            var query = _unitOfWork.CategoryRepository.GetQueryable();

            var totalCount = await query.CountAsync();

            var categories = await query
                .OrderBy(x => x.Name)
                .Skip((pageIndex - 1) * pageSize)
                .Take(pageSize)
                .Select(x => new GetCategoryDTO
                {
                    Id = x.Id,
                    Name = x.Name
                })
                .ToListAsync();

            return new PagedResult<GetCategoryDTO>
            {
                Items = categories,
                TotalCount = totalCount,
                PageIndex = pageIndex,
                PageSize = pageSize
            };
        }

        public async Task<PagedResult<GetTagDTO>> GetAllTag(int pageIndex, int pageSize)
        {
            var query = _unitOfWork.TagRepository.GetQueryable();

            var totalCount = await query.CountAsync();

            var tags = await query
                .OrderBy(x => x.Name)
                .Skip((pageIndex - 1) * pageSize)
                .Take(pageSize)
                .Select(x => new GetTagDTO
                {
                    Id = x.Id,
                    Name = x.Name
                })
                .ToListAsync();

            return new PagedResult<GetTagDTO>
            {
                Items = tags,
                TotalCount = totalCount,
                PageIndex = pageIndex,
                PageSize = pageSize
            };
        }
        public async Task<PagedResult<GetAllImageDTO>> GetAllImage(int pageIndex, int pageSize)
        {
            var query = _unitOfWork.MedicalImageRepository.GetQueryable();

            var totalCount = await query.CountAsync();

            var images = await query
                .OrderByDescending(x => x.CreatedAt) 
                .Skip((pageIndex - 1) * pageSize)
                .Take(pageSize)
              .Select(x => new GetAllImageDTO
              {
                  Id = x.Id,
                  ImageUrl = x.ImageUrl,
                  FileName = Path.GetFileName(x.ImageUrl)
              })
                .ToListAsync();

            return new PagedResult<GetAllImageDTO>
            {
                Items = images,
                TotalCount = totalCount,
                PageIndex = pageIndex,
                PageSize = pageSize
            };
        }
    }
}
