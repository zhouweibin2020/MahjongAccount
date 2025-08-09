namespace MahjongAccount.Models.Dtos
{
    public class ToggleReadyDto
    {
        /// <summary>
        /// 交易ID
        /// </summary>
        public int GameId { get; set; }

        /// <summary>
        /// 是否准备
        /// </summary>
        public bool IsReady { get; set; }
    }
}
