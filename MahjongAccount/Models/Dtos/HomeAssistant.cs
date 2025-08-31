namespace MahjongAccount.Models.Dtos
{
    public class HomeAssistant
    {
        /// <summary>
        /// HA地址
        /// </summary>
        public string BaseUrl { get; set; }

        /// <summary>
        /// 访问令牌
        /// </summary>
        public string AccessToken { get; set; }
    }
}
