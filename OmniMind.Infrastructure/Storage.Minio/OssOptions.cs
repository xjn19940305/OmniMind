using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace OmniMind.Storage.Minio
{
    public class OssOptions
    {
        public string? Bucket { get; set; }

        public string? Endpoint { get; set; }

        public string? AccessKey { get; set; }

        public string? SecretKey { get; set; }

        public string? BasePath { get; set; }

    }
}
