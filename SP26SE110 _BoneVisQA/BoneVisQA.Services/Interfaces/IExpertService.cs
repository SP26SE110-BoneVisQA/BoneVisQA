using BoneVisQA.Services.Models.Expert;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BoneVisQA.Services.Interfaces
{
    public interface IExpertService
    {
        Task<Guid> CreateMedicalCaseAsync(CreateMedicalCaseDTO dto);
    }
}
