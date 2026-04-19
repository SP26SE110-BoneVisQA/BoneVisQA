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
        public Guid? QuizId { get; set; }
        public string? QuizTitle { get; set; }
        public string? CaseTitle { get; set; }
        public string QuestionText { get; set; } = null!;
        public string? Type { get; set; }
        public string? OptionA { get; set; }
        public string? OptionB { get; set; }
        public string? OptionC { get; set; }
        public string? OptionD { get; set; }
        public string? CorrectAnswer { get; set; }
        /// <summary>
        /// URL của ảnh câu hỏi
        /// </summary>
        public string? ImageUrl { get; set; }
    }   
    public class CreateQuizQuestionRequestDTO
    {
        public Guid? QuizId { get; set; }  // Nullable - quizId đã có trong route URL
        public Guid? CaseId { get; set; }
        public string QuestionText { get; set; } = null!;
        public string? Type { get; set; }
        public string? OptionA { get; set; }
        public string? OptionB { get; set; }
        public string? OptionC { get; set; }
        public string? OptionD { get; set; }
        public string? CorrectAnswer { get; set; }
        /// <summary>
        /// URL của ảnh câu hỏi (bắt buộc khi tạo câu hỏi)
        /// Ảnh sẽ được upload lên Supabase và lưu URL vào đây
        /// </summary>
        public string? ImageUrl { get; set; }
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
        /// <summary>
        /// URL của ảnh câu hỏi
        /// </summary>
        public string? ImageUrl { get; set; }
    }
    public class UpdateQuizQuestionRequestDTO
    {
        public Guid QuestionId { get; set; }
        public Guid? QuizId { get; set; }
        public Guid? CaseId { get; set; }
        public string QuestionText { get; set; } = string.Empty;
        public string? Type { get; set; }
        public string? CorrectAnswer { get; set; }
        public string? OptionA { get; set; }
        public string? OptionB { get; set; }
        public string? OptionC { get; set; }
        public string? OptionD { get; set; }
        /// <summary>
        /// URL của ảnh câu hỏi (có thể cập nhật ảnh mới)
        /// </summary>
        public string? ImageUrl { get; set; }
    }

    public class UpdateQuizQuestionResponseDTO
    {
        public Guid QuestionId { get; set; }
        public string QuestionText { get; set; } = string.Empty;
        public string? QuizTitle { get; set; }
        public string? CaseTitle { get; set; }
        public string? Type { get; set; }
        public string? CorrectAnswer { get; set; }
        public string? OptionA { get; set; }
        public string? OptionB { get; set; }
        public string? OptionC { get; set; }
        public string? OptionD { get; set; }
        /// <summary>
        /// URL của ảnh câu hỏi
        /// </summary>
        public string? ImageUrl { get; set; }
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
    public class ClassQuizSessionDTO
    {
        public Guid ClassId { get; set; }
        public string? ClassName { get; set; }
        public Guid QuizId { get; set; }
        public string? QuizName { get; set; }
        public DateTime? AssignedAt { get; set; }
        public DateTime? OpenTime { get; set; }
        public DateTime? CloseTime { get; set; }
        public int? PassingScore { get; set; }
        public int? TimeLimitMinutes { get; set; }
    }

    public class GetClassDTO 
    { 
        public Guid Id { get; set; } 
        public string ClassName { get; set; } = null!; 
    }

    public class GetExpertDTO 
    { 
        public Guid Id { get; set; } 
        public string FullName { get; set; } = null!; 
    }

    public class GetQuizAttemptDTO
    {
        public Guid AttemptId { get; set; }
        public Guid QuizId { get; set; }
        public string? QuizTitle { get; set; }
        public Guid StudentId { get; set; }
        public string? StudentName { get; set; }
        public DateTime? StartedAt { get; set; }
        public DateTime? CompletedAt { get; set; }
        public double? Score { get; set; }
    }

    // Expert Quiz for Lecturer - Hiển thị quiz của Expert để Lecturer lấy gán vào lớp
    // 
    // MÔ TẢ:
    // - DTO này dùng để hiển thị quiz của Expert cho Lecturer xem và chọn
    // - Trả về thông tin quiz + số câu hỏi (QuestionCount) - để Lecturer biết quiz có bao nhiêu câu
    // - KHÔNG trả về đáp án (CorrectAnswer)
    // 
    // VÍ DỤ:
    // - Quiz "Lower Limb Module" có QuestionCount = 10 → Student nhận 10 câu khi làm quiz
    // - Quiz "Chest X-Ray Basics" có QuestionCount = 5 → Student nhận 5 câu khi làm quiz
    public class ExpertQuizForLecturerDto
    {
        public Guid Id { get; set; }
        public string Title { get; set; } = null!;                          // Tên quiz (vd: "Lower Limb Module")
        public string? Topic { get; set; }                                   // Chủ đề (vd: "Lower Limb")
        public DateTime? OpenTime { get; set; }                             // Thời gian mở quiz
        public DateTime? CloseTime { get; set; }                            // Thời gian đóng quiz
        public int? TimeLimit { get; set; }                                  // Thời gian làm bài (phút)
        public int? PassingScore { get; set; }                               // Điểm đạt
        public bool IsAiGenerated { get; set; }                              // Có phải AI tạo không
        public string? Difficulty { get; set; }                               // Độ khó: Easy, Medium, Hard
        public string? Classification { get; set; }                          // Phân loại: Year 1, Year 2
        public DateTime? CreatedAt { get; set; }                              // Ngày tạo
        public string? ExpertName { get; set; }                               // Tên Expert đã tạo quiz
        public int QuestionCount { get; set; }                                // SỐ CÂU HỎI - Quan trọng!
    }

    // Câu hỏi trong quiz - CÓ đáp án đúng + CÓ ảnh
    // 
    // MÔ TẢ:
    // - DTO này dùng để hiển thị câu hỏi cho Lecturer xem trước khi gán quiz
    // - CÓ trả về CorrectAnswer (đáp án đúng) vì Expert đã tạo câu hỏi kèm đáp án
    // - CÓ trả về ImageUrl (ảnh câu hỏi)
    // - Mỗi câu hỏi BẮT BUỘC phải có đáp án đúng khi được tạo
    // 
    // QUY TẮC:
    // - CorrectAnswer phải là một trong các giá trị: "A", "B", "C", "D"
    // - ImageUrl là URL của ảnh đã upload lên Supabase
    // - Không có câu hỏi nào được tạo mà không có đáp án đúng
    public class ExpertQuizQuestionDto
    {
        public Guid QuestionId { get; set; }
        public string QuestionText { get; set; } = null!;                    // Nội dung câu hỏi
        public string? Type { get; set; }                                     // Loại câu hỏi
        public string? OptionA { get; set; }                                  // Lựa chọn A
        public string? OptionB { get; set; }                                  // Lựa chọn B
        public string? OptionC { get; set; }                                  // Lựa chọn C
        public string? OptionD { get; set; }                                  // Lựa chọn D
        public string? CaseTitle { get; set; }                               // Case liên quan (nếu có)
        public string? CorrectAnswer { get; set; }                            // Đáp án đúng: A, B, C hoặc D
        /// <summary>
        /// URL của ảnh câu hỏi (bắt buộc)
        /// </summary>
        public string? ImageUrl { get; set; }
    }

    // Request DTO for assigning expert quiz to class
    // 
    // MÔ TẢ:
    // - DTO này dùng khi Lecturer muốn gán quiz Expert vào lớp
    // - Các trường đều optional - có thể gán quiz mà không cần thông số
    // - Nếu không truyền, hệ thống sẽ dùng thông số mặc định từ quiz gốc
    public class AssignExpertQuizRequestDto
    {
        public string? TitleOverride { get; set; }                            // Tiêu đề mới cho bản sao (optional)
        public DateTime? OpenTime { get; set; }                               // Thời gian mở quiz (override)
        public DateTime? CloseTime { get; set; }                              // Thời gian đóng quiz (override)
        public int? PassingScore { get; set; }                                 // Điểm đạt (override)
        public int? TimeLimitMinutes { get; set; }                            // Thời gian làm bài (override)
    }
}
