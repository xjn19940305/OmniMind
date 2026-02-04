using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;
using OmniMind.Entities;
using System.Security.Claims;

namespace OmniMind.Infrastructure
{
    public class MyUserClaimsPrincipalFactory : UserClaimsPrincipalFactory<User>
    {
        public MyUserClaimsPrincipalFactory(UserManager<User> userManager, IOptions<IdentityOptions> optionsAccessor) : base(userManager, optionsAccessor)
        {
        }

        //public MyUserClaimsPrincipalFactory(UserManager<User> userManager, RoleManager<Role> roleManager, IOptions<IdentityOptions> options) : base(userManager, roleManager, options)
        //{

        //}
        protected override async Task<ClaimsIdentity> GenerateClaimsAsync(User user)
        {
            var identity = await base.GenerateClaimsAsync(user);
            if (!string.IsNullOrWhiteSpace(user.Picture))
            {
                identity.AddClaim(new Claim("picture", user.Picture));
            }
            if (!string.IsNullOrWhiteSpace(user.NickName))
            {
                identity.AddClaim(new Claim("nickname", user.NickName));
            }
            return identity;
        }

    }
}
