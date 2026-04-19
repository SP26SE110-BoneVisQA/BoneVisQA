using BoneVisQA.Repositories.Models;
using BoneVisQA.Repositories.UnitOfWork;
using BoneVisQA.Services.Helpers;
using BoneVisQA.Services.Interfaces;
using BoneVisQA.Services.Models.Admin;
using Microsoft.AspNetCore.Http;
using System;
using System.Collections.Generic;
using System.Globalization;
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

        // Helper: upload file directly to Supabase (no local disk writes).
        private async Task<string> SaveFileAsync(IFormFile file)
        {
            // Avoid FileSystemWatcher/Hot Reload restarts caused by writing into project directories.
            return await _storageService.UploadFileAsync(file, "knowledge_base", "documents/admin");
        }

        // Helper: map entity to DTO.
        private async Task<DocumentDTO> MapToDTOAsync(Document doc)
        {

            var docTags = await _unitOfWork.DocumentTagRepository
                .FindIncludeAsync(dt => dt.DocumentId == doc.Id, dt => dt.Tag);
            var categoryName = doc.Category?.Name;
            if (string.IsNullOrWhiteSpace(categoryName) && doc.CategoryId.HasValue)
            {
                var category = await _unitOfWork.CategoryRepository.GetByIdAsync(doc.CategoryId.Value);
                categoryName = category?.Name;
            }

            return new DocumentDTO
            {
                Id = doc.Id,
                Title = doc.Title,
                FilePath = doc.FilePath,
                Version = SemanticDocumentVersion.Normalize(doc.Version),
                IsOutdated = doc.IsOutdated,
                CreatedAt = FormatUtc(doc.CreatedAt),
                UpdatedAt = FormatUtc(doc.UpdatedAt),
                IndexingStatus = NormalizeApiStatus(doc.IndexingStatus),
                CategoryId = doc.CategoryId,
                Category = categoryName,
                TagNames = docTags.Select(dt => dt.Tag.Name).ToList(),
            };
        }

        private static string? FormatUtc(DateTime? dt) =>
            dt.HasValue
                ? dt.Value.ToUniversalTime().ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture)
                : null;

        private static string NormalizeApiStatus(string? status)
        {
            if (string.Equals(status, "Completed", StringComparison.OrdinalIgnoreCase))
                return "Completed";
            if (string.Equals(status, "Failed", StringComparison.OrdinalIgnoreCase))
                return "Failed";
            if (string.Equals(status, "Pending", StringComparison.OrdinalIgnoreCase))
                return "Pending";
            if (string.Equals(status, "Processing", StringComparison.OrdinalIgnoreCase)
                || string.Equals(status, "In Progress", StringComparison.OrdinalIgnoreCase))
                return "Processing";
            return "Failed";
        }
        // Sync all document tags.
        public async Task<DocumentDTO> UpdateTagsAsync(Guid documentId, List<Guid> tagIds)
        {
            var doc = await _unitOfWork.DocumentRepository.GetByIdAsync(documentId)
                ?? throw new KeyNotFoundException("Document not found.");

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

        // Change category.
        public async Task<DocumentDTO> ChangeCategoryAsync(Guid documentId, Guid categoryId)
        {
            var doc = await _unitOfWork.DocumentRepository.GetByIdAsync(documentId)
                ?? throw new KeyNotFoundException("Document not found.");

            var category = await _unitOfWork.CategoryRepository.GetByIdAsync(categoryId)
                ?? throw new KeyNotFoundException("Category not found.");

            doc.CategoryId = categoryId;
            await _unitOfWork.DocumentRepository.UpdateAsync(doc);
            await _unitOfWork.SaveAsync();

            return await MapToDTOAsync(doc);
        }

        // Increase document version.
        public async Task<DocumentDTO> UploadNewVersionAsync(Guid documentId)
        {
            var doc = await _unitOfWork.DocumentRepository.GetByIdAsync(documentId)
                ?? throw new KeyNotFoundException("Document not found.");

            doc.Version = SemanticDocumentVersion.BumpMinor(doc.Version);
            doc.IsOutdated = false;
            doc.UpdatedAt = DateTime.UtcNow;

            await _unitOfWork.DocumentRepository.UpdateAsync(doc);
            await _unitOfWork.SaveAsync();

            return await MapToDTOAsync(doc);
        }

        // Mark document outdated.
        public async Task<DocumentDTO> MarkOutdatedAsync(Guid documentId, bool isOutdated)
        {
            var doc = await _unitOfWork.DocumentRepository.GetByIdAsync(documentId)
                ?? throw new KeyNotFoundException("Document not found.");

            doc.IsOutdated = isOutdated;
            await _unitOfWork.DocumentRepository.UpdateAsync(doc);
            await _unitOfWork.SaveAsync();

            return await MapToDTOAsync(doc);
        }

        // Get category list.
        public async Task<List<CategoryDto>> GetCategoriesAsync()
        {
            var categories = await _unitOfWork.CategoryRepository.GetAllAsync();
            return categories
                .OrderBy(c => c.Name)
                .Select(c => new CategoryDto
                {
                    Id = c.Id,
                    Name = c.Name,
                    Description = c.Description
                }).ToList();
        }

        // Get tag list.
        public async Task<List<TagDto>> GetTagsAsync()
        {
            var tags = await _unitOfWork.TagRepository.GetAllAsync();
            return tags
                .OrderBy(t => t.Name)
                .Select(t => new TagDto
                {
                    Id = t.Id,
                    Name = t.Name,
                    Type = t.Type
                }).ToList();
        }
    }
}
