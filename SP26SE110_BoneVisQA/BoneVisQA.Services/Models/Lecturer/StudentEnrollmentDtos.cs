using System;
using System.Collections.Generic;

namespace BoneVisQA.Services.Models.Lecturer;

public class StudentEnrollmentDto
{
    public Guid EnrollmentId { get; set; }
    public Guid StudentId { get; set; }
    public string StudentName { get; set; } = string.Empty;
    public string StudentEmail { get; set; } = string.Empty;
    public string? StudentCode { get; set; }
    public string? ClassName { get; set; }
    public DateTime? EnrolledAt { get; set; }
}

public class EnrollStudentsRequestDto
{
    public List<Guid> StudentIds { get; set; } = new List<Guid>();
}

public class EnrollStudentRequestDto
{
    public Guid StudentId { get; set; }
}

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
