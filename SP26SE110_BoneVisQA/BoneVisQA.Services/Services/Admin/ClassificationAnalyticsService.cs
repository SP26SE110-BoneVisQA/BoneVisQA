using BoneVisQA.Repositories.UnitOfWork;
using BoneVisQA.Services.Interfaces.Admin;
using BoneVisQA.Services.Models.Admin;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace BoneVisQA.Services.Services.Admin
{
    /// <summary>
    /// Service cho Classification Analytics Dashboard
    /// </summary>
    public class ClassificationAnalyticsService : IClassificationAnalyticsService
    {
        private readonly IUnitOfWork _unitOfWork;
        private readonly ILogger<ClassificationAnalyticsService> _logger;

        public ClassificationAnalyticsService(IUnitOfWork unitOfWork, ILogger<ClassificationAnalyticsService> logger)
        {
            _unitOfWork = unitOfWork;
            _logger = logger;
        }

        /// <summary>
        /// Lấy tổng hợp Classification Analytics
        /// </summary>
        public async Task<ClassificationAnalyticsDto> GetClassificationAnalyticsAsync()
        {
            // Get counts
            var totalBoneSpecialties = await _unitOfWork.BoneSpecialtyRepository.GetQueryable()
                .Where(s => s.IsActive)
                .CountAsync();

            var totalPathologyCategories = await _unitOfWork.PathologyCategoryRepository.GetQueryable()
                .Where(p => p.IsActive)
                .CountAsync();

            var totalClasses = await _unitOfWork.AcademicClassRepository.GetQueryable().CountAsync();
            var totalQuizzes = await _unitOfWork.QuizRepository.GetQueryable().CountAsync();
            var totalMedicalCases = await _unitOfWork.MedicalCaseRepository.GetQueryable().CountAsync();

            // Get stats by specialty
            var allSpecialties = await _unitOfWork.BoneSpecialtyRepository.GetQueryable()
                .Where(s => s.IsActive)
                .ToListAsync();

            var specialtyStats = new List<SpecialtyStatsDto>();
            foreach (var specialty in allSpecialties)
            {
                var stats = new SpecialtyStatsDto
                {
                    SpecialtyId = specialty.Id,
                    SpecialtyName = specialty.Name,
                    SpecialtyCode = specialty.Code,
                    Level = CalculateLevel(specialty, allSpecialties),
                    TotalClasses = await _unitOfWork.AcademicClassRepository.GetQueryable()
                        .CountAsync(c => c.ClassSpecialtyId == specialty.Id),
                    TotalQuizzes = await _unitOfWork.QuizRepository.GetQueryable()
                        .CountAsync(q => q.BoneSpecialtyId == specialty.Id),
                    TotalMedicalCases = await _unitOfWork.MedicalCaseRepository.GetQueryable()
                        .CountAsync(m => m.BoneSpecialtyId == specialty.Id),
                    TotalExperts = await _unitOfWork.ExpertSpecialtyRepository.GetQueryable()
                        .CountAsync(e => e.BoneSpecialtyId == specialty.Id && e.IsActive)
                };
                specialtyStats.Add(stats);
            }

            // Get experts by specialty
            var expertBySpecialty = await _unitOfWork.ExpertSpecialtyRepository.GetQueryable()
                .Include(e => e.BoneSpecialty)
                .Where(e => e.IsActive)
                .ToListAsync();

            var expertsBySpecialty = expertBySpecialty
                .GroupBy(e => e.BoneSpecialtyId)
                .Select(g => new ExpertBySpecialtyDto
                {
                    SpecialtyId = g.Key,
                    SpecialtyName = g.FirstOrDefault()?.BoneSpecialty?.Name ?? "Unknown",
                    TotalExperts = g.Select(e => e.ExpertId).Distinct().Count(),
                    PrimaryExperts = g.Count(e => e.IsPrimary),
                    AverageProficiencyLevel = g.Average(e => e.ProficiencyLevel)
                })
                .ToList();

            return new ClassificationAnalyticsDto
            {
                TotalBoneSpecialties = totalBoneSpecialties,
                TotalPathologyCategories = totalPathologyCategories,
                TotalClasses = totalClasses,
                TotalQuizzes = totalQuizzes,
                TotalMedicalCases = totalMedicalCases,
                SpecialtyStats = specialtyStats,
                ExpertsBySpecialty = expertsBySpecialty
            };
        }

        private int CalculateLevel(Repositories.Models.BoneSpecialty specialty, List<Repositories.Models.BoneSpecialty> allSpecialties)
        {
            int level = 0;
            var current = specialty;
            while (current.ParentId.HasValue)
            {
                level++;
                current = allSpecialties.FirstOrDefault(s => s.Id == current.ParentId);
                if (current == null) break;
            }
            return level;
        }
    }
}
