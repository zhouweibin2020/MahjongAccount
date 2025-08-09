using MahjongAccount.Models.Dtos;

namespace MahjongAccount.Models.ViewModels
{
    /// <summary>
    /// 牌局房间视图模型
    /// </summary>
    public class RoomViewModel
    {
        /// <summary>
        /// 牌局信息
        /// </summary>
        public Game Game { get; set; }

        /// <summary>
        /// 玩家列表
        /// </summary>
        public List<GamePlayerDto> Players { get; set; } = new List<GamePlayerDto>();

        /// <summary>
        /// 交易记录列表
        /// </summary>
        public List<TransactionDto> Transactions { get; set; } = new List<TransactionDto>();

        /// <summary>
        /// 玩家余额字典
        /// </summary>
        public Dictionary<int, int> PlayerBalances { get; set; } = new Dictionary<int, int>();

        /// <summary>
        /// 总玩家数
        /// </summary>
        public int TotalPlayers { get; set; }

        /// <summary>
        /// 已准备的玩家数
        /// </summary>
        public int ReadyPlayers { get; set; }

        /// <summary>
        /// 当前用户ID
        /// </summary>
        public int CurrentUserId { get; set; }

        /// <summary>
        /// 当前用户信息
        /// </summary>
        public GamePlayerDto CurrentUser { get; set; }
    }
}
