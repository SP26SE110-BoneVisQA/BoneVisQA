using BoneVisQA.Repositories.Models;
using Microsoft.AspNetCore.Http;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BoneVisQA.Services.Models.Admin
{
    public class DocumentQualityDTO
    {
        public Guid DocumentId { get; set; }
        public string Title { get; set; } = null!;
        public string? FilePath { get; set; }
        public DateTime? CreatedAt { get; set; }
        public string? Version { get; set; }
        public int CitationCount { get; set; }
        public int NegativeReviewCount { get; set; }
        public bool IsOutdated { get; set; }
        public bool RequiresReview { get; set; }
    }
}

