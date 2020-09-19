Imports MyCompany.Services
Imports System
Imports System.Collections.Generic
Imports System.Configuration
Imports System.Linq
Imports System.Security.Permissions
Imports System.Security.Principal
Imports System.Text
Imports System.Web
Imports System.Web.Security

Namespace MyCompany.Security

    <AspNetHostingPermission(SecurityAction.LinkDemand, Level:=AspNetHostingPermissionLevel.Minimal)>  _
    Partial Public Class ExportAuthenticationModule
        Inherits ExportAuthenticationModuleBase
    End Class

    Public Class ExportAuthenticationModuleBase
        Inherits Object
        Implements IHttpModule

        Sub IHttpModule_Init(ByVal context As HttpApplication) Implements IHttpModule.Init
            AddHandler context.BeginRequest, AddressOf Me.contextBeginRequest
            AddHandler context.AuthenticateRequest, AddressOf Me.contextAuthenticateRequest
            AddHandler context.EndRequest, AddressOf Me.contextEndRequest
        End Sub

        Sub IHttpModule_Dispose() Implements IHttpModule.Dispose
        End Sub

        Private Sub contextBeginRequest(ByVal sender As Object, ByVal e As EventArgs)
            Dim request = HttpContext.Current.Request
            Dim origin = request.Headers("Origin")
            If Not (String.IsNullOrEmpty(origin)) Then
                Dim myOrigin = (request.Url.Scheme  _
                            + (Uri.SchemeDelimiter + request.Url.Host))
                If Not (request.Url.IsDefaultPort) Then
                    myOrigin = (myOrigin  _
                                + (":" + Convert.ToString(request.Url.Port)))
                End If
                If Not ((origin = myOrigin)) Then
                    Dim allowed = false
                    Dim config = ApplicationServices.Create().CorsConfiguration(request)
                    If (Not (config) Is Nothing) Then
                        For Each kvp in config
                            HttpContext.Current.Response.Headers(kvp.Key) = kvp.Value
                            If ((kvp.Key = "Access-Control-Allow-Origin") AndAlso ((kvp.Value = "*") OrElse kvp.Value.Split(Global.Microsoft.VisualBasic.ChrW(44)).Contains(origin))) Then
                                allowed = true
                            End If
                        Next
                    End If
                    If (Not (allowed) OrElse (request.HttpMethod = "OPTIONS")) Then
                        CType(sender,HttpApplication).CompleteRequest()
                    End If
                End If
            End If
        End Sub

        Private Sub contextAuthenticateRequest(ByVal sender As Object, ByVal e As EventArgs)
            Dim app = CType(sender,HttpApplication)
            Dim appServices = New ApplicationServices()
            Dim authorization = app.Request.Headers("Authorization")
            If Not (String.IsNullOrEmpty(authorization)) Then
                'validate auth header
                If authorization.StartsWith("Basic", StringComparison.CurrentCultureIgnoreCase) Then
                    ValidateUserIdentity(app, authorization)
                Else
                    If authorization.StartsWith("Bearer ", StringComparison.CurrentCultureIgnoreCase) Then
                        ValidateUserToken(app, authorization.Substring(7))
                    End If
                End If
            Else
                If Not (appServices.RequiresAuthentication(app.Context.Request)) Then
                    Return
                End If
                If appServices.AuthenticateRequest(app.Context) Then
                    Return
                End If
                RequestAuthentication(app)
            End If
        End Sub

        Private Sub contextEndRequest(ByVal sender As Object, ByVal e As EventArgs)
            Dim app = CType(sender,HttpApplication)
            If ((app.Response.StatusCode = 401) AndAlso (app.Context.Items("IgnoreBasicAuthenticationRequest") Is Nothing)) Then
                RequestAuthentication(app)
            End If
        End Sub

        Private Sub RequestAuthentication(ByVal app As HttpApplication)
            Dim appServices = New ApplicationServices()
            app.Response.AppendHeader("WWW-Authenticate", String.Format("Basic realm=""{0}""", appServices.Realm))
            app.Response.StatusCode = 401
            app.CompleteRequest()
        End Sub

        Private Sub ValidateUserIdentity(ByVal app As HttpApplication, ByVal authorization As String)
            Dim login = Encoding.Default.GetString(Convert.FromBase64String(authorization.Substring(6))).Split(New Char() {Global.Microsoft.VisualBasic.ChrW(58)}, 2)
            If Membership.ValidateUser(login(0), login(1)) Then
                app.Context.User = New RolePrincipal(New FormsIdentity(New FormsAuthenticationTicket(login(0), false, 10)))
            Else
                app.Response.StatusCode = 401
                app.Response.StatusDescription = "Access Denied"
                app.Response.Write("Access denied. Please enter a valid user name and password.")
                app.CompleteRequest()
            End If
        End Sub

        Private Sub ValidateUserToken(ByVal app As HttpApplication, ByVal authorization As String)
            If Not (ApplicationServices.Current.ValidateToken(authorization)) Then
                app.Context.Items("IgnoreBasicAuthenticationRequest") = true
                app.Response.StatusCode = 401
                app.Response.StatusDescription = "Access Denied"
                app.Response.Write("Access denied. Please authenticate.")
                app.CompleteRequest()
            End If
        End Sub
    End Class
End Namespace
