using Duende.IdentityModel;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using OmniMind.Api.Swaggers;
using OmniMind.Entities;
using OmniMind.Persistence.PostgreSql;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;

namespace App.Controllers
{
    [ApiGroup(ApiGroupNames.USER)]
    [ApiController]
    [Route("api/[controller]")]
    public class AuthController : ControllerBase
    {
        private readonly UserManager<User> userManager;
        private readonly SignInManager<User> signInManager;
        private readonly IConfiguration configuration;
        private readonly OmniMindDbContext dbContext;

        public AuthController(
            UserManager<User> userManager,
            SignInManager<User> signInManager,
            IConfiguration configuration,
            OmniMindDbContext dbContext)
        {
            this.userManager = userManager;
            this.signInManager = signInManager;
            this.configuration = configuration;
            this.dbContext = dbContext;
        }

        [HttpPost("signIn")]
        public async Task<IActionResult> SignIn([FromBody] SignInRequest request)
        {
            if (string.IsNullOrWhiteSpace(request.Username) || string.IsNullOrWhiteSpace(request.Password))
            {
                return BadRequest(new { message = "用户名和密码不能为空" });
            }

            var normalized = request.Username.Trim();
            var user = await userManager.Users.FirstOrDefaultAsync(u =>
                u.UserName == normalized ||
                u.Email == normalized ||
                u.PhoneNumber == normalized);

            if (user == null)
            {
                return BadRequest(new { message = "用户名或密码错误" });
            }

            var result = await signInManager.CheckPasswordSignInAsync(user, request.Password, lockoutOnFailure: false);
            if (!result.Succeeded)
            {
                return BadRequest(new { message = "用户名或密码错误" });
            }

            user.LastSignDate = DateTime.UtcNow;
            await userManager.UpdateAsync(user);

            var (token, jti) = GenerateJwtToken(user);
            var refreshToken = await GenerateRefreshTokenAsync(user, jti);

            return Ok(new
            {
                token,
                refreshToken = refreshToken.Token,
                expiresIn = 7 * 24 * 60 * 60,
                user = new
                {
                    user.Id,
                    user.UserName,
                    user.NickName,
                    user.PhoneNumber,
                    user.Email,
                    user.Picture,
                    user.DateCreated,
                    user.LastSignDate
                }
            });
        }

        [HttpPost("refreshToken")]
        public async Task<IActionResult> RefreshToken([FromBody] RefreshTokenRequest request)
        {
            if (string.IsNullOrWhiteSpace(request.Token) || string.IsNullOrWhiteSpace(request.RefreshToken))
            {
                return BadRequest(new { message = "Token和RefreshToken不能为空" });
            }

            var storedRefreshToken = await dbContext.RefreshTokens
                .FirstOrDefaultAsync(rt => rt.Token == request.RefreshToken);

            if (storedRefreshToken == null)
            {
                return BadRequest(new { message = "RefreshToken无效" });
            }

            if (storedRefreshToken.IsUsed || storedRefreshToken.IsRevoked)
            {
                return BadRequest(new { message = "RefreshToken已使用或已撤销" });
            }

            if (storedRefreshToken.ExpiresAt < DateTime.UtcNow)
            {
                return BadRequest(new { message = "RefreshToken已过期" });
            }

            var user = await userManager.FindByIdAsync(storedRefreshToken.UserId);
            if (user == null)
            {
                return BadRequest(new { message = "用户不存在" });
            }

            var tokenHandler = new JwtSecurityTokenHandler();
            if (!tokenHandler.CanReadToken(request.Token))
            {
                return BadRequest(new { message = "Token格式无效" });
            }

            var jwtToken = tokenHandler.ReadJwtToken(request.Token);
            if (storedRefreshToken.JwtId != jwtToken.Id)
            {
                return BadRequest(new { message = "Token与RefreshToken不匹配" });
            }

            storedRefreshToken.IsUsed = true;

            var (newToken, newJti) = GenerateJwtToken(user);
            var newRefreshToken = await GenerateRefreshTokenAsync(user, newJti);
            storedRefreshToken.ReplacedByTokenId = newRefreshToken.Id;

            await dbContext.SaveChangesAsync();

            return Ok(new
            {
                token = newToken,
                refreshToken = newRefreshToken.Token,
                expiresIn = 7 * 24 * 60 * 60
            });
        }

        [HttpPost("revokeToken")]
        public async Task<IActionResult> RevokeToken([FromBody] RevokeTokenRequest request)
        {
            if (string.IsNullOrWhiteSpace(request.RefreshToken))
            {
                return BadRequest(new { message = "RefreshToken不能为空" });
            }

            var storedRefreshToken = await dbContext.RefreshTokens
                .FirstOrDefaultAsync(rt => rt.Token == request.RefreshToken);

            if (storedRefreshToken == null)
            {
                return BadRequest(new { message = "RefreshToken无效" });
            }

            await RevokeRefreshTokenChain(storedRefreshToken);
            return Ok(new { message = "Token已撤销" });
        }

        private async Task<RefreshToken> GenerateRefreshTokenAsync(User user, string jwtId)
        {
            var refreshToken = new RefreshToken
            {
                Token = Guid.NewGuid().ToString("N"),
                JwtId = jwtId,
                UserId = user.Id,
                CreatedAt = DateTime.UtcNow,
                ExpiresAt = DateTime.UtcNow.AddDays(30),
                DeviceInfo = Request.Headers["User-Agent"].ToString()
            };

            dbContext.RefreshTokens.Add(refreshToken);
            await dbContext.SaveChangesAsync();

            return refreshToken;
        }

        private async Task RevokeRefreshTokenChain(RefreshToken refreshToken)
        {
            refreshToken.IsRevoked = true;
            refreshToken.RevokedAt = DateTime.UtcNow;

            if (!string.IsNullOrEmpty(refreshToken.ReplacedByTokenId))
            {
                var nextToken = await dbContext.RefreshTokens.FindAsync(refreshToken.ReplacedByTokenId);
                if (nextToken != null)
                {
                    await RevokeRefreshTokenChain(nextToken);
                }
            }

            await dbContext.SaveChangesAsync();
        }

        private (string token, string jti) GenerateJwtToken(User user)
        {
            var jti = Guid.NewGuid().ToString();
            var claims = new List<Claim>
            {
                new Claim(JwtClaimTypes.Name, user.UserName ?? string.Empty),
                new Claim(JwtRegisteredClaimNames.Sub, user.Id),
                new Claim(JwtRegisteredClaimNames.Jti, jti),
                new Claim("nickname", user.NickName ?? string.Empty),
                new Claim("username", user.UserName ?? string.Empty)
            };

            if (!string.IsNullOrWhiteSpace(user.PhoneNumber))
            {
                claims.Add(new Claim(JwtClaimTypes.PhoneNumber, user.PhoneNumber));
            }

            if (!string.IsNullOrWhiteSpace(user.Email))
            {
                claims.Add(new Claim(JwtClaimTypes.Email, user.Email));
            }

            var secretKey = configuration["JwtBearerOptions:SecretKey"]
                ?? "OMNIMIND_2026_01_31_milo_RAG_OCR_OMNIMIND";
            var issuer = configuration["JwtBearerOptions:TokenValidationParameters:ValidIssuer"]
                ?? configuration["JwtBearerOptions:ValidIssuer"]
                ?? "OMNIMIND";
            var audience = configuration["JwtBearerOptions:TokenValidationParameters:ValidAudience"]
                ?? configuration["JwtBearerOptions:ValidAudience"]
                ?? "OMNIMIND";

            var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secretKey));
            var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

            var token = new JwtSecurityToken(
                issuer: issuer,
                audience: audience,
                claims: claims,
                expires: DateTime.UtcNow.AddDays(7),
                signingCredentials: creds);

            return (new JwtSecurityTokenHandler().WriteToken(token), jti);
        }
    }

    public sealed record SignInRequest
    {
        public string Username { get; init; } = string.Empty;
        public string Password { get; init; } = string.Empty;
    }

    public sealed record RefreshTokenRequest
    {
        public string Token { get; init; } = string.Empty;
        public string RefreshToken { get; init; } = string.Empty;
    }

    public sealed record RevokeTokenRequest
    {
        public string RefreshToken { get; init; } = string.Empty;
    }
}
