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
    public class TagCaseService : ITagCaseService
    {
        public readonly IUnitOfWork _unitOfWork;

        public TagCaseService(IUnitOfWork unitOfWork)
        {
            _unitOfWork = unitOfWork;
        }
        public async Task<bool> AddTagCasesAsync(CaseTagDTO dto)
        {
            var tagIds = new List<Guid>();

            // gom tất cả tag lại
            if (dto.SpecialtyTagIds != null)
                tagIds.AddRange(dto.SpecialtyTagIds);

            if (dto.BoneLocationTagIds != null)
                tagIds.AddRange(dto.BoneLocationTagIds);

            if (dto.LesionTypeTagIds != null)
                tagIds.AddRange(dto.LesionTypeTagIds);

            if (dto.DifficultyTagId.HasValue)
                tagIds.Add(dto.DifficultyTagId.Value);

            // check case tồn tại
            var caseExists = await _unitOfWork.MedicalCaseRepository
                .ExistsAsync(x => x.Id == dto.CaseId);

            if (!caseExists)
                throw new Exception("Medical case not found");

            foreach (var tagId in tagIds)
            {
                // check tag tồn tại
                var tagExists = await _unitOfWork.TagRepository
                    .ExistsAsync(x => x.Id == tagId);

                if (!tagExists)
                    continue;

                // check đã gắn chưa
                var exists = await _unitOfWork.CaseTagRepository
                    .ExistsAsync(x => x.CaseId == dto.CaseId && x.TagId == tagId);

                if (exists)
                    continue;

                var caseTag = new CaseTag
                {
                    CaseId = dto.CaseId,
                    TagId = tagId,
                    CreatedAt = DateTime.UtcNow
                };

                await _unitOfWork.CaseTagRepository.AddAsync(caseTag);
            }

            await _unitOfWork.SaveAsync();

            return true;
        }
    }
}
