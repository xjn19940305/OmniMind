using App.Swaggers;
using Microsoft.AspNetCore.Mvc;
using OmniMind.Abstractions.Storage;

namespace App.Controllers
{
    /// <summary>
    /// 测试控制器
    /// </summary>
    [ApiGroup(ApiGroupNames.USER)]
    [ApiController]
    [Route("api/[controller]")]
    public class TestController : ControllerBase
    {
        private readonly IObjectStorage storage;

        public TestController(IObjectStorage storage)
        {
            this.storage = storage;
        }
    }
}
