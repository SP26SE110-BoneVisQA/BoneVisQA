using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BoneVisQA.Repositories.Models
{
    [Table("document_tags")]  
    public class DocumentTag
    {
        [Column("document_id")]
        public Guid DocumentId { get; set; }

        [Column("tag_id")]
        public Guid TagId { get; set; }

        [Column("created_at")]
        public DateTime? CreatedAt { get; set; }

        // Navigation
        [ForeignKey("DocumentId")]
        public virtual Document Document { get; set; } = null!;

        [ForeignKey("TagId")]
        public virtual Tag Tag { get; set; } = null!;
    }
}
