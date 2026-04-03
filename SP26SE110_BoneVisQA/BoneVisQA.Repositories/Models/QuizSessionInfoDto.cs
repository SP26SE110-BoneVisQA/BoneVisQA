namespace BoneVisQA.Repositories.Models;

public class QuizSessionInfoDto
{
    public Guid QuizId { get; set; }
    public string Title { get; set; } = string.Empty;
    public Guid ClassId { get; set; }
    public string ClassName { get; set; } = string.Empty;
    public DateTime? OpenTime { get; set; }
    public DateTime? CloseTime { get; set; }
    public int? TimeLimitMinutes { get; set; }
    public double? PassingScore { get; set; }
}
