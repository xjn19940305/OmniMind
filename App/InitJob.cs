using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Newtonsoft.Json;
using OmniMind.Entities;
using OmniMind.Persistence.MySql;

namespace App
{
    public class InitJob
    {
        UserManager<User> userManage;
        OmniMindDbContext dbContext;
        public async Task Init(IServiceScope scope)
        {
            userManage = scope.ServiceProvider.GetRequiredService<UserManager<User>>();
            dbContext = scope.ServiceProvider.GetRequiredService<OmniMindDbContext>();
            var roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<Role>>();
            //var mongo = scope.ServiceProvider.GetRequiredService<MongoRepositoryContext>();
            var pendingMigrations = await dbContext.Database.GetPendingMigrationsAsync();

            if (!await dbContext.Database.CanConnectAsync())
            {
                await dbContext.Database.MigrateAsync();
                await CreateTenant();
                await CreateRole(roleManager);
                //await CreateUser();

                await ImportMasterData();


            }
            else if (pendingMigrations.Any())
            {
                await dbContext.Database.MigrateAsync();
            }
            //await InitPointTask(onepointService);
            //await InitLotteryDraw202312(mongo);
            //await GenerateCdKey(mongo);

        }


        async Task CreateTenant()
        {
            dbContext.Add(new Tenant
            {
                Name = "默认租户",
                Code = "default",
                Description = "系统默认租户",
                IsEnabled = true,
            });
            await dbContext.SaveChangesAsync();
        }

        async Task ImportMasterData()
        {
            dbContext.Database.SetCommandTimeout(1800);
            foreach (var file in Directory.GetFiles("MasterSql"))
            {
                string sql = File.ReadAllText(file);
                var sqlStatements = sql.Split(new[] { ";\n" }, StringSplitOptions.RemoveEmptyEntries);
                foreach (var statement in sqlStatements)
                {
                    await dbContext.Database.ExecuteSqlRawAsync(statement);
                }
            }

        }

        async Task CreateUser()
        {
            await AddSysAccount("milo", ["超级管理员"]);
        }
        private async Task AddSysAccount(string userName, string[] RoleNames)
        {
            var user = await userManage.FindByNameAsync(userName);
            if (user == null)
            {
                user = new User
                {
                    NickName = userName,

                };
                if (!string.IsNullOrEmpty(userName))
                {
                    await userManage.SetPhoneNumberAsync(user, userName);
                    await userManage.SetUserNameAsync(user, userName);
                }

                await userManage.AddPasswordAsync(user, "123456");

                var result = await userManage.CreateAsync(user);
                if (!result.Succeeded)
                    throw new Exception(JsonConvert.SerializeObject(result.Errors));
                await userManage.SetLockoutEnabledAsync(user, false);
                await dbContext.SaveChangesAsync();
            }
            foreach (var RoleName in RoleNames)
            {
                await userManage.AddToRoleAsync(user, RoleName);
            }

        }
        async Task CreateRole(RoleManager<Role> roleManager)
        {
            var role = new Role { Name = "超级管理员", Sort = 10 };
            await roleManager.CreateAsync(role);
            //核心demo.query
            await roleManager.AddClaimAsync(role, new System.Security.Claims.Claim("permission", "user.permission"));
            role = new Role { Name = "前端用户", Sort = 30 };
            await roleManager.CreateAsync(role);
            role = new Role { Name = "后端用户", Sort = 40 };
            await roleManager.CreateAsync(role);
        }

    }
}
