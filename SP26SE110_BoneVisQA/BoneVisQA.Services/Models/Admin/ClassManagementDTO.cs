using BoneVisQA.Repositories.Models;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BoneVisQA.Services.Models.Admin
{
    public class GetClassManagementDTO
    {
        public string ClassName { get; set; } = null!;

        public string Semester { get; set; } = null!;

        public DateTime? CreatedAt { get; set; }

        public DateTime? UpdatedAt { get; set; }
    }
    public class CreateClassManagementDTO
    {
        public Guid Id { get; set; }

        public string ClassName { get; set; } = null!;

        public string Semester { get; set; } = null!;

        public DateTime? CreatedAt { get; set; }
    }
    public class UpdateClassManagementDTO
    {
        public Guid Id { get; set; }

        public string ClassName { get; set; } = null!;

        public string Semester { get; set; } = null!;

        public DateTime? UpdatedAt { get; set; }
    }

    //=======================================================  ASSIGN CLASS  ===================================================
    public class GetAssignClassDTO
    {
        public Guid Id { get; set; }

        public string? ClassName { get; set; }

        public string? LecturerName { get; set; }

        public string? ExpertName { get; set; }

        public string? StudentName { get; set; }
       
        public DateTime? EnrolledAt { get; set; }
    }
    public class AssignClassDTO
    {       
        public Guid ClassId { get; set; }

        public string? ClassName { get; set; }

        public Guid StudentId { get; set; }

        public Guid LecturerId { get; set; }

        public Guid ExpertId { get; set; }

        public DateTime? EnrolledAt { get; set; }
    }
}
