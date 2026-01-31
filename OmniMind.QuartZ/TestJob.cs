using Microsoft.Extensions.Logging;
using Quartz;
using System;
using System.Collections.Generic;
using System.Text;

namespace OmniMind.QuartZ
{
    public class TestJob : IJob
    {
        private readonly IServiceProvider provider;
        private readonly ILogger<TestJob> logger;

        public TestJob(
        IServiceProvider provider,
        ILogger<TestJob> logger
        )
        {
            this.provider = provider;
            this.logger = logger;
        }
        public async Task Execute(IJobExecutionContext context)
        {
            //var dbContext = provider.GetRequiredService<ESCAPEDbContext>();
            //var ls = await dbContext.LiveStreamings.Where(x => x.ReviewEnable == false).ToListAsync();
            ////ls = ls.Where(f => f.Id == "efc4d458-ecfe-474b-acf7-3f6caf1f29fb").ToList();
            //StringBuilder stringBuilder = new StringBuilder();
            //foreach (var item in ls)
            //{
            //    if (DateTime.UtcNow >= item.StartDate && !item.IsPushStreaming && DateTime.UtcNow <= item.EndDate)
            //    {
            //        item.LiveStatus = Enum.LiveStreamingStatus.PENDING;
            //        stringBuilder.Append($"{DateTime.UtcNow} {item.Name}状态修改为:{item.LiveStatus.GetDescription()}\r\n");
            //    }
            //    else if (DateTime.UtcNow >= item.StartDate && item.IsPushStreaming || DateTime.UtcNow >= item.EndDate && item.IsPushStreaming)
            //    {
            //        item.LiveStatus = Enum.LiveStreamingStatus.LIVE;
            //        stringBuilder.Append($"{DateTime.UtcNow} {item.Name}状态修改为:{item.LiveStatus.GetDescription()}\r\n");
            //    }
            //    else if (DateTime.UtcNow >= item.EndDate && !item.IsPushStreaming)
            //    {
            //        item.LiveStatus = Enum.LiveStreamingStatus.ENDING;
            //        stringBuilder.Append($"{DateTime.UtcNow} {item.Name}状态修改为:{item.LiveStatus.GetDescription()} \r\n");
            //    }
            //}
            //logger.LogWarning(stringBuilder.ToString());
            //await dbContext.SaveChangesAsync();
        }
    }
}
