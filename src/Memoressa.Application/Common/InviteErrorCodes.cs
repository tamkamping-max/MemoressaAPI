namespace Memoressa.Application.Common;

public static class InviteErrorCodes
{
    public const string InvalidEmail = "invalid_email";
    public const string EmailNotRegistered = "email_not_registered";
    public const string FriendAlreadyConnected = "friend_already_connected";
    public const string FamilyAlreadyConnected = "family_already_connected";
    public const string CannotInviteSelf = "cannot_invite_self";

    public const string EmailNotRegisteredMessage = "No Memoressa account for this email.";
    public const string FriendAlreadyConnectedMessage =
        "You are already friends or have a pending friend invite with this user.";
    public const string FamilyAlreadyConnectedMessage =
        "This user is already in your family or has a pending family invite.";
    public const string CannotInviteSelfMessage = "You cannot invite your own email address.";
}
