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
    public class CreateMedicalCaseDTO
    {
        public string Title { get; set; } = null!;

        public string Description { get; set; } = null!;

        public string? Difficulty { get; set; }

        public Guid? CategoryId { get; set; }

        public List<CreateMedicalImageDTO>? Images { get; set; }
    }
    public class CreateMedicalImageDTO
    {
        public string ImageUrl { get; set; } = null!;

        public string? Modality { get; set; }

        public List<CreateAnnotationDTO>? Annotations { get; set; }
    }
    public class CreateAnnotationDTO
    {
        public string Label { get; set; } = null!;

        public string? Coordinates { get; set; }
    }
}
