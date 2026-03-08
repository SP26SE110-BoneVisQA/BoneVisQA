using System.Threading.Tasks;
using BoneVisQA.Repositories.Basic;
using BoneVisQA.Repositories.DBContext;
using BoneVisQA.Repositories.Interfaces;
using BoneVisQA.Repositories.Models;
using Microsoft.EntityFrameworkCore;

namespace BoneVisQA.Repositories.Services;

public class AuthRepository : GenericRepository<User>, IAuthRepository
{
    public AuthRepository(BoneVisQADbContext context) : base(context)
    {
    }

    public async Task<User?> GetByEmailAsync(string email)
    {
        return await _context.Users.AsNoTracking().FirstOrDefaultAsync(u => u.Email == email);
    }

    public async Task<User> CreateUserAsync(User user)
    {
        _context.Users.Add(user);
        await _context.SaveChangesAsync();
        return user;
    }

    public async Task UpdateUserAsync(User user)
    {
        _context.Users.Update(user);
        await _context.SaveChangesAsync();
    }

    public async Task<Role?> GetRoleByNameAsync(string roleName)
    {
        return await _context.Roles.AsNoTracking().FirstOrDefaultAsync(r => r.Name == roleName);
    }

    public async Task AddUserRoleAsync(UserRole userRole)
    {
        _context.UserRoles.Add(userRole);
        await _context.SaveChangesAsync();
    }
}

