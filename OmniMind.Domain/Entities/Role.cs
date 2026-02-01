using Microsoft.AspNetCore.Identity;
using System;
using System.Collections.Generic;
using System.Text;

namespace OmniMind.Entities
{
    public class Role : IdentityRole
    {
        public int? Sort { get; set; }
        public string? Description { get; set; }

        public DateTime DateCreated { get; set; } = DateTime.UtcNow;

        public DateTime? DateModify { get; set; }
    }
}
