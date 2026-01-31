using OmniMind.Entities;
using System.Security.Claims;

namespace App.AuthenticationPolicy
{
    public class JwtTenantProvider : ITenantProvider
    {
        private readonly IHttpContextAccessor _http;

        public JwtTenantProvider(IHttpContextAccessor http)
        {
            _http = http;
        }

        public bool IsResolved => TryGetTenantId(out _);

        public long TenantId
        {
            get
            {
                if (!TryGetTenantId(out var id))
                    return 0; // 或者抛异常，看你策略
                return id;
            }
        }

        private bool TryGetTenantId(out long tenantId)
        {
            tenantId = 0;

            var user = _http.HttpContext?.User;
            if (user?.Identity?.IsAuthenticated != true) return false;

            // 推荐：tenant_id
            var value = user.FindFirstValue("tenant_id")
                        ?? user.FindFirstValue("tenantId")
                        ?? user.FindFirstValue("tid"); // 兼容

            if (string.IsNullOrWhiteSpace(value)) return false;
            return long.TryParse(value, out tenantId) && tenantId > 0;
        }
    }
}
