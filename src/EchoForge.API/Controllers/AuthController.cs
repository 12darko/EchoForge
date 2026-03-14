using EchoForge.Core.DTOs;
using EchoForge.Infrastructure.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Threading.Tasks;

namespace EchoForge.API.Controllers;

[ApiController]
[Route("api/[controller]")]
public class AuthController : ControllerBase
{
    private readonly EchoForgeDbContext _dbContext;

    public AuthController(EchoForgeDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    [HttpPost("login")]
    public async Task<IActionResult> Login([FromBody] LoginRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Username) || string.IsNullOrWhiteSpace(request.Password))
            return BadRequest(new AuthResponse { Success = false, Message = "Username and password are required." });

        var user = await _dbContext.Users.FirstOrDefaultAsync(u => u.Username == request.Username);
        
        if (user == null)
            return Unauthorized(new AuthResponse { Success = false, Message = "Invalid username or password." });

        if (!user.IsActive)
            return Unauthorized(new AuthResponse { Success = false, Message = "Your account has been deactivated. Please contact the administrator." });

        if (!BCrypt.Net.BCrypt.Verify(request.Password, user.PasswordHash))
            return Unauthorized(new AuthResponse { Success = false, Message = "Invalid username or password." });

        // Update Last Login
        user.LastLoginAt = System.DateTime.UtcNow;
        await _dbContext.SaveChangesAsync();

        // Normally you would generate a JWT here. 
        // For WPF local authentication, returning Success = true is often enough.
        return Ok(new AuthResponse 
        { 
            Success = true, 
            Message = "Login successful.", 
            UserId = user.Id,
            IsAdmin = user.IsAdmin,
            Token = "eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9.dummy_token_for_wpf" // Placeholder
        });
    }

    [HttpPost("register")]
    public async Task<IActionResult> Register([FromBody] LoginRequest request)
    {
        // Simple registration for demo/initial setup
        if (string.IsNullOrWhiteSpace(request.Username) || string.IsNullOrWhiteSpace(request.Password))
            return BadRequest(new AuthResponse { Success = false, Message = "Username and password are required." });

        if (await _dbContext.Users.AnyAsync(u => u.Username == request.Username))
            return BadRequest(new AuthResponse { Success = false, Message = "Username already exists." });

        var user = new EchoForge.Core.Models.User
        {
            Username = request.Username,
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(request.Password),
            Email = $"{request.Username}@echoforge.local",
            IsActive = true
        };

        _dbContext.Users.Add(user);
        await _dbContext.SaveChangesAsync();

        return Ok(new AuthResponse { Success = true, Message = "Registration successful." });
    }
}
