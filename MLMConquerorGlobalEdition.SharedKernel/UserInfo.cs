namespace MLMConquerorGlobalEdition.SharedKernel;

public class UserInfo
{
    public string UserId   { get; set; } = string.Empty;
    public string MemberId { get; set; } = string.Empty;
    public string Email    { get; set; } = string.Empty;
    public string[] Roles  { get; set; } = [];
}
