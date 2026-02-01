using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;


namespace OmniMind.Abstractions.Tenant
{
    public interface ITenantProvider
    {
        string? TenantId { get; }          // 0 表示未解析/系统级
        bool IsResolved { get; }
    }
}
