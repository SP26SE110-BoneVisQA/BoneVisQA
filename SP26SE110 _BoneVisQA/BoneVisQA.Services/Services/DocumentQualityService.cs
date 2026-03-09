using BoneVisQA.Repositories.UnitOfWork;
using BoneVisQA.Services.Interfaces;
using BoneVisQA.Services.Models.Admin;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BoneVisQA.Services.Services
{
    public class DocumentQualityService : IDocumentQualityService
    {
        public readonly IUnitOfWork unitOfWork;

        public DocumentQualityService(IUnitOfWork unitOfWork)
        {
            this.unitOfWork = unitOfWork;
        }

        //public async Task<List<DocumentQualityDTO>> GetMostReferencedDocumentsAsync(int top = 10)
        //{
        //    var citations = await unitOfWork.CitationRepository.GetAllAsync();

        //    var result = citations
        //        .GroupBy(c => c.DocumentId)
        //        .Select(g => new
        //        {
        //            DocumentId = g.Key,
        //            Count = g.Count()
        //        })
        //        .OrderByDescending(x => x.Count)
        //        .Take(top)
        //        .ToList();

        //    var documents = await unitOfWork.DocumentRepository.GetAllAsync();

        //    return result.Join(documents,
        //        r => r.DocumentId,
        //        d => d.Id,
        //        (r, d) => new DocumentQualityDTO
        //        {
        //            DocumentId = d.Id,
        //            Title = d.Title,
        //            ReferenceCount = r.Count
        //        }).ToList();
        //}
        //public async Task<List<DocumentQualityDTO>> GetDocumentsWithNegativeExpertReviewsAsync()
        //{
        //    var reviews = await unitOfWork.ExpertReviewRepository.FindAsync(r => r.Score < 0);

        //    var documents = await unitOfWork.DocumentRepository.GetAllAsync();

        //    return reviews
        //        .GroupBy(r => r.DocumentId)
        //        .Select(g => new DocumentQualityDTO
        //        {
        //            DocumentId = g.Key,
        //            NegativeReviewCount = g.Count()
        //        }).ToList();
        //}

        //public async Task<List<DocumentQualityDTO>> GetDocumentsWithHighStudentQuestionRateAsync(int minQuestionCount)
        //{
        //    var questions = await unitOfWork.StudentQuestionRepository.GetAllAsync();

        //    return questions
        //        .GroupBy(q => q.DocumentId)
        //        .Where(g => g.Count() >= minQuestionCount)
        //        .Select(g => new DocumentQualityDTO
        //        {
        //            DocumentId = g.Key,
        //            StudentQuestionCount = g.Count()
        //        })
        //        .ToList();
        //}

        //public async Task<List<DocumentQualityDTO>> GetOutdatedDocumentsAsync(int yearsThreshold = 2)
        //{
        //    var thresholdDate = DateTime.Now.AddYears(-yearsThreshold);

        //    var docs = await unitOfWork.DocumentRepository
        //        .FindAsync(d => d.LastUpdated < thresholdDate);

        //    return docs.Select(d => new DocumentQualityDTO
        //    {
        //        DocumentId = d.Id,
        //        Title = d.Title,
        //        LastUpdated = d.LastUpdated
        //    }).ToList();
        //}

        //public async Task<List<DocumentQualityDTO>> GetDocumentsRequireReviewAsync()
        //{
        //    var negative = await GetDocumentsWithNegativeExpertReviewsAsync();
        //    var outdated = await GetOutdatedDocumentsAsync();
        //    var questions = await GetDocumentsWithHighStudentQuestionRateAsync(5);

        //    return negative
        //        .Concat(outdated)
        //        .Concat(questions)
        //        .GroupBy(d => d.DocumentId)
        //        .Select(g => g.First())
        //        .ToList();
        //}







    }
}
