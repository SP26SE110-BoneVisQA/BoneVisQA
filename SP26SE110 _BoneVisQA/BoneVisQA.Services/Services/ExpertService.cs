using BoneVisQA.Repositories.Models;
using BoneVisQA.Repositories.UnitOfWork;
using BoneVisQA.Services.Interfaces;
using BoneVisQA.Services.Models.Expert;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BoneVisQA.Services.Services
{
    public class ExpertService : IExpertService
    {
        private readonly IUnitOfWork _unitOfWork;

        public ExpertService(IUnitOfWork unitOfWork)
        {
            _unitOfWork = unitOfWork;
        }

        public async Task<Guid> CreateMedicalCaseAsync(CreateMedicalCaseDTO dto)
        {
            var caseId = Guid.NewGuid();

            var medicalCase = new MedicalCase
            {
                Id = caseId,
                Title = dto.Title,
                Description = dto.Description,
                Difficulty = dto.Difficulty,
                CategoryId = dto.CategoryId,
                CreatedAt = DateTime.UtcNow
            };

            _unitOfWork.MedicalCaseRepository.PrepareCreate(medicalCase);

            if (dto.Images != null)
            {
                foreach (var img in dto.Images)
                {
                    var imageId = Guid.NewGuid();

                    var medicalImage = new MedicalImage
                    {
                        Id = imageId,
                        CaseId = caseId,
                        ImageUrl = img.ImageUrl,
                        Modality = img.Modality,
                        CreatedAt = DateTime.UtcNow
                    };

                    _unitOfWork.MedicalImageRepository.PrepareCreate(medicalImage);

                    if (img.Annotations != null)
                    {
                        foreach (var ann in img.Annotations)
                        {
                            var annotation = new CaseAnnotation
                            {
                                Id = Guid.NewGuid(),
                                ImageId = imageId,
                                Label = ann.Label,
                                Coordinates = ann.Coordinates,
                                CreatedAt = DateTime.UtcNow
                            };

                            _unitOfWork.CaseAnnotationRepository.PrepareCreate(annotation);
                        }
                    }
                }
            }

            await _unitOfWork.SaveAsync();

            return caseId;
        }
    }
}
