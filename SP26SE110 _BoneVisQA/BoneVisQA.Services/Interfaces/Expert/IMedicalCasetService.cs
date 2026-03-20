using BoneVisQA.Services.Models.Expert;
using BoneVisQA.Services.Models.Student;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BoneVisQA.Services.Interfaces.Expert
{
    public interface IMedicalCaseService
    {
        Task<MedicalCaseDTO> CreateMedicalCaseAsync(CreateMedicalCaseDTO dto);
        Task<MedicalImageDTO> AddImageAsync(AddMedicalImageDTO dto);
        Task<AnnotationDTO> AddAnnotationAsync(AddAnnotationDTO dto);
    }
}
