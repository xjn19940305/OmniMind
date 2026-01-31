using Microsoft.AspNetCore.Identity;
using System;
using System.Collections.Generic;
using System.Text;

namespace OmniMind.Entities
{
    public class UserClaim : IdentityUserClaim<string>, ITenantEntity
    {
        public long TenantId { get; set; } = default!;
    }
}
