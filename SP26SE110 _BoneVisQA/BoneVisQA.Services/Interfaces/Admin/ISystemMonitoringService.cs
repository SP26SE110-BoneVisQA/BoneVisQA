using BoneVisQA.Services.Models.Admin;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BoneVisQA.Services.Interfaces.Admin
{
    public interface ISystemMonitoringService
    {
        Task<SystemOverviewDTO> GetOverviewAsync();

        Task<UserStatDTO> GetUserStatsAsync();
        Task<ActivityStatDTO> GetActivityStatsAsync(DateTime from, DateTime to);
        Task<RagStatDTO> GetRagStatsAsync();
        Task<ExpertReviewStatDTO> GetExpertReviewStatsAsync();
    }
}
