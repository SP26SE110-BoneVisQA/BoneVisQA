using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace BoneVisQA.Repositories.Models;

[Table("flashcards")]
public class Flashcard
{
    [Key]
    [Column("id")]
    public Guid Id { get; set; }

    [Column("deck_id")]
    public Guid DeckId { get; set; }

    [Column("front_content")]
    [Required]
    public string FrontContent { get; set; } = null!;

    [Column("back_content")]
    [Required]
    public string BackContent { get; set; } = null!;

    [Column("image_url")]
    [MaxLength(500)]
    public string? ImageUrl { get; set; }

    [Column("ease_factor")]
    public decimal EaseFactor { get; set; } = 2.5m;

    [Column("interval_days")]
    public int IntervalDays { get; set; } = 1;

    [Column("repetition_count")]
    public int RepetitionCount { get; set; } = 0;

    [Column("next_review_date")]
    public DateOnly? NextReviewDate { get; set; }

    [Column("last_review_date")]
    public DateTime? LastReviewDate { get; set; }

    [Column("is_bookmarked")]
    public bool IsBookmarked { get; set; } = false;

    [Column("bookmarked_at")]
    public DateTime? BookmarkedAt { get; set; }

    [Column("created_at")]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    [Column("updated_at")]
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    [ForeignKey("DeckId")]
    public virtual FlashcardDeck? Deck { get; set; }
}
