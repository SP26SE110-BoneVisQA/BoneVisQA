using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using BoneVisQA.Services.Models.Lecturer;

namespace BoneVisQA.Services.Interfaces;

public interface ILecturerNotificationService
{
    Task<LecturerNotificationSummaryDto> GetNotificationSummaryAsync(Guid lecturerId);
    Task<IReadOnlyList<LecturerNotificationItemDto>> GetRecentNotificationsAsync(Guid lecturerId, int limit = 20);
    Task<int> GetPendingQuestionsCountAsync(Guid lecturerId);
    Task<int> GetEscalatedAnswersCountAsync(Guid lecturerId);
    Task<int> GetPendingReviewCountAsync(Guid lecturerId);
    Task MarkNotificationAsReadAsync(Guid notificationId);
    Task MarkAllNotificationsAsReadAsync(Guid lecturerId);
}
