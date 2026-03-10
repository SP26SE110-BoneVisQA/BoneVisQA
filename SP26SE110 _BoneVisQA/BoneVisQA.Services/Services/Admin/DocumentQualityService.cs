using BoneVisQA.Repositories.Models;
using BoneVisQA.Repositories.UnitOfWork;
using BoneVisQA.Services.Interfaces.Admin;
using BoneVisQA.Services.Models.Admin;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BoneVisQA.Services.Services.Admin
{
    public class DocumentQualityService : IDocumentQualityService
    {
        public readonly IUnitOfWork _unitOfWork;

        public DocumentQualityService(IUnitOfWork unitOfWork)
        {
            _unitOfWork = unitOfWork;
        }

        private async Task<DocumentQualityDTO> BuildDTOAsync(Document doc)
        {
            // Đếm số lần chunk của doc được cite
            var chunks = await _unitOfWork.DocumentChunkRepository
                .FindAsync(c => c.DocId == doc.Id);
            var chunkIds = chunks.Select(c => c.Id).ToHashSet();

            var allCitations = await _unitOfWork.CitationRepository
                .FindAsync(c => chunkIds.Contains(c.ChunkId));
            int citationCount = allCitations.Count;

           
            
            // Đếm số StudentQuestion liên quan (qua citation -> answer -> question)
            var answerIds = allCitations.Select(c => c.AnswerId).Distinct().ToHashSet();
            var studentQuestions = await _unitOfWork.StudentQuestionRepository.FindAsync(q => answerIds.Contains(q.Id)); // tuỳ mapping
            int studentQuestionCount = studentQuestions.Count;

          
            
            // Đếm ExpertReview tiêu cực (action == "rejected" hoặc tương tự)
            var answers = await _unitOfWork.CaseAnswerRepository
                .FindAsync(a => answerIds.Contains(a.Id));
            var caseAnswerIds = answers.Select(a => a.Id).ToHashSet();
            var negativeReviews = await _unitOfWork.ExpertReviewRepository
                .FindAsync(r => caseAnswerIds.Contains(r.AnswerId)
                             && r.Action == "rejected");
            int negativeReviewCount = negativeReviews.Count;



            // Kiểm tra outdated (tạo cách đây > yearsThreshold năm)
            bool isOutdated = doc.CreatedAt.HasValue &&
                (DateTime.UtcNow - doc.CreatedAt.Value).TotalDays > 2 * 365;

            bool requiresReview = negativeReviewCount > 0 || isOutdated;

           
            
            return new DocumentQualityDTO
            {
                DocumentId = doc.Id,
                Title = doc.Title,
                FilePath = doc.FilePath,
                CreatedAt = doc.CreatedAt,
                CitationCount = citationCount,
                StudentQuestionCount = studentQuestionCount,
                NegativeReviewCount = negativeReviewCount,
                IsOutdated = isOutdated,
                RequiresReview = requiresReview
            };
        }


        // 1. Top tài liệu được truy xuất nhiều nhất
        public async Task<List<DocumentQualityDTO>> GetMostReferencedDocumentsAsync(int top = 10)
        {
            var allDocs = await _unitOfWork.DocumentRepository.GetAllAsync();
            var dtos = new List<DocumentQualityDTO>();
            foreach (var doc in allDocs)
                dtos.Add(await BuildDTOAsync(doc));

            return dtos.OrderByDescending(d => d.CitationCount)
                       .Take(top)
                       .ToList();
        }

        // 2. Tài liệu có expert review tiêu cực
        public async Task<List<DocumentQualityDTO>> GetDocumentsWithNegativeExpertReviewsAsync()
        {
            var allDocs = await _unitOfWork.DocumentRepository.GetAllAsync();
            var dtos = new List<DocumentQualityDTO>();
            foreach (var doc in allDocs)
            {
                var dto = await BuildDTOAsync(doc);
                if (dto.NegativeReviewCount > 0)
                    dtos.Add(dto);
            }
            return dtos.OrderByDescending(d => d.NegativeReviewCount).ToList();
        }

        // 3. Tài liệu có nhiều câu hỏi của sinh viên (có thể nội dung khó hiểu)
        public async Task<List<DocumentQualityDTO>> GetDocumentsWithHighStudentQuestionRateAsync(
            int minQuestionCount)
        {
            var allDocs = await _unitOfWork.DocumentRepository.GetAllAsync();
            var dtos = new List<DocumentQualityDTO>();
            foreach (var doc in allDocs)
            {
                var dto = await BuildDTOAsync(doc);
                if (dto.StudentQuestionCount >= minQuestionCount)
                    dtos.Add(dto);
            }
            return dtos.OrderByDescending(d => d.StudentQuestionCount).ToList();
        }

        // 4. Tài liệu lỗi thời (tạo cách đây > yearsThreshold năm)
        public async Task<List<DocumentQualityDTO>> GetOutdatedDocumentsAsync(
            int yearsThreshold = 2)
        {
            var cutoff = DateTime.UtcNow.AddYears(-yearsThreshold);
            var outdatedDocs = await _unitOfWork.DocumentRepository
                .FindAsync(d => d.CreatedAt.HasValue && d.CreatedAt.Value < cutoff);

            var dtos = new List<DocumentQualityDTO>();
            foreach (var doc in outdatedDocs)
                dtos.Add(await BuildDTOAsync(doc));

            return dtos.OrderBy(d => d.CreatedAt).ToList();
        }

        // 5. Tài liệu cần rà soát (negative review HOẶC outdated)
        public async Task<List<DocumentQualityDTO>> GetDocumentsRequireReviewAsync()
        {
            var allDocs = await _unitOfWork.DocumentRepository.GetAllAsync();
            var dtos = new List<DocumentQualityDTO>();
            foreach (var doc in allDocs)
            {
                var dto = await BuildDTOAsync(doc);
                if (dto.RequiresReview)
                    dtos.Add(dto);
            }
            return dtos
                .OrderByDescending(d => d.NegativeReviewCount)
                .ThenBy(d => d.CreatedAt)
                .ToList();
        }
    }
}
