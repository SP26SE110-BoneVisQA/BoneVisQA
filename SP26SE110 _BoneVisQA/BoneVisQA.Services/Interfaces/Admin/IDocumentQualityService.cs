using BoneVisQA.Services.Models.Admin;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BoneVisQA.Services.Interfaces.Admin
{
    public interface IDocumentQualityService
    {
        Task<List<DocumentQualityDTO>> GetMostReferencedDocumentsAsync(int top = 10);
        Task<List<DocumentQualityDTO>> GetDocumentsWithNegativeExpertReviewsAsync();
        Task<List<DocumentQualityDTO>> GetOutdatedDocumentsAsync(int yearsThreshold = 2);
    }
}
