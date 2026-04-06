namespace BoneVisQA.Services.Models.Lecturer;

public class CreateAnnouncementRequestDto
{
    public string Title { get; set; } = string.Empty;
    public string Content { get; set; } = string.Empty;
    public bool SendEmail { get; set; } = true;
}

