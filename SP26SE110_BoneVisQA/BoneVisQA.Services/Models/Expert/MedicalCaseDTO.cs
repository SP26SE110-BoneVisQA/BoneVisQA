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
    public class GetMedicalCaseDTO
    {
        public Guid Id { get; set; }
        public string Title { get; set; } = null!;
        public Guid? CreatedByExpertId { get; set; }
        /// <summary>Display name of the expert who created the case.</summary>
        public string? ExpertName { get; set; }
        public string Description { get; set; } = null!;
        public string? Difficulty { get; set; }
        public Guid? CategoryId { get; set; }
        public string? CategoryName { get; set; }
        /// <summary>From tags with type Location / BoneLocation (comma-separated if multiple).</summary>
        public string BoneLocation { get; set; } = string.Empty;
        /// <summary>Derived: approved / pending / draft (expert dashboard).</summary>
        public string Status { get; set; } = string.Empty;
        public bool? IsApproved { get; set; }
        public bool? IsActive { get; set; }
        public string? SuggestedDiagnosis { get; set; }
        public string? KeyFindings { get; set; }
        public DateTime? CreatedAt { get; set; }
        public DateTime? UpdatedAt { get; set; }
        public string ThumbnailUrl { get; set; } = string.Empty;
    }

    /// <summary>GET <c>/api/expert/cases/{id}</c> — full case row plus images and tags for the expert UI.</summary>
    public class GetExpertMedicalCaseDetailDto : GetMedicalCaseDTO
    {
        public IReadOnlyList<ExpertMedicalCaseImageSummaryDto> MedicalImages { get; set; } = Array.Empty<ExpertMedicalCaseImageSummaryDto>();
        public IReadOnlyList<ExpertCaseTagSummaryDto> Tags { get; set; } = Array.Empty<ExpertCaseTagSummaryDto>();
    }

    public class ExpertMedicalCaseImageSummaryDto
    {
        public Guid Id { get; set; }
        public string ImageUrl { get; set; } = string.Empty;
        public string? Modality { get; set; }
        public DateTime? CreatedAt { get; set; }
    }

    public class ExpertCaseTagSummaryDto
    {
        public Guid Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string Type { get; set; } = string.Empty;
    }
    /// <summary>JSON body for <c>POST /api/expert/cases</c>: case metadata plus pre-uploaded image URLs and optional annotations (FE uploads files to Supabase first).</summary>
    public class CreateExpertMedicalCaseJsonRequest
    {
        public string Title { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        /// <summary>Optional; normalized to <c>Easy</c>, <c>Medium</c>, or <c>Hard</c> (DB check). Omitted → <c>Medium</c>.</summary>
        public string? Difficulty { get; set; }
        public Guid? CategoryId { get; set; }
        public string? SuggestedDiagnosis { get; set; }
        public string? KeyFindings { get; set; }
        public List<Guid>? TagIds { get; set; }
        public List<CreateExpertMedicalCaseImageJson>? MedicalImages { get; set; }
    }

    public class CreateExpertMedicalCaseImageJson
    {
        public string ImageUrl { get; set; } = string.Empty;
        /// <summary>Optional; normalized to DB modality check: <c>X-Ray</c>, <c>CT</c>, <c>MRI</c>, <c>Ultrasound</c>, <c>Other</c>. Omitted → <c>Other</c>.</summary>
        public string? Modality { get; set; }
        public List<CreateAnnotationDTO>? Annotations { get; set; }
    }

    public class CreateMedicalCaseRequestDTO
    {
        public string Title { get; set; } = null!;
        public Guid? CreatedByExpertId { get; set; }
        public string Description { get; set; } = null!;
        public string? Difficulty { get; set; }
        public bool? IsApproved { get; set; }
        public bool? IsActive { get; set; }
        public Guid? CategoryId { get; set; }
        public string? SuggestedDiagnosis { get; set; }
        public string? KeyFindings { get; set; }
        public DateTime? CreatedAt { get; set; }
    }
    public class CreateMedicalCaseResponseDTO
    {
        public Guid Id { get; set; }
        public string? ExpertName { get; set; }
        public string Title { get; set; } = null!;
        public string Description { get; set; } = null!;
        public string? Difficulty { get; set; }
        public string? CategoryName { get; set; }   
        public bool? IsApproved { get; set; }
        public bool? IsActive { get; set; }
        public string? SuggestedDiagnosis { get; set; }
        public string? KeyFindings { get; set; }
        public DateTime? CreatedAt { get; set; }
    }
    public class UpdateMedicalCaseDTORequest
    {
        public string Title { get; set; } = null!;
        public Guid? CreatedByExpertId { get; set; }
        public string Description { get; set; } = null!;
        public string? Difficulty { get; set; }
        public bool? IsApproved { get; set; }
        public bool? IsActive { get; set; }
        public Guid? CategoryId { get; set; }
        public string? SuggestedDiagnosis { get; set; }
        public string? KeyFindings { get; set; }
        public DateTime? UpdatedAt { get; set; }
    }
    public class UpdateMedicalCaseResponseDTO
    {
        public Guid Id { get; set; }
        public string? ExpertName { get; set; }
        public string Title { get; set; } = null!;
        public string Description { get; set; } = null!;
        public string? Difficulty { get; set; }
        public string? CategoryName { get; set; }
        public bool? IsApproved { get; set; }
        public bool? IsActive { get; set; }
        public string? SuggestedDiagnosis { get; set; }
        public string? KeyFindings { get; set; }
        public DateTime? UpdatedAt { get; set; }
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
        /// <summary>Optional; if empty BE stores <c>finding</c> (<c>case_annotations.label</c> is NOT NULL).</summary>
        public string? Label { get; set; }
        public string? Coordinates { get; set; }
    }
    public class AddAnnotationDTOResponse
    {
        public Guid ImageId { get; set; }
        /// <summary>Optional; if empty BE stores <c>finding</c>.</summary>
        public string? Label { get; set; }
        public string? Coordinates { get; set; }
    }


    //==========================================================================
    public class GetTagDTO 
    { 
        public Guid Id { get; set; } 
        public string Name { get; set; } = null!; 
    }
    
    public class GetCategoryDTO 
    { public Guid Id { get; set; } 
      public string Name { get; set; } = null!; 
    }
   
    public class GetAllImageDTO
    {
        public Guid Id { get; set; }
        public Guid CaseId { get; set; }    
        public string ImageUrl { get; set; } = null!;
        public string FileName { get; set; } = null!;
    }


    public class GetAllAnnotationDTO
    {
        public Guid Id { get; set; }
        public Guid ImageId { get; set; }
        public string ImageUrl { get; set; } = null!;
        public string Label { get; set; } = null!;
        public string? Coordinates { get; set; }
        
    }
}
