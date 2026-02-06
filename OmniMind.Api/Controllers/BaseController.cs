using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace App.Controllers
{
    /// <summary>
    /// ID 生成器
    /// </summary>
    public static class IdGenerator
    {
        /// <summary>
        /// 生成新的有序 GUID (UUID v7)
        /// </summary>
        public static string NewId() => Guid.CreateVersion7().ToString();
    }

    /// <summary>
    /// Controller 基类，提供通用的辅助方法
    /// </summary>
    [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
    public abstract class BaseController : ControllerBase
    {
        /// <summary>
        /// 获取当前用户ID
        /// </summary>
        /// <exception cref="UnauthorizedAccessException">当无法从 Token 中获取用户信息时抛出</exception>
        protected string GetUserId()
        {
            var userId = User.FindFirst("sub")?.Value
                      ?? User.FindFirst(ClaimTypes.NameIdentifier)?.Value;

            if (string.IsNullOrWhiteSpace(userId))
            {
                throw new UnauthorizedAccessException("无法获取用户信息");
            }
            return userId;
        }

        /// <summary>
        /// 尝试获取当前用户ID
        /// </summary>
        /// <returns>如果成功返回用户ID，否则返回 null</returns>
        protected string? TryGetUserId()
        {
            return User.FindFirst("sub")?.Value
                ?? User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        }

        /// <summary>
        /// 获取当前用户名
        /// </summary>
        protected string? GetUserName()
        {
            return User.FindFirst("username")?.Value
                ?? User.FindFirst(ClaimTypes.Name)?.Value;
        }

        /// <summary>
        /// 获取当前用户昵称
        /// </summary>
        protected string? GetNickname()
        {
            return User.FindFirst("nickname")?.Value;
        }

        /// <summary>
        /// 获取当前用户手机号
        /// </summary>
        protected string? GetPhoneNumber()
        {
            return User.FindFirst("phone_number")?.Value
                ?? User.FindFirst(ClaimTypes.MobilePhone)?.Value;
        }
    }
}
