using Microsoft.AspNetCore.Http;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BoneVisQA.Services.Models.Admin
{
    public class SaveDocumentDTO
    {
        public Guid? Id { get; set; }              
        public string Title { get; set; } = null!;
        public IFormFile? File { get; set; }
        public Guid? CategoryId { get; set; }
    }

    public class DocumentFilterDTO
    {
        public string? Keyword { get; set; }
        public Guid? CategoryId { get; set; }
        public bool? IsOutdated { get; set; }
        public int PageIndex { get; set; } = 1;
        public int PageSize { get; set; } = 10;
    }

    public class DocumentDTO
    {
        public Guid Id { get; set; }
        public string Title { get; set; } = null!;
        public string? FilePath { get; set; }
        public int Version { get; set; }
        public bool IsOutdated { get; set; }
        public DateTime? CreatedAt { get; set; }
        public string? IndexingStatus { get; set; } 
        public Guid? CategoryId { get; set; }
        public string? CategoryName { get; set; }
        public List<string> TagNames { get; set; } = new();  
    }

    public class PagedResultDTO<T>
    {
        public List<T> Items { get; set; } = new();
        public int TotalCount { get; set; }
        public int PageIndex { get; set; }
        public int PageSize { get; set; }
        public int TotalPages => (int)Math.Ceiling((double)TotalCount / PageSize);
    }
    public class UploadNewVersionRequestDTO
    {
        public IFormFile File { get; set; } = null!;
    }
}
