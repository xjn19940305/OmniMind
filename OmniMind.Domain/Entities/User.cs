using Microsoft.AspNetCore.Identity;
using OmniMind.Enums;
using System;
using System.Collections.Generic;

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

        /// <summary>
        /// 是否已完善信息
        /// </summary>
        public bool IsProfileCompleted { get; set; } = false;

        /// <summary>
        /// 用户扩展信息
        /// </summary>
        public UserProfile? Profile { get; set; }

        /// <summary>
        /// 推送设备列表
        /// </summary>
        public ICollection<PushDevice> PushDevices { get; set; } = new List<PushDevice>();

        /// <summary>
        /// 用户拥有的知识库集合
        /// </summary>
        public ICollection<KnowledgeBase> OwnedKnowledgeBases { get; set; } = new List<KnowledgeBase>();

        /// <summary>
        /// 用户作为成员参与的知识库集合
        /// </summary>
        public ICollection<KnowledgeBaseMember> KnowledgeBaseMemberships { get; set; } = new List<KnowledgeBaseMember>();
    }
}
