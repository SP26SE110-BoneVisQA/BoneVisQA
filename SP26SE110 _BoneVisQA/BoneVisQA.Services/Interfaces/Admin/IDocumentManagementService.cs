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
        Task<DocumentDTO> UploadDocumentAsync(SaveDocumentDTO dto);   


        // ── Tổ chức chủ đề & thẻ ────────────────────────────
        Task UpdateTagsAsync(Guid documentId, List<Guid> tagIds);
        Task ChangeCategoryAsync(Guid documentId, Guid categoryId);

        // ── Quản lý phiên bản & lỗi thời ────────────────────
        Task<DocumentDTO> UploadNewVersionAsync(Guid documentId);
        Task MarkOutdatedAsync(Guid documentId, bool isOutdated);
    }
}
