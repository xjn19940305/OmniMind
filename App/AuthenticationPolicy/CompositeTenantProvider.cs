using Microsoft.AspNetCore.Http;
using OmniMind.Abstractions.Tenant;
using System.Security.Claims;
using System.Threading;

namespace App.AuthenticationPolicy
{
    /// <summary>
    /// 复合租户提供者 - 同时支持 HTTP 上下文和手动设置
    /// 优先级：手动设置 > HTTP 上下文
    ///
    /// 线程安全性：
    /// - HTTP 场景：每个请求独立的 HttpContext，天然隔离
    /// - 消息队列场景：使用 AsyncLocal + Scoped 注册，每个消息的租户上下文完全隔离
    /// </summary>
    public sealed class CompositeTenantProvider : ITenantProvider
    {
        private readonly IHttpContextAccessor? _httpContextAccessor;
        private readonly AsyncLocal<string?> _manualTenantId = new();

        public CompositeTenantProvider(IHttpContextAccessor? httpContextAccessor)
        {
            _httpContextAccessor = httpContextAccessor;
        }

        public bool IsResolved => !string.IsNullOrWhiteSpace(TenantId);

        public string TenantId
        {
            get
            {
                // 优先返回手动设置的租户 ID
                if (!string.IsNullOrWhiteSpace(_manualTenantId.Value))
                {
                    return _manualTenantId.Value;
                }

                // 回退到 HTTP 上下文
                if (_httpContextAccessor?.HttpContext != null)
                {
                    var ctx = _httpContextAccessor.HttpContext;

                    // 1. JWT
                    var user = ctx.User;
                    if (user?.Identity?.IsAuthenticated == true)
                    {
                        var claim = user.FindFirstValue("tenant_id") ?? user.FindFirstValue("tenantId") ?? user.FindFirstValue("tid");
                        if (!string.IsNullOrWhiteSpace(claim))
                        {
                            return claim.Trim();
                        }
                    }

                    // 2. Header
                    if (ctx.Request.Headers.TryGetValue("X-Tenant-Id", out var headerVal))
                    {
                        var v = headerVal.ToString();
                        if (!string.IsNullOrWhiteSpace(v))
                        {
                            return v.Trim();
                        }
                    }
                }

                return string.Empty;
            }
        }

        /// <summary>
        /// 手动设置租户 ID（用于消息队列、后台任务等非 HTTP 场景）
        ///
        /// 线程安全性：AsyncLocal 确保每个异步执行流的租户 ID 完全隔离
        /// </summary>
        public void SetTenant(string tenantId)
        {
            _manualTenantId.Value = tenantId;
        }

        /// <summary>
        /// 清除手动设置的租户 ID
        /// </summary>
        public void ClearTenant()
        {
            _manualTenantId.Value = null;
        }
    }
}
