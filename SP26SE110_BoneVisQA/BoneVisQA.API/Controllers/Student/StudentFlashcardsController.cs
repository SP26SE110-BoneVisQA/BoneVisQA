using System.Security.Claims;
using BoneVisQA.Services.Interfaces;
using BoneVisQA.Services.Models.Student;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace BoneVisQA.API.Controllers.Student;

[ApiController]
[Route("api/student/flashcards")]
[Tags("Student - Flashcards")]
[Authorize(Roles = "Student")]
public class StudentFlashcardsController : ControllerBase
{
    private readonly IFlashcardService _flashcardService;
    private readonly IFlashcardGeneratorService _flashcardGeneratorService;

    public StudentFlashcardsController(
        IFlashcardService flashcardService,
        IFlashcardGeneratorService flashcardGeneratorService)
    {
        _flashcardService = flashcardService;
        _flashcardGeneratorService = flashcardGeneratorService;
    }

    private Guid? GetUserId()
    {
        var rawUserId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        return Guid.TryParse(rawUserId, out var userId) ? userId : null;
    }

    #region Deck Endpoints

    [HttpGet("decks")]
    public async Task<ActionResult<IReadOnlyList<FlashcardDeckDto>>> GetDecks()
    {
        var studentId = GetUserId();
        if (studentId == null)
            return Unauthorized(new { message = "Token does not contain a valid user id." });

        var result = await _flashcardService.GetDecksByStudentAsync(studentId.Value);
        return Ok(result);
    }

    [HttpGet("decks/{deckId:guid}")]
    public async Task<ActionResult<FlashcardDeckDto>> GetDeck(Guid deckId)
    {
        var studentId = GetUserId();
        if (studentId == null)
            return Unauthorized(new { message = "Token does not contain a valid user id." });

        var result = await _flashcardService.GetDeckByIdAsync(deckId, studentId.Value);
        if (result == null)
            return NotFound(new { message = "Deck not found." });

        return Ok(result);
    }

    [HttpPost("decks")]
    public async Task<ActionResult<FlashcardDeckDto>> CreateDeck([FromBody] CreateFlashcardDeckDto dto)
    {
        var studentId = GetUserId();
        if (studentId == null)
            return Unauthorized(new { message = "Token does not contain a valid user id." });

        if (string.IsNullOrWhiteSpace(dto.DeckName))
            return BadRequest(new { message = "Deck name is required." });

        var result = await _flashcardService.CreateDeckAsync(studentId.Value, dto);
        return CreatedAtAction(nameof(GetDeck), new { deckId = result.Id }, result);
    }

    [HttpPut("decks/{deckId:guid}")]
    public async Task<ActionResult<FlashcardDeckDto>> UpdateDeck(Guid deckId, [FromBody] UpdateFlashcardDeckDto dto)
    {
        var studentId = GetUserId();
        if (studentId == null)
            return Unauthorized(new { message = "Token does not contain a valid user id." });

        var result = await _flashcardService.UpdateDeckAsync(deckId, studentId.Value, dto);
        if (result == null)
            return NotFound(new { message = "Deck not found." });

        return Ok(result);
    }

    [HttpDelete("decks/{deckId:guid}")]
    public async Task<ActionResult> DeleteDeck(Guid deckId)
    {
        var studentId = GetUserId();
        if (studentId == null)
            return Unauthorized(new { message = "Token does not contain a valid user id." });

        var success = await _flashcardService.DeleteDeckAsync(deckId, studentId.Value);
        if (!success)
            return NotFound(new { message = "Deck not found." });

        return NoContent();
    }

    #endregion

    #region Flashcard Endpoints

    [HttpGet("decks/{deckId:guid}/cards")]
    public async Task<ActionResult<IReadOnlyList<FlashcardDto>>> GetFlashcards(Guid deckId)
    {
        var studentId = GetUserId();
        if (studentId == null)
            return Unauthorized(new { message = "Token does not contain a valid user id." });

        var result = await _flashcardService.GetFlashcardsByDeckAsync(deckId, studentId.Value);
        return Ok(result);
    }

    [HttpGet("cards/{cardId:guid}")]
    public async Task<ActionResult<FlashcardDto>> GetFlashcard(Guid cardId)
    {
        var studentId = GetUserId();
        if (studentId == null)
            return Unauthorized(new { message = "Token does not contain a valid user id." });

        var result = await _flashcardService.GetFlashcardByIdAsync(cardId, studentId.Value);
        if (result == null)
            return NotFound(new { message = "Flashcard not found." });

        return Ok(result);
    }

    [HttpPost("cards")]
    public async Task<ActionResult<FlashcardDto>> CreateFlashcard([FromBody] CreateFlashcardDto dto)
    {
        var studentId = GetUserId();
        if (studentId == null)
            return Unauthorized(new { message = "Token does not contain a valid user id." });

        if (string.IsNullOrWhiteSpace(dto.FrontContent) || string.IsNullOrWhiteSpace(dto.BackContent))
            return BadRequest(new { message = "Front content and back content are required." });

        try
        {
            var result = await _flashcardService.CreateFlashcardAsync(studentId.Value, dto);
            return CreatedAtAction(nameof(GetFlashcard), new { cardId = result.Id }, result);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpPut("cards/{cardId:guid}")]
    public async Task<ActionResult<FlashcardDto>> UpdateFlashcard(Guid cardId, [FromBody] UpdateFlashcardDto dto)
    {
        var studentId = GetUserId();
        if (studentId == null)
            return Unauthorized(new { message = "Token does not contain a valid user id." });

        var result = await _flashcardService.UpdateFlashcardAsync(cardId, studentId.Value, dto);
        if (result == null)
            return NotFound(new { message = "Flashcard not found." });

        return Ok(result);
    }

    [HttpDelete("cards/{cardId:guid}")]
    public async Task<ActionResult> DeleteFlashcard(Guid cardId)
    {
        var studentId = GetUserId();
        if (studentId == null)
            return Unauthorized(new { message = "Token does not contain a valid user id." });

        var success = await _flashcardService.DeleteFlashcardAsync(cardId, studentId.Value);
        if (!success)
            return NotFound(new { message = "Flashcard not found." });

        return NoContent();
    }

    #endregion

    #region Study/Review Endpoints

    [HttpGet("study/{deckId:guid}")]
    public async Task<ActionResult<FlashcardStudySessionDto>> GetStudySession(Guid deckId)
    {
        var studentId = GetUserId();
        if (studentId == null)
            return Unauthorized(new { message = "Token does not contain a valid user id." });

        var result = await _flashcardService.GetStudySessionAsync(deckId, studentId.Value);
        if (result == null)
            return NotFound(new { message = "Deck not found." });

        return Ok(result);
    }

    [HttpPost("review")]
    public async Task<ActionResult<FlashcardReviewResultDto>> SubmitReview([FromBody] ReviewFlashcardDto dto)
    {
        var studentId = GetUserId();
        if (studentId == null)
            return Unauthorized(new { message = "Token does not contain a valid user id." });

        if (dto.Quality < 0 || dto.Quality > 5)
            return BadRequest(new { message = "Quality must be between 0 and 5." });

        var result = await _flashcardService.SubmitReviewAsync(studentId.Value, dto);
        if (result == null)
            return NotFound(new { message = "Flashcard not found." });

        return Ok(result);
    }

    [HttpGet("stats")]
    public async Task<ActionResult<FlashcardStatsDto>> GetStats()
    {
        var studentId = GetUserId();
        if (studentId == null)
            return Unauthorized(new { message = "Token does not contain a valid user id." });

        var result = await _flashcardService.GetStatsAsync(studentId.Value);
        return Ok(result);
    }

    #endregion

    #region Bookmark Endpoints

    [HttpPost("bookmark/{cardId:guid}")]
    public async Task<ActionResult<FlashcardDto>> ToggleBookmark(Guid cardId)
    {
        var studentId = GetUserId();
        if (studentId == null)
            return Unauthorized(new { message = "Token does not contain a valid user id." });

        var result = await _flashcardService.ToggleBookmarkAsync(cardId, studentId.Value);
        if (result == null)
            return NotFound(new { message = "Flashcard not found." });

        return Ok(result);
    }

    [HttpGet("bookmarks")]
    public async Task<ActionResult<BookmarkedFlashcardsDto>> GetBookmarkedCards()
    {
        var studentId = GetUserId();
        if (studentId == null)
            return Unauthorized(new { message = "Token does not contain a valid user id." });

        var result = await _flashcardService.GetBookmarkedCardsAsync(studentId.Value);
        return Ok(result);
    }

    [HttpGet("study/bookmarks")]
    public async Task<ActionResult<FlashcardStudySessionDto>> GetBookmarkedStudySession()
    {
        var studentId = GetUserId();
        if (studentId == null)
            return Unauthorized(new { message = "Token does not contain a valid user id." });

        var result = await _flashcardService.GetBookmarkedStudySessionAsync(studentId.Value);
        if (result == null)
            return NotFound(new { message = "No bookmarked cards found." });

        return Ok(result);
    }

    #endregion

    #region Import Endpoints

    [HttpPost("import")]
    public async Task<ActionResult<ImportFlashcardsResultDto>> ImportFlashcards([FromBody] ImportFlashcardsDto dto)
    {
        var studentId = GetUserId();
        if (studentId == null)
            return Unauthorized(new { message = "Token does not contain a valid user id." });

        if (dto.DeckId == Guid.Empty)
            return BadRequest(new { message = "Deck ID is required." });

        if (dto.Cards == null || dto.Cards.Count == 0)
            return BadRequest(new { message = "At least one card is required." });

        var result = await _flashcardService.ImportFlashcardsAsync(studentId.Value, dto);
        return Ok(result);
    }

    #endregion

    #region Flashcard Generator Endpoints

    [HttpPost("generate/from-document/{documentId:guid}")]
    public async Task<ActionResult<FlashcardGenerationResultDto>> GenerateFromDocument(
        Guid documentId,
        [FromQuery] int cardCount = 10)
    {
        var studentId = GetUserId();
        if (studentId == null)
            return Unauthorized(new { message = "Token does not contain a valid user id." });

        if (cardCount < 1 || cardCount > 50)
            return BadRequest(new { message = "Card count must be between 1 and 50." });

        var result = await _flashcardGeneratorService.GenerateFromDocumentAsync(
            documentId, studentId.Value, cardCount);

        if (!result.Success)
            return BadRequest(result);

        return Ok(result);
    }

    [HttpPost("generate/from-chunks")]
    public async Task<ActionResult<FlashcardGenerationResultDto>> GenerateFromChunks(
        [FromBody] GenerateFromChunksRequestDto request)
    {
        var studentId = GetUserId();
        if (studentId == null)
            return Unauthorized(new { message = "Token does not contain a valid user id." });

        if (request.ChunkIds == null || request.ChunkIds.Count == 0)
            return BadRequest(new { message = "At least one chunk ID is required." });

        if (request.CardCount < 1 || request.CardCount > 50)
            return BadRequest(new { message = "Card count must be between 1 and 50." });

        var result = await _flashcardGeneratorService.GenerateFromChunksAsync(
            studentId.Value, request.ChunkIds, request.CardCount);

        if (!result.Success)
            return BadRequest(result);

        return Ok(result);
    }

    [HttpPost("generate/from-text")]
    public async Task<ActionResult<FlashcardGenerationResultDto>> GenerateFromText(
        [FromBody] GenerateFromTextRequestDto request)
    {
        var studentId = GetUserId();
        if (studentId == null)
            return Unauthorized(new { message = "Token does not contain a valid user id." });

        if (string.IsNullOrWhiteSpace(request.SourceText))
            return BadRequest(new { message = "Source text is required." });

        if (request.CardCount < 1 || request.CardCount > 50)
            return BadRequest(new { message = "Card count must be between 1 and 50." });

        var result = await _flashcardGeneratorService.GenerateFromTextAsync(
            studentId.Value, request.SourceText, request.DeckName, request.CardCount);

        if (!result.Success)
            return BadRequest(result);

        return Ok(result);
    }

    [HttpPost("generate/from-case/{caseId:guid}")]
    public async Task<ActionResult<FlashcardGenerationResultDto>> GenerateFromCase(
        Guid caseId,
        [FromQuery] int cardCount = 10)
    {
        var studentId = GetUserId();
        if (studentId == null)
            return Unauthorized(new { message = "Token does not contain a valid user id." });

        if (cardCount < 1 || cardCount > 50)
            return BadRequest(new { message = "Card count must be between 1 and 50." });

        var result = await _flashcardGeneratorService.GenerateFromCaseAsync(
            caseId, studentId.Value, cardCount);

        if (!result.Success)
            return BadRequest(result);

        return Ok(result);
    }

    public class GenerateFromChunksRequestDto
    {
        public List<Guid> ChunkIds { get; set; } = new();
        public int CardCount { get; set; } = 10;
    }

    public class GenerateFromTextRequestDto
    {
        public string SourceText { get; set; } = string.Empty;
        public string? DeckName { get; set; }
        public int CardCount { get; set; } = 10;
    }

    #endregion
}
