using System.Linq;
using System.Text.Json;
using BoneVisQA.Repositories.Models;

namespace BoneVisQA.Services.Helpers;

public static class CaseMediaDicomMetadataHelper
{
    public static JsonElement? ResolveFirstMetadata(MedicalCase? medicalCase)
    {
        var json = medicalCase?.CaseMedia?
            .Where(m => !string.IsNullOrWhiteSpace(m.DicomMetadata))
            .OrderBy(m => m.CreatedAt)
            .ThenBy(m => m.Id)
            .Select(m => m.DicomMetadata)
            .FirstOrDefault();

        return DicomClinicalContextHelper.TryParseJson(json);
    }
}
