using BoneVisQA.Repositories.Models;
using BoneVisQA.Repositories.UnitOfWork;
using BoneVisQA.Services.Interfaces.Expert;
using BoneVisQA.Services.Models.Expert;
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

        public async Task<Guid> CreateMedicalCaseAsync(CreateMedicalCaseDTO dto)
        {
            var medicalCase = new MedicalCase
            {
                Id = Guid.NewGuid(),
                Title = dto.Title,
                Description = dto.Description,
                Difficulty = dto.Difficulty,
                CategoryId = dto.CategoryId,
                CreatedAt = DateTime.UtcNow,

                MedicalImages = dto.Images.Select(img => new MedicalImage
                {
                    Id = Guid.NewGuid(),
                    ImageUrl = img.ImageUrl,
                    Modality = img.Modality,
                    CreatedAt = DateTime.UtcNow,

                    CaseAnnotations = img.Annotations.Select(a => new CaseAnnotation
                    {
                        Id = Guid.NewGuid(),
                        Label = a.Label,
                        Coordinates = a.Coordinates,
                        CreatedAt = DateTime.UtcNow
                    }).ToList()

                }).ToList()
            };

            await _unitOfWork.MedicalCaseRepository.AddAsync(medicalCase);

            await _unitOfWork.SaveAsync();

            return medicalCase.Id;
        }
    }
}
