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
        Task<bool> AddTagCasesAsync(CaseTagDTO dto);
    }
}
