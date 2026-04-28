using BoneVisQA.Services.Interfaces.Admin;
using BoneVisQA.Services.Models.Admin;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace BoneVisQA.API.Controllers.Admin
{
    [ApiController]
    [Route("api/admin/classes")]
    [Tags("Admin - Classes")]
    [Authorize(Roles = "Admin")]
    public class ClassManagementController : ControllerBase
    {
        private readonly IClassManagementService _classservice;

        public ClassManagementController(IClassManagementService classservice)
        {
            _classservice = classservice;
        }

        [HttpGet]
        public async Task<IActionResult> GetAll(int pageIndex = 1, int pageSize = 10)
        {
            var result = await _classservice.GetAcademicClassAsync(pageIndex, pageSize);
            return Ok(result);
        }

        /// <summary>Single class with lecturer/expert ids, names, emails, and student enrollment count.</summary>
        [HttpGet("{id:guid}")]
        public async Task<IActionResult> GetById(Guid id)
        {
            var result = await _classservice.GetAcademicClassByIdAsync(id);
            if (result == null)
                return NotFound(new { message = "Không tìm thấy lớp." });
            return Ok(result);
        }

        [HttpPost]
        public async Task<IActionResult> Create([FromBody] CreateClassManagementDTO dto)
        {
            try
            {
                var result = await _classservice.CreateAcademicClassAsync(dto);
                return Ok(result);
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }

        [HttpPut]
        public async Task<IActionResult> Update([FromBody] UpdateClassManagementDTO dto)
        {
            try
            {
                var result = await _classservice.UpdateAcademicClassAsync(dto);
                return Ok(result);
            }
            catch (Exception ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }

        [HttpDelete("{id:guid}")]
        public async Task<IActionResult> Delete(Guid id)
        {
            var result = await _classservice.DeleteAcademicClassAsync(id);
            if (!result)
                return NotFound(new { message = "Class not found" });
            return Ok(new { deleted = true });
        }

        /// <summary>Paged list of class enrollments (student in class + class staff snapshot). Optional <paramref name="classId"/> filters to one class.</summary>
        [HttpGet("/api/admin/classes/enrollments")]
        public async Task<IActionResult> GetEnrollments(int pageIndex = 1, int pageSize = 10, [FromQuery] Guid? classId = null)
        {
            var result = await _classservice.GetAssignClassAsync(pageIndex, pageSize, classId);
            return Ok(result);
        }

        /// <summary>
        /// Admin: assign or replace lecturer/expert on a class, enroll a student, and/or clear expert.
        /// At least one of <c>LecturerId</c>, <c>ExpertId</c>, <c>StudentId</c>, or <c>RemoveExpert</c> must be used.
        /// </summary>
        [HttpPost("/api/admin/classes/enrollments")]
        public async Task<IActionResult> AssignClass([FromBody] AssignClassDTO dto)
        {
            try
            {
                var result = await _classservice.AssignClassAsync(dto);
                return Ok(result);
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }

        /// <summary>Refresh enrollment timestamp and/or update class lecturer/expert (same payload rules as POST).</summary>
        [HttpPut("/api/admin/classes/enrollments")]
        public async Task<IActionResult> UpdateAssignClass([FromBody] AssignClassDTO dto)
        {
            try
            {
                var result = await _classservice.UpdateAssignClassAsync(dto);
                return Ok(result);
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }

        [HttpDelete("/api/admin/classes/enrollments/{id:guid}")]
        public async Task<IActionResult> DeleteEnrollment(Guid id)
        {
            var deleted = await _classservice.DeleteAssignClassAsync(id);
            if (!deleted)
                return NotFound(new { message = "Enrollment not found" });
            return Ok(new { deleted = true });
        }
    }
}
