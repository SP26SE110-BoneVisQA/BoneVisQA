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

        public async Task<MedicalCaseDTO> CreateMedicalCaseAsync(MedicalCaseDTO dto)
        {
            var medicalCase = new MedicalCase
            {
                Id = Guid.NewGuid(),
                Title = dto.Title,
                Description = dto.Description,
                Difficulty = dto.Difficulty,
                CategoryId = dto.CategoryId,
                IsApproved = dto.IsApproved ?? false,
                IsActive = dto.IsActive ?? true,
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

            // Map sang DTO trả về
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
                Images = medicalCase.MedicalImages.Select(img => new CreateMedicalImageDTO
                {
                    ImageUrl = img.ImageUrl,
                    Modality = img.Modality,
                    Annotations = img.CaseAnnotations.Select(a => new CreateAnnotationDTO
                    {
                        Label = a.Label,
                        Coordinates = a.Coordinates
                    }).ToList()
                }).ToList()
            };
        }
    }
}
