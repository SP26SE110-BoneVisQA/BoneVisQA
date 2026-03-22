using BoneVisQA.Repositories.Models;
using BoneVisQA.Repositories.UnitOfWork;
using BoneVisQA.Services.Interfaces.Expert;
using BoneVisQA.Services.Models.Expert;
using BoneVisQA.Services.Models.Student;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
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

        public MedicalCaseService(IUnitOfWork unitOfWork, IWebHostEnvironment env)
        {
            _unitOfWork = unitOfWork;
            _env = env;
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

        public async Task<MedicalCaseDTO> CreateMedicalCaseAsync(MedicalCaseDTOResponse dto)
        {
            var medicalCase = new MedicalCase
            {
                Id = Guid.NewGuid(),
                Title = dto.Title,
                Description = dto.Description,
                Difficulty = dto.Difficulty,
                CategoryId = dto.CategoryId,
                SuggestedDiagnosis = dto.SuggestedDiagnosis,
                KeyFindings = dto.KeyFindings,
                IsApproved = false,
                IsActive = true,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow,
            };

            await _unitOfWork.MedicalCaseRepository.AddAsync(medicalCase);
            await _unitOfWork.SaveAsync();

            return new MedicalCaseDTO
            {
                Id = medicalCase.Id,
                Title = medicalCase.Title,
                Description = medicalCase.Description,
                Difficulty = medicalCase.Difficulty,
                CategoryId = medicalCase.CategoryId,
                IsApproved = medicalCase.IsApproved,
                IsActive = medicalCase.IsActive,
                SuggestedDiagnosis = medicalCase.SuggestedDiagnosis,
                KeyFindings = medicalCase.KeyFindings,
                CreatedAt = medicalCase.CreatedAt,
            };
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
    }
}
