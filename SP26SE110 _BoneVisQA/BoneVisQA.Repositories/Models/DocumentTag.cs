using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BoneVisQA.Repositories.Models
{
    public class DocumentTag
    {
        public Guid DocumentId { get; set; }
        public Guid TagId { get; set; }
        public DateTime? CreatedAt { get; set; }

        public Document Document { get; set; } = null!;
        public Tag Tag { get; set; } = null!;
    }
}
