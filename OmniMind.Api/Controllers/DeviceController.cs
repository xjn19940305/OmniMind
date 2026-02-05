using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using OmniMind.Api.Swaggers;
using OmniMind.Contracts.Device;
using OmniMind.Entities;
using OmniMind.Persistence.PostgreSql;

namespace App.Controllers
{
    /// <summary>
    /// 设备管理模块
    /// </summary>
    [ApiGroup(ApiGroupNames.USER)]
    [ApiController]
    [Route("api/[controller]")]
    public class DeviceController : BaseController
    {
        private readonly OmniMindDbContext _dbContext;
        private readonly ILogger<DeviceController> _logger;

        public DeviceController(OmniMindDbContext dbContext, ILogger<DeviceController> logger)
        {
            _dbContext = dbContext;
            _logger = logger;
        }

        /// <summary>
        /// 绑定设备（登录时调用）
        /// </summary>
        [HttpPost("bind")]
        [ProducesResponseType(typeof(PushDeviceResponse), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(object), StatusCodes.Status400BadRequest)]
        public async Task<IActionResult> BindDevice([FromBody] BindPushDeviceRequest request)
        {
            var userId = GetUserId();

            if (string.IsNullOrWhiteSpace(request.ClientId))
            {
                return BadRequest(new { message = "ClientId 不能为空" });
            }

            // 查找是否已存在该设备
            var existingDevice = await _dbContext.PushDevices
                .FirstOrDefaultAsync(d => d.ClientId == request.ClientId);

            if (existingDevice != null)
            {
                // 如果设备已绑定给其他用户，转移给当前用户
                if (existingDevice.UserId != userId)
                {
                    existingDevice.UserId = userId;
                }

                // 更新设备信息
                existingDevice.Platform = request.Platform;
                existingDevice.DeviceModel = request.DeviceModel;
                existingDevice.OsVersion = request.OsVersion;
                existingDevice.AppVersion = request.AppVersion;
                existingDevice.LastActiveAt = DateTime.UtcNow;
                existingDevice.PushEnabled = true;

                _dbContext.PushDevices.Update(existingDevice);
                await _dbContext.SaveChangesAsync();

                _logger.LogInformation("[Device] 设备重新绑定: DeviceId={DeviceId}, UserId={UserId}, ClientId={ClientId}",
                    existingDevice.Id, userId, request.ClientId);

                return Ok(MapToPushDeviceResponse(existingDevice));
            }

            // 创建新设备
            var device = new PushDevice
            {
                Id = Guid.CreateVersion7().ToString(),
                UserId = userId,
                ClientId = request.ClientId,
                Platform = request.Platform,
                DeviceModel = request.DeviceModel,
                OsVersion = request.OsVersion,
                AppVersion = request.AppVersion,
                Alias = userId, // 使用用户ID作为别名
                PushEnabled = true,
                LastActiveAt = DateTime.UtcNow
            };

            _dbContext.PushDevices.Add(device);
            await _dbContext.SaveChangesAsync();

            _logger.LogInformation("[Device] 设备绑定成功: DeviceId={DeviceId}, UserId={UserId}, ClientId={ClientId}",
                device.Id, userId, request.ClientId);

            return Ok(MapToPushDeviceResponse(device));
        }

        /// <summary>
        /// 解绑设备
        /// </summary>
        [HttpPost("unbind")]
        [ProducesResponseType(typeof(object), StatusCodes.Status200OK)]
        public async Task<IActionResult> UnbindDevice([FromBody] UnbindDeviceRequest request)
        {
            var userId = GetUserId();

            var device = await _dbContext.PushDevices
                .FirstOrDefaultAsync(d => d.Id == request.DeviceId && d.UserId == userId);

            if (device == null)
            {
                return NotFound(new { message = "设备不存在" });
            }

            _dbContext.PushDevices.Remove(device);
            await _dbContext.SaveChangesAsync();

            _logger.LogInformation("[Device] 设备解绑: DeviceId={DeviceId}, UserId={UserId}", device.Id, userId);

            return Ok(new { message = "解绑成功" });
        }

        /// <summary>
        /// 获取当前用户的设备列表
        /// </summary>
        [HttpGet("list")]
        [ProducesResponseType(typeof(List<PushDeviceResponse>), StatusCodes.Status200OK)]
        public async Task<IActionResult> GetDevices()
        {
            var userId = GetUserId();

            var devices = await _dbContext.PushDevices
                .Where(d => d.UserId == userId)
                .OrderByDescending(d => d.LastActiveAt)
                .ToListAsync();

            var response = devices.Select(MapToPushDeviceResponse).ToList();

            return Ok(response);
        }

        /// <summary>
        /// 更新设备推送开关
        /// </summary>
        [HttpPost("push-toggle")]
        [ProducesResponseType(typeof(PushDeviceResponse), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(object), StatusCodes.Status404NotFound)]
        public async Task<IActionResult> TogglePush([FromBody] TogglePushRequest request)
        {
            var userId = GetUserId();

            var device = await _dbContext.PushDevices
                .FirstOrDefaultAsync(d => d.Id == request.DeviceId && d.UserId == userId);

            if (device == null)
            {
                return NotFound(new { message = "设备不存在" });
            }

            device.PushEnabled = request.Enabled;
            device.LastActiveAt = DateTime.UtcNow;

            _dbContext.PushDevices.Update(device);
            await _dbContext.SaveChangesAsync();

            _logger.LogInformation("[Device] 推送开关更新: DeviceId={DeviceId}, Enabled={Enabled}",
                device.Id, request.Enabled);

            return Ok(MapToPushDeviceResponse(device));
        }

        /// <summary>
        /// 更新设备最后活跃时间（心跳）
        /// </summary>
        [HttpPost("heartbeat")]
        [ProducesResponseType(typeof(object), StatusCodes.Status200OK)]
        public async Task<IActionResult> Heartbeat([FromBody] HeartbeatRequest request)
        {
            var userId = GetUserId();

            var device = await _dbContext.PushDevices
                .FirstOrDefaultAsync(d => d.ClientId == request.ClientId && d.UserId == userId);

            if (device != null)
            {
                device.LastActiveAt = DateTime.UtcNow;
                _dbContext.PushDevices.Update(device);
                await _dbContext.SaveChangesAsync();
            }

            return Ok(new { message = "ok" });
        }

        private static PushDeviceResponse MapToPushDeviceResponse(PushDevice device)
        {
            return new PushDeviceResponse
            {
                Id = device.Id,
                ClientId = device.ClientId,
                Platform = device.Platform,
                DeviceModel = device.DeviceModel,
                OsVersion = device.OsVersion,
                AppVersion = device.AppVersion,
                PushEnabled = device.PushEnabled,
                LastActiveAt = device.LastActiveAt
            };
        }
    }

    /// <summary>
    /// 解绑设备请求
    /// </summary>
    public record UnbindDeviceRequest
    {
        public string DeviceId { get; init; } = string.Empty;
    }

    /// <summary>
    /// 推送开关请求
    /// </summary>
    public record TogglePushRequest
    {
        public string DeviceId { get; init; } = string.Empty;
        public bool Enabled { get; init; }
    }

    /// <summary>
    /// 心跳请求
    /// </summary>
    public record HeartbeatRequest
    {
        public string ClientId { get; init; } = string.Empty;
    }
}
