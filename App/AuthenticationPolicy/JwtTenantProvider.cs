using OmniMind.Abstractions.Tenant;
using OmniMind.Entities;
using System.Security.Claims;

namespace App.AuthenticationPolicy
{
    /// <summary>
    /// 从 JWT Claims 解析租户信息（tenant_id / tenantId / tid）
    /// </summary>
    public sealed class JwtTenantProvider : ITenantProvider
    {
        private readonly IHttpContextAccessor _http;

        public JwtTenantProvider(IHttpContextAccessor http)
        {
            _http = http;
        }

        /// <summary>
        /// 是否已解析到租户ID
        /// </summary>
        public bool IsResolved => TryGetTenantId(out var tid) && !string.IsNullOrWhiteSpace(tid);

        /// <summary>
        /// 当前租户ID（未解析到则返回空字符串）
        /// </summary>
        public string TenantId
        {
            get
            {
                return TryGetTenantId(out var tid) ? tid : string.Empty;
            }
        }

        /// <summary>
        /// 尝试从 JWT Claims 获取租户ID
        /// </summary>
        private bool TryGetTenantId(out string tenantId)
        {
            tenantId = string.Empty;
            var ctx = _http.HttpContext;
            if (ctx == null) return false;

            // 1) JWT
            var user = ctx.User;
            if (user?.Identity?.IsAuthenticated == true)
            {
                var claim = user.FindFirstValue("tenant_id") ?? user.FindFirstValue("tenantId") ?? user.FindFirstValue("tid");
                if (!string.IsNullOrWhiteSpace(claim))
                {
                    tenantId = claim.Trim();
                    return true;
                }
            }

            // 2) Header
            if (ctx.Request.Headers.TryGetValue("X-Tenant-Id", out var headerVal))
            {
                var v = headerVal.ToString();
                if (!string.IsNullOrWhiteSpace(v))
                {
                    tenantId = v.Trim();
                    return true;
                }
            }

            return false;
        }
    }
}