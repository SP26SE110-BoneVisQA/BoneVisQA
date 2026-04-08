using BoneVisQA.Repositories.Models;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BoneVisQA.Services.Models.Expert
{

    //Quiz
    public class GetQuizDTO
    {
        public Guid Id { get; set; }
        public string Title { get; set; } = null!;
        public string? Topic { get; set; }
        public DateTime? OpenTime { get; set; }
        public DateTime? CloseTime { get; set; }
        public int? TimeLimit { get; set; }
        public int? PassingScore { get; set; }
        public bool IsAiGenerated { get; set; }
        public string? Difficulty { get; set; }
        public string? Classification { get; set; }
        public DateTime? CreatedAt { get; set; }
    }   
    public class CreateQuizRequestDTO
    {
        public Guid Id { get; set; }

        public string Title { get; set; } = null!;
       
        public Guid? CreatedByExpertId { get; set; }
       
        public string? Topic { get; set; }

        public DateTime? OpenTime { get; set; }

        public DateTime? CloseTime { get; set; }

        public int? TimeLimit { get; set; }

        public int? PassingScore { get; set; }    

        public bool IsAiGenerated { get; set; }

        public string? Difficulty { get; set; }

        public string? Classification { get; set; }

        public DateTime? CreatedAt { get; set; }
    }
    public class CreateQuizResponseDTO
    {
        public Guid Id { get; set; }

        public string Title { get; set; } = null!;

        public string? ExpertName { get; set; }

        public string? Topic { get; set; }

        public DateTime? OpenTime { get; set; }

        public DateTime? CloseTime { get; set; }

        public int? TimeLimit { get; set; }

        public int? PassingScore { get; set; }

        public bool IsAiGenerated { get; set; }

        public string? Difficulty { get; set; }

        public string? Classification { get; set; }

        public DateTime? CreatedAt { get; set; }
    }
    public class UpdateQuizRequestDTO
    {
        public Guid Id { get; set; }
        public string Title { get; set; } = null!;
        public string? Topic { get; set; }
        public DateTime? OpenTime { get; set; }
        public DateTime? CloseTime { get; set; }
        public int? TimeLimit { get; set; }
        public int? PassingScore { get; set; }
        public string? Difficulty { get; set; }
        public string? Classification { get; set; }
    }

    public class UpdateQuizResponseDTO
    {
        public string Title { get; set; } = null!;
        public string? Topic { get; set; }
        public DateTime? OpenTime { get; set; }
        public DateTime? CloseTime { get; set; }
        public int? TimeLimit { get; set; }
        public int? PassingScore { get; set; }
        public string? Difficulty { get; set; }
        public string? Classification { get; set; }
        public DateTime? CreatedAt { get; set; }
    }


    //Question
    public class GetQuizQuestionDTO
    {
        public Guid QuestionId { get; set; }
        public string? QuizTitle { get; set; }
        public string? CaseTitle { get; set; }
        public string QuestionText { get; set; } = null!;
        public string? Type { get; set; }
        public string? OptionA { get; set; }
        public string? OptionB { get; set; }
        public string? OptionC { get; set; }
        public string? OptionD { get; set; }
        public string? CorrectAnswer { get; set; }
    }   
    public class CreateQuizQuestionRequestDTO
    {
        public Guid QuizId { get; set; }
        public Guid? CaseId { get; set; }
        public string QuestionText { get; set; } = null!;
        public string? Type { get; set; }
        public string? OptionA { get; set; }
        public string? OptionB { get; set; }
        public string? OptionC { get; set; }
        public string? OptionD { get; set; }
        public string? CorrectAnswer { get; set; }
    }
    public class CreateQuizQuestionResponseDTO
    {
        public Guid Id { get; set; }
        public Guid QuizId { get; set; }
        public string? QuizTitle { get; set; }
        public Guid? CaseId { get; set; }
        public string? CaseTitle { get; set; }
        public string QuestionText { get; set; } = null!;
        public string? Type { get; set; }
        public string? OptionA { get; set; }
        public string? OptionB { get; set; }
        public string? OptionC { get; set; }
        public string? OptionD { get; set; }
        public string? CorrectAnswer { get; set; }
    }
    public class UpdateQuizQuestionRequestDTO
    {
        public Guid QuestionId { get; set; }
        public Guid QuizId { get; set; }
        public Guid CaseId { get; set; }
        public string QuestionText { get; set; } = string.Empty;
        public string? Type { get; set; }
        public string? CorrectAnswer { get; set; }
        public string? OptionA { get; set; }
        public string? OptionB { get; set; }
        public string? OptionC { get; set; }
        public string? OptionD { get; set; }
    }

    public class UpdateQuizQuestionResponseDTO
    {
        public string QuestionText { get; set; } = string.Empty;
        public string? QuizTitle { get; set; }
        public string? CaseTitle { get; set; }
        public string? Type { get; set; }
        public string? CorrectAnswer { get; set; }
        public string? OptionA { get; set; }
        public string? OptionB { get; set; }
        public string? OptionC { get; set; }
        public string? OptionD { get; set; }
    }

    // Assign Quiz to Class
    public class AssignQuizRequestDTO
    {
        public Guid ClassId { get; set; }

        public Guid QuizId { get; set; }

        public Guid? AssignedExpertId { get; set; }

        public DateTime? OpenTime { get; set; }

        public DateTime? CloseTime { get; set; }

        public int? PassingScore { get; set; }

        public int? TimeLimitMinutes { get; set; }
    }
    public class ClassQuizSessionResponseDTO
    {
        public Guid ClassId { get; set; }

        public string? ClassName { get; set; }

        public Guid QuizId { get; set; }

        public string? QuizName { get; set; }

        public string? ExpertName { get; set; }

        public DateTime? AssignedAt { get; set; }

        public DateTime? OpenTime { get; set; }

        public DateTime? CloseTime { get; set; }

        public int? PassingScore { get; set; }

        public int? TimeLimitMinutes { get; set; }
    }
    public class PagedResult<T>
    {
        public List<T> Items { get; set; } = new();

        public int TotalCount { get; set; }

        public int PageIndex { get; set; }

        public int PageSize { get; set; }
    }
}
