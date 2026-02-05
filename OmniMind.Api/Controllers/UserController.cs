using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using OmniMind.Api.Swaggers;
using OmniMind.Contracts.User;
using OmniMind.Entities;
using OmniMind.Persistence.PostgreSql;
using System.Text.Json;

namespace App.Controllers
{
    /// <summary>
    /// 用户模块
    /// </summary>
    [ApiGroup(ApiGroupNames.USER)]
    [ApiController]
    [Route("api/[controller]")]
    public class UserController : BaseController
    {
        private readonly OmniMindDbContext _dbContext;
        private readonly ILogger<UserController> _logger;

        public UserController(OmniMindDbContext dbContext, ILogger<UserController> logger)
        {
            _dbContext = dbContext;
            _logger = logger;
        }

        /// <summary>
        /// 获取当前用户资料
        /// </summary>
        [HttpGet("profile")]
        [ProducesResponseType(typeof(UserProfileResponse), StatusCodes.Status200OK)]
        public async Task<IActionResult> GetUserProfile()
        {
            var userId = GetUserId();
            var user = await _dbContext.Users
                .Include(u => u.Profile)
                .FirstOrDefaultAsync(u => u.Id == userId);

            if (user == null)
            {
                return NotFound(new { message = "用户不存在" });
            }

            var response = new UserProfileResponse
            {
                UserId = user.Id,
                NickName = user.NickName,
                Picture = user.Picture,
                Gender = user.Gender != null ? (int)user.Gender : null,
                BirthDate = user.BirthDate,
                IsProfileCompleted = user.IsProfileCompleted,
                Industry = user.Profile?.Industry,
                Occupation = user.Profile?.Occupation,
                SourceChannel = user.Profile?.SourceChannel,
                Company = user.Profile?.Company,
                Position = user.Profile?.Position,
                Bio = user.Profile?.Bio,
                InterestTags = !string.IsNullOrEmpty(user.Profile?.InterestTags)
                    ? JsonSerializer.Deserialize<List<string>>(user.Profile.InterestTags)
                    : null,
                CompletedAt = user.Profile?.CompletedAt
            };

            return Ok(response);
        }

        /// <summary>
        /// 完善用户信息
        /// </summary>
        [HttpPost("profile/complete")]
        [ProducesResponseType(typeof(UserProfileResponse), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(object), StatusCodes.Status400BadRequest)]
        public async Task<IActionResult> CompleteUserProfile([FromBody] CompleteUserProfileRequest request)
        {
            var userId = GetUserId();
            var user = await _dbContext.Users
                .Include(u => u.Profile)
                .FirstOrDefaultAsync(u => u.Id == userId);

            if (user == null)
            {
                return NotFound(new { message = "用户不存在" });
            }

            var profile = user.Profile ?? new UserProfile { UserId = userId };

            profile.Industry = request.Industry;
            profile.Occupation = request.Occupation;
            profile.SourceChannel = request.SourceChannel;
            profile.Company = request.Company;
            profile.Position = request.Position;
            profile.Bio = request.Bio;
            profile.InterestTags = request.InterestTags != null && request.InterestTags.Count > 0
                ? JsonSerializer.Serialize(request.InterestTags)
                : null;
            profile.UpdatedAt = DateTime.UtcNow;

            // 首次完善信息时记录时间
            if (!user.IsProfileCompleted)
            {
                profile.CompletedAt = DateTime.UtcNow;
                user.IsProfileCompleted = true;
            }

            if (user.Profile == null)
            {
                _dbContext.UserProfiles.Add(profile);
                user.Profile = profile;
            }
            else
            {
                _dbContext.UserProfiles.Update(profile);
            }

            await _dbContext.SaveChangesAsync();

            _logger.LogInformation("[User] 用户完善信息: UserId={UserId}", userId);

            var response = new UserProfileResponse
            {
                UserId = user.Id,
                NickName = user.NickName,
                Picture = user.Picture,
                Gender = user.Gender != null ? (int)user.Gender : null,
                BirthDate = user.BirthDate,
                IsProfileCompleted = user.IsProfileCompleted,
                Industry = profile.Industry,
                Occupation = profile.Occupation,
                SourceChannel = profile.SourceChannel,
                Company = profile.Company,
                Position = profile.Position,
                Bio = profile.Bio,
                InterestTags = !string.IsNullOrEmpty(profile.InterestTags)
                    ? JsonSerializer.Deserialize<List<string>>(profile.InterestTags)
                    : null,
                CompletedAt = profile.CompletedAt
            };

            return Ok(response);
        }

        /// <summary>
        /// 检查用户是否完善信息
        /// </summary>
        [HttpGet("profile/status")]
        [ProducesResponseType(typeof(object), StatusCodes.Status200OK)]
        public async Task<IActionResult> GetProfileStatus()
        {
            var userId = GetUserId();
            var user = await _dbContext.Users
                .FirstOrDefaultAsync(u => u.Id == userId);

            if (user == null)
            {
                return NotFound(new { message = "用户不存在" });
            }

            return Ok(new
            {
                isProfileCompleted = user.IsProfileCompleted
            });
        }
    }
}
