using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using BoneVisQA.Repositories.UnitOfWork;
using Microsoft.EntityFrameworkCore;

namespace BoneVisQA.Services.Models.Admin
{
    /// <summary>
    /// DTO cho Classification Analytics Dashboard
    /// </summary>
    public class ClassificationAnalyticsDto
    {
        // Summary counts
        public int TotalBoneSpecialties { get; set; }
        public int TotalPathologyCategories { get; set; }
        public int TotalClasses { get; set; }
        public int TotalQuizzes { get; set; }
        public int TotalMedicalCases { get; set; }

        // Distribution by Specialty
        public List<SpecialtyStatsDto> SpecialtyStats { get; set; } = new();

        // Top Experts by Specialty
        public List<ExpertBySpecialtyDto> ExpertsBySpecialty { get; set; } = new();
    }

    public class SpecialtyStatsDto
    {
        public Guid SpecialtyId { get; set; }
        public string SpecialtyName { get; set; } = null!;
        public string SpecialtyCode { get; set; } = null!;
        public int Level { get; set; }
        public int TotalClasses { get; set; }
        public int TotalQuizzes { get; set; }
        public int TotalMedicalCases { get; set; }
        public int TotalExperts { get; set; }
    }

    public class ExpertBySpecialtyDto
    {
        public Guid SpecialtyId { get; set; }
        public string SpecialtyName { get; set; } = null!;
        public int TotalExperts { get; set; }
        public int PrimaryExperts { get; set; }
        public double AverageProficiencyLevel { get; set; }
    }
}
