using System.Threading.Tasks;
using BoneVisQA.Repositories.Models;

namespace BoneVisQA.Repositories.Interfaces;

public interface IAuthRepository
{
    Task<User?> GetByEmailAsync(string email);
    Task<User> CreateUserAsync(User user);
    Task UpdateUserAsync(User user);
    Task<Role?> GetRoleByNameAsync(string roleName);
    Task AddUserRoleAsync(UserRole userRole);

    /// <summary>
    /// Returns the names of all roles assigned to the given user.
    /// </summary>
    Task<IReadOnlyList<string>> GetRoleNamesForUserAsync(Guid userId);
}

