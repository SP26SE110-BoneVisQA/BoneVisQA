using BoneVisQA.Repositories.Models;
using BoneVisQA.Repositories.UnitOfWork;
using BoneVisQA.Services.Interfaces.Expert;
using BoneVisQA.Services.Models.Expert;
using Microsoft.EntityFrameworkCore;
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
        public async Task<PagedResult<GetTagDTO>> GetAllTagAsync(int pageIndex, int pageSize)
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
        //public async Task<UpdateTagCaseDTO> UpdateTagCaseAsync(UpdateTagCaseDTO dto)
        //{
        //    var tagCase = await _unitOfWork.Context.CaseTags
        //        .FirstOrDefaultAsync(x =>
        //            x.CaseId == dto.CaseId &&
        //            x.TagId == dto.OldTagId);

        //    if (tagCase == null)
        //    {
        //        throw new Exception("TagCase not found");
        //    }

        //    // Update tag
        //    tagCase.TagId = dto.NewTagId;

        //    _unitOfWork.Context.CaseTags.Update(tagCase);

        //    await _unitOfWork.SaveAsync();

        //    return new UpdateTagCaseDTO
        //    {
        //        CaseId = tagCase.CaseId,
        //        OldTagId = dto.OldTagId,
        //        NewTagId = tagCase.TagId
        //    };
        //}

        public async Task<bool> DeleteCaseTagAsync(Guid caseId, Guid tagId)
        {
            var caseTag = await _unitOfWork.Context.CaseTags
                .FirstOrDefaultAsync(x =>
                    x.CaseId == caseId &&
                    x.TagId == tagId);

            if (caseTag == null)
            {
                throw new Exception("CaseTag not found");
            }

            _unitOfWork.Context.CaseTags.Remove(caseTag);

            await _unitOfWork.SaveAsync();

            return true;
        }
        public async Task<CaseTagDTOResponse> AddTagCasesAsync(CaseTagDTO dto)
        {
            var medicalCase = await _unitOfWork.MedicalCaseRepository.GetByIdAsync(dto.MedicalCaseId)
               ?? throw new KeyNotFoundException("Medical case not found.");

            var tageCase = await _unitOfWork.TagRepository.GetByIdAsync(dto.TagId)
               ?? throw new KeyNotFoundException("Tag not found.");


            // Check case tồn tại
            var caseExists = await _unitOfWork.MedicalCaseRepository
                .ExistsAsync(x => x.Id == dto.MedicalCaseId);
            if (!caseExists)
                throw new KeyNotFoundException("Medical case not found.");

            // Check tag tồn tại
            var tagExists = await _unitOfWork.TagRepository
                .ExistsAsync(x => x.Id == dto.TagId);
            if (!tagExists)
                throw new KeyNotFoundException("Tag not found.");

            // Check đã gắn chưa
            var exists = await _unitOfWork.CaseTagRepository
                .ExistsAsync(x => x.CaseId == dto.MedicalCaseId && x.TagId == dto.TagId);
            if (exists)
                throw new InvalidOperationException("This tag has already been assigned to this case.");

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
