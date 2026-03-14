using EchoForge.Infrastructure.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace EchoForge.API.Controllers;

[ApiController]
[Route("api/[controller]")]
public class UsersController : ControllerBase
{
    private readonly EchoForgeDbContext _dbContext;

    public UsersController(EchoForgeDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    /// <summary>
    /// List all users (admin management)
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> GetAll()
    {
        var users = await _dbContext.Users
            .Select(u => new
            {
                u.Id,
                u.Username,
                u.Email,
                u.IsActive,
                u.CreatedAt,
                u.LastLoginAt
            })
            .OrderByDescending(u => u.CreatedAt)
            .ToListAsync();

        return Ok(users);
    }

    /// <summary>
    /// Toggle user active status (enable/disable access)
    /// </summary>
    [HttpPut("{id}/toggle-active")]
    public async Task<IActionResult> ToggleActive(int id)
    {
        var user = await _dbContext.Users.FindAsync(id);
        if (user == null) return NotFound(new { Message = "User not found." });

        user.IsActive = !user.IsActive;
        await _dbContext.SaveChangesAsync();

        return Ok(new
        {
            user.Id,
            user.Username,
            user.IsActive,
            Message = user.IsActive ? "User activated." : "User deactivated."
        });
    }

    /// <summary>
    /// Delete a user entirely
    /// </summary>
    [HttpDelete("{id}")]
    public async Task<IActionResult> Delete(int id)
    {
        var user = await _dbContext.Users.FindAsync(id);
        if (user == null) return NotFound(new { Message = "User not found." });

        _dbContext.Users.Remove(user);
        await _dbContext.SaveChangesAsync();

        return Ok(new { Message = $"User '{user.Username}' deleted." });
    }
}
