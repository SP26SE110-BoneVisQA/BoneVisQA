using BoneVisQA.Repositories.Models;
using BoneVisQA.Repositories.UnitOfWork;
using BoneVisQA.Services.Models.Admin;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Hosting;
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
        private readonly IWebHostEnvironment _env;

        public DocumentManagementService(IUnitOfWork unitOfWork, IWebHostEnvironment env)
        {
            _unitOfWork = unitOfWork;
            _env = env;
        }

        // ── Helper: lưu file vật lý ─────────────────────────
        private async Task<string> SaveFileAsync(IFormFile file)
        {
            var uploadFolder = Path.Combine(_env.WebRootPath, "uploads", "documents");
            if (!Directory.Exists(uploadFolder))
                Directory.CreateDirectory(uploadFolder);

            var fileName = $"{Guid.NewGuid()}_{file.FileName}";
            var filePath = Path.Combine(uploadFolder, fileName);

            using var stream = new FileStream(filePath, FileMode.Create);
            await file.CopyToAsync(stream);

            return $"/uploads/documents/{fileName}";
        }

        // ── Helper: map Entity → DTO ─────────────────────────
        private async Task<DocumentDTO> MapToDTOAsync(Document doc)
        {
            var chunks = await _unitOfWork.DocumentChunkRepository
                .FindAsync(c => c.DocId == doc.Id);

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
                CategoryId = doc.CategoryId,
                CategoryName = category?.Name,
                TagNames = docTags.Select(dt => dt.Tag.Name).ToList(),
                ChunkCount = chunks.Count
            };
        }

        // ── Helper: sync tags ────────────────────────────────
        private async Task SyncTagsAsync(Guid documentId, List<Guid> newTagIds)
        {
            var existing = await _unitOfWork.DocumentTagRepository
                .FindAsync(dt => dt.DocumentId == documentId);

            // Xóa tags cũ không còn trong danh sách mới
            var toRemove = existing
                .Where(dt => !newTagIds.Contains(dt.TagId))
                .ToList();
            await _unitOfWork.DocumentTagRepository.RemoveRangeAsync(toRemove);

            // Thêm tags mới chưa có
            var existingTagIds = existing.Select(dt => dt.TagId).ToHashSet();
            var toAdd = newTagIds
                .Where(id => !existingTagIds.Contains(id))
                .Select(id => new DocumentTag
                {
                    DocumentId = documentId,
                    TagId = id,
                    CreatedAt = DateTime.UtcNow
                }).ToList();
            await _unitOfWork.DocumentTagRepository.AddRangeAsync(toAdd);
        }

        // ── UploadDocumentAsync: tạo mới hoặc cập nhật ──────
        public async Task<DocumentDTO> UploadDocumentAsync(SaveDocumentDTO dto)
        {
            await _unitOfWork.BeginTransactionAsync();
            try
            {
                Document doc;

                if (dto.Id == null)
                {
                    // Tạo mới
                    doc = new Document
                    {
                        Id = Guid.NewGuid(),
                        Title = dto.Title,
                        CategoryId = dto.CategoryId,
                        Version = 1,
                        IsOutdated = false,
                        CreatedAt = DateTime.UtcNow
                    };

                    if (dto.File != null)
                        doc.FilePath = await SaveFileAsync(dto.File);

                    await _unitOfWork.DocumentRepository.AddAsync(doc);
                }
                else
                {
                    // Cập nhật
                    doc = await _unitOfWork.DocumentRepository.GetByIdAsync(dto.Id.Value)
                        ?? throw new KeyNotFoundException("Không tìm thấy tài liệu.");

                    doc.Title = dto.Title;
                    doc.CategoryId = dto.CategoryId;

                    if (dto.File != null)
                        doc.FilePath = await SaveFileAsync(dto.File);

                    await _unitOfWork.DocumentRepository.UpdateAsync(doc);
                }

                await _unitOfWork.SaveAsync();
                await _unitOfWork.CommitTransactionAsync();

                return await MapToDTOAsync(doc);
            }
            catch
            {
                await _unitOfWork.RollbackTransactionAsync();
                throw;
            }
        }

        // ── UpdateTagsAsync: sync toàn bộ tags ──────────────
        public async Task UpdateTagsAsync(Guid documentId, List<Guid> tagIds)
        {
            var doc = await _unitOfWork.DocumentRepository.GetByIdAsync(documentId)
                ?? throw new KeyNotFoundException("Không tìm thấy tài liệu.");

            await SyncTagsAsync(documentId, tagIds);
            await _unitOfWork.SaveAsync();
        }

        // ── ChangeCategoryAsync ──────────────────────────────
        public async Task ChangeCategoryAsync(Guid documentId, Guid categoryId)
        {
            var doc = await _unitOfWork.DocumentRepository.GetByIdAsync(documentId)
                ?? throw new KeyNotFoundException("Không tìm thấy tài liệu.");

            var category = await _unitOfWork.CategoryRepository.GetByIdAsync(categoryId)
                ?? throw new KeyNotFoundException("Không tìm thấy danh mục.");

            doc.CategoryId = categoryId;
            await _unitOfWork.DocumentRepository.UpdateAsync(doc);
            await _unitOfWork.SaveAsync();
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
        public async Task MarkOutdatedAsync(Guid documentId, bool isOutdated)
        {
            var doc = await _unitOfWork.DocumentRepository.GetByIdAsync(documentId)
                ?? throw new KeyNotFoundException("Không tìm thấy tài liệu.");

            doc.IsOutdated = isOutdated;
            await _unitOfWork.DocumentRepository.UpdateAsync(doc);
            await _unitOfWork.SaveAsync();
        }
    }
}
