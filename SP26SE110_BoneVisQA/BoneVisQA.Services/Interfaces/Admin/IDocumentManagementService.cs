using BoneVisQA.Services.Models.Admin;
using Microsoft.AspNetCore.Http;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BoneVisQA.Services.Interfaces.Admin
{
    public interface IDocumentManagementService
    {
        // ── Tổ chức chủ đề & thẻ ────────────────────────────
        Task<DocumentDTO> UpdateTagsAsync(Guid documentId, List<Guid> tagIds);
        Task<DocumentDTO> ChangeCategoryAsync(Guid documentId, Guid categoryId);

        // ── Quản lý phiên bản & lỗi thời ────────────────────
        Task<DocumentDTO> UploadNewVersionAsync(Guid documentId);
        Task<DocumentDTO> MarkOutdatedAsync(Guid documentId, bool isOutdated);

        // ── Danh sách categories & tags ──────────────────────
        Task<List<CategoryDto>> GetCategoriesAsync();
        Task<List<TagDto>> GetTagsAsync();
    }

    public class CategoryDto
    {
        public Guid Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string? Description { get; set; }
    }

    public class TagDto
    {
        public Guid Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string Type { get; set; } = string.Empty;
    }
}
