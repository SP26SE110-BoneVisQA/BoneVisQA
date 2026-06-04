using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace BoneVisQA.Repositories.Models;

[Table("flashcard_decks")]
public class FlashcardDeck
{
    [Key]
    [Column("id")]
    public Guid Id { get; set; }

    [Column("deck_name")]
    [Required]
    [MaxLength(255)]
    public string DeckName { get; set; } = null!;

    [Column("description")]
    public string? Description { get; set; }

    [Column("student_id")]
    public Guid StudentId { get; set; }

    [Column("card_count")]
    public int CardCount { get; set; } = 0;

    [Column("created_at")]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    [Column("updated_at")]
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    [ForeignKey("StudentId")]
    public virtual User? Student { get; set; }

    [InverseProperty("Deck")]
    public virtual ICollection<Flashcard> Flashcards { get; set; } = new List<Flashcard>();
}
