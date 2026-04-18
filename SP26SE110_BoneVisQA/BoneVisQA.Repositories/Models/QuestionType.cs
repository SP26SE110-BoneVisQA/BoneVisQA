namespace BoneVisQA.Repositories.Models;

/// <summary>
/// Enum representing the type of a quiz question.
/// </summary>
public enum QuestionType
{
    /// <summary>
    /// Multiple choice question with 4 options (A, B, C, D).
    /// </summary>
    MultipleChoice = 1,

    /// <summary>
    /// True/False question with two boolean options.
    /// </summary>
    TrueFalse = 2,

    /// <summary>
    /// Essay question requiring a free-text answer.
    /// </summary>
    Essay = 3
}
