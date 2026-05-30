using BoneVisQA.Repositories.UnitOfWork;
using BoneVisQA.Services.Constants;
using BoneVisQA.Services.Interfaces;
using BoneVisQA.Services.Models.Lecturer;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace BoneVisQA.Services.Services.Lecturer;

public class LecturerNotificationService : ILecturerNotificationService
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<LecturerNotificationService> _logger;

    public LecturerNotificationService(IUnitOfWork unitOfWork, ILogger<LecturerNotificationService> logger)
    {
        _unitOfWork = unitOfWork;
        _logger = logger;
    }

    public async Task<LecturerNotificationSummaryDto> GetNotificationSummaryAsync(Guid lecturerId)
    {
        var classIds = await _unitOfWork.AcademicClassRepository
            .FindByCondition(c => c.LecturerId == lecturerId)
            .Select(c => c.Id)
            .ToListAsync();

        var pendingQuestions = await GetPendingQuestionsCountAsync(lecturerId);
        var escalatedAnswers = await GetEscalatedAnswersCountAsync(lecturerId);
        var pendingReview = await GetPendingReviewCountAsync(lecturerId);
        var unreadNotifications = await _unitOfWork.Context.Notifications
            .CountAsync(n => n.UserId == lecturerId && !n.IsRead);

        var lastActivity = await _unitOfWork.Context.Notifications
            .Where(n => n.UserId == lecturerId)
            .OrderByDescending(n => n.CreatedAt)
            .Select(n => n.CreatedAt)
            .FirstOrDefaultAsync();

        return new LecturerNotificationSummaryDto
        {
            PendingQuestionsCount = pendingQuestions,
            EscalatedAnswersCount = escalatedAnswers,
            PendingReviewCount = pendingReview,
            UnreadNotificationsCount = unreadNotifications,
            LastActivityAt = lastActivity
        };
    }

    public async Task<IReadOnlyList<LecturerNotificationItemDto>> GetRecentNotificationsAsync(Guid lecturerId, int limit = 20)
    {
        var notifications = await _unitOfWork.Context.Notifications
            .Where(n => n.UserId == lecturerId)
            .OrderByDescending(n => n.CreatedAt)
            .Take(limit)
            .ToListAsync();

        return notifications.Select(n => new LecturerNotificationItemDto
        {
            Id = n.Id,
            Title = n.Title,
            Message = n.Message,
            Type = n.Type,
            TargetUrl = n.TargetUrl,
            IsRead = n.IsRead,
            CreatedAt = n.CreatedAt
        }).ToList();
    }

    public async Task<int> GetPendingQuestionsCountAsync(Guid lecturerId)
    {
        var classIds = await _unitOfWork.AcademicClassRepository
            .FindByCondition(c => c.LecturerId == lecturerId)
            .Select(c => c.Id)
            .ToListAsync();

        if (!classIds.Any())
            return 0;

        // Count questions from classes assigned to this lecturer that need answers
        var count = await _unitOfWork.Context.ClassCases
            .Where(cc => classIds.Contains(cc.ClassId))
            .SelectMany(cc => cc.Case.StudentQuestions)
            .Where(q => !q.CaseAnswers.Any())
            .CountAsync();

        return count;
    }

    public async Task<int> GetEscalatedAnswersCountAsync(Guid lecturerId)
    {
        var classIds = await _unitOfWork.AcademicClassRepository
            .FindByCondition(c => c.LecturerId == lecturerId)
            .Select(c => c.Id)
            .ToListAsync();

        if (!classIds.Any())
            return 0;

        // Count escalated answers from lecturer's classes
        var count = await _unitOfWork.Context.CaseAnswers
            .Include(a => a.Question)
            .Where(a => a.Question != null)
            .Where(a => _unitOfWork.Context.ClassCases
                .Any(cc => cc.CaseId == a.Question.CaseId && classIds.Contains(cc.ClassId)))
            .Where(a => a.Status == CaseAnswerStatuses.EscalatedToExpert
                     || a.Status == CaseAnswerStatuses.Escalated)
            .CountAsync();

        return count;
    }

    public async Task<int> GetPendingReviewCountAsync(Guid lecturerId)
    {
        var classIds = await _unitOfWork.AcademicClassRepository
            .FindByCondition(c => c.LecturerId == lecturerId)
            .Select(c => c.Id)
            .ToListAsync();

        if (!classIds.Any())
            return 0;

        // Count answers needing lecturer review from lecturer's classes
        var count = await _unitOfWork.Context.CaseAnswers
            .Include(a => a.Question)
            .Where(a => a.Question != null)
            .Where(a => _unitOfWork.Context.ClassCases
                .Any(cc => cc.CaseId == a.Question.CaseId && classIds.Contains(cc.ClassId)))
            .Where(a => a.Status == CaseAnswerStatuses.RequiresLecturerReview)
            .CountAsync();

        return count;
    }

    public async Task MarkNotificationAsReadAsync(Guid notificationId)
    {
        var notification = await _unitOfWork.Context.Notifications
            .FirstOrDefaultAsync(n => n.Id == notificationId);

        if (notification != null)
        {
            notification.IsRead = true;
            await _unitOfWork.SaveAsync();
        }
    }

    public async Task MarkAllNotificationsAsReadAsync(Guid lecturerId)
    {
        var unreadNotifications = await _unitOfWork.Context.Notifications
            .Where(n => n.UserId == lecturerId && !n.IsRead)
            .ToListAsync();

        foreach (var notification in unreadNotifications)
        {
            notification.IsRead = true;
        }

        await _unitOfWork.SaveAsync();
    }
}
