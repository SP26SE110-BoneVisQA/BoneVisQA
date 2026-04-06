namespace BoneVisQA.Services.Models.Lecturer;

public class UpdateAnnouncementRequestDto
{
    public string Title { get; set; } = string.Empty;
    public string Content { get; set; } = string.Empty;
    /// <summary>Nếu true, gửi lại email cho sinh viên trong lớp (giống lúc tạo mới).</summary>
    public bool SendEmail { get; set; }
}
