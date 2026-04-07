using BoneVisQA.Repositories.Models;
using Microsoft.AspNetCore.Http;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BoneVisQA.Services.Models.Expert
{
    public class MedicalCaseDTORequest
    {
        public string Title { get; set; } = null!;
        public Guid? CreatedByExpertId { get; set; }
        public string Description { get; set; } = null!;
        public string? Difficulty { get; set; }
        public bool? IsApproved { get; set; }
        public bool? IsActive { get; set; }
        public Guid? CategoryId { get; set; }
        public string? SuggestedDiagnosis { get; set; }
        public string? ReflectiveQuestions { get; set; }
        public string? KeyFindings { get; set; }
        public DateTime? CreatedAt { get; set; }
    }
    public class MedicalCaseDTO
    {
        public Guid Id { get; set; }
        public string? ExpertName { get; set; }
        public string Title { get; set; } = null!;
        public string Description { get; set; } = null!;
        public string? Difficulty { get; set; }
        public Guid? CategoryId { get; set; }
        public bool? IsApproved { get; set; }
        public bool? IsActive { get; set; }
        public string? SuggestedDiagnosis { get; set; }
        public string? ReflectiveQuestions { get; set; }
        public string? KeyFindings { get; set; }
        public DateTime? CreatedAt { get; set; }
    }

    //===============================================================================
    public class AddMedicalImageDTO
    {
        public Guid Id { get; set; }
        public string ImageUrl { get; set; } = null!;
        public string? Modality { get; set; }
        public string CaseTitle { get; set; } = null!;
        public List<AddAnnotationDTO> Annotations { get; set; } = new();
    }
    public class AddMedicalImageDTOResponse
    {
        public Guid CaseId { get; set; }
        public IFormFile Image { get; set; } = null!;
        public string? Modality { get; set; }
    }
    public class CreateMedicalImageDTO
    {
        public IFormFile Image { get; set; } = null!;
        public string? Modality { get; set; }
        public List<CreateAnnotationDTO>? Annotations { get; set; }
    }

    //===============================================================================
    public class AddAnnotationDTO
    {
        public Guid Id { get; set; }
        public string Label { get; set; } = null!;
        public string? Coordinates { get; set; }
    }
    public class CreateAnnotationDTO
    {
        public string Label { get; set; } = null!;
        public string? Coordinates { get; set; }
    }
    public class AddAnnotationDTOResponse
    {
        public Guid ImageId { get; set; }
        public string Label { get; set; } = null!;
        public string? Coordinates { get; set; }
    }
}
