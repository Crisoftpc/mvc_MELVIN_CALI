Imports MyCompany.Handlers
Imports MyCompany.Web
Imports System.Web.Configuration

Namespace MyCompany.Services

    Public Class AppFrameworkConfig

        Public Overridable Sub Initialize()
            ApplicationServices.FrameworkAppName = "7690109778"
            ApplicationServices.Version = "8.9.10.0"
            ApplicationServices.JqmVersion = "1.4.6"
            ApplicationServices.HostVersion = "1.2.5.0"
            Dim compilation = CType(WebConfigurationManager.GetSection("system.web/compilation"),CompilationSection)
            Dim releaseMode = Not (compilation.Debug)
            AquariumExtenderBase.EnableMinifiedScript = releaseMode
            AquariumExtenderBase.EnableCombinedScript = releaseMode
            ApplicationServices.EnableMinifiedCss = releaseMode
            ApplicationServices.EnableCombinedCss = releaseMode
            BlobFactoryConfig.Initialize()
        End Sub
    End Class
End Namespace
