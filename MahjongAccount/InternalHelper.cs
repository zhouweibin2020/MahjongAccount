using System.Net.Sockets;
using System.Net;

namespace MahjongAccount
{
    public static class InternalHelper
    {
        /// <summary>
        /// 获取客户端真实IP地址
        /// </summary>
        public static string GetClientIpAddress(HttpContext httpContext)
        {
            // 优先从反向代理头获取
            if (httpContext.Request.Headers.TryGetValue("X-Forwarded-For", out var forwardedFor))
            {
                // X-Forwarded-For可能包含多个IP，取第一个
                var firstIp = forwardedFor.ToString().Split(',', StringSplitOptions.RemoveEmptyEntries).FirstOrDefault();
                if (!string.IsNullOrEmpty(firstIp))
                {
                    return firstIp.Trim();
                }
            }

            if (httpContext.Request.Headers.TryGetValue("X-Real-IP", out var realIp))
            {
                return realIp;
            }

            // 直接获取连接的IP
            return httpContext.Connection.RemoteIpAddress?.ToString() ?? string.Empty;
        }

        /// <summary>
        /// 判断IP地址是否为内网地址
        /// </summary>
        public static bool IsInternalIpAddress(string ipAddress)
        {
            if (string.IsNullOrEmpty(ipAddress))
                return false;

            if ("127.0.0.1".Equals(ipAddress))
                return true;

            // 尝试解析IP地址
            if (!IPAddress.TryParse(ipAddress, out var ip))
                return false;

            // IPv4内网地址判断
            if (ip.AddressFamily == AddressFamily.InterNetwork)
            {
                var bytes = ip.GetAddressBytes();
                return (bytes[0] == 10) // 10.0.0.0-10.255.255.255
                    || (bytes[0] == 172 && bytes[1] >= 16 && bytes[1] <= 31) // 172.16.0.0-172.31.255.255
                    || (bytes[0] == 192 && bytes[1] == 168); // 192.168.0.0-192.168.255.255
            }
            // IPv6内网地址判断（可选）
            else if (ip.AddressFamily == AddressFamily.InterNetworkV6)
            {
                return ip.IsIPv6LinkLocal // 链路本地地址（fe80::/10）
                    || ip.IsIPv6SiteLocal; // 站点本地地址（fec0::/10，已被 deprecated但部分场景仍使用）
            }

            return false;
        }
    }
}
