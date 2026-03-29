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
    public class TagCaseService : ITagCaseService
    {
        public readonly IUnitOfWork _unitOfWork;

        public TagCaseService(IUnitOfWork unitOfWork)
        {
            _unitOfWork = unitOfWork;
        }
        public async Task<CaseTagDTOResponse> AddTagCasesAsync(CaseTagDTO dto)
        {
            var medicalCase = await _unitOfWork.MedicalCaseRepository.GetByIdAsync(dto.MedicalCaseId)
               ?? throw new KeyNotFoundException("Không tìm thấy ca bệnh.");

            var tageCase = await _unitOfWork.TagRepository.GetByIdAsync(dto.TagId)
               ?? throw new KeyNotFoundException("Không tìm thấy tag.");


            // Check case tồn tại
            var caseExists = await _unitOfWork.MedicalCaseRepository
                .ExistsAsync(x => x.Id == dto.MedicalCaseId);
            if (!caseExists)
                throw new KeyNotFoundException("Không tìm thấy medical case.");

            // Check tag tồn tại
            var tagExists = await _unitOfWork.TagRepository
                .ExistsAsync(x => x.Id == dto.TagId);
            if (!tagExists)
                throw new KeyNotFoundException("Không tìm thấy tag.");

            // Check đã gắn chưa
            var exists = await _unitOfWork.CaseTagRepository
                .ExistsAsync(x => x.CaseId == dto.MedicalCaseId && x.TagId == dto.TagId);
            if (exists)
                throw new InvalidOperationException("Tag đã được gắn vào case này rồi.");

            var caseTag = new CaseTag
            {
                CaseId = dto.MedicalCaseId,
                TagId = dto.TagId,
                CreatedAt = DateTime.UtcNow
            };

            await _unitOfWork.CaseTagRepository.AddAsync(caseTag);
            await _unitOfWork.SaveAsync();

            return new CaseTagDTOResponse
            {
                CaseId = caseTag.CaseId,
                CaseTitle = medicalCase.Title,
                TagId = caseTag.TagId,
                TagName = tageCase.Name,
                CreatedAt = caseTag.CreatedAt
            };
        }
    }
}
