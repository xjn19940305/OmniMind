using Microsoft.AspNetCore.Identity;
using System;
using System.Collections.Generic;
using System.Text;

namespace OmniMind.Entities
{
    public class UserRole : IdentityUserRole<string>, ITenantEntity
    {

        public DateTime DateCreated { get; set; } = DateTime.UtcNow;

        public DateTime? DateModify { get; set; }
        public long TenantId { get; set; } = default!;

    }
}
