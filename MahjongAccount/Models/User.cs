using System.ComponentModel.DataAnnotations;

namespace MahjongAccount.Models
{
    public class User
    {
        public int Id { get; set; }

        [Required]
        public string Nickname { get; set; }

        public byte[] Avatar { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.Now;
    }
}
