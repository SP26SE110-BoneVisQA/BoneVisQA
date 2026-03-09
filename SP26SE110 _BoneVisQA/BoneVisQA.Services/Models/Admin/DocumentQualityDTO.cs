using BoneVisQA.Repositories.Models;
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

        public class DocumentDTO
        {
            public Guid Id { get; set; }

            public string Title { get; set; } = null!;

            public string? FilePath { get; set; }

            public Guid? CategoryId { get; set; }

            public DateTime? CreatedAt { get; set; }
        }

        public class DocumentChunkDTO
        {
            public Guid Id { get; set; }

            public Guid DocId { get; set; }

            public string Content { get; set; } = null!;

            public int ChunkOrder { get; set; }
        }

        public class CitationDTO
        {
            public Guid Id { get; set; }

            public Guid AnswerId { get; set; }

            public Guid ChunkId { get; set; }

            public double SimilarityScore { get; set; }

        }

        public class StudentQuestionDTO
        {
            public Guid Id { get; set; }

            public Guid StudentId { get; set; }

            public Guid CaseId { get; set; }

            public Guid? AnnotationId { get; set; }

            public string QuestionText { get; set; } = null!;

            public string? Language { get; set; }

            public DateTime? CreatedAt { get; set; }
        }

        public class ExpertReviewDTO
        {
            public Guid Id { get; set; }

            public Guid ExpertId { get; set; }

            public Guid AnswerId { get; set; }

            public string? ReviewNote { get; set; }

            public string? Action { get; set; }

            public DateTime? CreatedAt { get; set; }

        }
    }
}

