using Microsoft.AspNetCore.Mvc;
using OmniMind.Api.Swaggers;
using OmniMind.Contracts.Config;

namespace App.Controllers
{
    /// <summary>
    /// 配置管理模块
    /// </summary>
    [ApiGroup(ApiGroupNames.USER)]
    [ApiController]
    [Route("api/[controller]")]
    public class ConfigController : BaseController
    {
        private readonly IConfiguration _configuration;

        public ConfigController(IConfiguration configuration)
        {
            _configuration = configuration;
        }

        /// <summary>
        /// 获取模型配置信息
        /// </summary>
        [HttpGet("models")]
        [ProducesResponseType(typeof(ModelConfigResponse), StatusCodes.Status200OK)]
        public IActionResult GetModelConfig()
        {
            var chatModels = _configuration.GetSection("AlibabaCloud:Chat:Model").Get<List<string>>() ?? new List<string>();
            var embeddingModel = _configuration["AlibabaCloud:Model"] ?? string.Empty;
            var vectorSize = _configuration.GetValue<int>("AlibabaCloud:VectorSize", 0);
            var maxTokens = _configuration.GetValue<int>("AlibabaCloud:Chat:MaxTokens", 2000);
            var temperature = _configuration.GetValue<double>("AlibabaCloud:Chat:Temperature", 0);
            var topP = _configuration.GetValue<double>("AlibabaCloud:Chat:TopP", 1);

            return Ok(new ModelConfigResponse
            {
                ChatModels = chatModels,
                EmbeddingModel = embeddingModel,
                VectorSize = vectorSize,
                MaxTokens = maxTokens,
                Temperature = temperature,
                TopP = topP
            });
        }

        /// <summary>
        /// 获取所有可用模型列表（兼容前端旧接口 obtainLargeModel）
        /// </summary>
        [HttpGet("models/list")]
        [ProducesResponseType(typeof(List<ModelDetailResponse>), StatusCodes.Status200OK)]
        public IActionResult GetModelsList()
        {
            var chatModels = _configuration.GetSection("AlibabaCloud:Chat:Model").Get<List<string>>() ?? new List<string>();

            var result = chatModels.Select(m => new ModelDetailResponse
            {
                Model = m,
                Description = m
            }).ToList();

            return Ok(result);
        }
    }
}
