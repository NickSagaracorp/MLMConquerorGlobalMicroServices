namespace MLMConquerorGlobalEdition.SharedComponents.Constants;

public static class Permissions
{
    public static class Commission
    {
        public const string Delete  = "commission.delete";
        public const string ForcePay = "commission.forcepay";
        public const string View    = "commission.view";
    }

    public static class Member
    {
        public const string ChangeStatus       = "member.changestatus";
        public const string OverrideMembership = "member.overridemembership";
        public const string Impersonate        = "member.impersonate";
        public const string ImpersonateReadOnly = "member.impersonate.readonly";
    }

    public static class GhostPoints
    {
        public const string Add    = "ghostpoints.add";
        public const string Delete = "ghostpoints.delete";
    }

    public static class Tokens
    {
        public const string AdminGrant = "tokens.admingrant";
    }

    public static class Rank
    {
        public const string View     = "rank.view";
        public const string Override = "rank.override";
    }

    public static class Loyalty
    {
        public const string ManualUnlock = "loyalty.manualunlock";
    }

    public static class Wallet
    {
        public const string ViewFullHistory = "wallet.viewfullhistory";
    }

    public static class Ticket
    {
        public const string EscalateToL2  = "ticket.escalate.l2";
        public const string EscalateToL3  = "ticket.escalate.l3";
        public const string EscalateToIT  = "ticket.escalate.it";
        public const string Assign        = "ticket.assign";
        public const string Resolve       = "ticket.resolve";
        public const string Merge         = "ticket.merge";
        public const string ViewAll       = "ticket.viewall";
    }

    public static class SystemUsers
    {
        public const string Manage = "systemusers.manage";
    }
}
