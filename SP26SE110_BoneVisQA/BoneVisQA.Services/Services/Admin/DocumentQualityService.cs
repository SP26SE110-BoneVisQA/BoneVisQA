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

        // ── Helper: build DocumentQualityDTO từ Document ────
        private async Task<DocumentQualityDTO> BuildDTOAsync(Document doc)
        {
            var chunks = await _unitOfWork.DocumentChunkRepository
                .FindAsync(c => c.DocId == doc.Id);
            var chunkIds = chunks.Select(c => c.Id).ToHashSet();

            var citations = await _unitOfWork.CitationRepository
                .FindAsync(c => chunkIds.Contains(c.ChunkId));
            int citationCount = citations.Count;

            // Đếm NegativeReview qua citations → case_answers → expert_reviews
            var answerIds = citations.Select(c => c.AnswerId).Distinct().ToHashSet();

            var negativeReviews = await _unitOfWork.ExpertReviewRepository
                .FindAsync(r => answerIds.Contains(r.AnswerId) && r.Action == "Reject");
            int negativeReviewCount = negativeReviews.Count;

            bool requiresReview = negativeReviewCount > 0 || doc.IsOutdated;

            return new DocumentQualityDTO
            {
                DocumentId = doc.Id,
                Title = doc.Title,
                FilePath = doc.FilePath,
                CreatedAt = doc.CreatedAt,
                Version = doc.Version,
                CitationCount = citationCount,
                NegativeReviewCount = negativeReviewCount,
                IsOutdated = doc.IsOutdated,
                RequiresReview = requiresReview
            };
        }

        // ── Top tài liệu được trích dẫn nhiều nhất ──────────
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

        // ── Tài liệu có expert review tiêu cực ──────────────
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

        // ── Tài liệu lỗi thời ───────────────────────────────
        public async Task<List<DocumentQualityDTO>> GetOutdatedDocumentsAsync(int yearsThreshold = 2)
        {
            var cutoff = DateTime.UtcNow.AddYears(-yearsThreshold);
            var outdatedDocs = await _unitOfWork.DocumentRepository
                .FindAsync(d => d.IsOutdated ||
                           (d.CreatedAt.HasValue && d.CreatedAt.Value < cutoff));

            var dtos = new List<DocumentQualityDTO>();
            foreach (var doc in outdatedDocs)
                dtos.Add(await BuildDTOAsync(doc));

            return dtos.OrderBy(d => d.CreatedAt).ToList();
        }

        public async Task<List<DocumentQualityDTO>> GetDocumentsFlaggedForReviewAsync()
        {
            var allDocs = await _unitOfWork.DocumentRepository.GetAllAsync();
            var dtos = new List<DocumentQualityDTO>();

            foreach (var doc in allDocs)
            {
                var dto = await BuildDTOAsync(doc);
                if (dto.RequiresReview)
                    dtos.Add(dto);
            }

            return dtos.OrderByDescending(d => d.NegativeReviewCount)
                       .ThenBy(d => d.CreatedAt)
                       .ToList();
        }
    }
}
