using BoneVisQA.Repositories.Models;
using BoneVisQA.Repositories.UnitOfWork;
using BoneVisQA.Services.Interfaces.Expert;
using BoneVisQA.Services.Models.Expert;
using BoneVisQA.Services.Models.Student;
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

        public MedicalCaseService(IUnitOfWork unitOfWork)
        {
            _unitOfWork = unitOfWork;
        }

        public async Task<MedicalCaseDTO> CreateMedicalCaseAsync(CreateMedicalCaseDTO dto) 
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
                MedicalImages = dto.Images?.Select(img => new MedicalImage
                {
                    Id = Guid.NewGuid(),
                    ImageUrl = img.ImageUrl,
                    Modality = img.Modality,
                    CreatedAt = DateTime.UtcNow,
                    CaseAnnotations = img.Annotations?.Select(a => new CaseAnnotation
                    {
                        Id = Guid.NewGuid(),
                        Label = a.Label,
                        Coordinates = a.Coordinates,
                        CreatedAt = DateTime.UtcNow
                    }).ToList() ?? new List<CaseAnnotation>()
                }).ToList() ?? new List<MedicalImage>()
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
                Images = medicalCase.MedicalImages.Select(img => new MedicalImageDTO 
                {
                    Id = img.Id,
                    ImageUrl = img.ImageUrl,
                    Modality = img.Modality,
                    Annotations = img.CaseAnnotations.Select(a => new AnnotationDTO 
                    {
                        Id = a.Id,
                        Label = a.Label,
                        Coordinates = a.Coordinates
                    }).ToList()
                }).ToList()
            };
        }
        // Thêm image cho case
        public async Task<MedicalImageDTO> AddImageAsync(AddMedicalImageDTO dto)
        {
            var medicalCase = await _unitOfWork.MedicalCaseRepository.GetByIdAsync(dto.CaseId)
                ?? throw new KeyNotFoundException("Không tìm thấy ca bệnh.");

            var image = new MedicalImage
            {
                Id = Guid.NewGuid(),
                CaseId = dto.CaseId,
                ImageUrl = dto.ImageUrl,
                Modality = dto.Modality,
                CreatedAt = DateTime.UtcNow
            };

            await _unitOfWork.MedicalImageRepository.AddAsync(image);
            await _unitOfWork.SaveAsync();

            return new MedicalImageDTO
            {
                Id = image.Id,
                ImageUrl = image.ImageUrl,
                Modality = image.Modality,
                Annotations = new List<AnnotationDTO>()
            };
        }

        // Thêm annotation cho image
        public async Task<AnnotationDTO> AddAnnotationAsync(AddAnnotationDTO dto)
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

            return new AnnotationDTO
            {
                Id = annotation.Id,
                Label = annotation.Label,
                Coordinates = annotation.Coordinates
            };
        }
    }
}
