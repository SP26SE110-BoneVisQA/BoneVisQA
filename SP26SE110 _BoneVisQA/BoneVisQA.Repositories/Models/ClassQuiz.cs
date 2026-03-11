using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BoneVisQA.Repositories.Models
{
    [Table("class_quizzes")]
    public class ClassQuiz
    {
        [Column("class_id")]
        public Guid ClassId { get; set; }

        [Column("quiz_id")]
        public Guid QuizId { get; set; }

        [Column("assigned_at")]
        public DateTime? AssignedAt { get; set; }

        [ForeignKey("ClassId")]
        public virtual AcademicClass AcademicClass { get; set; } = null!;

        [ForeignKey("QuizId")]
        public virtual Quiz Quiz { get; set; } = null!;
    }
}
