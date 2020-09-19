Imports MyCompany.Data
Imports MyCompany.Services
Imports Newtonsoft.Json
Imports System
Imports System.Collections.Generic
Imports System.Data
Imports System.IO
Imports System.Linq
Imports System.Text
Imports System.Text.RegularExpressions
Imports System.Web
Imports System.Web.Security
Imports System.Xml
Imports System.Xml.XPath

Namespace MyCompany.Handlers

    Partial Public Class Export
        Inherits ExportBase
    End Class

    Public Class ExportBase
        Inherits GenericHandlerBase
        Implements IHttpHandler, System.Web.SessionState.IRequiresSessionState

        ReadOnly Property IHttpHandler_IsReusable() As Boolean Implements IHttpHandler.IsReusable
            Get
                Return true
            End Get
        End Property

        Public Overridable ReadOnly Property PageSize() As Integer
            Get
                Return 1000
            End Get
        End Property

        Sub IHttpHandler_ProcessRequest(ByVal context As HttpContext) Implements IHttpHandler.ProcessRequest
            Dim q = context.Request.Params("q")
            If Not (String.IsNullOrEmpty(q)) Then
                If q.Contains("{") Then
                    q = Convert.ToBase64String(Encoding.Default.GetBytes(q))
                    context.Response.Redirect(((context.Request.AppRelativeCurrentExecutionFilePath + "?q=")  _
                                    + ((HttpUtility.UrlEncode(q) + "&t=")  _
                                    + context.Request.Params("t"))))
                End If
                q = Encoding.Default.GetString(Convert.FromBase64String(q))
                Dim args = JsonConvert.DeserializeObject(Of ActionArgs)(q)
                Dim viewId = args.CommandArgument
                If String.IsNullOrEmpty(viewId) Then
                    viewId = args.View
                End If
                Dim commandName = args.CommandName
                Dim attchmentFileName = String.Format(String.Format("attachment; filename={0}", GenerateOutputFileName(args, String.Format("{0}_{1}", args.Controller, viewId))))
                'create an Excel Web Query
                If ((commandName = "ExportRowset") AndAlso Not (context.Request.Url.AbsoluteUri.Contains("&d"))) Then
                    Dim webQueryUrl = context.Request.Url.AbsoluteUri
                    Dim accessToken = String.Empty
                    Dim user = Membership.GetUser()
                    If (Not (user) Is Nothing) Then
                        accessToken = ApplicationServicesBase.Create().CreateTicket(user, Nothing, "export.rowset.accessTokenDuration", String.Empty).AccessToken
                    End If
                    Dim indexOfToken = webQueryUrl.IndexOf("&t=")
                    If (indexOfToken = -1) Then
                        webQueryUrl = (webQueryUrl + "&t=")
                        indexOfToken = webQueryUrl.Length
                    Else
                        indexOfToken = (indexOfToken + 3)
                    End If
                    webQueryUrl = (webQueryUrl.Substring(0, indexOfToken) + accessToken)
                    webQueryUrl = ToClientUrl((webQueryUrl + "&d=true"))
                    context.Response.Write(("Web" & ControlChars.CrLf &"1" & ControlChars.CrLf  + webQueryUrl))
                    context.Response.ContentType = "text/x-ms-iqy"
                    context.Response.AddHeader("Content-Disposition", (attchmentFileName + ".iqy"))
                    Return
                End If
                'execute data export
                Dim requestPageSize = PageSize
                Dim requiresRowCount = true
                Dim methodNameSuffix = "Csv"
                If (commandName = "ExportCsv") Then
                    context.Response.ContentType = "text/csv"
                    context.Response.AddHeader("Content-Disposition", (attchmentFileName + ".csv"))
                    context.Response.Charset = "utf-8"
                Else
                    If (commandName = "ExportRowset") Then
                        context.Response.ContentType = "text/xml"
                        methodNameSuffix = "Rowset"
                    Else
                        context.Response.ContentType = "application/rss+xml"
                        methodNameSuffix = "Rss"
                        requestPageSize = DataControllerBase.MaximumRssItems
                        requiresRowCount = false
                    End If
                End If
                Dim r = New PageRequest()
                r.Controller = args.Controller
                r.View = viewId
                r.Filter = args.Filter
                r.ExternalFilter = args.ExternalFilter
                r.PageSize = requestPageSize
                r.RequiresMetaData = true
                r.MetadataFilter = New String() {"fields", "items"}
                Dim pageIndex = 0
                Dim totalRowCount = -1
                Using writer = New StreamWriter(context.Response.OutputStream, Encoding.UTF8, (1024 * 10), true)
                    Do While ((totalRowCount = -1) OrElse (totalRowCount > 0))
                        r.PageIndex = pageIndex
                        r.RequiresRowCount = (requiresRowCount AndAlso (pageIndex = 0))
                        Dim controller = ControllerFactory.CreateDataController()
                        Dim p = controller.GetPage(r.Controller, r.View, r)
                        For Each field in p.Fields
                            field.NormalizeDataFormatString()
                        Next
                        Dim scope = "current"
                        If (pageIndex = 0) Then
                            totalRowCount = p.TotalRowCount
                            If (totalRowCount > requestPageSize) Then
                                scope = "start"
                            Else
                                scope = "all"
                            End If
                        End If
                        totalRowCount = (totalRowCount - p.Rows.Count)
                        If ((totalRowCount <= 0) AndAlso (pageIndex > 0)) Then
                            scope = "end"
                        End If
                        pageIndex = (pageIndex + 1)
                        ResolveManyToManyFields(p)
                        'send data to the output
                        controller.GetType().GetMethod(("ExportDataAs" + methodNameSuffix)).Invoke(controller, New Object() {p, New DataTableReader(p.ToDataTable()), writer, scope})
                    Loop
                End Using
            End If
        End Sub

        Protected Overridable Function ToClientUrl(ByVal url As String) As String
            Return url
        End Function

        Public Shared Sub ResolveManyToManyFields(ByVal page As ViewPage)
            Dim manyToManyFields = New List(Of Integer)()
            For Each df in page.Fields
                If ((df.ItemsStyle = "CheckBoxList") OrElse Not (String.IsNullOrEmpty(df.ItemsTargetController))) Then
                    Dim fieldIndex = page.IndexOfField(df.Name)
                    manyToManyFields.Add(fieldIndex)
                End If
            Next
            If (manyToManyFields.Count > 0) Then
                For Each row in page.Rows
                    For Each fieldIndex in manyToManyFields
                        Dim v = CType(row(fieldIndex),String)
                        Dim newValue = New List(Of String)()
                        If Not (String.IsNullOrEmpty(v)) Then
                            Dim lov = Regex.Split(v, ",")
                            For Each item in page.Fields(fieldIndex).Items
                                If lov.Contains(Convert.ToString(item(0))) Then
                                    newValue.Add(Convert.ToString(item(1)))
                                End If
                            Next
                        End If
                        row(fieldIndex) = String.Join(", ", newValue)
                    Next
                Next
            End If
        End Sub
    End Class
End Namespace
