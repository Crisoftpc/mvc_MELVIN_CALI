Imports MyCompany.Data
Imports MyCompany.Handlers
Imports MyCompany.Security
Imports MyCompany.Web
Imports Newtonsoft.Json
Imports Newtonsoft.Json.Linq
Imports System
Imports System.Collections.Generic
Imports System.Collections.Specialized
Imports System.ComponentModel
Imports System.Drawing
Imports System.Drawing.Imaging
Imports System.Globalization
Imports System.IO
Imports System.IO.Compression
Imports System.Linq
Imports System.Net
Imports System.Text
Imports System.Text.RegularExpressions
Imports System.Threading
Imports System.Web
Imports System.Web.Routing
Imports System.Web.Security
Imports System.Web.UI
Imports System.Xml
Imports System.Xml.XPath

Namespace MyCompany.Services

    Public Class UriRestConfig

        Private m_Uri As Regex

        Private m_Properties As SortedDictionary(Of String, String)

        Public Shared SupportedJSONContentTypes() As String = New String() {"application/json", "text/javascript", "application/javascript", "application/ecmascript", "application/x-ecmascript"}

        Public Sub New(ByVal uri As String)
            MyBase.New
            m_Uri = New Regex(uri, RegexOptions.IgnoreCase)
            m_Properties = New SortedDictionary(Of String, String)()
        End Sub

        Public Default Property Item(ByVal propertyName As String) As String
            Get
                Dim result As String = Nothing
                m_Properties.TryGetValue(propertyName.ToLower(), result)
                Return result
            End Get
            Set
                If Not (String.IsNullOrEmpty(value)) Then
                    value = value.Trim()
                End If
                m_Properties(propertyName.ToLower()) = value
            End Set
        End Property

        Public Shared Function Enumerate(ByVal config As ControllerConfiguration) As List(Of UriRestConfig)
            Dim list = New List(Of UriRestConfig)()
            Dim restConfigNode = config.SelectSingleNode("/c:dataController/c:restConfig")
            If (Not (restConfigNode) Is Nothing) Then
                Dim urc As UriRestConfig = Nothing
                'configuration regex: ^\s*(?'Property'\w+)\s*(:|=)\s*(?'Value'.+?)\s*$
                Dim m = Regex.Match(restConfigNode.Value, "^\s*(?'Property'\w+)\s*(:|=)\s*(?'Value'.+?)\s*$", (RegexOptions.IgnoreCase Or RegexOptions.Multiline))
                Do While m.Success
                    Dim propertyName = m.Groups("Property").Value
                    Dim propertyValue = m.Groups("Value").Value
                    If propertyName.Equals("Uri", StringComparison.CurrentCultureIgnoreCase) Then
                        Try
                            urc = New UriRestConfig(propertyValue)
                            list.Add(urc)
                        Catch __exception As Exception
                        End Try
                    Else
                        If (Not (urc) Is Nothing) Then
                            urc(propertyName) = propertyValue
                        End If
                    End If
                    m = m.NextMatch()
                Loop
            End If
            Return list
        End Function

        Public Overridable Function IsMatch(ByVal request As HttpRequest) As Boolean
            Return m_Uri.IsMatch(request.Path)
        End Function

        Public Shared Function RequiresAuthentication(ByVal request As HttpRequest, ByVal config As ControllerConfiguration) As Boolean
            For Each urc in Enumerate(config)
                If (urc.IsMatch(request) AndAlso (urc("Users") = "?")) Then
                    Return false
                End If
            Next
            Return true
        End Function

        Public Shared Function IsAuthorized(ByVal request As HttpRequest, ByVal config As ControllerConfiguration) As Boolean
            If (request.AcceptTypes Is Nothing) Then
                Return false
            End If
            For Each urc in Enumerate(config)
                If urc.IsMatch(request) Then
                    'verify HTTP method
                    Dim httpMethod = urc("Method")
                    If Not (String.IsNullOrEmpty(httpMethod)) Then
                        Dim methodList = Regex.Split(httpMethod, "(\s*,\s*)")
                        If Not (methodList.Contains(request.HttpMethod)) Then
                            Return false
                        End If
                    End If
                    'verify user identity
                    Dim users = urc("Users")
                    If (Not (String.IsNullOrEmpty(users)) AndAlso Not ((users = "?"))) Then
                        If Not (HttpContext.Current.User.Identity.IsAuthenticated) Then
                            Return false
                        End If
                        If Not ((users = "*")) Then
                            Dim userList = Regex.Split(users, "(\s*,\s*)")
                            If Not (userList.Contains(HttpContext.Current.User.Identity.Name)) Then
                                Return false
                            End If
                        End If
                    End If
                    'verify user roles
                    Dim roles = urc("Roles")
                    If (Not (String.IsNullOrEmpty(roles)) AndAlso Not (DataControllerBase.UserIsInRole(roles))) Then
                        Return false
                    End If
                    'verify SSL, Xml, and JSON constrains
                    If (true.ToString().Equals(urc("Ssl"), StringComparison.OrdinalIgnoreCase) AndAlso Not (request.IsSecureConnection)) Then
                        Return false
                    End If
                    If (false.ToString().Equals(urc("Xml"), StringComparison.OrdinalIgnoreCase) AndAlso Not (IsJSONRequest(request))) Then
                        Return false
                    End If
                    If (false.ToString().Equals(urc("Json"), StringComparison.OrdinalIgnoreCase) AndAlso IsJSONRequest(request)) Then
                        Return false
                    End If
                    Return true
                End If
            Next
            Return false
        End Function

        Public Shared Function TypeOfJSONRequest(ByVal request As HttpRequest) As String
            If (((request.QueryString("_dataType") = "json") OrElse Not (String.IsNullOrEmpty(request.QueryString("_instance")))) OrElse Not (String.IsNullOrEmpty(request.QueryString("callback")))) Then
                Return "application/javascript"
            End If
            If (Not (request.AcceptTypes) Is Nothing) Then
                For Each t in request.AcceptTypes
                    Dim typeIndex = Array.IndexOf(UriRestConfig.SupportedJSONContentTypes, t)
                    If Not ((typeIndex = -1)) Then
                        Return t
                    End If
                Next
            End If
            Return Nothing
        End Function

        Public Shared Function IsJSONRequest(ByVal request As HttpRequest) As Boolean
            Return Not (String.IsNullOrEmpty(TypeOfJSONRequest(request)))
        End Function

        Public Shared Function IsJSONPRequest(ByVal request As HttpRequest) As Boolean
            Dim t = TypeOfJSONRequest(request)
            Return (Not (String.IsNullOrEmpty(t)) AndAlso Not ((t = SupportedJSONContentTypes(0))))
        End Function
    End Class

    Partial Public Class RepresentationalStateTransfer
        Inherits RepresentationalStateTransferBase
    End Class

    Public Class RepresentationalStateTransferBase
        Inherits Object
        Implements IHttpHandler, System.Web.SessionState.IRequiresSessionState

        Public Shared JsonDateRegex As Regex = New Regex("""\\/Date\((\-?\d+)\)\\/""")

        Public Shared ScriptResourceRegex As Regex = New Regex("^(?'ScriptName'[\w\-]+?)(\-(?'Version'[\.\d]+))?(\.(?'Culture'[\w\-]+?))?(\.(?'Ac"& _
                "cent'\w+))?\.(?'Extension'js|css)", RegexOptions.IgnoreCase)

        Public Shared CultureJavaScriptRegex As Regex = New Regex("//<\!\[CDATA\[\s+(?'JavaScript'var __cultureInfo[\s\S]*?)//\]\]>")

        Public Shared NumericTypes() As String = New String() {"SByte", "Byte", "Int16", "Int32", "UInt32", "Int64", "Single", "Double", "Decimal", "Currency"}

        Overridable ReadOnly Property IHttpHandler_IsReusable() As Boolean Implements IHttpHandler.IsReusable
            Get
                Return true
            End Get
        End Property

        Protected Overridable ReadOnly Property HttpMethod() As String
            Get
                Dim request = HttpContext.Current.Request
                Dim requestType = request.HttpMethod
                If ((requestType = "GET") AndAlso Not (String.IsNullOrEmpty(request("callback")))) Then
                    Dim t = request.QueryString("_type")
                    If Not (String.IsNullOrEmpty(t)) Then
                        requestType = t
                    End If
                End If
                Return requestType
            End Get
        End Property

        Overridable Sub IHttpHandler_ProcessRequest(ByVal context As HttpContext) Implements IHttpHandler.ProcessRequest
            CultureManager.Initialize()
            Dim routeValues = context.Request.RequestContext.RouteData.Values
            Dim controllerName = CType(routeValues("Controller"),String)
            If String.IsNullOrEmpty(controllerName) Then
                controllerName = context.Request.QueryString("_controller")
            End If
            Dim output = context.Response.OutputStream
            Dim contentType = "text/xml"
            Dim json = UriRestConfig.IsJSONRequest(context.Request)
            If json Then
                contentType = (UriRestConfig.TypeOfJSONRequest(context.Request) + "; charset=utf-8")
            End If
            context.Response.ContentType = contentType
            Try
                If (controllerName = "saas") Then
                    Dim service = CType(routeValues("Segment1"),String)
                    Dim handler = OAuthHandlerFactory.Create(service)
                    If (Not (handler) Is Nothing) Then
                        handler.ProcessRequest(context)
                    End If
                Else
                    If (controllerName = "_authenticate") Then
                        AuthenticateSaaS(context)
                    Else
                        Dim script = ScriptResourceRegex.Match(controllerName)
                        Dim scriptName = script.Groups("ScriptName").Value
                        Dim isSaaS = (scriptName = "factory")
                        Dim isCombinedScript = (scriptName = "combined")
                        Dim isStylesheet = (scriptName = "stylesheet")
                        If ((isStylesheet OrElse (scriptName = "touch-theme")) AndAlso (script.Groups("Extension").Value = "css")) Then
                            context.Response.ContentType = "text/css"
                            Dim css = String.Empty
                            If isStylesheet Then
                                css = ApplicationServices.CombineTouchUIStylesheets(context)
                            Else
                                css = StylesheetGenerator.Compile(controllerName)
                            End If
                            ApplicationServices.CompressOutput(context, css)
                        Else
                            If ((isSaaS OrElse isCombinedScript) AndAlso (HttpMethod = "GET")) Then
                                CombineScripts(context, isSaaS, scriptName, script.Groups("Culture").Value, script.Groups("Version").Value)
                            Else
                                If Regex.IsMatch(HttpMethod, "^(GET|POST|DELETE|PUT)$") Then
                                    PerformRequest(context, output, json, controllerName)
                                Else
                                    context.Response.StatusCode = 400
                                End If
                            End If
                        End If
                    End If
                End If
            Catch er As Exception
                If Not ((context.Response.StatusCode = 302)) Then
                    context.Response.ContentType = "text/xml"
                    context.Response.Clear()
                    Dim writer = CreateXmlWriter(output)
                    RenderException(context, er, writer)
                    writer.Close()
                    context.Response.StatusCode = 400
                End If
            End Try
        End Sub

        Protected Overridable Sub CombineScripts(ByVal context As HttpContext, ByVal isSaaS As Boolean, ByVal scriptName As String, ByVal culture As String, ByVal version As String)
            Dim request = context.Request
            Dim response = context.Response
            If Not (isSaaS) Then
                Dim cache = response.Cache
                cache.SetCacheability(HttpCacheability.Public)
                cache.VaryByParams("_touch") = true
                cache.VaryByHeaders("User-Agent") = true
                cache.SetOmitVaryStar(true)
                cache.SetExpires(DateTime.Now.AddDays(365))
                cache.SetValidUntilExpires(true)
                cache.SetLastModifiedFromFileDependencies()
            End If
            If isSaaS Then
                If Not (String.IsNullOrEmpty(culture)) Then
                    Try
                        Thread.CurrentThread.CurrentCulture = New CultureInfo(culture)
                        Thread.CurrentThread.CurrentUICulture = New CultureInfo(culture)
                    Catch __exception As Exception
                    End Try
                End If
            End If
            Dim sb = New StringBuilder()
            Dim baseUrl = String.Format("{0}://{1}{2}", request.Url.Scheme, request.Url.Authority, request.ApplicationPath)
            Dim scripts = AquariumExtenderBase.StandardScripts(true)
            For Each sr in scripts
                Dim add = true
                Dim path = sr.Path
                Dim index = path.IndexOf("?")
                If (index > 0) Then
                    path = path.Substring(0, index)
                    If path.EndsWith("_System.js") Then
                        add = Not ((request.QueryString("jquery") = "false"))
                    Else
                        If (path.Contains("daf-membership") AndAlso Not (ApplicationServicesBase.AuthorizationIsSupported)) Then
                            add = false
                        End If
                    End If
                End If
                If add Then
                    Try
                        Dim script As String
                        If path.Equals("~/js/daf/add.min.js") Then
                            script = ApplicationServices.Current.AddScripts()
                        Else
                            If String.IsNullOrEmpty(path) Then
                                script = New StreamReader([GetType]().Assembly.GetManifestResourceStream(sr.Name)).ReadToEnd()
                            Else
                                script = File.ReadAllText(context.Server.MapPath(path))
                            End If
                        End If
                        script = script.Replace(" sourceMappingURL=", " sourceMappingURL=../js/")
                        sb.AppendLine(script)
                        If Not (script.EndsWith(";")) Then
                            sb.Append(";")
                        End If
                    Catch ex As Exception
                        sb.AppendFormat("alert('{0}');", BusinessRules.JavaScriptString(String.Format("Unable to load {0}{1}:"&Global.Microsoft.VisualBasic.ChrW(10)&Global.Microsoft.VisualBasic.ChrW(10)&"{2}", path, sr.Name, ex.Message)))
                    End Try
                End If
            Next
            If isSaaS Then
                If ApplicationServices.IsTouchClient Then
                    sb.AppendFormat(String.Format("$('<link></link>').appendTo($('head')).attr({{ href: '{0}/css//jquery.mobile-{1}."& _
                                "min.css', type: 'text/css', rel: 'stylesheet' }});", ApplicationServices.JqmVersion), baseUrl, ApplicationServices.JqmVersion)
                Else
                    sb.AppendFormat(String.Format("$('<link></link>').appendTo($('head')).attr({{ href: '{0}/App_Themes/MyCompany/_T"& _
                                "heme_DarkKnight.css?{0}', type: 'text/css', rel: 'stylesheet' }});", ApplicationServices.Version), baseUrl)
                End If
                Try
                    Dim blankPage = New StringBuilder()
                    Dim sw = New StringWriter(blankPage)
                    context.Server.Execute("~/default.aspx?_page=_blank", sw)
                    sw.Flush()
                    sw.Close()
                    Dim cultureJS = CultureJavaScriptRegex.Match(blankPage.ToString())
                    If cultureJS.Success Then
                        sb.AppendLine(cultureJS.Groups("JavaScript").Value)
                        sb.AppendLine("Sys.CultureInfo.CurrentCulture=__cultureInfo;")
                    End If
                Catch __exception As Exception
                End Try
                sb.AppendFormat("var __targetFramework='4.7.2';__tf=4.0;__cothost='appfactory';__appInfo='76901097"& _
                        "78|{0}';", BusinessRules.JavaScriptString(context.User.Identity.Name))
                sb.AppendFormat("Sys.Application.add_init(function() {{ Web.DataView._run('{0}','{0}/Services/Data"& _
                        "ControllerService.asmx', {1}) }});", baseUrl, context.User.Identity.IsAuthenticated.ToString().ToLower())
            End If
            context.Response.ContentType = "application/javascript"
            ApplicationServices.CompressOutput(context, sb.ToString())
        End Sub

        Protected Overridable Sub AuthenticateSaaS(ByVal context As HttpContext)
            Dim request = context.Request
            Dim response = context.Response
            Dim args = request.Params("args")
            Dim result = New StringBuilder(String.Format("{0}(", request.QueryString("callback")))
            Dim resultObject As Object = false
            Dim login = JsonConvert.DeserializeObject(Of String())(args)
            resultObject = ApplicationServices.Login(CType(login(0),String), CType(login(1),String), false)
            result.Append(JsonConvert.SerializeObject(resultObject))
            result.Append(")")
            Dim jsonp = result.ToString()
            response.Write(jsonp)
        End Sub

        Private Function DoReplaceDateTicks(ByVal m As Match) As String
            Return String.Format("new Date({0})", m.Groups(1).Value)
        End Function

        Friend Overridable Function CreateXmlWriter(ByVal output As Stream) As XmlWriter
            Dim settings = New XmlWriterSettings()
            settings.CloseOutput = false
            settings.Indent = true
            Dim writer = XmlWriter.Create(output, settings)
            Return writer
        End Function

        Friend Overridable Sub RenderException(ByVal context As HttpContext, ByVal er As Exception, ByVal writer As XmlWriter)
            If (Not (er) Is Nothing) Then
                writer.WriteStartElement("error")
                writer.WriteElementString("message", er.Message)
                writer.WriteElementString("type", er.GetType().ToString())
                If (context.Request.UserHostName = "::1") Then
                    writer.WriteStartElement("stackTrace")
                    writer.WriteCData(er.StackTrace)
                    writer.WriteEndElement()
                    RenderException(context, er.InnerException, writer)
                End If
                writer.WriteEndElement()
            End If
        End Sub

        Protected Function SelectView(ByVal config As ControllerConfiguration, ByVal viewId As String) As XPathNavigator
            Return config.SelectSingleNode("/c:dataController/c:views/c:view[@id='{0}']", viewId)
        End Function

        Protected Function SelectDataField(ByVal config As ControllerConfiguration, ByVal viewId As String, ByVal fieldName As String) As XPathNavigator
            Return config.SelectSingleNode("/c:dataController/c:views/c:view[@id='{0}']/.//c:dataField[@fieldName='{1}' or @a"& _
                    "liasFieldName='{1}']", viewId, fieldName)
        End Function

        Protected Function SelectField(ByVal config As ControllerConfiguration, ByVal name As String) As XPathNavigator
            Return config.SelectSingleNode("/c:dataController/c:fields/c:field[@name='{0}']", name)
        End Function

        Protected Function SelectActionGroup(ByVal config As ControllerConfiguration, ByVal actionGroupId As String) As XPathNavigator
            Return config.SelectSingleNode("/c:dataController/c:actions/c:actionGroup[@id='{0}']", actionGroupId)
        End Function

        Protected Function SelectAction(ByVal config As ControllerConfiguration, ByVal actionGroupId As String, ByVal actionId As String) As XPathNavigator
            Return config.SelectSingleNode("/c:dataController/c:actions/c:actionGroup[@id='{0}']/c:action[@id='{1}']", actionGroupId, actionId)
        End Function

        Private Function VerifyActionSegments(ByVal config As ControllerConfiguration, ByVal actionGroupId As String, ByVal actionId As String, ByVal keyIsAvailable As Boolean) As Boolean
            Dim result = true
            If (Not (SelectActionGroup(config, actionGroupId)) Is Nothing) Then
                Dim actionNode = SelectAction(config, actionGroupId, actionId)
                If (actionNode Is Nothing) Then
                    result = false
                Else
                    If (Not (keyIsAvailable) AndAlso ((actionNode.GetAttribute("whenKeySelected", String.Empty) = "true") OrElse Regex.IsMatch(actionNode.GetAttribute("commandName", String.Empty), "^(Update|Delete)$"))) Then
                        result = false
                    End If
                End If
            Else
                result = false
            End If
            Return result
        End Function

        Private Sub AnalyzeRouteValues(ByVal request As HttpRequest, ByVal response As HttpResponse, ByVal isHttpGetMethod As Boolean, ByVal config As ControllerConfiguration, ByRef view As String, ByRef key As String, ByRef fieldName As String, ByRef actionGroupId As String, ByRef actionId As String, ByRef commandName As String)
            Dim routeValues = request.RequestContext.RouteData.Values
            Dim segment1 = CType(routeValues("Segment1"),String)
            Dim segment2 = CType(routeValues("Segment2"),String)
            Dim segment3 = CType(routeValues("Segment3"),String)
            Dim segment4 = CType(routeValues("Segment4"),String)
            view = Nothing
            key = Nothing
            fieldName = Nothing
            actionGroupId = Nothing
            actionId = Nothing
            commandName = Nothing
            If Not (String.IsNullOrEmpty(segment1)) Then
                If (Not (SelectView(config, segment1)) Is Nothing) Then
                    view = segment1
                    If isHttpGetMethod Then
                        key = segment2
                        fieldName = segment3
                    Else
                        If VerifyActionSegments(config, segment2, segment3, false) Then
                            actionGroupId = segment2
                            actionId = segment3
                        Else
                            If String.IsNullOrEmpty(segment2) Then
                                If Not ((HttpMethod = "POST")) Then
                                    response.StatusCode = 404
                                End If
                            Else
                                key = segment2
                                If VerifyActionSegments(config, segment3, segment4, true) Then
                                    actionGroupId = segment3
                                    actionId = segment4
                                Else
                                    If Not (((HttpMethod = "PUT") OrElse (HttpMethod = "DELETE"))) Then
                                        response.StatusCode = 404
                                    End If
                                End If
                            End If
                        End If
                    End If
                Else
                    If isHttpGetMethod Then
                        key = segment1
                        fieldName = segment2
                    Else
                        If VerifyActionSegments(config, segment1, segment2, false) Then
                            actionGroupId = segment1
                            actionId = segment2
                        Else
                            If String.IsNullOrEmpty(segment1) Then
                                response.StatusCode = 404
                            Else
                                key = segment1
                                If VerifyActionSegments(config, segment2, segment3, true) Then
                                    actionGroupId = segment2
                                    actionId = segment3
                                Else
                                    If Not (((HttpMethod = "PUT") OrElse (HttpMethod = "DELETE"))) Then
                                        response.StatusCode = 404
                                    End If
                                End If
                            End If
                        End If
                    End If
                End If
            Else
                view = request.QueryString("_view")
                key = request.QueryString("_key")
                fieldName = request.QueryString("_fieldName")
                If Not (isHttpGetMethod) Then
                    actionGroupId = request.QueryString("_actionId")
                End If
            End If
            If Not (isHttpGetMethod) Then
                Dim actionNode = SelectAction(config, actionGroupId, actionId)
                If (Not (actionNode) Is Nothing) Then
                    commandName = actionNode.GetAttribute("commandName", String.Empty)
                Else
                    commandName = HttpMethodToCommandName(request)
                End If
            End If
        End Sub

        Private Function HttpMethodToCommandName(ByVal request As HttpRequest) As String
            If (HttpMethod = "POST") Then
                Return "Insert"
            End If
            If (HttpMethod = "PUT") Then
                Return "Update"
            End If
            If (HttpMethod = "DELETE") Then
                Return "Delete"
            End If
            Return Nothing
        End Function

        Protected Overridable Function AuthorizeRequest(ByVal request As HttpRequest, ByVal config As ControllerConfiguration) As Boolean
            Return UriRestConfig.IsAuthorized(request, config)
        End Function

        Private Sub PerformRequest(ByVal context As HttpContext, ByVal output As Stream, ByVal json As Boolean, ByVal controllerName As String)
            Dim request = context.Request
            Dim response = context.Response
            Dim config As ControllerConfiguration = Nothing
            Try
                config = DataControllerBase.CreateConfigurationInstance([GetType](), controllerName)
            Catch __exception As Exception
                response.StatusCode = 404
                Return
            End Try
            If Not (AuthorizeRequest(request, config)) Then
                response.StatusCode = 404
                Return
            End If
            'analyze route segments
            Dim isHttpGetMethod = (HttpMethod = "GET")
            Dim view As String = Nothing
            Dim key As String = Nothing
            Dim fieldName As String = Nothing
            Dim actionGroupId As String = Nothing
            Dim actionId As String = Nothing
            Dim commandName As String = Nothing
            AnalyzeRouteValues(request, response, isHttpGetMethod, config, view, key, fieldName, actionGroupId, actionId, commandName)
            If (response.StatusCode = 404) Then
                Return
            End If
            Dim keyIsAvailable = Not (String.IsNullOrEmpty(key))
            If String.IsNullOrEmpty(view) Then
                If isHttpGetMethod Then
                    view = Controller.GetSelectView(controllerName)
                Else
                    If (commandName = "Insert") Then
                        view = Controller.GetInsertView(controllerName)
                    Else
                        If (commandName = "Update") Then
                            view = Controller.GetUpdateView(controllerName)
                        Else
                            If (commandName = "Delete") Then
                                view = Controller.GetDeleteView(controllerName)
                            End If
                        End If
                    End If
                End If
            End If
            If (SelectView(config, view) Is Nothing) Then
                response.StatusCode = 404
                Return
            End If
            Dim dataFieldNode As XPathNavigator = Nothing
            Dim fieldNode As XPathNavigator = Nothing
            If Not (String.IsNullOrEmpty(fieldName)) Then
                dataFieldNode = SelectDataField(config, view, fieldName)
                fieldNode = SelectField(config, fieldName)
                If ((dataFieldNode Is Nothing) OrElse (fieldNode Is Nothing)) Then
                    response.StatusCode = 404
                    Return
                End If
            End If
            'create a filter
            Dim filter = New List(Of String)()
            'process key fields
            If keyIsAvailable Then
                Dim values = key.Split(New Char() {Global.Microsoft.VisualBasic.ChrW(44)}, StringSplitOptions.RemoveEmptyEntries)
                Dim keyIterator = config.Select("/c:dataController/c:fields/c:field[@isPrimaryKey='true']")
                Dim index = 0
                Do While keyIterator.MoveNext()
                    filter.Add(String.Format("{0}:={1}", keyIterator.Current.GetAttribute("name", String.Empty), values(index)))
                    index = (index + 1)
                Loop
            End If
            'process quick find
            Dim quickFind = request.Params("_q")
            If Not (String.IsNullOrEmpty(quickFind)) Then
                filter.Add(String.Format("{0}:~{1}", config.SelectSingleNode("/c:dataController/c:views/c:view[@id='{0}']/.//c:dataField[1]/@fieldName", view).Value, quickFind))
            End If
            'process filter parameters
            If Not (keyIsAvailable) Then
                For Each filterName As String in request.Params.Keys
                    If (Not (SelectDataField(config, view, filterName)) Is Nothing) Then
                        filter.Add(String.Format("{0}:={1}", filterName, request.Params(filterName)))
                    Else
                        Dim m = BusinessRules.SqlFieldFilterOperationRegex.Match(filterName)
                        Dim filterFieldName = m.Groups("Name").Value
                        If (m.Success AndAlso (Not (SelectDataField(config, view, filterFieldName)) Is Nothing)) Then
                            Dim operation = m.Groups("Operation").Value
                            Dim filterOperation = CType(TypeDescriptor.GetConverter(GetType(RowFilterOperation)).ConvertFromString(operation),RowFilterOperation)
                            Dim filterValue = request.Params(filterName)
                            If ((filterOperation = RowFilterOperation.Includes) OrElse (filterOperation = RowFilterOperation.DoesNotInclude)) Then
                                filterValue = Regex.Replace(filterValue, ",", "$or$")
                            Else
                                If (filterOperation = RowFilterOperation.Between) Then
                                    filterValue = Regex.Replace(filterValue, ",", "$and$")
                                End If
                            End If
                            filter.Add(String.Format("{0}:{1}{2}", filterFieldName, RowFilterAttribute.ComparisonOperations(Convert.ToInt32(filterOperation)), filterValue))
                        End If
                    End If
                Next
            End If
            'execute request
            If isHttpGetMethod Then
                If (Not (fieldNode) Is Nothing) Then
                    Dim style = "o"
                    If (request.QueryString("_style") = "Thumbnail") Then
                        style = "t"
                    End If
                    Dim blobPath = String.Format("~/Blob.ashx?{0}={1}|{2}", fieldNode.GetAttribute("onDemandHandler", String.Empty), style, key)
                    context.RewritePath(blobPath)
                    Dim blobHandler = New Blob()
                    CType(blobHandler,IHttpHandler).ProcessRequest(context)
                Else
                    ExecuteHttpGetRequest(request, response, output, json, controllerName, view, filter, keyIsAvailable)
                End If
            Else
                ExecuteActionRequest(request, response, output, json, config, controllerName, view, key, filter, actionGroupId, actionId)
            End If
        End Sub

        Private Sub ExecuteActionRequest(ByVal request As HttpRequest, ByVal response As HttpResponse, ByVal output As Stream, ByVal json As Boolean, ByVal config As ControllerConfiguration, ByVal controllerName As String, ByVal view As String, ByVal key As String, ByVal filter As List(Of String), ByVal actionGroupId As String, ByVal actionId As String)
            Dim actionNode = SelectAction(config, actionGroupId, actionId)
            Dim commandName = HttpMethodToCommandName(request)
            Dim commandArgument = String.Empty
            Dim lastCommandName = String.Empty
            If (actionNode Is Nothing) Then
                If String.IsNullOrEmpty(commandName) Then
                    response.StatusCode = 404
                    Return
                End If
            Else
                commandName = actionNode.GetAttribute("commandName", String.Empty)
                commandArgument = actionNode.GetAttribute("commandArgument", String.Empty)
                lastCommandName = actionNode.GetAttribute("whenLastCommandName", String.Empty)
            End If
            'prepare action arguments
            Dim args = New ActionArgs()
            args.Controller = controllerName
            args.View = view
            args.CommandName = commandName
            args.CommandArgument = commandArgument
            args.LastCommandName = lastCommandName
            args.Filter = filter.ToArray()
            args.SortExpression = request.QueryString("_sortExpression")
            Dim selectedValues = request.Params("_selectedValues")
            If Not (String.IsNullOrEmpty(selectedValues)) Then
                args.SelectedValues = selectedValues.Split(New Char() {Global.Microsoft.VisualBasic.ChrW(44)}, StringSplitOptions.RemoveEmptyEntries)
            End If
            args.Trigger = request.Params("_trigger")
            args.Path = String.Format("{0}/{1}", actionGroupId, actionId)
            Dim form = request.Form
            If (request.HttpMethod = "GET") Then
                form = request.QueryString
            End If
            Dim values = New List(Of FieldValue)()
            For Each fieldName As String in form.Keys
                Dim field = SelectField(config, fieldName)
                Dim dataField = SelectDataField(config, view, fieldName)
                If (Not (field) Is Nothing) Then
                    Dim oldValue As Object = form((fieldName + "_OldValue"))
                    Dim value As Object = form(fieldName)
                    'try parsing the values
                    Dim dataFormatString As String = Nothing
                    If (Not (dataField) Is Nothing) Then
                        dataFormatString = dataField.GetAttribute("dataFormatString", String.Empty)
                    End If
                    If String.IsNullOrEmpty(dataFormatString) Then
                        dataFormatString = field.GetAttribute("dataFormatString", String.Empty)
                    End If
                    If (Not (String.IsNullOrEmpty(dataFormatString)) AndAlso Not (dataFormatString.StartsWith("{"))) Then
                        dataFormatString = String.Format("{{0:{0}}}", dataFormatString)
                    End If
                    Dim fieldType = field.GetAttribute("type", String.Empty)
                    If NumericTypes.Contains(fieldType) Then
                        Dim d As Double
                        If [Double].TryParse(CType(value,String), NumberStyles.Any, CultureInfo.CurrentUICulture, d) Then
                            value = d
                        End If
                        If [Double].TryParse(CType(oldValue,String), NumberStyles.Any, CultureInfo.CurrentUICulture, d) Then
                            oldValue = d
                        End If
                    Else
                        If (fieldType = "DateTime") Then
                            Dim dt As DateTime
                            If Not (String.IsNullOrEmpty(dataFormatString)) Then
                                If DateTime.TryParseExact(CType(value,String), dataFormatString, CultureInfo.CurrentUICulture, DateTimeStyles.None, dt) Then
                                    value = dt
                                End If
                                If DateTime.TryParseExact(CType(oldValue,String), dataFormatString, CultureInfo.CurrentUICulture, DateTimeStyles.None, dt) Then
                                    oldValue = dt
                                End If
                            Else
                                If DateTime.TryParse(CType(value,String), dt) Then
                                    value = dt
                                End If
                                If DateTime.TryParse(CType(oldValue,String), dt) Then
                                    oldValue = dt
                                End If
                            End If
                        End If
                    End If
                    'create a field value
                    Dim fvo As FieldValue = Nothing
                    If (Not (oldValue) Is Nothing) Then
                        fvo = New FieldValue(fieldName, oldValue, value)
                    Else
                        fvo = New FieldValue(fieldName, value)
                    End If
                    'figure if the field is read-only
                    Dim isReadOnly = (field.GetAttribute("readOnly", String.Empty) = "true")
                    Dim writeRoles = field.GetAttribute("writeRoles", String.Empty)
                    If (Not (String.IsNullOrEmpty(writeRoles)) AndAlso Not (DataControllerBase.UserIsInRole(writeRoles))) Then
                        isReadOnly = true
                    End If
                    If (dataField Is Nothing) Then
                        isReadOnly = true
                    End If
                    fvo.ReadOnly = isReadOnly
                    'add field value to the list
                    values.Add(fvo)
                End If
            Next
            Dim keyIndex = 0
            Dim keyIterator = config.Select("/c:dataController/c:fields/c:field[@isPrimaryKey='true']")
            Do While keyIterator.MoveNext()
                Dim fieldName = keyIterator.Current.GetAttribute("name", String.Empty)
                For Each fvo in values
                    If (fvo.Name = fieldName) Then
                        fieldName = Nothing
                        If ((fvo.OldValue Is Nothing) AndAlso ((commandName = "Update") OrElse (commandName = "Delete"))) Then
                            fvo.OldValue = fvo.NewValue
                            fvo.Modified = false
                        End If
                        Exit For
                    End If
                Next
                If Not (String.IsNullOrEmpty(fieldName)) Then
                    Dim oldValue As String = Nothing
                    If Not (String.IsNullOrEmpty(key)) Then
                        Dim keyValues = key.Split(New Char() {Global.Microsoft.VisualBasic.ChrW(44)}, StringSplitOptions.RemoveEmptyEntries)
                        If (keyIndex < keyValues.Length) Then
                            oldValue = keyValues(keyIndex)
                        End If
                    End If
                    values.Add(New FieldValue(fieldName, oldValue, oldValue))
                End If
                keyIndex = (keyIndex + 1)
            Loop
            args.Values = values.ToArray()
            'execute action
            Dim controllerInstance = ControllerFactory.CreateDataController()
            Dim result = controllerInstance.Execute(controllerName, view, args)
            'redirect response location if success or error url has been specified
            Dim successUrl = request.Params("_successUrl")
            Dim errorUrl = request.Params("_errorUrl")
            If ((result.Errors.Count = 0) AndAlso Not (String.IsNullOrEmpty(successUrl))) Then
                response.RedirectLocation = successUrl
                response.StatusCode = 301
                Return
            End If
            If ((result.Errors.Count > 0) AndAlso Not (String.IsNullOrEmpty(errorUrl))) Then
                If errorUrl.Contains("?") Then
                    errorUrl = (errorUrl + "&")
                Else
                    errorUrl = (errorUrl + "?")
                End If
                errorUrl = String.Format("{0}_error={1}", errorUrl, HttpUtility.UrlEncode(result.Errors(0)))
                response.RedirectLocation = errorUrl
                response.StatusCode = 301
                Return
            End If
            If json Then
                Dim sw = CreateStreamWriter(request, response, output)
                BeginResponsePadding(request, sw)
                sw.Write("{{""rowsAffected"":{0}", result.RowsAffected)
                If ((Not (result.Errors) Is Nothing) AndAlso (result.Errors.Count > 0)) Then
                    sw.Write(",""errors"":[")
                    Dim first = true
                    For Each er in result.Errors
                        If first Then
                            first = false
                        Else
                            sw.Write(",")
                        End If
                        sw.Write("{{""message"":""{0}""}}", BusinessRules.JavaScriptString(er))
                    Next
                    sw.Write("]")
                End If
                If Not (String.IsNullOrEmpty(result.ClientScript)) Then
                    sw.Write(",""clientScript"":""{0}""", BusinessRules.JavaScriptString(result.ClientScript))
                End If
                If Not (String.IsNullOrEmpty(result.NavigateUrl)) Then
                    sw.Write(",""navigateUrl"":""{0}""", BusinessRules.JavaScriptString(result.NavigateUrl))
                End If
                If (Not (result.Values) Is Nothing) Then
                    For Each fvo in result.Values
                        sw.Write(",""{0}"":", fvo.Name)
                        WriteJSONValue(sw, fvo.Value, Nothing)
                    Next
                End If
                sw.Write("}")
                EndResponsePadding(request, sw)
                sw.Close()
            Else
                Dim writer = CreateXmlWriter(output)
                writer.WriteStartDocument()
                writer.WriteStartElement("result")
                writer.WriteAttributeString("rowsAffected", result.RowsAffected.ToString())
                If ((Not (result.Errors) Is Nothing) AndAlso (result.Errors.Count > 0)) Then
                    writer.WriteStartElement("errors")
                    For Each er in result.Errors
                        writer.WriteStartElement("error")
                        writer.WriteAttributeString("message", er)
                        writer.WriteEndElement()
                    Next
                    writer.WriteEndElement()
                End If
                If Not (String.IsNullOrEmpty(result.ClientScript)) Then
                    writer.WriteAttributeString("clientScript", result.ClientScript)
                End If
                If Not (String.IsNullOrEmpty(result.NavigateUrl)) Then
                    writer.WriteAttributeString("navigateUrl", result.NavigateUrl)
                End If
                If (Not (result.Values) Is Nothing) Then
                    For Each fvo in result.Values
                        writer.WriteElementString(fvo.Name, Convert.ToString(fvo.Value))
                    Next
                End If
                writer.WriteEndElement()
                writer.WriteEndDocument()
                writer.Close()
            End If
        End Sub

        Protected Overridable Sub WriteJSONValue(ByVal writer As StreamWriter, ByVal v As Object, ByVal field As DataField)
            Dim dataFormatString As String = Nothing
            If (Not (field) Is Nothing) Then
                dataFormatString = field.DataFormatString
            End If
            If (v Is Nothing) Then
                writer.Write("null")
            Else
                If TypeOf v Is String Then
                    writer.Write("""{0}""", BusinessRules.JavaScriptString(CType(v,String)))
                Else
                    If TypeOf v Is DateTime Then
                        writer.Write("""{0}""", ConvertDateToJSON(CType(v,DateTime), dataFormatString))
                    Else
                        If TypeOf v Is Guid Then
                            writer.Write("""{0}""", BusinessRules.JavaScriptString(v.ToString()))
                        Else
                            If TypeOf v Is Boolean Then
                                writer.Write(v.ToString().ToLower())
                            Else
                                If Not (String.IsNullOrEmpty(dataFormatString)) Then
                                    writer.Write("""{0}""", ConvertValueToJSON(v, dataFormatString))
                                Else
                                    writer.Write(ConvertValueToJSON(v, Nothing))
                                End If
                            End If
                        End If
                    End If
                End If
            End If
        End Sub

        Protected Overridable Sub ExecuteHttpGetRequest(ByVal request As HttpRequest, ByVal response As HttpResponse, ByVal output As Stream, ByVal json As Boolean, ByVal controllerName As String, ByVal view As String, ByVal filter As List(Of String), ByVal keyIsAvailable As Boolean)
            'prepare a page request
            Dim pageSize As Integer
            Integer.TryParse(request.QueryString("_pageSize"), pageSize)
            If (pageSize = 0) Then
                pageSize = 100
            End If
            Dim pageIndex As Integer
            Integer.TryParse(request.QueryString("_pageIndex"), pageIndex)
            Dim r = New PageRequest()
            r.Controller = controllerName
            r.View = view
            r.RequiresMetaData = true
            r.PageSize = pageSize
            r.PageIndex = pageIndex
            r.Filter = filter.ToArray()
            r.RequiresRowCount = ((pageIndex = 0) AndAlso Not (keyIsAvailable))
            r.SortExpression = request.QueryString("_sortExpression")
            'request the data
            Dim controllerInstance = ControllerFactory.CreateDataController()
            Dim page = controllerInstance.GetPage(r.Controller, r.View, r)
            If (keyIsAvailable AndAlso (page.Rows.Count = 0)) Then
                response.StatusCode = 404
                Return
            End If
            'stream out the data
            Dim writer As XmlWriter = Nothing
            Dim sw As StreamWriter = Nothing
            If json Then
                sw = CreateStreamWriter(request, response, output)
                BeginResponsePadding(request, sw)
                If Not (keyIsAvailable) Then
                    sw.Write("{")
                    If r.RequiresRowCount Then
                        sw.Write("""totalRowCount"":{0},", page.TotalRowCount)
                    End If
                    sw.Write("""pageSize"":{0},""pageIndex"":{1},""rowCount"":{2},", page.PageSize, page.PageIndex, page.Rows.Count)
                    sw.Write("""{0}"":[", controllerName)
                End If
            Else
                writer = CreateXmlWriter(output)
                writer.WriteStartDocument()
                writer.WriteStartElement(controllerName)
                If r.RequiresRowCount Then
                    writer.WriteAttributeString("totalRowCount", page.TotalRowCount.ToString())
                End If
                If Not (keyIsAvailable) Then
                    writer.WriteAttributeString("pageSize", page.PageSize.ToString())
                    writer.WriteAttributeString("pageIndex", page.PageIndex.ToString())
                    writer.WriteAttributeString("rowCount", page.Rows.Count.ToString())
                    writer.WriteStartElement("items")
                End If
            End If
            Dim firstRow = true
            For Each field in page.Fields
                If (Not (String.IsNullOrEmpty(field.DataFormatString)) AndAlso Not (field.DataFormatString.StartsWith("{"))) Then
                    field.DataFormatString = String.Format("{{0:{0}}}", field.DataFormatString)
                End If
            Next
            For Each row in page.Rows
                Dim index = 0
                If json Then
                    If firstRow Then
                        firstRow = false
                    Else
                        sw.Write(",")
                    End If
                    sw.Write("{")
                Else
                    If Not (keyIsAvailable) Then
                        writer.WriteStartElement("item")
                    End If
                End If
                Dim firstField = true
                For Each field in page.Fields
                    If json Then
                        If firstField Then
                            firstField = false
                        Else
                            sw.Write(",")
                        End If
                        sw.Write("""{0}"":", field.Name)
                        WriteJSONValue(sw, row(index), field)
                    Else
                        Dim v = row(index)
                        If (Not (v) Is Nothing) Then
                            Dim s As String = Nothing
                            If Not (String.IsNullOrEmpty(field.DataFormatString)) Then
                                s = String.Format(field.DataFormatString, v)
                            Else
                                s = Convert.ToString(v)
                            End If
                            writer.WriteAttributeString(field.Name, s)
                        End If
                    End If
                    index = (index + 1)
                Next
                If json Then
                    sw.Write("}")
                Else
                    If Not (keyIsAvailable) Then
                        writer.WriteEndElement()
                    End If
                End If
                If keyIsAvailable Then
                    Exit For
                End If
            Next
            If json Then
                If Not (keyIsAvailable) Then
                    sw.Write("]}")
                End If
                EndResponsePadding(request, sw)
                sw.Close()
            Else
                If Not (keyIsAvailable) Then
                    writer.WriteEndElement()
                End If
                writer.WriteEndElement()
                writer.WriteEndDocument()
                writer.Close()
            End If
        End Sub

        Protected Overridable Function ConvertValueToJSON(ByVal v As Object, ByVal dataFormatString As String) As String
            If String.IsNullOrEmpty(dataFormatString) Then
                Return v.ToString()
            Else
                Return String.Format(dataFormatString, v)
            End If
        End Function

        Protected Overridable Function ConvertDateToJSON(ByVal dt As DateTime, ByVal dataFormatString As String) As String
            dt = dt.ToUniversalTime()
            If String.IsNullOrEmpty(dataFormatString) Then
                Return dt.ToString("F")
            Else
                Return String.Format(dataFormatString, dt)
            End If
        End Function

        Protected Overridable Sub BeginResponsePadding(ByVal request As HttpRequest, ByVal sw As StreamWriter)
            Dim callback = request.QueryString("callback")
            If Not (String.IsNullOrEmpty(callback)) Then
                sw.Write("{0}(", callback)
            Else
                If ((request.HttpMethod = "GET") AndAlso UriRestConfig.IsJSONPRequest(request)) Then
                    Dim instance = request.QueryString("_instance")
                    If String.IsNullOrEmpty(instance) Then
                        instance = CType(request.RequestContext.RouteData.Values("Controller"),String)
                    End If
                    sw.Write("MyCompany=typeof MyCompany=='undefined'?{{}}:MyCompany;MyCompany.{0}=", instance)
                End If
            End If
        End Sub

        Protected Overridable Sub EndResponsePadding(ByVal request As HttpRequest, ByVal sw As StreamWriter)
            Dim callback = request.QueryString("callback")
            If Not (String.IsNullOrEmpty(callback)) Then
                sw.Write(")")
            Else
                If ((request.HttpMethod = "GET") AndAlso UriRestConfig.IsJSONPRequest(request)) Then
                    sw.Write(";")
                End If
            End If
        End Sub

        Protected Overridable Function CreateStreamWriter(ByVal request As HttpRequest, ByVal response As HttpResponse, ByVal output As Stream) As StreamWriter
            Dim acceptEncoding = request.Headers("Accept-Encoding")
            If Not (String.IsNullOrEmpty(acceptEncoding)) Then
                Dim encodings = acceptEncoding.Split(Global.Microsoft.VisualBasic.ChrW(44))
                If encodings.Contains("gzip") Then
                    output = New GZipStream(output, CompressionMode.Compress)
                    response.AppendHeader("Content-Encoding", "gzip")
                Else
                    If encodings.Contains("deflate") Then
                        output = New DeflateStream(output, CompressionMode.Compress)
                        response.AppendHeader("Content-Encoding", "deflate")
                    End If
                End If
            End If
            Return New StreamWriter(output)
        End Function
    End Class

    Partial Public Class FacebookOAuthHandler
        Inherits FacebookOAuthHandlerBase
    End Class

    Partial Public Class FacebookOAuthHandlerBase
        Inherits OAuthHandler

        Private m_UserObj As JObject

        Protected Overrides ReadOnly Property Scope() As String
            Get
                Return ("email" + Config("Scope")).Trim()
            End Get
        End Property

        Protected Overridable Function GetVersion() As String
            Dim version = Config("Version")
            If String.IsNullOrEmpty(version) Then
                version = "v2.8"
            End If
            Return version
        End Function

        Public Overrides Function GetHandlerName() As String
            Return "Facebook"
        End Function

        Public Overrides Function GetAuthorizationUrl() As String
            Return String.Format("https://www.facebook.com/{0}/dialog/oauth?response_type=code&client_id={1}&redire"& _
                    "ct_uri={2}&scope={3}&state={4}", GetVersion(), Config.ClientId, Config.RedirectUri, Uri.EscapeDataString(Scope), Uri.EscapeDataString(GetState()))
        End Function

        Protected Overrides Function GetAccessTokenRequest(ByVal code As String, ByVal refresh As Boolean) As WebRequest
            Return WebRequest.Create(String.Format("https://graph.facebook.com/{0}/oauth/access_token?client_id={1}&redirect_uri={2}&"& _
                        "client_secret={3}&code={4}", GetVersion(), Config.ClientId, Config.RedirectUri, Config.ClientSecret, code))
        End Function

        Protected Overrides Function GetQueryRequest(ByVal method As String, ByVal token As String) As WebRequest
            Dim request = WebRequest.Create((("https://graph.facebook.com/" + GetVersion())  _
                            + ("/" + method)))
            request.Headers(HttpRequestHeader.Authorization) = ("Bearer " + token)
            Return request
        End Function

        Public Overrides Function GetUserName() As String
            m_UserObj = Query(("me?fields=" + ProfileFieldList()), false)
            Return CType(m_UserObj("email"),String)
        End Function

        Public Overrides Function GetUserImageUrl(ByVal user As MembershipUser) As String
            Return ("https://graph.facebook.com/"  _
                        + (GetVersion()  _
                        + ("/"  _
                        + (CType(m_UserObj("id"),String) + "/picture"))))
        End Function

        Protected Overrides Sub HandleError(ByVal context As HttpContext)
            If (context.Request.QueryString("error_reason") = "user_denied") Then
                context.Response.Redirect(ApplicationServices.HomePageUrl)
            End If
            MyBase.HandleError(context)
        End Sub

        Protected Overridable Function ProfileFieldList() As String
            Dim fieldList = New List(Of String)()
            Dim s = Config("Profile Field List")
            If Not (String.IsNullOrEmpty(s)) Then
                fieldList.AddRange(Regex.Split(s, "\s*\,\s*"))
            End If
            If Not (fieldList.Contains("email")) Then
                fieldList.Insert(0, "email")
            End If
            Return String.Join(",", fieldList.ToArray())
        End Function
    End Class

    Partial Public Class GoogleOAuthHandler
        Inherits GoogleOAuthHandlerBase
    End Class

    Partial Public Class GoogleOAuthHandlerBase
        Inherits OAuthHandler

        Private m_UserObj As JObject

        Protected Overrides ReadOnly Property Scope() As String
            Get
                Dim defaultScope = ("email " + Config("Scope")).Trim()
                If StoreToken Then
                    defaultScope = (defaultScope + " https://www.googleapis.com/auth/admin.directory.group.readonly")
                End If
                Return defaultScope
            End Get
        End Property

        Public Overrides Function GetHandlerName() As String
            Return "Google"
        End Function

        Public Overrides Function GetAuthorizationUrl() As String
            Dim accessType = "online"
            If StoreToken Then
                accessType = "offline"
            End If
            Dim authUrl = String.Format("https://accounts.google.com/o/oauth2/v2/auth?response_type=code&client_id={0}&red"& _
                    "irect_uri={1}&scope={2}&state={3}&access_type={4}&prompt=select_account", Config.ClientId, Uri.EscapeDataString(Config.RedirectUri), Uri.EscapeDataString(Scope), Uri.EscapeDataString(GetState()), accessType)
            Return authUrl
        End Function

        Protected Overrides Function GetAccessTokenRequest(ByVal code As String, ByVal refresh As Boolean) As WebRequest
            Dim request = WebRequest.Create("https://www.googleapis.com/oauth2/v4/token")
            request.Method = "POST"
            Dim codeType = "code"
            If refresh Then
                codeType = "access_token"
            End If
            Dim body = String.Format("{0}={1}&client_id={2}&client_secret={3}&redirect_uri={4}&grant_type=authorization"& _
                    "_code", codeType, code, Config.ClientId, Config.ClientSecret, Config.RedirectUri)
            Dim bodyBytes = Encoding.UTF8.GetBytes(body)
            request.ContentType = "application/x-www-form-urlencoded"
            request.ContentLength = bodyBytes.Length
            Using stream = request.GetRequestStream()
                stream.Write(bodyBytes, 0, bodyBytes.Length)
            End Using
            Return request
        End Function

        Protected Overrides Function GetQueryRequest(ByVal method As String, ByVal token As String) As WebRequest
            Dim request = WebRequest.Create(("https://www.googleapis.com/" + method))
            request.Headers(HttpRequestHeader.Authorization) = ("Bearer " + token)
            Return request
        End Function

        Public Overrides Function GetUserName() As String
            m_UserObj = Query("userinfo/v2/me", false)
            Return CType(m_UserObj("email"),String)
        End Function

        Public Overrides Function GetUserRoles(ByVal user As MembershipUser) As List(Of String)
            Dim roles = MyBase.GetUserRoles(user)
            Dim result = Query(("admin/directory/v1/groups?userKey=" + CType(m_UserObj("id"),String)), true)
            If (Not (result) Is Nothing) Then
                For Each group in CType(result("groups"),JArray)
                    roles.Add(CType(group("name"),String))
                Next
            Else
                Throw New Exception("Unable to get roles.")
            End If
            Return roles
        End Function

        Public Overrides Function GetUserImageUrl(ByVal user As MembershipUser) As String
            Return CType(m_UserObj("picture"),String)
        End Function
    End Class

    Partial Public Class MSGraphOAuthHandler
        Inherits MSGraphOAuthHandlerBase
    End Class

    Partial Public Class MSGraphOAuthHandlerBase
        Inherits OAuthHandler

        Private m_UserID As String = Nothing

        Private m_TenantID As String

        Protected Overrides ReadOnly Property Scope() As String
            Get
                Dim sc = Config("Scope")
                If StoreToken Then
                    sc = (sc + " https://graph.microsoft.com/.default")
                Else
                    sc = (sc + " User.Read")
                End If
                Return sc.Trim()
            End Get
        End Property

        Public Overridable ReadOnly Property TenantID() As String
            Get
                If String.IsNullOrEmpty(m_TenantID) Then
                    m_TenantID = Config("Tenant ID")
                    If String.IsNullOrEmpty(m_TenantID) Then
                        m_TenantID = "common"
                    End If
                End If
                Return m_TenantID
            End Get
        End Property

        Public Overrides Function GetHandlerName() As String
            Return "MSGraph"
        End Function

        Public Overrides Function GetAuthorizationUrl() As String
            Dim sb = New StringBuilder("https://login.microsoftonline.com/")
            sb.Append(TenantID)
            If Not (StoreToken) Then
                sb.Append("/oauth2/v2.0/authorize")
            Else
                sb.Append("/adminconsent")
            End If
            sb.AppendFormat("?client_id={0}&redirect_uri={1}&state={2}", Config.ClientId, Uri.EscapeDataString(Config.RedirectUri), Uri.EscapeDataString(GetState()))
            If Not (StoreToken) Then
                sb.Append("&response_type=code&response_mode=query&scope=")
                sb.Append(Uri.EscapeDataString(Scope))
            End If
            Return sb.ToString()
        End Function

        Protected Overrides Function GetAccessTokenRequest(ByVal code As String, ByVal refresh As Boolean) As WebRequest
            Dim request = WebRequest.Create(String.Format("https://login.microsoftonline.com/{0}/oauth2/v2.0/token", TenantID))
            request.Method = "POST"
            Dim body = String.Format("client_id={0}&redirect_uri={1}&client_secret={2}&scope={3}", Config.ClientId, Uri.EscapeDataString(Config.RedirectUri), Config.ClientSecret, Uri.EscapeDataString(Scope))
            If refresh Then
                body = (body  _
                            + ("&grant_type=refresh_token&refresh_token=" + code))
            Else
                If Not (StoreToken) Then
                    body = (body  _
                                + ("&grant_type=authorization_code&code=" + code))
                Else
                    body = (body + "&grant_type=client_credentials")
                End If
            End If
            Dim bodyBytes = Encoding.UTF8.GetBytes(body)
            request.ContentType = "application/x-www-form-urlencoded"
            request.ContentLength = bodyBytes.Length
            Using stream = request.GetRequestStream()
                stream.Write(bodyBytes, 0, bodyBytes.Length)
            End Using
            Return request
        End Function

        Protected Overrides Function GetQueryRequest(ByVal method As String, ByVal token As String) As WebRequest
            Dim request = WebRequest.Create(("https://graph.microsoft.com/v1.0/" + method))
            request.Headers(HttpRequestHeader.Authorization) = ("Bearer " + token)
            Return request
        End Function

        Protected Overrides Function RefreshTokens(ByVal useSystemToken As Boolean) As Boolean
            If Not (useSystemToken) Then
                Return MyBase.RefreshTokens(useSystemToken)
            End If
            Dim success = GetAccessTokens("True", false)
            If success Then
                StoreTokens(Tokens, true)
            End If
            Return success
        End Function

        Public Overrides Function GetUserName() As String
            Dim userObj = Query("me", false)
            m_UserID = CType(userObj("id"),String)
            Return CType(userObj("userPrincipalName"),String)
        End Function

        Public Overrides Function GetUserRoles(ByVal user As MembershipUser) As List(Of String)
            Dim roles = MyBase.GetUserRoles(user)
            Dim roleObj = Query((("users/" + m_UserID)  _
                            + "/memberOf"), true)
            If (Not (roleObj) Is Nothing) Then
                For Each role in CType(roleObj("value"),JArray)
                    roles.Add(CType(role("displayName"),String))
                Next
            End If
            Return roles
        End Function

        Protected Overrides Function GetAuthCode(ByVal request As HttpRequest) As String
            If StoreToken Then
                Return request.QueryString("admin_consent")
            End If
            Return MyBase.GetAuthCode(request)
        End Function

        Public Overrides Sub SetUserAvatar(ByVal user As MembershipUser)
        End Sub
    End Class

    Partial Public Class LinkedInOAuthHandler
        Inherits LinkedInOAuthHandlerBase
    End Class

    Partial Public Class LinkedInOAuthHandlerBase
        Inherits OAuthHandler

        Private m_UserObj As JObject

        Protected Overrides ReadOnly Property Scope() As String
            Get
                Return ("r_liteprofile r_emailaddress " + Config("Scope")).Trim()
            End Get
        End Property

        Public Overrides Function GetHandlerName() As String
            Return "LinkedIn"
        End Function

        Public Overrides Function GetAuthorizationUrl() As String
            Return String.Format("https://www.linkedin.com/oauth/v2/authorization?response_type=code&client_id={0}&"& _
                    "redirect_uri={1}&state={2}&scope={3}", Config.ClientId, Config.RedirectUri, Uri.EscapeDataString(GetState()), Uri.EscapeDataString(Scope))
        End Function

        Public Overrides Function GetUserName() As String
            Dim obj = Query("emailAddress?q=members&projection=(elements*(handle~))", false)
            m_UserObj = Query("me?projection=(id,firstName,lastName,profilePicture(displayImage~:playableStreams"& _
                    "))", false)
            Return CType(obj("elements")(0)("handle~")("emailAddress"),String)
        End Function

        Public Overrides Function GetUserImageUrl(ByVal user As MembershipUser) As String
            Return CType(m_UserObj("profilePicture")("displayImage~")("elements").Last("identifiers")(0)("identifier"),String)
        End Function

        Protected Overrides Function GetAccessTokenRequest(ByVal code As String, ByVal refresh As Boolean) As WebRequest
            Dim request = WebRequest.Create("https://www.linkedin.com/oauth/v2/accessToken")
            request.Method = "POST"
            Dim codeType = "code"
            If refresh Then
                codeType = "access_token"
            End If
            Dim body = String.Format("{0}={1}&client_id={2}&client_secret={3}&redirect_uri={4}&grant_type=authorization"& _
                    "_code", codeType, code, Config.ClientId, Config.ClientSecret, Config.RedirectUri)
            Dim bodyBytes = Encoding.UTF8.GetBytes(body)
            request.ContentType = "application/x-www-form-urlencoded"
            request.ContentLength = bodyBytes.Length
            Using stream = request.GetRequestStream()
                stream.Write(bodyBytes, 0, bodyBytes.Length)
            End Using
            Return request
        End Function

        Protected Overrides Function GetQueryRequest(ByVal method As String, ByVal token As String) As WebRequest
            Dim request = WebRequest.Create(("https://api.linkedin.com/v2/" + method))
            request.Headers(HttpRequestHeader.Authorization) = ("Bearer " + token)
            Return request
        End Function
    End Class

    Partial Public Class SharePointOAuthHandler
        Inherits SharePointOAuthHandlerBase
    End Class

    Public Class SharePointOAuthHandlerBase
        Inherits OAuthHandler

        Private m_Realm As String

        Private m_ShowNavigation As String

        Private m_UserObj As JObject

        Protected Overrides ReadOnly Property Scope() As String
            Get
                Dim sc = Config("Scope")
                If StoreToken Then
                    sc = (sc + " Web.Read AllProfiles.Read")
                End If
                Return sc.Trim()
            End Get
        End Property

        Public Overrides Function GetHandlerName() As String
            Return "SharePoint"
        End Function

        Public Overrides Function GetAuthorizationUrl() As String
            Dim version = Config("Version")
            If String.IsNullOrEmpty(version) Then
                version = "15"
            End If
            Dim authUrl = String.Format("{0}/_layouts/{1}/OAuthAuthorize.aspx?client_id={2}&scope={3}&response_type=code&r"& _
                    "edirect_uri={4}&state={5}", ClientUri, version, Config.ClientId, Uri.EscapeDataString(Scope), Uri.EscapeDataString(Config.RedirectUri), Uri.EscapeDataString(GetState()))
            Return authUrl
        End Function

        Protected Overrides Function GetAccessTokenRequest(ByVal code As String, ByVal refresh As Boolean) As WebRequest
            If String.IsNullOrEmpty(m_Realm) Then
                Dim getRealm = WebRequest.Create((ClientUri + "/_vti_bin/client.svc"))
                getRealm.Headers.Add(HttpRequestHeader.Authorization, "Bearer")
                Dim response As WebResponse
                Try
                    response = getRealm.GetResponse()
                Catch ex As WebException
                    response = ex.Response
                End Try
                Dim wwwAuthentication = response.Headers("WWW-Authenticate")
                Dim realmMatch = Regex.Match(wwwAuthentication, "Bearer realm=""(.+?)""")
                m_Realm = realmMatch.Groups(1).Value
            End If
            Dim request = WebRequest.Create((("https://accounts.accesscontrol.windows.net/" + m_Realm)  _
                            + "/tokens/OAuth/2"))
            request.Method = "POST"
            Dim grantType = "authorization_code"
            Dim codeName = "code"
            If refresh Then
                grantType = "refresh_token"
                codeName = grantType
            End If
            Dim body = String.Format("grant_type={0}&client_id={1}%40{2}&client_secret={3}&{4}={5}&redirect_uri={6}&res"& _
                    "ource=00000003-0000-0ff1-ce00-000000000000%2F{7}%40{2}", grantType, Config.ClientId, m_Realm, Uri.EscapeDataString(Config.ClientSecret), codeName, code, Config.RedirectUri, ClientUri.Substring(8))
            Dim bodyBytes = Encoding.UTF8.GetBytes(body)
            request.ContentType = "application/x-www-form-urlencoded"
            request.ContentLength = bodyBytes.Length
            Using stream = request.GetRequestStream()
                stream.Write(bodyBytes, 0, bodyBytes.Length)
            End Using
            Return request
        End Function

        Protected Overrides Function GetQueryRequest(ByVal method As String, ByVal token As String) As WebRequest
            Dim request = WebRequest.Create(String.Format("{0}/_api/{1}", ClientUri, method))
            CType(request,HttpWebRequest).Accept = "application/json;odata=verbose"
            request.Headers(HttpRequestHeader.Authorization) = ("Bearer " + token)
            Return request
        End Function

        Public Overrides Function Query(ByVal method As String, ByVal useSystemToken As Boolean) As JObject
            Dim result = MyBase.Query(method, useSystemToken)
            If ((result Is Nothing) OrElse (result("d") Is Nothing)) Then
                Return result
            End If
            Return CType(result("d"),JObject)
        End Function

        Public Overrides Function GetState() As String
            Return ((MyBase.GetState() + "|showNavigation=")  _
                        + m_ShowNavigation)
        End Function

        Public Overrides Sub SetState(ByVal state As String)
            MyBase.SetState(state)
            For Each part in state.Split(Global.Microsoft.VisualBasic.ChrW(124))
                Dim ps = part.Split(Global.Microsoft.VisualBasic.ChrW(61))
                If (ps(0) = "showNavigation") Then
                    m_ShowNavigation = ps(1)
                End If
            Next
        End Sub

        Public Overrides Function GetUserName() As String
            m_UserObj = Query("Web/CurrentUser", false)
            Return CType(m_UserObj("LoginName"),String).Split(Global.Microsoft.VisualBasic.ChrW(124)).Last()
        End Function

        Public Overrides Function GetUserRoles(ByVal user As MembershipUser) As List(Of String)
            Dim roles = MyBase.GetUserRoles(user)
            Dim groups = Query(("Web/GetUserById("  _
                            + (CType(m_UserObj("Id"),String) + ")/Groups")), Not (StoreToken))
            If (groups Is Nothing) Then
                Throw New Exception("Unable to get roles.")
            End If
            For Each group in CType(groups("results"),JArray).Children()
                Dim name = CType(group("LoginName"),String)
                If Not (name.StartsWith("SharingLinks.")) Then
                    roles.Add(name)
                End If
            Next
            Return roles
        End Function

        Public Overrides Sub RestoreSession(ByVal context As HttpContext)
            If String.IsNullOrEmpty(m_ShowNavigation) Then
                m_ShowNavigation = context.Request.QueryString("showNavigation")
            End If
            Dim session = context.Request.QueryString("session")
            If (Not (String.IsNullOrEmpty(session)) AndAlso (session = "new")) Then
                ApplicationServices.Current.UserLogout()
            Else
                MyBase.RestoreSession(context)
                If (Not (StoreToken) AndAlso context.User.Identity.IsAuthenticated) Then
                    RedirectToStartPage(context)
                End If
            End If
        End Sub

        Public Overrides Sub RedirectToStartPage(ByVal context As HttpContext)
            Dim connector = "?"
            If StartPage.Contains("?") Then
                connector = "&"
            End If
            StartPage = ((StartPage  _
                        + (connector + "_showNavigation="))  _
                        + m_ShowNavigation)
            MyBase.RedirectToStartPage(context)
        End Sub

        Public Overrides Function GetUserImageUrl(ByVal user As MembershipUser) As String
            Dim info = Query(String.Format("SP.UserProfiles.PeopleManager/GetPropertiesFor(accountName=@v)?@v='{0}'&$select=P"& _
                        "ictureUrl", HttpUtility.UrlEncode(CType(m_UserObj("LoginName"),String))), true)
            Return CType(info("PictureUrl"),String)
        End Function
    End Class

    Partial Public Class WindowsLiveOAuthHandler
        Inherits WindowsLiveOAuthHandlerBase
    End Class

    Partial Public Class WindowsLiveOAuthHandlerBase
        Inherits OAuthHandler

        Protected Overrides ReadOnly Property Scope() As String
            Get
                Return ("wl.emails " + Config("Scope")).Trim()
            End Get
        End Property

        Public Overrides Function GetHandlerName() As String
            Return "WindowsLive"
        End Function

        Public Overrides Function GetAuthorizationUrl() As String
            Return String.Format("https://login.live.com/oauth20_authorize.srf?client_id={0}&scope={1}&response_typ"& _
                    "e=code&redirect_uri={2}&state={3}", Config.ClientId, Uri.EscapeDataString(Scope), Uri.EscapeDataString(Config.RedirectUri), Uri.EscapeDataString(GetState()))
        End Function

        Protected Overrides Function GetAccessTokenRequest(ByVal code As String, ByVal refresh As Boolean) As WebRequest
            Dim request = WebRequest.Create("https://login.live.com/oauth20_token.srf")
            request.Method = "POST"
            Dim codeName = "code"
            Dim grantType = "authorization_code"
            If refresh Then
                codeName = "refresh_token"
                grantType = codeName
            End If
            Dim body = String.Format("client_id={0}&redirect_uri={1}&client_secret={2}&{3}={4}&grant_type={5}", Config.ClientId, Config.RedirectUri, Config.ClientSecret, codeName, code, grantType)
            Dim bodyBytes = Encoding.UTF8.GetBytes(body)
            request.ContentType = "application/x-www-form-urlencoded"
            request.ContentLength = bodyBytes.Length
            Using stream = request.GetRequestStream()
                stream.Write(bodyBytes, 0, bodyBytes.Length)
            End Using
            Return request
        End Function

        Protected Overrides Function GetQueryRequest(ByVal method As String, ByVal token As String) As WebRequest
            Dim version = Config("Version")
            If String.IsNullOrEmpty(version) Then
                version = "v5.0"
            End If
            Return WebRequest.Create(String.Format("https://apis.live.net/{0}/{1}?access_token={2}", version, method, token))
        End Function

        Public Overrides Function GetUserName() As String
            Dim userObj = Query("me", false)
            Return CType(userObj("emails")("account"),String)
        End Function
    End Class

    Partial Public Class IdentityServerOAuthHandler
        Inherits IdentityServerOAuthHandlerBase
    End Class

    Partial Public Class IdentityServerOAuthHandlerBase
        Inherits OAuthHandler

        Protected Overrides ReadOnly Property Scope() As String
            Get
                Dim configScope = Config("Scope")
                If Not (String.IsNullOrEmpty(configScope)) Then
                    Return configScope
                End If
                Return "openid email"
            End Get
        End Property

        Public Overrides Function GetHandlerName() As String
            Return "IdentityServer"
        End Function

        Public Overrides Function GetAuthorizationUrl() As String
            Return String.Format("{0}/connect/authorize?response_type=code&client_id={1}&redirect_uri={2}&scope={3}"& _
                    "&state={4}", ClientUri, Config.ClientId, Config.RedirectUri, Uri.EscapeDataString(Scope), Uri.EscapeDataString(GetState()))
        End Function

        Protected Overrides Function GetAccessTokenRequest(ByVal code As String, ByVal refresh As Boolean) As WebRequest
            Dim request = WebRequest.Create((ClientUri + "/connect/token"))
            request.Method = "POST"
            Dim codeName = "code"
            Dim grantType = "authorization_code"
            If refresh Then
                codeName = "refresh_token"
                grantType = codeName
            End If
            Dim body = String.Format("client_id={0}&redirect_uri={1}&client_secret={2}&{3}={4}&grant_type={5}", Config.ClientId, Config.RedirectUri, Config.ClientSecret, codeName, code, grantType)
            Dim bodyBytes = Encoding.UTF8.GetBytes(body)
            request.ContentType = "application/x-www-form-urlencoded"
            request.ContentLength = bodyBytes.Length
            Using stream = request.GetRequestStream()
                stream.Write(bodyBytes, 0, bodyBytes.Length)
            End Using
            Return request
        End Function

        Protected Overrides Function GetQueryRequest(ByVal method As String, ByVal token As String) As WebRequest
            Dim request = WebRequest.Create(String.Format("{0}/connect/{1}", ClientUri, method))
            request.Headers(HttpRequestHeader.Authorization) = ("Bearer " + token)
            Return request
        End Function

        Public Overrides Function GetUserName() As String
            Dim userObj = Query("userinfo", false)
            Return CType(userObj("email"),String)
        End Function
    End Class
End Namespace
