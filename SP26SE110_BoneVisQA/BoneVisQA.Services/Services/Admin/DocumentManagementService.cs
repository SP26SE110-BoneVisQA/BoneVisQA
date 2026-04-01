using BoneVisQA.Repositories.Models;
using BoneVisQA.Repositories.UnitOfWork;
using BoneVisQA.Services.Interfaces;
using BoneVisQA.Services.Models.Admin;
using Microsoft.AspNetCore.Http;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using BoneVisQA.Services.Interfaces.Admin;

namespace BoneVisQA.Services.Services.Admin
{
    public class DocumentManagementService : IDocumentManagementService
    {
        private readonly IUnitOfWork _unitOfWork;
        private readonly ISupabaseStorageService _storageService;

        public DocumentManagementService(IUnitOfWork unitOfWork, ISupabaseStorageService storageService)
        {
            _unitOfWork = unitOfWork;
            _storageService = storageService;
        }

        // ── Helper: upload file directly to Supabase (no local disk writes) ─────────────────────────
        private async Task<string> SaveFileAsync(IFormFile file)
        {
            // Avoid FileSystemWatcher/Hot Reload restarts caused by writing into project directories.
            return await _storageService.UploadFileAsync(file, "knowledge_base", "documents/admin");
        }

        // ── Helper: map Entity → DTO ─────────────────────────
        private async Task<DocumentDTO> MapToDTOAsync(Document doc)
        {

            var docTags = await _unitOfWork.DocumentTagRepository
                .FindIncludeAsync(dt => dt.DocumentId == doc.Id, dt => dt.Tag);

            var category = doc.CategoryId.HasValue
                ? await _unitOfWork.CategoryRepository.GetByIdAsync(doc.CategoryId.Value)
                : null;

            return new DocumentDTO
            {
                Id = doc.Id,
                Title = doc.Title,
                FilePath = doc.FilePath,
                Version = doc.Version,
                IsOutdated = doc.IsOutdated,
                CreatedAt = doc.CreatedAt,
                IndexingStatus = NormalizeApiStatus(doc.IndexingStatus),
                CategoryId = doc.CategoryId,
                CategoryName = category?.Name,
                TagNames = docTags.Select(dt => dt.Tag.Name).ToList(),
            };
        }

        private static string NormalizeApiStatus(string? status)
        {
            return string.Equals(status, "Completed", StringComparison.OrdinalIgnoreCase)
                ? "Completed"
                : "Failed";
        }
        // ── UpdateTagsAsync: sync toàn bộ tags ──────────────
        public async Task<DocumentDTO> UpdateTagsAsync(Guid documentId, List<Guid> tagIds)
        {
            var doc = await _unitOfWork.DocumentRepository.GetByIdAsync(documentId)
                ?? throw new KeyNotFoundException("Không tìm thấy tài liệu.");

            // Sync tags trực tiếp, không cần helper riêng
            var existing = await _unitOfWork.DocumentTagRepository
                .FindAsync(dt => dt.DocumentId == documentId);

            var toRemove = existing
                .Where(dt => !tagIds.Contains(dt.TagId))
                .ToList();
            await _unitOfWork.DocumentTagRepository.RemoveRangeAsync(toRemove);

            var existingTagIds = existing.Select(dt => dt.TagId).ToHashSet();
            var toAdd = tagIds
                .Where(id => !existingTagIds.Contains(id))
                .Select(id => new DocumentTag
                {
                    DocumentId = documentId,
                    TagId = id,
                    CreatedAt = DateTime.UtcNow
                }).ToList();
            await _unitOfWork.DocumentTagRepository.AddRangeAsync(toAdd);

            await _unitOfWork.SaveAsync();
            return await MapToDTOAsync(doc);
        }

        // ── ChangeCategoryAsync ──────────────────────────────
        public async Task<DocumentDTO> ChangeCategoryAsync(Guid documentId, Guid categoryId)
        {
            var doc = await _unitOfWork.DocumentRepository.GetByIdAsync(documentId)
                ?? throw new KeyNotFoundException("Không tìm thấy tài liệu.");

            var category = await _unitOfWork.CategoryRepository.GetByIdAsync(categoryId)
                ?? throw new KeyNotFoundException("Không tìm thấy danh mục.");

            doc.CategoryId = categoryId;
            await _unitOfWork.DocumentRepository.UpdateAsync(doc);
            await _unitOfWork.SaveAsync();

            return await MapToDTOAsync(doc);
        }

        // ── UploadNewVersionAsync: tăng version ──────────────
        public async Task<DocumentDTO> UploadNewVersionAsync(Guid documentId)
        {
            var doc = await _unitOfWork.DocumentRepository.GetByIdAsync(documentId)
                ?? throw new KeyNotFoundException("Không tìm thấy tài liệu.");

            doc.Version += 1;
            doc.IsOutdated = false;

            await _unitOfWork.DocumentRepository.UpdateAsync(doc);
            await _unitOfWork.SaveAsync();

            return await MapToDTOAsync(doc);
        }

        // ── MarkOutdatedAsync ────────────────────────────────
        public async Task<DocumentDTO> MarkOutdatedAsync(Guid documentId, bool isOutdated)
        {
            var doc = await _unitOfWork.DocumentRepository.GetByIdAsync(documentId)
                ?? throw new KeyNotFoundException("Không tìm thấy tài liệu.");

            doc.IsOutdated = isOutdated;
            await _unitOfWork.DocumentRepository.UpdateAsync(doc);
            await _unitOfWork.SaveAsync();

            return await MapToDTOAsync(doc);
        }
    }
}
