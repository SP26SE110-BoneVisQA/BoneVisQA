using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BoneVisQA.Services.Models.Expert
{
    public class QuizDTO
    {
        public Guid Id { get; set; }
      
        public Guid ClassId { get; set; }
       
        public string Title { get; set; } = null!;

        public DateTime? OpenTime { get; set; }

        public DateTime? CloseTime { get; set; }

        public int? TimeLimit { get; set; }

        public int? PassingScore { get; set; }
       
        public DateTime? CreatedAt { get; set; }
    }
}
