Imports MyCompany.Data
Imports MyCompany.Services
Imports System
Imports System.Collections.Generic
Imports System.Linq
Imports System.Net.Mail
Imports System.Text
Imports System.Web
Imports System.Web.Security

Namespace MyCompany.Security

    Partial Public Class MembershipBusinessRules
        Inherits MembershipBusinessRulesBase
    End Class

    Public Class MembershipBusinessRulesBase
        Inherits BusinessRules

        Public Shared CreateErrors As SortedDictionary(Of MembershipCreateStatus, String) = New SortedDictionary(Of MembershipCreateStatus, String)()

        Shared Sub New()
            CreateErrors.Add(MembershipCreateStatus.DuplicateEmail, "Duplicate email address.")
            CreateErrors.Add(MembershipCreateStatus.DuplicateProviderUserKey, "Duplicate provider key.")
            CreateErrors.Add(MembershipCreateStatus.DuplicateUserName, "Duplicate user name.")
            CreateErrors.Add(MembershipCreateStatus.InvalidAnswer, "Invalid password recovery answer.")
            CreateErrors.Add(MembershipCreateStatus.InvalidEmail, "Invalid email address.")
            CreateErrors.Add(MembershipCreateStatus.InvalidPassword, "Invalid password.")
            CreateErrors.Add(MembershipCreateStatus.InvalidProviderUserKey, "Invalid provider user key.")
            CreateErrors.Add(MembershipCreateStatus.InvalidQuestion, "Invalid password recovery question.")
            CreateErrors.Add(MembershipCreateStatus.InvalidUserName, "Invalid user name.")
            CreateErrors.Add(MembershipCreateStatus.ProviderError, "Provider error.")
            CreateErrors.Add(MembershipCreateStatus.UserRejected, "User has been rejected.")
        End Sub

        <ControllerAction("aspnet_Membership", "Delete", ActionPhase.Before)>  _
        Protected Overridable Sub DeleteUser(ByVal userId As Guid)
            PreventDefault()
            Dim user = Membership.GetUser(userId)
            Membership.DeleteUser(user.UserName)
            If Not (ApplicationServicesBase.IsTouchClient) Then
                Result.ShowLastView()
                Result.ShowMessage(String.Format(Localize("UserHasBeenDeleted", "User '{0}' has been deleted."), user.UserName))
            End If
        End Sub

        <ControllerAction("aspnet_Membership", "Update", ActionPhase.Before)>  _
        Protected Overridable Sub UpdateUser(ByVal userId As Guid, ByVal email As FieldValue, ByVal isApproved As FieldValue, ByVal isLockedOut As FieldValue, ByVal comment As FieldValue, ByVal roles As FieldValue)
            PreventDefault()
            Dim user = Membership.GetUser(userId)
            'update user information
            If email.Modified Then
                user.Email = Convert.ToString(email.Value)
                Membership.UpdateUser(user)
            End If
            If isApproved.Modified Then
                user.IsApproved = Convert.ToBoolean(isApproved.Value)
                Membership.UpdateUser(user)
            End If
            If isLockedOut.Modified Then
                If Convert.ToBoolean(isLockedOut.Value) Then
                    Result.Focus("IsLockedOut", Localize("UserCannotBeLockedOut", "User cannot be locked out. If you want to prevent this user from being able to lo"& _
                                "gin then simply mark user as 'not-approved'."))
                    Throw New Exception(Localize("ErrorSavingUser", "Error saving user account."))
                End If
                user.UnlockUser()
            End If
            If comment.Modified Then
                user.Comment = Convert.ToString(comment.Value)
                Membership.UpdateUser(user)
            End If
            If ((Not (roles) Is Nothing) AndAlso roles.Modified) Then
                Dim newRoles = Convert.ToString(roles.Value).Split(Global.Microsoft.VisualBasic.ChrW(44))
                Dim oldRoles = System.Web.Security.Roles.GetRolesForUser(user.UserName)
                For Each role in oldRoles
                    If (Not (String.IsNullOrEmpty(role)) AndAlso (Array.IndexOf(newRoles, role) = -1)) Then
                        System.Web.Security.Roles.RemoveUserFromRole(user.UserName, role)
                    End If
                Next
                For Each role in newRoles
                    If (Not (String.IsNullOrEmpty(role)) AndAlso (Array.IndexOf(oldRoles, role) = -1)) Then
                        System.Web.Security.Roles.AddUserToRole(user.UserName, role)
                    End If
                Next
            End If
        End Sub

        <ControllerAction("aspnet_Membership", "Insert", ActionPhase.Before)>  _
        Protected Overridable Sub InsertUser(ByVal username As String, ByVal password As String, ByVal confirmPassword As String, ByVal email As String, ByVal passwordQuestion As String, ByVal passwordAnswer As String, ByVal isApproved As Boolean, ByVal comment As String, ByVal roles As String)
            PreventDefault()
            If Not ((password = confirmPassword)) Then
                Throw New Exception(Localize("PasswordAndConfirmationDoNotMatch", "Password and confirmation do not match."))
            End If
            'create a user
            Dim status As MembershipCreateStatus
            Membership.CreateUser(username, password, email, passwordQuestion, passwordAnswer, isApproved, status)
            If Not ((status = MembershipCreateStatus.Success)) Then
                Throw New Exception(Localize(status.ToString(), MembershipBusinessRules.CreateErrors(status)))
            End If
            'retrieve the primary key of the new user account
            Dim newUser = Membership.GetUser(username)
            Dim providerUserKey = newUser.ProviderUserKey
            If TypeOf providerUserKey Is Byte Then
                providerUserKey = New Guid(CType(providerUserKey,Byte()))
            End If
            Result.Values.Add(New FieldValue("UserId", providerUserKey))
            'update a comment
            If Not (String.IsNullOrEmpty(comment)) Then
                newUser.Comment = comment
                Membership.UpdateUser(newUser)
            End If
            If Not (String.IsNullOrEmpty(roles)) Then
                For Each role in roles.Split(Global.Microsoft.VisualBasic.ChrW(44))
                    System.Web.Security.Roles.AddUserToRole(username, role)
                Next
            End If
        End Sub

        <RowBuilder("aspnet_Membership", "createForm1", RowKind.New)>  _
        Protected Overridable Sub NewUserRow()
            UpdateFieldValue("IsApproved", true)
        End Sub

        <RowBuilder("aspnet_Membership", "editForm1", RowKind.Existing)>  _
        Protected Overridable Sub PrepareUserRow()
            Dim userName = CType(SelectFieldValue("UserUserName"),String)
            Dim sb = New StringBuilder()
            For Each role in System.Web.Security.Roles.GetRolesForUser(userName)
                If (sb.Length > 0) Then
                    sb.Append(Global.Microsoft.VisualBasic.ChrW(44))
                End If
                sb.Append(role)
            Next
            UpdateFieldValue("Roles", sb.ToString())
            Dim dt = CType(SelectFieldValue("LastLockoutDate"),DateTime)
            If dt.Equals(New DateTime(1754, 1, 1)) Then
                UpdateFieldValue("LastLockoutDate", Nothing)
            End If
            dt = CType(SelectFieldValue("FailedPasswordAttemptWindowStart"),DateTime)
            If dt.Equals(New DateTime(1754, 1, 1)) Then
                UpdateFieldValue("FailedPasswordAttemptWindowStart", Nothing)
            End If
            dt = CType(SelectFieldValue("FailedPasswordAnswerAttemptWindowStart"),DateTime)
            If dt.Equals(New DateTime(1754, 1, 1)) Then
                UpdateFieldValue("FailedPasswordAnswerAttemptWindowStart", Nothing)
            End If
        End Sub

        <ControllerAction("aspnet_Roles", "Insert", ActionPhase.Before)>  _
        Protected Overridable Sub InsertRole(ByVal roleName As String)
            PreventDefault()
            System.Web.Security.Roles.CreateRole(roleName)
        End Sub

        <ControllerAction("aspnet_Roles", "Update", ActionPhase.Before)>  _
        Protected Overridable Sub UpdateRole(ByVal roleName As String)
            UpdateFieldValue("LoweredRoleName", roleName.ToLower())
        End Sub

        <ControllerAction("aspnet_Roles", "Delete", ActionPhase.Before)>  _
        Protected Overridable Sub DeleteRole(ByVal roleName As String)
            PreventDefault()
            System.Web.Security.Roles.DeleteRole(roleName)
        End Sub

        <ControllerAction("aspnet_Membership", "myAccountForm", "Update", ActionPhase.Before)>  _
        Protected Overridable Sub UpdateMyAccount(ByVal userName As String, ByVal oldPassword As String, ByVal password As String, ByVal confirmPassword As String, ByVal email As String, ByVal passwordQuestion As String, ByVal passwordAnswer As String)
            PreventDefault()
            Dim user = Membership.GetUser(userName)
            If (Not (user) Is Nothing) Then
                If String.IsNullOrEmpty(oldPassword) Then
                    Result.ShowAlert(Localize("EnterCurrentPassword", "Please enter your current password."), "OldPassword")
                    Return
                End If
                If Not (Membership.ValidateUser(userName, oldPassword)) Then
                    Result.ShowAlert(Localize("PasswordDoesNotMatchRecords", "Your password does not match our records."), "OldPassword")
                    Return
                End If
                If (Not (String.IsNullOrEmpty(password)) OrElse Not (String.IsNullOrEmpty(confirmPassword))) Then
                    If Not ((password = confirmPassword)) Then
                        Result.ShowAlert(Localize("NewPasswordAndConfirmatinDoNotMatch", "New password and confirmation do not match."), "Password")
                        Return
                    End If
                    If Not (user.ChangePassword(oldPassword, password)) Then
                        Result.ShowAlert(Localize("NewPasswordInvalid", "Your new password is invalid."), "Password")
                        Return
                    End If
                End If
                If Not ((email = user.Email)) Then
                    user.Email = email
                    Membership.UpdateUser(user)
                End If
                If (Not ((user.PasswordQuestion = passwordQuestion)) AndAlso String.IsNullOrEmpty(passwordAnswer)) Then
                    Result.ShowAlert(Localize("EnterPasswordAnswer", "Please enter a password answer."), "PasswordAnswer")
                    Return
                End If
                If Not (String.IsNullOrEmpty(passwordAnswer)) Then
                    user.ChangePasswordQuestionAndAnswer(oldPassword, passwordQuestion, passwordAnswer)
                    Membership.UpdateUser(user)
                End If
                Result.HideModal()
            Else
                Result.ShowAlert(Localize("UserNotFound", "User not found."))
            End If
        End Sub

        Public Shared Sub CreateStandardMembershipAccounts()
            ApplicationServices.RegisterStandardMembershipAccounts()
        End Sub

        <ControllerAction("aspnet_Membership", "Select", ActionPhase.Before),  _
         ControllerAction("aspnet_Membership", "Update", ActionPhase.Before),  _
         ControllerAction("aspnet_Membership", "Insert", ActionPhase.Before),  _
         ControllerAction("aspnet_Membership", "Delete", ActionPhase.Before),  _
         ControllerAction("aspnet_Roles", "Select", ActionPhase.Before),  _
         ControllerAction("aspnet_Roles", "Insert", ActionPhase.Before),  _
         ControllerAction("aspnet_Roles", "Update", ActionPhase.Before),  _
         ControllerAction("aspnet_Roles", "Delete", ActionPhase.Before)>  _
        Public Overridable Sub AccessControlValidation()
            If Not (Context.User.Identity.IsAuthenticated) Then
                Throw New Exception("Not Authorized.")
            End If
            If (Not (UserIsInRole("Administrators")) AndAlso Not (((Not (Request) Is Nothing) AndAlso (Request.View = "lookup")))) Then
                Throw New Exception("Not Authorized.")
            End If
        End Sub
    End Class
End Namespace
