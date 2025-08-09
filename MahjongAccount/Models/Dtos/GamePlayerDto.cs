namespace MahjongAccount.Models.Dtos
{
    /// <summary>
    /// 牌局中的玩家信息DTO
    /// </summary>
    public class GamePlayerDto
    {
        /// <summary>
        /// 用户ID
        /// </summary>
        public int Id { get; set; }

        /// <summary>
        /// 用户昵称
        /// </summary>
        public string Nickname { get; set; }

        /// <summary>
        /// 用户头像（Base64）
        /// </summary>
        public byte[] Avatar { get; set; }

        /// <summary>
        /// 是否准备结束牌局
        /// </summary>
        public bool IsReady { get; set; }

        /// <summary>
        /// 是否为牌局创建者
        /// </summary>
        public bool IsCreator { get; set; }
    }
}
