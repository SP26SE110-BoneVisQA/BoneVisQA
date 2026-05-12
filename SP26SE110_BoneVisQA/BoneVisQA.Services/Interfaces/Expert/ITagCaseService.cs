using BoneVisQA.Services.Models.Expert;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BoneVisQA.Services.Interfaces.Expert
{
    public interface ITagCaseService
    {
        Task<PagedResult<GetTagDTO>> GetAllTagAsync(int pageIndex, int pageSize);
        Task<CaseTagDTOResponse> AddTagCasesAsync(CaseTagDTO dto);
    }
}
