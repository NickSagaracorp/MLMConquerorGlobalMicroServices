namespace MLMConquerorGlobalEdition.Domain.Entities.Security;

public enum TwoFactorChannel
{
    Authenticator = 0,
    Email         = 1,
    Sms           = 2
}

public enum TwoFactorPurpose
{
    Login      = 0,
    Enrollment = 1,
    StepUp     = 2
}

public enum StepUpCategory
{
    Money           = 0,
    Identity        = 1,
    FinancialConfig = 2,
    BusinessData    = 3
}

public enum AuthEventOutcome
{
    Issued   = 0,
    Verified = 1,
    Failed   = 2,
    Denied   = 3
}

public enum AuthEventType
{
    LoginTwoFactorIssued     = 0,
    LoginTwoFactorVerified   = 1,
    LoginTwoFactorFailed     = 2,
    EnrollmentStarted        = 3,
    EnrollmentCompleted      = 4,
    TwoFactorDisabledByAdmin = 5,
    PhoneAdded               = 6,
    PhoneVerified            = 7,
    EmailConfirmed           = 8,
    PasswordChanged          = 9,
    StepUpIssued             = 10,
    StepUpVerified           = 11,
    StepUpFailed             = 12,
    StepUpDenied             = 13,
    StepUpPolicyChanged      = 14
}
