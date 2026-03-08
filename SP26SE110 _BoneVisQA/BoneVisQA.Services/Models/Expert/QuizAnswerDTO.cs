using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BoneVisQA.Services.Models.Expert
{
    public class QuizAnswerDTO
    {
        public Guid Id { get; set; }

        public Guid QuestionId { get; set; }

        public string? AnswerText { get; set; }

        public string? StructuredDiagnosis { get; set; }

        public string? DifferentialDiagnoses { get; set; }

        public string? Status { get; set; }

        public string? ReviewedById { get; set; }
       
        public DateTime? GeneratedAt { get; set; }

        public DateTime? ReviewedAt { get; set; }
    }
}
