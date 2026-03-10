using BoneVisQA.Repositories.Models;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BoneVisQA.Services.Models.Expert
{
    public class CaseTagDTO
    {
        public Guid CaseId { get; set; }

        public List<Guid>? SpecialtyTagIds { get; set; }

        public List<Guid>? BoneLocationTagIds { get; set; }

        public List<Guid>? LesionTypeTagIds { get; set; }

        public Guid? DifficultyTagId { get; set; }
    }
}
