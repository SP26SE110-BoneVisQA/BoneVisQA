using BoneVisQA.Services.Models.Admin;
using System.Threading.Tasks;

namespace BoneVisQA.Services.Interfaces.Admin
{
    /// <summary>
    /// Interface cho Classification Analytics Service
    /// </summary>
    public interface IClassificationAnalyticsService
    {
        /// <summary>
        /// Lấy tổng hợp Classification Analytics
        /// </summary>
        Task<ClassificationAnalyticsDto> GetClassificationAnalyticsAsync();
    }
}
