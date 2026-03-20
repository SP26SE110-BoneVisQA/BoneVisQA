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
    public class MedicalCaseDTO
    {
        public Guid Id { get; set; }
        public string Title { get; set; } = null!;
        public string Description { get; set; } = null!;
        public string? Difficulty { get; set; }
        public Guid? CategoryId { get; set; }
        public bool? IsApproved { get; set; }
        public bool? IsActive { get; set; }
        public string? SuggestedDiagnosis { get; set; }
        public string? KeyFindings { get; set; }
        public DateTime? CreatedAt { get; set; }
        public List<MedicalImageDTO> Images { get; set; } = new(); 
    }

    public class MedicalImageDTO
    {
        public Guid Id { get; set; }                              
        public string ImageUrl { get; set; } = null!;
        public string? Modality { get; set; }
        public List<AnnotationDTO> Annotations { get; set; } = new();
    }

    public class AnnotationDTO
    {
        public Guid Id { get; set; }                             
        public string Label { get; set; } = null!;
        public string? Coordinates { get; set; }
    }
    public class CreateMedicalCaseDTO
    {
        public string Title { get; set; } = null!;
        public string Description { get; set; } = null!;
        public string? Difficulty { get; set; }
        public Guid? CategoryId { get; set; }
        public string? SuggestedDiagnosis { get; set; }
        public string? KeyFindings { get; set; }
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

    public class AddMedicalImageDTO
    {
        public Guid CaseId { get; set; }
        public string ImageUrl { get; set; } = null!;
        public string? Modality { get; set; }
    }

    public class AddAnnotationDTO
    {
        public Guid ImageId { get; set; }
        public string Label { get; set; } = null!;
        public string? Coordinates { get; set; }
    }
}
