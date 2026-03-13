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
        public Guid MedicalCaseId { get; set; }
        public Guid TagId { get; set; }
    }

    public class CaseTagDTOResponse
    {
        public Guid CaseId { get; set; }
        public Guid TagId { get; set; }
        public DateTime? CreatedAt { get; set; }
    }
}
