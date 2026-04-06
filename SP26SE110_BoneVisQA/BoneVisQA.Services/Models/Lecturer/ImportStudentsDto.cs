namespace BoneVisQA.Services.Models.Lecturer;

public class ImportStudentsFromExcelResultDto
{
    public int TotalRows { get; set; }
    public int SuccessCount { get; set; }
    public int FailedCount { get; set; }
    public List<string> Errors { get; set; } = new List<string>();
    public List<StudentEnrollmentDto> EnrolledStudents { get; set; } = new List<StudentEnrollmentDto>();
}

public class ImportStudentsResultItemDto
{
    public int RowNumber { get; set; }
    public string? Email { get; set; }
    public bool Success { get; set; }
    public string? ErrorMessage { get; set; }
    public StudentEnrollmentDto? Student { get; set; }
}

public class ImportStudentsSummaryDto
{
    public int TotalRows { get; set; }
    public int SuccessCount { get; set; }
    public int FailedCount { get; set; }
    public int AlreadyEnrolledCount { get; set; }
    public int NotFoundCount { get; set; }
    public List<ImportStudentsResultItemDto> Results { get; set; } = new List<ImportStudentsResultItemDto>();
}