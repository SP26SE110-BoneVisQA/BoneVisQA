using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BoneVisQA.Services.Models.Expert
{
    public class QuizQuestionDTO
    {
        public Guid Id { get; set; }

        public Guid QuizId { get; set; }

        public Guid? CaseId { get; set; }

        public string QuestionText { get; set; } = null!;

        public string? Type { get; set; }

        public string? CorrectAnswer { get; set; }
    }
}
