using BoneVisQA.Services.Models.Admin;
using BoneVisQA.Services.Models.Expert;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BoneVisQA.Services.Interfaces.Admin
{
    public interface IClassManagementService
    {
        Task<PagedResult<GetClassManagementDTO>> GetAcademicClassAsync(int pageIndex, int pageSize);
        Task<CreateClassManagementDTO>CreateAcademicClassAsync(CreateClassManagementDTO createClassManagementDTO);
        Task<UpdateClassManagementDTO> UpdateAcademicClassAsync(UpdateClassManagementDTO updateClassManagementDTO);  
        Task<bool> DeleteAcademicClassAsync(Guid id);

        //=======================================================  ASSIGN CLASS  ===================================================
        Task<PagedResult<GetAssignClassDTO>> GetAssignClassAsync(int pageIndex, int pageSize);
        Task<AssignClassDTO> AssignClassAsync(AssignClassDTO assignClassDTO);
        Task<AssignClassDTO> UpdateAssignClassAsync(AssignClassDTO assignClassDTO);
        Task<bool> DeleteAssignClassAsync(Guid id);
    }
}
