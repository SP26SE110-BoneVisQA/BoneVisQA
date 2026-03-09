using BoneVisQA.Services.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace BoneVisQA.API.Controllers
{
  //  [Authorize(Roles = "Admin")]
    [ApiController]
    [Route("api/admin/users")]
    public class AdminController : ControllerBase
    {
        private readonly IUserManagementService _service;

        public AdminController(IUserManagementService service)
        {
            _service = service;
        }

        [HttpGet("role/{role}")]
        public async Task<IActionResult> GetUsersByRole(string role)
        {
            var users = await _service.GetUserByRoleAsync(role);
            return Ok(users);
        }

        [HttpPut("{id}/activate")]
        public async Task<IActionResult> Activate(Guid id)
        {
            await _service.ActivateUserAccountAsync(id);
            return Ok();
        }

        [HttpPut("{id}/deactivate")]
        public async Task<IActionResult> Deactivate(Guid id)
        {
            await _service.DeactivateUserAccountAsync(id);
            return Ok();
        }

        [HttpPost("{id}/assign-role")]
        public async Task<IActionResult> AssignRole(Guid id, string role)
        {
            await _service.AssignRoleAsync(id, role);
            return Ok();
        }

        [HttpDelete("{id}/revoke-role")]
        public async Task<IActionResult> RevokeRole(Guid id, string role)
        {
            await _service.RevokeRoleAsync(id, role);
            return Ok();
        }
    }
}
