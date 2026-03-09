using BoneVisQA.Services.Models.Admin;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BoneVisQA.Services.Interfaces
{
    public interface IDocumentQualityService
    {
        Task<List<DocumentQualityDTO>> GetMostReferencedDocumentsAsync(int top = 10);

        Task<List<DocumentQualityDTO>> GetDocumentsWithNegativeExpertReviewsAsync();

        Task<List<DocumentQualityDTO>> GetDocumentsWithHighStudentQuestionRateAsync(int minQuestionCount);

        Task<List<DocumentQualityDTO>> GetOutdatedDocumentsAsync(int yearsThreshold = 2);
        Task<List<DocumentQualityDTO>> GetDocumentsRequireReviewAsync();
    }
}
