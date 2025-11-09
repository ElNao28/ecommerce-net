
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using ApiEcommerce.Models;
using ApiEcommerce.Models.Dtos;
using ApiEcommerce.Repository.IRepository;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;

namespace ApiEcommerce.Repository;

public class UserRepository : IUserRepository
{
    private readonly ApplicationDbContext _db;
    private string? secretKey;

    public UserRepository(ApplicationDbContext db, IConfiguration configuration)
    {
        _db = db;
        secretKey = configuration.GetValue<string>("ApiSettings:SecretKey");
    }
    public User? GetUser(int id)
    {
        return _db.Users.FirstOrDefault(u => u.Id == id);
    }
    public ICollection<User> GetUsers()
    {
        return _db.Users.OrderBy(u => u.Id).ToList();
    }
    public bool IsUniqueUser(string username)
    {
        return !_db.Users.Any(u => u.Username.ToLower().Trim() == username.ToLower().Trim());
    }
    public async Task<UserLoginResponseDto> Login(UserLoginDto userLogin)
    {
        if (string.IsNullOrEmpty(userLogin.Username))
        {
            return new UserLoginResponseDto()
            {
                Token = "",
                User = null,
                Message = "Username is required"
            };
        }
        var user = await _db.Users.FirstOrDefaultAsync(u => u.Username.ToLower().Trim() == userLogin.Username.ToLower().Trim());
        if (user == null)
        {
            return new UserLoginResponseDto()
            {
                Token = "",
                User = null,
                Message = "Username not found"
            };
        }
        if (!BCrypt.Net.BCrypt.Verify(userLogin.Password, user.Password))
        {
            return new UserLoginResponseDto()
            {
                Token = "",
                User = null,
                Message = "Credentials are incorrect"
            };
        }
        if (string.IsNullOrWhiteSpace(secretKey))
        {
            throw new InvalidOperationException("Secret key is not configured");
        }
        //generate token
        var handlerToken = new JwtSecurityTokenHandler();
        var key = Encoding.UTF8.GetBytes(secretKey);
        var tokenDescriptor = new SecurityTokenDescriptor
        {
            Subject = new ClaimsIdentity(new[]
            {
                new Claim("id", user.Id.ToString()),
                new Claim("username", user.Username),
                new Claim("role", user.Role ?? "No role"),
            }),
            Expires = DateTime.UtcNow.AddHours(2),
            SigningCredentials = new SigningCredentials(new SymmetricSecurityKey(key), SecurityAlgorithms.HmacSha256Signature)
        };
        var token = handlerToken.CreateToken(tokenDescriptor);
        return new UserLoginResponseDto()
        {
            Token = handlerToken.WriteToken(token),
            User = new UserRegisterDto()
            {
                Username = user.Username,
                Name = user.Name,
                Role = user.Role,
                Password = user.Password ?? ""
            },
            Message = "Login successful"
        };
    }
    public async Task<User> Register(CreateUserDto createUser)
    {
        var encriptedPassword = BCrypt.Net.BCrypt.HashPassword(createUser.Password);
        var user = new User()
        {
            Username = createUser.Username ?? "No username",
            Name = createUser.Name,
            Password = encriptedPassword,
            Role = createUser.Role
        };
        _db.Users.Add(user);

        await _db.SaveChangesAsync();
        return user;
    }
}
