Imports MyCompany.Data
Imports System
Imports System.Collections.Generic
Imports System.ComponentModel
Imports System.Configuration
Imports System.Data
Imports System.Globalization
Imports System.Text
Imports System.Web
Imports System.Web.Security
Imports System.Web.UI
Imports System.Web.UI.HtmlControls
Imports System.Web.UI.WebControls
Imports System.Web.UI.WebControls.WebParts

Namespace MyCompany.Web

    Partial Public Class MembershipBar
        Inherits MembershipBarBase
    End Class

    Public Class MembershipBarBase
        Inherits Control
        Implements INamingContainer

        Private m_ServicePath As String

        Private m_Welcome As String

        Private m_DisplayRememberMe As Boolean

        Private m_DisplayLogin As Boolean

        Private m_RememberMeSet As Boolean

        Private m_DisplayPasswordRecovery As Boolean

        Private m_DisplaySignUp As Boolean

        Private m_DisplayMyAccount As Boolean

        Private m_DisplayHelp As Boolean

        Private m_EnableHistory As Boolean

        Private m_EnablePermalinks As Boolean

        Private m_IdleUserTimeout As Integer

        Public Sub New()
            MyBase.New
            m_DisplayLogin = true
            m_DisplaySignUp = true
            m_DisplayPasswordRecovery = true
            m_DisplayRememberMe = true
            m_DisplayMyAccount = true
            m_DisplayHelp = true
            m_DisplayLogin = true
        End Sub

        <System.ComponentModel.Description("A path to a data controller web service."),  _
         System.ComponentModel.DefaultValue("~/_invoke")>  _
        Public Property ServicePath() As String
            Get
                If String.IsNullOrEmpty(m_ServicePath) Then
                    Return "~/_invoke"
                End If
                Return m_ServicePath
            End Get
            Set
                m_ServicePath = value
            End Set
        End Property

        <System.ComponentModel.Description("Specifies a welcome message for an authenticated user. Example: Welcome <b>{0}</b"& _
            ">, Today is {1:D}")>  _
        Public Property Welcome() As String
            Get
                Return m_Welcome
            End Get
            Set
                m_Welcome = value
            End Set
        End Property

        <System.ComponentModel.DefaultValue(true),  _
         System.ComponentModel.Description("Controls display of 'Remember me' check box in a login window.")>  _
        Public Property DisplayRememberMe() As Boolean
            Get
                Return m_DisplayRememberMe
            End Get
            Set
                m_DisplayRememberMe = value
            End Set
        End Property

        <System.ComponentModel.DefaultValue(true),  _
         System.ComponentModel.Description("Specifies if a fly-over login dialog is displayed on the memberhhip bar.")>  _
        Public Property DisplayLogin() As Boolean
            Get
                Return m_DisplayLogin
            End Get
            Set
                m_DisplayLogin = value
            End Set
        End Property

        <System.ComponentModel.DefaultValue(false),  _
         System.ComponentModel.Description("Specifies if 'Remember me' check box in a login window is selected by default.")>  _
        Public Property RememberMeSet() As Boolean
            Get
                Return m_RememberMeSet
            End Get
            Set
                m_RememberMeSet = value
            End Set
        End Property

        <System.ComponentModel.DefaultValue(true),  _
         System.ComponentModel.Description("Controls display of a password recovery link in a login window.")>  _
        Public Property DisplayPasswordRecovery() As Boolean
            Get
                Return m_DisplayPasswordRecovery
            End Get
            Set
                m_DisplayPasswordRecovery = value
            End Set
        End Property

        <System.ComponentModel.DefaultValue(true),  _
         System.ComponentModel.Description("Controls display of a anonymous user account sign up link in a login window.")>  _
        Public Property DisplaySignUp() As Boolean
            Get
                Return m_DisplaySignUp
            End Get
            Set
                m_DisplaySignUp = value
            End Set
        End Property

        <System.ComponentModel.DefaultValue(true),  _
         System.ComponentModel.Description("Controls display of 'My Account' link for authenticated users on a membership bar"& _
            ".")>  _
        Public Property DisplayMyAccount() As Boolean
            Get
                Return m_DisplayMyAccount
            End Get
            Set
                m_DisplayMyAccount = value
            End Set
        End Property

        <System.ComponentModel.DefaultValue(true),  _
         System.ComponentModel.Description("Controls display of a 'Help' link on a membership bar.")>  _
        Public Property DisplayHelp() As Boolean
            Get
                Return m_DisplayHelp
            End Get
            Set
                m_DisplayHelp = value
            End Set
        End Property

        <System.ComponentModel.DefaultValue(false),  _
         System.ComponentModel.Description("Enables interactive history of most recent used data objects.")>  _
        Public Property EnableHistory() As Boolean
            Get
                Return m_EnableHistory
            End Get
            Set
                m_EnableHistory = value
            End Set
        End Property

        <System.ComponentModel.DefaultValue(false),  _
         System.ComponentModel.Description("Enables bookmarking of selected master records by end users.")>  _
        Public Property EnablePermalinks() As Boolean
            Get
                Return m_EnablePermalinks
            End Get
            Set
                m_EnablePermalinks = value
            End Set
        End Property

        <System.ComponentModel.DefaultValue(0),  _
         System.ComponentModel.Description("The idle user detection timeout in minutes.")>  _
        Public Property IdleUserTimeout() As Integer
            Get
                Return m_IdleUserTimeout
            End Get
            Set
                m_IdleUserTimeout = value
            End Set
        End Property

        Protected Overrides Sub CreateChildControls()
            MyBase.CreateChildControls()
            Dim div = New HtmlGenericControl("div")
            div.ID = "d"
            div.Style.Add(HtmlTextWriterStyle.Display, "none")
            Controls.Add(div)
            Dim bar = New MembershipBarExtender()
            bar.ID = "b"
            bar.TargetControlID = div.ID
            bar.ServicePath = ServicePath
            bar.Properties.Add("DisplaySignUp", DisplaySignUp)
            bar.Properties.Add("DisplayLogin", DisplayLogin)
            bar.Properties.Add("DisplayRememberMe", DisplayRememberMe)
            bar.Properties.Add("DisplayPasswordRecovery", DisplayPasswordRecovery)
            bar.Properties.Add("RememberMeSet", RememberMeSet)
            bar.Properties.Add("DisplayMyAccount", DisplayMyAccount)
            bar.Properties.Add("DisplayHelp", DisplayHelp)
            bar.Properties.Add("EnablePermalinks", EnablePermalinks)
            bar.Properties.Add("EnableHistory", EnableHistory)
            bar.Properties.Add("User", Page.User.Identity.Name)
            bar.Properties.Add("Welcome", m_Welcome)
            Dim sb = New StringBuilder()
            For Each c in CultureManager.SupportedCultures
                Dim ci = New CultureInfo(c.Split(Global.Microsoft.VisualBasic.ChrW(44))(1))
                sb.AppendFormat("{0}|{1}|{2};", c, ci.NativeName, ci.Equals(System.Threading.Thread.CurrentThread.CurrentUICulture))
            Next
            bar.Properties.Add("Cultures", sb.ToString())
            If (IdleUserTimeout > 0) Then
                bar.Properties.Add("IdleUserTimeout", (IdleUserTimeout * 60000))
            End If
            Controls.Add(bar)
        End Sub
    End Class
End Namespace
