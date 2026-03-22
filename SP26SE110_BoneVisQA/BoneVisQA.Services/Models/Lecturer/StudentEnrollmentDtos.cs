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
