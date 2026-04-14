namespace LedgerFlow.Application.Common;

public static class Roles
{
    public const string Admin = "Admin";
    public const string Accountant = "Accountant";
    public const string User = "User";

    public static readonly string[] All = { Admin, Accountant, User };
    public static readonly string[] ReversalRoles = { Admin, Accountant };
}
