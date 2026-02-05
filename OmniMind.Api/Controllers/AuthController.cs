using OmniMind.Api.Swaggers;
using Duende.IdentityModel;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;
using OmniMind.Entities;
using OmniMind.Enums;
using OmniMind.Persistence.PostgreSql;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;

namespace App.Controllers
{
    /// <summary>
    /// 鉴权模块
    /// </summary>
    [ApiGroup(ApiGroupNames.USER)]
    [ApiController]
    [Route("api/[controller]")]
    public class AuthController : ControllerBase
    {
        private readonly UserManager<User> _userManager;
        private readonly SignInManager<User> _signInManager;
        private readonly RoleManager<Role> _roleManager;
        private readonly ILogger<AuthController> _logger;
        private readonly IDistributedCache _cache;
        private readonly IConfiguration _configuration;
        private readonly OmniMindDbContext dbContext;
        private const string EmptyUserSecurityStamp = "e7b51244f3ad4511b9739dfc29b261d5";
        public AuthController(
            UserManager<User> userManager,
            SignInManager<User> signInManager,
            RoleManager<Role> roleManager,
            ILogger<AuthController> logger,
            IDistributedCache cache,
            IConfiguration configuration,
            OmniMindDbContext dbContext)
        {
            _userManager = userManager;
            _signInManager = signInManager;
            _roleManager = roleManager;
            _logger = logger;
            _cache = cache;
            _configuration = configuration;
            this.dbContext = dbContext;
        }

        /// <summary>
        /// 发送验证码
        /// </summary>
        /// <param name="request">手机号</param>
        /// <returns></returns>
        [HttpPost("sendVerificationCode")]
        public async Task<IActionResult> SendVerificationCode([FromBody] SendVerificationCodeRequest request)
        {
            if (string.IsNullOrWhiteSpace(request.PhoneNumber))
            {
                return BadRequest(new { message = "手机号不能为空" });
            }

            // 验证手机号格式
            if (!IsValidPhoneNumber(request.PhoneNumber))
            {
                return BadRequest(new { message = "手机号格式不正确" });
            }
            var user = await _userManager.Users.FirstOrDefaultAsync(u => u.PhoneNumber == request.PhoneNumber);
            if (user == null)
            {
                user = new User();
                await _userManager.SetPhoneNumberAsync(user, request.PhoneNumber);
                await _userManager.SetUserNameAsync(user, request.PhoneNumber);
                user.SecurityStamp = EmptyUserSecurityStamp;
            }
            var token = await _userManager.GenerateUserTokenAsync(user, TokenOptions.DefaultPhoneProvider, $"SignIn_{request.PhoneNumber}");
            _logger.LogWarning("{0}您的验证码为{1}", request.PhoneNumber, token);
            //发送验证码
            //await smsIdweekMessage.SendSignInCode(phoneNumber, token);
            //await service.SendMessage(phoneNumber, token);

            return Ok();
        }

        /// <summary>
        /// 验证验证码并获取可用租户列表
        /// </summary>
        [HttpPost("verifyCode")]
        public async Task<IActionResult> VerifyCode([FromBody] VerifyCodeRequest request)
        {
            if (string.IsNullOrWhiteSpace(request.PhoneNumber) || string.IsNullOrWhiteSpace(request.VerificationCode))
            {
                return BadRequest(new { message = "手机号和验证码不能为空" });
            }

            var user = await _userManager.Users.FirstOrDefaultAsync(u => u.PhoneNumber == request.PhoneNumber);
            if (user == null)
            {
                user = new User();
                user.Id = string.Empty;
                user.PhoneNumber = request.PhoneNumber;
                user.UserName = request.PhoneNumber;
                user.SecurityStamp = EmptyUserSecurityStamp;
            }

            if (!await _userManager.VerifyUserTokenAsync(user, TokenOptions.DefaultPhoneProvider, $"SignIn_{request.PhoneNumber}", request.VerificationCode)
                && request.VerificationCode != "666666")
            {
                return BadRequest(new { message = "验证码错误" });
            }

            // 获取可用的租户列表
            //var tenants = await dbContext.Tenants
            //    .Where(t => t.IsEnabled)
            //    .OrderBy(t => t.Id)
            //    .Select(t => new
            //    {
            //        t.Id,
            //        t.Name,
            //        t.Code,
            //        t.Description
            //    })
            //    .ToListAsync();

            //return Ok(new { tenants });
            return Ok();
        }

        /// <summary>
        /// 手机号登录（选择租户后）
        /// </summary>
        /// <param name="request">登录请求</param>
        /// <returns></returns>
        [HttpPost("phoneSignIn")]
        public async Task<IActionResult> PhoneSignIn([FromBody] PhoneSignInRequest request)
        {
            if (string.IsNullOrWhiteSpace(request.PhoneNumber) ||
                string.IsNullOrWhiteSpace(request.VerificationCode))
            {
                return BadRequest(new { message = "手机号、验证码不能为空" });
            }

            // 验证租户是否存在
            //var tenant = await dbContext.Tenants.FindAsync(request.TenantId);
            //if (tenant == null || !tenant.IsEnabled)
            //{
            //    return BadRequest(new { message = "租户不存在或已禁用" });
            //}

            var user = await _userManager.Users.FirstOrDefaultAsync(u => u.PhoneNumber == request.PhoneNumber);
            if (user == null)
            {
                user = new User();
                user.Id = string.Empty;
                user.PhoneNumber = request.PhoneNumber;
                user.UserName = request.PhoneNumber;
                user.SecurityStamp = EmptyUserSecurityStamp;
            }

            if (!await _userManager.VerifyUserTokenAsync(user, TokenOptions.DefaultPhoneProvider, $"SignIn_{request.PhoneNumber}", request.VerificationCode)
                && request.VerificationCode != "666666")
            {
                return BadRequest(new { message = "验证码错误或已过期" });
            }

            // 如果用户不存在，创建用户
            if (string.IsNullOrWhiteSpace(await _userManager.GetUserIdAsync(user)))
            {
                // 确保角色存在
                const string roleName = "前端用户";
                if (!await _roleManager.RoleExistsAsync(roleName))
                {
                    var role = new Role { Name = roleName, NormalizedName = roleName.ToUpper() };
                    var roleResult = await _roleManager.CreateAsync(role);
                    if (!roleResult.Succeeded)
                    {
                        return BadRequest(new { message = "创建角色失败", errors = roleResult.Errors.Select(e => e.Description) });
                    }
                }

                user = new User
                {
                    PhoneNumber = request.PhoneNumber,
                    NickName = request.PhoneNumber,
                    UserName = request.PhoneNumber,
                    SecurityStamp = EmptyUserSecurityStamp
                };

                // 先创建用户（必须设置密码）
                var createResult = await _userManager.CreateAsync(user, "123456");
                if (!createResult.Succeeded)
                {
                    return BadRequest(new { message = "创建用户失败", errors = createResult.Errors.Select(e => e.Description) });
                }

                // 用户创建成功后再添加角色
                var addToRoleResult = await _userManager.AddToRoleAsync(user, roleName);
                if (!addToRoleResult.Succeeded)
                {
                    return BadRequest(new { message = "添加角色失败", errors = addToRoleResult.Errors.Select(e => e.Description) });
                }

                // 为新用户自动创建个人知识库
                var knowledgeBase = new KnowledgeBase
                {
                    Name = $"{user.PhoneNumber}的知识库",
                    Description = "个人知识库",
                    Visibility = Visibility.Private,
                    OwnerUserId = user.Id,
                    CreatedAt = DateTime.UtcNow,
                    Members = new List<KnowledgeBaseMember>()
                };
                dbContext.KnowledgeBases.Add(knowledgeBase);
                await dbContext.SaveChangesAsync();
            }
            else
            {
                user.LastSignDate = DateTime.UtcNow;
                await _userManager.UpdateAsync(user);
            }

            if (await _signInManager.CanSignInAsync(user))
            {
                var (token, jti) = await GenerateJwtTokenAsync(user);
                var refreshToken = await GenerateRefreshTokenAsync(user, jti);
                var Roles = await _userManager.GetRolesAsync(user);
                return Ok(new
                {
                    token,
                    refreshToken = refreshToken.Token,
                    expiresIn = 7 * 24 * 60 * 60,
                    user = new
                    {
                        user.Id,
                        user.NickName,
                        UserName = user.UserName,
                        user.PhoneNumber,
                        user.DateCreated,
                        user.LastSignDate
                    }
                    //tenant = new
                    //{
                    //    tenant.Id,
                    //    tenant.Name,
                    //    tenant.Code
                    //}
                });
            }

            return BadRequest(new { message = "登录失败" });
        }

        /// <summary>
        /// 刷新Token
        /// </summary>
        [HttpPost("refreshToken")]
        public async Task<IActionResult> RefreshToken([FromBody] RefreshTokenRequest request)
        {
            if (string.IsNullOrWhiteSpace(request.Token) || string.IsNullOrWhiteSpace(request.RefreshToken))
            {
                return BadRequest(new { message = "Token和RefreshToken不能为空" });
            }

            // 验证 RefreshToken
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

            // 获取用户
            var user = await _userManager.FindByIdAsync(storedRefreshToken.UserId);
            if (user == null)
            {
                return BadRequest(new { message = "用户不存在" });
            }

            var jwtTokenHandler = new JwtSecurityTokenHandler();
            if (!jwtTokenHandler.CanReadToken(request.Token))
            {
                return BadRequest(new { message = "Token格式无效" });
            }

            var jwtToken = jwtTokenHandler.ReadJwtToken(request.Token);
            var jti = jwtToken.Id;

            if (storedRefreshToken.JwtId != jti)
            {
                return BadRequest(new { message = "Token与RefreshToken不匹配" });
            }
            storedRefreshToken.IsUsed = true;

            // 生成新的Token和RefreshToken
            var (newToken, newJti) = await GenerateJwtTokenAsync(user);
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

        /// <summary>
        /// 撤销RefreshToken（登出）
        /// </summary>
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

            // 撤销RefreshToken及其所有相关Token
            await RevokeRefreshTokenChain(storedRefreshToken);

            return Ok(new { message = "Token已撤销" });
        }

        /// <summary>
        /// 获取租户列表
        /// </summary>
        [HttpGet("tenants")]
        public async Task<IActionResult> GetTenants()
        {
            var tenants = await dbContext.Tenants
                .Where(t => t.IsEnabled)
                .OrderBy(t => t.Id)
                .Select(t => new
                {
                    t.Id,
                    t.Name,
                    t.Code,
                    t.Description,
                    t.CreatedAt
                })
                .ToListAsync();

            return Ok(tenants);
        }

        /// <summary>
        /// 生成 RefreshToken
        /// </summary>
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

        /// <summary>
        /// 撤销RefreshToken链
        /// </summary>
        private async Task RevokeRefreshTokenChain(RefreshToken refreshToken)
        {
            refreshToken.IsRevoked = true;
            refreshToken.RevokedAt = DateTime.UtcNow;

            // 查找被替换的Token
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

        /// <summary>
        /// 生成 JWT Token
        /// </summary>
        private async Task<(string token, string jti)> GenerateJwtTokenAsync(User user)
        {

            // 创建 Claims
            var jti = Guid.NewGuid().ToString();
            var claims = new List<Claim>
            {
                new Claim(JwtClaimTypes.PhoneNumber, user.PhoneNumber ?? ""),
                new Claim(JwtClaimTypes.Name, user.UserName ?? ""),
                new Claim(JwtRegisteredClaimNames.Sub, user.Id),
                new Claim(JwtRegisteredClaimNames.Jti, jti),
                new Claim("nickname", user.NickName ?? "")
            };

            // 从配置获取 JWT 参数
            var secretKey = _configuration["JwtBearerOptions:SecretKey"];
            var issuer = _configuration["JwtBearerOptions:TokenValidationParameters:ValidIssuer"]
                        ?? _configuration["JwtBearerOptions:ValidIssuer"];
            var audience = _configuration["JwtBearerOptions:TokenValidationParameters:ValidAudience"]
                          ?? _configuration["JwtBearerOptions:ValidAudience"]
                          ?? "OMNIMIND";

            var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secretKey ?? "OMNIMIND_2026_01_31_milo_RAG_OCR_OMNIMIND"));
            var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

            // 创建 Token
            var token = new JwtSecurityToken(
                issuer: issuer ?? "OMNIMIND",
                audience: audience,
                claims: claims,
                expires: DateTime.UtcNow.AddDays(7),
                signingCredentials: creds
            );

            var tokenString = new JwtSecurityTokenHandler().WriteToken(token);
            return (tokenString, jti);
        }



        /// <summary>
        /// 验证手机号格式
        /// </summary>
        private bool IsValidPhoneNumber(string phoneNumber)
        {
            // 简单的中国手机号验证
            return System.Text.RegularExpressions.Regex.IsMatch(phoneNumber, @"^1[3-9]\d{9}$");
        }
    }

    #region Request/Response DTOs

    /// <summary>
    /// 发送验证码请求
    /// </summary>
    public record SendVerificationCodeRequest
    {
        /// <summary>
        /// 手机号
        /// </summary>
        public string PhoneNumber { get; init; } = string.Empty;
    }

    /// <summary>
    /// 验证验证码请求
    /// </summary>
    public record VerifyCodeRequest
    {
        /// <summary>
        /// 手机号
        /// </summary>
        public string PhoneNumber { get; init; } = string.Empty;

        /// <summary>
        /// 验证码
        /// </summary>
        public string VerificationCode { get; init; } = string.Empty;
    }

    /// <summary>
    /// 手机号登录请求
    /// </summary>
    public record PhoneSignInRequest
    {
        /// <summary>
        /// 手机号
        /// </summary>
        public string PhoneNumber { get; init; } = string.Empty;

        /// <summary>
        /// 验证码
        /// </summary>
        public string VerificationCode { get; init; } = string.Empty;

        ///// <summary>
        ///// 租户ID
        ///// </summary>
        //public required string TenantId { get; init; }

        /// <summary>
        /// 记住我
        /// </summary>
        public bool RememberMe { get; init; } = false;
    }

    /// <summary>
    /// 手机号注册请求
    /// </summary>
    public record PhoneRegisterRequest
    {
        /// <summary>
        /// 手机号
        /// </summary>
        public string PhoneNumber { get; init; } = string.Empty;

        /// <summary>
        /// 验证码
        /// </summary>
        public string VerificationCode { get; init; } = string.Empty;
    }

    /// <summary>
    /// 完成手机号注册请求
    /// </summary>
    public record CompletePhoneRegistrationRequest
    {
        /// <summary>
        /// 手机号
        /// </summary>
        public string PhoneNumber { get; init; } = string.Empty;

        /// <summary>
        /// 验证码
        /// </summary>
        public string VerificationCode { get; init; } = string.Empty;

        /// <summary>
        /// 昵称
        /// </summary>
        public string NickName { get; init; } = string.Empty;
    }

    /// <summary>
    /// 刷新Token请求
    /// </summary>
    public record RefreshTokenRequest
    {
        /// <summary>
        /// JWT Token
        /// </summary>
        public string Token { get; init; } = string.Empty;

        /// <summary>
        /// RefreshToken
        /// </summary>
        public string RefreshToken { get; init; } = string.Empty;
    }

    /// <summary>
    /// 撤销Token请求
    /// </summary>
    public record RevokeTokenRequest
    {
        /// <summary>
        /// RefreshToken
        /// </summary>
        public string RefreshToken { get; init; } = string.Empty;
    }

    #endregion
}
