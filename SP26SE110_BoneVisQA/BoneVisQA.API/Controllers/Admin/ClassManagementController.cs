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

        [HttpGet("classes")]
        public async Task<IActionResult> GetAll(int pageIndex = 1,int pageSize = 10)
        {
            var result = await _classservice.GetAcademicClassAsync(pageIndex, pageSize);

            return Ok(result);
        }

        [HttpPost("classes")]
        public async Task<IActionResult> Create(CreateClassManagementDTO dto)
        {
            var result = await _classservice.CreateAcademicClassAsync(dto);

            return Ok(result);  
        }

        [HttpPut("classes")]
        public async Task<IActionResult> Update(UpdateClassManagementDTO dto)
        {
            var result = await _classservice.UpdateAcademicClassAsync(dto);

            return Ok(result);
        }

        [HttpDelete("classes/{id}")]
        public async Task<IActionResult> Delete(Guid id)
        {
            var result = await _classservice.DeleteAcademicClassAsync(id);

            if (!result)
                return NotFound("Class not found");

            return Ok(result);
        }



        [HttpGet("assign")]
        public async Task<IActionResult> GetAssignClass(int pageIndex = 1,int pageSize = 10)
        {
            var result = await _classservice.GetAssignClassAsync(pageIndex, pageSize);

            return Ok(result);
        }

        [HttpPost("assign")]
        public async Task<IActionResult> AssignClass(AssignClassDTO dto)
        {
            var result = await _classservice.AssignClassAsync(dto);

            return Ok(result);
        }

        [HttpPut("assign")]
        public async Task<IActionResult> UpdateAssignClass(AssignClassDTO dto)
        {
            var result = await _classservice.UpdateAssignClassAsync(dto);

            return Ok(result);
        }

        [HttpDelete("{id}")]
        public async Task<IActionResult> DeleteAssignClass(Guid id)
        {
            var deleted = await _classservice.DeleteAssignClassAsync(id);

            if (!deleted)
            {
                return NotFound("Assignment not found");
            }

            return Ok(deleted);
        }
    }
}
