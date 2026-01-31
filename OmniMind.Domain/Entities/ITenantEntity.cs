using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace OmniMind.Entities
{
    public interface ITenantEntity
    {
        long TenantId { get; set; }
    }
}
