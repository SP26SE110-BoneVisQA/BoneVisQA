using System;

namespace BoneVisQA.Services.Models.Lecturer;

public class AssignExpertRequestDto
{
    /// <summary>Expert user id, or <c>null</c> to remove expert from the class.</summary>
    public Guid? ExpertId { get; set; }
}
