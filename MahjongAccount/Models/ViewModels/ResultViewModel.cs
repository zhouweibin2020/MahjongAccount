using MahjongAccount.Models.Dtos;

namespace MahjongAccount.Models.ViewModels
{
    /// <summary>
    /// 牌局结果页视图模型
    /// </summary>
    public class ResultViewModel
    {
        /// <summary>
        /// 牌局信息
        /// </summary>
        public Game Game { get; set; }

        /// <summary>
        /// 当前用户结果
        /// </summary>
        public GameResultDto CurrentUserResult { get; set; }

        /// <summary>
        /// 所有玩家结果列表
        /// </summary>
        public List<GameResultDto> Results { get; set; } = new List<GameResultDto>();

        /// <summary>
        /// 结算转账记录列表
        /// </summary>
        public List<SettlementTransactionDto> Settlements { get; set; } = new List<SettlementTransactionDto>();
    }
}
