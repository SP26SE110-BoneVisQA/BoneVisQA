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
    public class QuizDTO
    {
        public Guid Id { get; set; }

        public string Title { get; set; } = null!;

        public DateTime? OpenTime { get; set; }

        public DateTime? CloseTime { get; set; }

        public int? TimeLimit { get; set; }

        public int? PassingScore { get; set; }
        public DateTime? CreatedAt { get; set; }
    }
    public class ClassQuizDTO
    {
        public Guid ClassId { get; set; }
        public Guid QuizId { get; set; }
        public string? QuizName { get; set; }
        public string? ClassName { get; set; }   
        public DateTime? AssignedAt { get; set; }
    }
    public class QuizQuestionDTO
    {
        public Guid Id { get; set; }
        public Guid QuizId { get; set; }
        public string? QuizTitle { get; set; }
        public Guid? CaseId { get; set; }
        public string QuestionText { get; set; } = null!;
        public string? Type { get; set; }
        public string? OptionA { get; set; }
        public string? OptionB { get; set; }
        public string? OptionC { get; set; }
        public string? OptionD { get; set; }
        public string? CorrectAnswer { get; set; }
    }

    public class CreateQuizQuestionDTO
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

    public class QuizScoreResultDTO
    {
        public Guid AttemptId { get; set; }
        public Guid StudentId { get; set; }
        public Guid QuizId { get; set; }
        public string QuizTitle { get; set; } = null!;
        public int TotalQuestions { get; set; }
        public int CorrectAnswers { get; set; }
        public float Score { get; set; }         
        public int? PassingScore { get; set; }   
        public bool IsPassed { get; set; }
        public DateTime? CompletedAt { get; set; }
    }
    public class UpdateQuizsQuestionRequestDto
    {
        public string QuestionText { get; set; } = string.Empty;
        public string? Type { get; set; }
        public string? CorrectAnswer { get; set; }
        public string? OptionA { get; set; }
        public string? OptionB { get; set; }
        public string? OptionC { get; set; }
        public string? OptionD { get; set; }
    }

    public class StudentSubmitQuestionDTO   
    {
        public Guid StudentId { get; set; }

        public Guid AttemptId { get; set; }

        public Guid QuestionId { get; set; }

        public string? StudentAnswer { get; set; }

    }

    public class StudentSubmitQuestionResponseDTO
    {
        public string? QuizTile { get; set; }
     
        public  string? QuestionText { get; set; }
       
        public string? StudentAnswer { get; set; }
       
        public string? CorrectAnswer { get; set; }
      
        public bool? IsCorrect { get; set; }
    }

}
