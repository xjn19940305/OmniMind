using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;

namespace OmniMind.Infrastructure
{
    public static class ToolsHelper
    {
        public static string GetDescription(this Enum enumName)
        {
            string description;
            FieldInfo fieldInfo = enumName.GetType().GetField(enumName.ToString());
            DescriptionAttribute[] attributes = fieldInfo.GetCustomAttributes<DescriptionAttribute>().ToArray();
            if (attributes != null && attributes.Length > 0)
                description = attributes[0].Description;
            else
                return string.Empty;
            return description;
        }

        public static string GenerateOrderNo(string Prefix = "escape_")
        {
            string date = DateTime.UtcNow.ToString("yyyyMMddHHmmss");
            string random = new Random(Guid.NewGuid().GetHashCode()).Next(10000, 99999).ToString();
            string orderNo = $"{Prefix}{date}{random}";
            return orderNo;
        }
        /// <summary>
        /// 根据IANA时区获取对应的时间偏移量
        /// </summary>
        /// <param name="IANAZone"></param>
        /// <returns></returns>
        //public static TimeZoneInfo? GetTimeZoneInfo(this string IANAZone)
        //{
        //    DateTimeZone dateTimeZone = DateTimeZoneProviders.Tzdb[IANAZone];
        //    string windowsTimeZone = TzdbDateTimeZoneSource.Default.WindowsMapping.MapZones
        //        .FirstOrDefault(x => x.TzdbIds.Contains(IANAZone))?.WindowsId;

        //    return TimeZoneInfo.FindSystemTimeZoneById(windowsTimeZone);
        //}
        public static long GenerateTimeStamp()
        {
            return ((long)(DateTime.UtcNow - new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc)).TotalSeconds);
        }
        public static string GenerateNonceStr()
        {
            return Guid.CreateVersion7().ToString("n");
        }
        /// <summary>
        /// 进行MD5加密
        /// </summary>
        /// <param name="md5"></param>
        /// <returns></returns>
        public static string Get32Md5(this string md5)
        {
            using (var md = System.Security.Cryptography.MD5.Create())
            {
                byte[] value = System.Text.Encoding.UTF8.GetBytes(md5);
                byte[] hash = md.ComputeHash(value);

                StringBuilder sb = new StringBuilder(hash.Length * 2);
                foreach (byte b in hash)
                {
                    sb.Append(b.ToString("X2"));
                }
                return sb.ToString();
            }
        }
        public static string ComputeSHA1Hash(string input)
        {
            using (SHA1 sha1 = SHA1.Create())
            {
                byte[] inputBytes = Encoding.UTF8.GetBytes(input);
                byte[] hashBytes = sha1.ComputeHash(inputBytes);

                StringBuilder sb = new StringBuilder();
                foreach (byte b in hashBytes)
                {
                    sb.Append(b.ToString("x2"));
                }

                return sb.ToString();
            }
        }
        public static string SecondToMintueAndSecond(this int time)
        {
            int hour = time / 3600;
            int minute = (time - hour * 3600) / 60;
            int seconds = time % 60;
            var str = string.Empty;
            if (minute > 0)
                str = $"{minute}分";
            if (seconds > 0)
                str += $"{seconds}秒";
            return str;
        }
    }
}
