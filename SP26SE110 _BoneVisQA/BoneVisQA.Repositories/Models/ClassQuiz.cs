using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BoneVisQA.Repositories.Models
{
    public class ClassQuiz
    {
        public Guid ClassId { get; set; }
        public Guid QuizId { get; set; }
        public DateTime? AssignedAt { get; set; }

        public AcademicClass AcademicClass { get; set; } = null!;
        public Quiz Quiz { get; set; } = null!;
    }

}
