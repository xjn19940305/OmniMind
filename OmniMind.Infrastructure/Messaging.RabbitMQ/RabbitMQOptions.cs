namespace OmniMind.Messaging.RabbitMQ
{
    /// <summary>
    /// RabbitMQ 配置选项
    /// </summary>
    public class RabbitMQOptions
    {
        /// <summary>
        /// 主机名或IP地址
        /// </summary>
        public const string SectionName = "RabbitMQ";

        /// <summary>
        /// 主机名或IP地址
        /// </summary>
        public string HostName { get; set; } = "localhost";

        /// <summary>
        /// 端口
        /// </summary>
        public int Port { get; set; } = 5672;

        /// <summary>
        /// 用户名
        /// </summary>
        public string UserName { get; set; } = "guest";

        /// <summary>
        /// 密码
        /// </summary>
        public string Password { get; set; } = "guest";

        /// <summary>
        /// 虚拟主机
        /// </summary>
        public string VirtualHost { get; set; } = "/";

        /// <summary>
        /// 是否自动重连
        /// </summary>
        public bool AutomaticRecoveryEnabled { get; set; } = true;

        /// <summary>
        /// 文档上传队列名称
        /// </summary>
        public string DocumentUploadQueue { get; set; } = "document-upload";

        /// <summary>
        /// 文档处理交换机名称
        /// </summary>
        public string DocumentExchange { get; set; } = "document-exchange";

        /// <summary>
        /// 路由键
        /// </summary>
        public string DocumentUploadRoutingKey { get; set; } = "document.upload";

        #region 转写相关配置

        /// <summary>
        /// 转写请求队列名称
        /// </summary>
        public string TranscribeRequestQueue { get; set; } = "transcribe-request";

        /// <summary>
        /// 转写请求路由键
        /// </summary>
        public string TranscribeRequestRoutingKey { get; set; } = "transcribe.request";

        /// <summary>
        /// 转写完成队列名称
        /// </summary>
        public string TranscribeCompletedQueue { get; set; } = "transcribe-completed";

        /// <summary>
        /// 转写完成路由键
        /// </summary>
        public string TranscribeCompletedRoutingKey { get; set; } = "transcribe.completed";

        #endregion
    }
}
