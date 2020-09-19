Imports System
Imports System.Collections.Generic
Imports System.ComponentModel
Imports System.Configuration
Imports System.Data
Imports System.Data.Common
Imports System.IO
Imports System.Linq
Imports System.Text
Imports System.Text.RegularExpressions
Imports System.Transactions
Imports System.Web
Imports System.Web.Caching
Imports System.Web.Configuration
Imports System.Web.Security
Imports System.Xml
Imports System.Xml.XPath
Imports System.Xml.Xsl

Namespace MyCompany.Data

    Partial Public Class DataControllerBase

        Public Const MaximumRssItems As Integer = 200

        Private Shared m_RowsetTypeMap As SortedDictionary(Of String, String)

        Public Shared ReadOnly Property RowsetTypeMap() As SortedDictionary(Of String, String)
            Get
                Return m_RowsetTypeMap
            End Get
        End Property

        Public Overridable Sub ExportDataAsRowset(ByVal page As ViewPage, ByVal reader As DbDataReader, ByVal writer As StreamWriter, ByVal scope As String)
            Dim fields = New List(Of DataField)()
            For Each field in page.Fields
                If (Not ((field.Hidden OrElse (field.OnDemand OrElse (field.Type = "DataView")))) AndAlso Not (fields.Contains(field))) Then
                    Dim aliasField = field
                    If Not (String.IsNullOrEmpty(field.AliasName)) Then
                        aliasField = page.FindField(field.AliasName)
                    End If
                    fields.Add(aliasField)
                End If
            Next
            Dim s = "uuid:BDC6E3F0-6DA3-11d1-A2A3-00AA00C14882"
            Dim dt = "uuid:C2F41010-65B3-11d1-A29F-00AA00C14882"
            Dim rs = "urn:schemas-microsoft-com:rowset"
            Dim z = "#RowsetSchema"
            Dim output = CType(HttpContext.Current.Items("Export_XmlWriter"),XmlWriter)
            If (output Is Nothing) Then
                Dim settings = New XmlWriterSettings()
                settings.CloseOutput = false
                output = XmlWriter.Create(writer, settings)
                HttpContext.Current.Items("Export_XmlWriter") = output
            End If
            If ((scope = "all") OrElse (scope = "start")) Then
                output.WriteStartDocument()
                output.WriteStartElement("xml")
                output.WriteAttributeString("xmlns", "s", Nothing, s)
                output.WriteAttributeString("xmlns", "dt", Nothing, dt)
                output.WriteAttributeString("xmlns", "rs", Nothing, rs)
                output.WriteAttributeString("xmlns", "z", Nothing, z)
                'declare rowset schema
                output.WriteStartElement("Schema", s)
                output.WriteAttributeString("id", "RowsetSchema")
                output.WriteStartElement("ElementType", s)
                output.WriteAttributeString("name", "row")
                output.WriteAttributeString("content", "eltOnly")
                output.WriteAttributeString("CommandTimeout", rs, "60")
                Dim number = 1
                For Each field in fields
                    output.WriteStartElement("AttributeType", s)
                    output.WriteAttributeString("name", field.Name)
                    output.WriteAttributeString("number", rs, number.ToString())
                    output.WriteAttributeString("nullable", rs, "true")
                    output.WriteAttributeString("name", rs, field.Label)
                    output.WriteStartElement("datatype", s)
                    Dim type = RowsetTypeMap(field.Type)
                    Dim dbType As String = Nothing
                    If "{0:c}".Equals(field.DataFormatString, StringComparison.CurrentCultureIgnoreCase) Then
                        dbType = "currency"
                    Else
                        If (Not (String.IsNullOrEmpty(field.DataFormatString)) AndAlso Not ((field.Type = "DateTime"))) Then
                            type = "string"
                        End If
                    End If
                    output.WriteAttributeString("type", dt, type)
                    output.WriteAttributeString("dbtype", rs, dbType)
                    output.WriteEndElement()
                    output.WriteEndElement()
                    number = (number + 1)
                Next
                output.WriteStartElement("extends", s)
                output.WriteAttributeString("type", "rs:rowbase")
                output.WriteEndElement()
                output.WriteEndElement()
                output.WriteEndElement()
                output.WriteStartElement("data", rs)
            End If
            'output rowset data
            Do While reader.Read()
                output.WriteStartElement("row", z)
                For Each field in fields
                    Dim v = reader(field.Name)
                    If Not (DBNull.Value.Equals(v)) Then
                        If (Not (String.IsNullOrEmpty(field.DataFormatString)) AndAlso Not (((field.DataFormatString = "{0:d}") OrElse (field.DataFormatString = "{0:c}")))) Then
                            output.WriteAttributeString(field.Name, String.Format(field.DataFormatString, v))
                        Else
                            If (field.Type = "DateTime") Then
                                output.WriteAttributeString(field.Name, CType(v,DateTime).ToString("s"))
                            Else
                                output.WriteAttributeString(field.Name, v.ToString())
                            End If
                        End If
                    End If
                Next
                output.WriteEndElement()
            Loop
            If ((scope = "all") OrElse (scope = "end")) Then
                output.WriteEndElement()
                output.WriteEndElement()
                output.WriteEndDocument()
                output.Close()
                HttpContext.Current.Items.Remove("Export_XmlWriter")
            End If
        End Sub

        Public Overridable Sub ExportDataAsRss(ByVal page As ViewPage, ByVal reader As DbDataReader, ByVal writer As StreamWriter, ByVal scope As String)
            Dim appPath = Regex.Replace(HttpContext.Current.Request.Url.AbsoluteUri, "^(.+)Export.ashx.+$", "$1", RegexOptions.IgnoreCase)
            Dim settings = New XmlWriterSettings()
            settings.CloseOutput = false
            Dim output = XmlWriter.Create(writer, settings)
            output.WriteStartDocument()
            output.WriteStartElement("rss")
            output.WriteAttributeString("version", "2.0")
            output.WriteStartElement("channel")
            output.WriteElementString("title", CType(m_View.Evaluate("string(concat(/c:dataController/@label, ' | ',  @label))", Resolver),String))
            output.WriteElementString("lastBuildDate", DateTime.Now.ToString("r"))
            output.WriteElementString("language", System.Threading.Thread.CurrentThread.CurrentCulture.Name.ToLower())
            Dim rowCount = 0
            Do While ((rowCount < MaximumRssItems) AndAlso reader.Read())
                output.WriteStartElement("item")
                Dim hasTitle = false
                Dim hasPubDate = false
                Dim desc = New StringBuilder()
                Dim i = 0
                Do While (i < page.Fields.Count)
                    Dim field = page.Fields(i)
                    If (Not (field.Hidden) AndAlso Not ((field.Type = "DataView"))) Then
                        If Not (String.IsNullOrEmpty(field.AliasName)) Then
                            field = page.FindField(field.AliasName)
                        End If
                        Dim text = String.Empty
                        Dim v = reader(field.Name)
                        If Not (DBNull.Value.Equals(v)) Then
                            If Not (String.IsNullOrEmpty(field.DataFormatString)) Then
                                text = String.Format(field.DataFormatString, v)
                            Else
                                text = Convert.ToString(v)
                            End If
                        End If
                        If (Not (hasPubDate) AndAlso (field.Type = "DateTime")) Then
                            hasPubDate = true
                            If Not (String.IsNullOrEmpty(text)) Then
                                output.WriteElementString("pubDate", CType(reader(field.Name),DateTime).ToString("r"))
                            End If
                        End If
                        If Not (hasTitle) Then
                            hasTitle = true
                            output.WriteElementString("title", text)
                            Dim link = New StringBuilder()
                            link.Append(m_Config.Evaluate("string(/c:dataController/@name)"))
                            For Each pkf in page.Fields
                                If pkf.IsPrimaryKey Then
                                    link.Append(String.Format("&{0}={1}", pkf.Name, reader(pkf.Name)))
                                End If
                            Next
                            Dim itemGuid = String.Format("{0}Details.aspx?l={1}", appPath, HttpUtility.UrlEncode(Convert.ToBase64String(Encoding.Default.GetBytes(link.ToString()))))
                            output.WriteElementString("link", itemGuid)
                            output.WriteElementString("guid", itemGuid)
                        Else
                            If (Not (String.IsNullOrEmpty(field.OnDemandHandler)) AndAlso (field.OnDemandStyle = OnDemandDisplayStyle.Thumbnail)) Then
                                If text.Equals("1") Then
                                    desc.AppendFormat("{0}:<br /><img src=""{1}Blob.ashx?{2}=t", HttpUtility.HtmlEncode(field.Label), appPath, field.OnDemandHandler)
                                    For Each f in page.Fields
                                        If f.IsPrimaryKey Then
                                            desc.Append("|")
                                            desc.Append(reader(f.Name))
                                        End If
                                    Next
                                    desc.Append(""" style=""width:92px;height:71px;""/><br />")
                                End If
                            Else
                                desc.AppendFormat("{0}: {1}<br />", HttpUtility.HtmlEncode(field.Label), HttpUtility.HtmlEncode(text))
                            End If
                        End If
                    End If
                    i = (i + 1)
                Loop
                output.WriteStartElement("description")
                output.WriteCData(String.Format("<span style=\""font-size:small;\"">{0}</span>", desc.ToString()))
                output.WriteEndElement()
                output.WriteEndElement()
                rowCount = (rowCount + 1)
            Loop
            output.WriteEndElement()
            output.WriteEndElement()
            output.WriteEndDocument()
            output.Close()
        End Sub

        Public Overridable Sub ExportDataAsCsv(ByVal page As ViewPage, ByVal reader As DbDataReader, ByVal writer As StreamWriter, ByVal scope As String)
            Dim firstField = true
            If ((scope = "all") OrElse (scope = "start")) Then
                Dim i = 0
                Do While (i < page.Fields.Count)
                    Dim field = page.Fields(i)
                    If (Not (field.Hidden) AndAlso (field.Type <> "DataView")) Then
                        If firstField Then
                            firstField = false
                        Else
                            writer.Write(System.Globalization.CultureInfo.CurrentCulture.TextInfo.ListSeparator)
                        End If
                        If Not (String.IsNullOrEmpty(field.AliasName)) Then
                            field = page.FindField(field.AliasName)
                        End If
                        writer.Write("""{0}""", field.Label.Replace("""", """"""))
                    End If
                    field.NormalizeDataFormatString()
                    i = (i + 1)
                Loop
                writer.WriteLine()
            End If
            Do While reader.Read()
                firstField = true
                Dim j = 0
                Do While (j < page.Fields.Count)
                    Dim field = page.Fields(j)
                    If (Not (field.Hidden) AndAlso (field.Type <> "DataView")) Then
                        If firstField Then
                            firstField = false
                        Else
                            writer.Write(System.Globalization.CultureInfo.CurrentCulture.TextInfo.ListSeparator)
                        End If
                        If Not (String.IsNullOrEmpty(field.AliasName)) Then
                            field = page.FindField(field.AliasName)
                        End If
                        Dim text = String.Empty
                        Dim v = reader(field.Name)
                        If Not (DBNull.Value.Equals(v)) Then
                            If Not (String.IsNullOrEmpty(field.DataFormatString)) Then
                                text = String.Format(field.DataFormatString, v)
                            Else
                                text = Convert.ToString(v)
                            End If
                            writer.Write("""{0}""", text.Replace("""", """"""))
                        Else
                            writer.Write("""""")
                        End If
                    End If
                    j = (j + 1)
                Loop
                writer.WriteLine()
            Loop
        End Sub
    End Class
End Namespace
