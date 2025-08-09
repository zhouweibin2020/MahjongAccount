using MahjongAccount.Models.Dtos;

namespace MahjongAccount.Models.ViewModels
{
    /// <summary>
    /// 排行榜视图模型
    /// </summary>
    public class RankingsViewModel
    {
        /// <summary>
        /// 榜单日期类型
        /// </summary>
        public string Period { get; set; }

        /// <summary>
        /// 榜单统计类型
        /// </summary>
        public string RankType { get; set; }

        /// <summary>
        /// 极值类型
        /// </summary>
        public string ExtremeType { get; set; }

        /// <summary>
        /// 排行榜数据列表
        /// </summary>
        public List<UserRankingDto> Rankings { get; set; } = new List<UserRankingDto>();

        /// <summary>
        /// 赢最多数据列表
        /// </summary>
        public List<ExtremeDto> TopExtremes { get; set; } = new List<ExtremeDto>();

        /// <summary>
        /// 输最多数据列表
        /// </summary>
        public List<ExtremeDto> BottomExtremes { get; set; } = new List<ExtremeDto>();

    }
}
