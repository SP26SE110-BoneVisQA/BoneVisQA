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
        public Guid Id { get; set; }

        public string ClassName { get; set; } = null!;

        public string Semester { get; set; } = null!;

        public DateTime? CreatedAt { get; set; }

        public DateTime? UpdatedAt { get; set; }

        /// <summary>Assigned lecturer for this class (at most one).</summary>
        public Guid? LecturerId { get; set; }

        /// <summary>Assigned expert for this class (at most one).</summary>
        public Guid? ExpertId { get; set; }

        public string? LecturerName { get; set; }

        public string? ExpertName { get; set; }

        public string? LecturerEmail { get; set; }

        public string? ExpertEmail { get; set; }

        /// <summary>Number of student enrollment rows for this class.</summary>
        public int StudentCount { get; set; }

        /// <summary>FK to <c>bone_specialties</c>; routes experts by medical focus (e.g. Spine, Trauma).</summary>
        public Guid? ClassSpecialtyId { get; set; }

        public string? ClassSpecialtyName { get; set; }
    }
    public class CreateClassManagementDTO
    {
        public Guid Id { get; set; }

        public string ClassName { get; set; } = null!;

        public string Semester { get; set; } = null!;

        public DateTime? CreatedAt { get; set; }

        /// <summary>Required: class medical specialty for expert routing.</summary>
        public Guid ClassSpecialtyId { get; set; }
    }
    public class UpdateClassManagementDTO
    {
        public Guid Id { get; set; }

        public string ClassName { get; set; } = null!;

        public string Semester { get; set; } = null!;

        public DateTime? UpdatedAt { get; set; }

        /// <summary>Required: class medical specialty for expert routing.</summary>
        public Guid ClassSpecialtyId { get; set; }
    }

    //=======================================================  ASSIGN CLASS  ===================================================
    public class GetAssignClassDTO
    {
        public Guid Id { get; set; }

        /// <summary>Academic class this enrollment row belongs to (use for stable filtering on the FE).</summary>
        public Guid ClassId { get; set; }

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

        /// <summary>Optional: enroll this student (must have Student role).</summary>
        public Guid? StudentId { get; set; }

        /// <summary>Optional: set or replace class lecturer (must have Lecturer role).</summary>
        public Guid? LecturerId { get; set; }

        /// <summary>Optional: set or replace class expert (must have Expert role).</summary>
        public Guid? ExpertId { get; set; }

        /// <summary>When true, clears the class expert (cannot combine with <see cref="ExpertId"/>).</summary>
        public bool RemoveExpert { get; set; }

        public DateTime? EnrolledAt { get; set; }
    }
}
