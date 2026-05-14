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
        public string? CaseTitle { get; set; }
        public string? TagName { get; set; }
        public DateTime? CreatedAt { get; set; }
    }

    public class UpdateTagCaseDTO
    {
        public Guid CaseId { get; set; }

        public Guid OldTagId { get; set; }

        public Guid NewTagId { get; set; }
    }
}
