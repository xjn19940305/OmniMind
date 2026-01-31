using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;


namespace OmniMind.Entities
{
    public interface ITenantProvider
    {
        long TenantId { get; }          // 0 表示未解析/系统级
        bool IsResolved { get; }
    }
}
