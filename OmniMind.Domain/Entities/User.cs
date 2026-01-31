using Microsoft.AspNetCore.Identity;
using OmniMind.Enums;
using System;
using System.Collections.Generic;
using System.Text;

namespace OmniMind.Entities
{
    public class User : IdentityUser
    {
        public string? NickName { get; set; }
        /// <summary>
        /// 用户的性别
        /// </summary>
        public GenderEnum? Gender { get; set; }

        public string? RealName { get; set; }
        public string? FirstName { get; set; }

        public string? LastName { get; set; }
        public string? Picture { get; set; }
        public string? Province { get; set; }
        public string? City { get; set; }
        public string? Area { get; set; }
        public string? Address { get; set; }
        public DateTime? BirthDate { get; set; }
        public DateTime? UpdateAt { get; set; }
        public DateTime DateCreated { get; set; } = DateTime.UtcNow;


        /// <summary>
        /// 备注
        /// </summary>
        public string? Notes { get; set; }

        /// <summary>
        /// 上次登录时间
        /// </summary>
        public DateTime? LastSignDate { get; set; }

    }
}
