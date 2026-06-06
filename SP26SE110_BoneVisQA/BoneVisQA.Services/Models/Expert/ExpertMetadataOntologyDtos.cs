using System.Collections.Generic;

namespace BoneVisQA.Services.Models.Expert;

/// <summary>GET <c>/api/expert/metadata-ontology</c> — canonical values and DICOM tag mappings for auto-fill.</summary>
public sealed class ExpertMetadataOntologyResponse
{
    public IReadOnlyList<string> Modalities { get; set; } = [];
    public IReadOnlyList<string> AnatomySites { get; set; } = [];
    public IReadOnlyList<string> PathologyGroups { get; set; } = [];
    /// <summary>DICOM Modality codes (e.g. DX, CT, MR) → canonical modality string.</summary>
    public IReadOnlyDictionary<string, string> DicomModalityMap { get; set; } = new Dictionary<string, string>();
    /// <summary>DICOM BodyPartExamined tokens (e.g. UP_EXM, KNEE) → canonical anatomy site.</summary>
    public IReadOnlyDictionary<string, string> DicomBodyPartMap { get; set; } = new Dictionary<string, string>();
}
