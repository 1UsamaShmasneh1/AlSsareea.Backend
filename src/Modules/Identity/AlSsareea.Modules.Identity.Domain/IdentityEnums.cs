namespace AlSsareea.Modules.Identity.Domain;

public enum UserType : short { Customer = 1, Driver, MerchantOwner, MerchantEmployee, Operations, Support, Finance, Administrator, SuperAdministrator }
public enum UserStatus : short { PendingActivation = 1, Active, Locked, Suspended, Disabled, Deleted }
public enum DevicePlatform : short { Android = 1, Ios, Web, Windows, MacOs, Linux }
public enum SessionState : short { Active = 1, Expired, Revoked, LoggedOut }
public enum SessionEndReason : short { UserLogout = 1, RefreshRevoked, PasswordChanged, SecurityStampChanged, AdministratorRevoked, Expired }
public enum LoginResult : short { Succeeded = 1, Failed }
public enum LoginFailureReason : short { InvalidCredentials = 1, LockedOut, Suspended, Disabled, ExpiredCredentials, Unknown }
public enum OtpPurpose : short { Login = 1, PasswordReset, PhoneVerification, EmailVerification }
