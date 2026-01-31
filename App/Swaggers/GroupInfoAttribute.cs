namespace App.Swaggers
{
    public class GroupInfoAttribute : Attribute
    {
        public string Title { get; set; }
    }
    public enum ApiGroupNames
    {
        [GroupInfo(Title = "管理端")]
        MANAGEMENT,
        [GroupInfo(Title = "前端")]
        USER
    }

    /// <summary>
    /// 系统分组特性
    /// </summary>
    public class ApiGroupAttribute : Attribute
    {
        public ApiGroupAttribute(params ApiGroupNames[] name)
        {
            GroupName = name;
        }

        public ApiGroupNames[] GroupName { get; set; }

        public bool Igrone { get; set; } = false;
    }
}
