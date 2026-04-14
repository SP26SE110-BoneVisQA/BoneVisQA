using BoneVisQA.Services.Models.Expert;
using BoneVisQA.Services.Models.Student;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace BoneVisQA.Services.Interfaces.Expert
{
    public interface IMedicalCaseService
    {
        Task<PagedResult<GetMedicalCaseDTO>> GetAllMedicalCasesAsync(int pageIndex, int pageSize);
        Task<GetExpertMedicalCaseDetailDto?> GetMedicalCaseByIdAsync(Guid id);
        Task<CreateMedicalCaseResponseDTO> CreateMedicalCaseAsync(CreateMedicalCaseRequestDTO dto);
        Task<CreateMedicalCaseResponseDTO> CreateMedicalCaseWithImagesJsonAsync(
            CreateExpertMedicalCaseJsonRequest request,
            Guid expertUserId,
            CancellationToken cancellationToken = default);
        Task<UpdateMedicalCaseResponseDTO?> UpdateMedicalCaseAsync(Guid id,UpdateMedicalCaseDTORequest request);
        Task<bool>DeleteMedicalCaseAsync(Guid medicalCaseId);


        Task<PagedResult<GetAllImageDTO>> GetAllImage(int pageIndex, int pageSize);
        Task<AddMedicalImageDTO> AddImageAsync(AddMedicalImageDTOResponse dto);

        Task<PagedResult<GetAllAnnotationDTO>> GetAllAnnotation(int pageIndex, int pageSize);
        Task<AddAnnotationDTO> AddAnnotationAsync(AddAnnotationDTOResponse dto);
       

        Task<PagedResult<GetCategoryDTO>> GetAllCategory(int pageIndex, int pageSize);





    }
}
