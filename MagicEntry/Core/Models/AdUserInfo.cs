namespace MagicEntry.Core.Models
{
    /// <summary>
    /// Информация о пользователе из Active Directory
    /// </summary>
    public class AdUserInfo
    {
        public string FullName { get; set; }
        public string Email { get; set; }
        public string Phone { get; set; }
        public string Position { get; set; }
        public string Login { get; set; }
        public string Company { get; set; }
        public int? WorkGroup { get; set; }
        public string Department { get; set; }
    }
}
