using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BoneVisQA.Services.Models.Expert
{
    public class StudentQuizAnswerDTO
    {
        public Guid Id { get; set; }

        public Guid AttemptId { get; set; }

        public Guid QuestionId { get; set; }

        public string StudentAnswer { get; set; } = null!;

        public bool? IsCorrect { get; set; }
    }
}
