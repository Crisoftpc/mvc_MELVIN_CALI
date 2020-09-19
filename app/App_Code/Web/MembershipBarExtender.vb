Imports System
Imports System.ComponentModel
Imports System.Web.UI
Imports System.Web.UI.HtmlControls

Namespace MyCompany.Web

    Partial Public Class MembershipBarExtender
        Inherits MembershipBarExtenderBase
    End Class

    <TargetControlType(GetType(HtmlGenericControl)),  _
     ToolboxItem(false)>  _
    Public Class MembershipBarExtenderBase
        Inherits AquariumExtenderBase

        Public Sub New()
            MyBase.New("Web.Membership")
        End Sub

        Protected Overrides ReadOnly Property RequiresMembershipScripts() As Boolean
            Get
                Return true
            End Get
        End Property

        Protected Overrides Sub ConfigureDescriptor(ByVal descriptor As ScriptBehaviorDescriptor)
            descriptor.AddProperty("displayRememberMe", Properties("DisplayRememberMe"))
            descriptor.AddProperty("rememberMeSet", Properties("RememberMeSet"))
            descriptor.AddProperty("displaySignUp", Properties("DisplaySignUp"))
            descriptor.AddProperty("displayPasswordRecovery", Properties("DisplayPasswordRecovery"))
            descriptor.AddProperty("displayMyAccount", Properties("DisplayMyAccount"))
            Dim s = CType(Properties("Welcome"),String)
            If Not (String.IsNullOrEmpty(s)) Then
                descriptor.AddProperty("welcome", Properties("Welcome"))
            End If
            s = CType(Properties("User"),String)
            If Not (String.IsNullOrEmpty(s)) Then
                descriptor.AddProperty("user", Properties("User"))
            End If
            descriptor.AddProperty("displayHelp", Properties("DisplayHelp"))
            descriptor.AddProperty("enableHistory", Properties("EnableHistory"))
            descriptor.AddProperty("enablePermalinks", Properties("EnablePermalinks"))
            descriptor.AddProperty("displayLogin", Properties("DisplayLogin"))
            If Properties.ContainsKey("IdleUserTimeout") Then
                descriptor.AddProperty("idleTimeout", Properties("IdleUserTimeout"))
            End If
            Dim cultures = CType(Properties("Cultures"),String)
            If (cultures.Split(New Char() {Global.Microsoft.VisualBasic.ChrW(59)}, StringSplitOptions.RemoveEmptyEntries).Length > 1) Then
                descriptor.AddProperty("cultures", cultures)
            End If
        End Sub
    End Class
End Namespace
