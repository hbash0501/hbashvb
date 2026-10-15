Imports System
Imports System.IO
Imports System.Xml.Linq

Public Class AppSettings
    Public Property BaseDataFolder As String
    Public Property ArchiveRootFolder As String
    Public Property TesseractExePath As String
    Public Property OcrLanguage As String
    Public Property CurrentEmployeeName As String
    Public Property EnableRuleBasedAi As Boolean
    Public Property EnableOpenAI As Boolean
    Public Property AiProvider As String
    Public Property CloudAiEndpoint As String
    Public Property CloudAiModel As String
    Public Property CloudAiApiKey As String
    Public Property CloudAiApiKeyEnvironmentName As String
    Public Property EnableAiFailover As Boolean
    Public Property AiProviderPriority As String
    Public Property AiRequestTimeoutSeconds As Integer
    Public Property ProviderFailureCooldownMinutes As Integer
    Public Property EnableAutomaticProcessing As Boolean
    Public Property MinimumQualityScoreForApproval As Integer
    Public Property OpenAiEndpoint As String
    Public Property OpenAiModel As String
    Public Property OpenAiApiKey As String
    Public Property OpenAiOrganization As String
    Public Property OpenAiProject As String
    Public Property PopplerPdftoppmPath As String
    Public Property PopplerPdftotextPath As String
    Public Property GhostscriptExePath As String
    Public Property PdfOcrDpi As Integer
    Public Property PdfOcrMaxPages As Integer
    Public Property PdfOcrUseDirectTextFirst As Boolean
    Public Property EnableFileSignatureValidation As Boolean
    Public Property EnableSensitiveDocumentEncryption As Boolean
    Public Property SensitiveDocumentRiskThresholdPercent As Integer

    Public Shared Function DefaultSettings() As AppSettings
        Dim baseFolder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "SaeedAIDocArchive")
        Return New AppSettings With {
            .BaseDataFolder = baseFolder,
            .ArchiveRootFolder = Path.Combine(baseFolder, "ArchiveFiles"),
            .TesseractExePath = "C:\Program Files\Tesseract-OCR\tesseract.exe",
            .OcrLanguage = "ara+eng",
            .CurrentEmployeeName = Environment.UserName,
            .EnableRuleBasedAi = True,
            .EnableOpenAI = True,
            .AiProvider = "Groq",
            .CloudAiEndpoint = "https://api.groq.com/openai/v1/chat/completions",
            .CloudAiModel = "llama-3.1-8b-instant",
            .CloudAiApiKey = "",
            .CloudAiApiKeyEnvironmentName = "GROQ_API_KEY",
            .EnableAiFailover = True,
            .AiProviderPriority = "Groq;Gemini;OpenRouter",
            .AiRequestTimeoutSeconds = 45,
            .ProviderFailureCooldownMinutes = 10,
            .EnableAutomaticProcessing = True,
            .MinimumQualityScoreForApproval = 80,
            .OpenAiEndpoint = "https://api.groq.com/openai/v1/chat/completions",
            .OpenAiModel = "llama-3.1-8b-instant",
            .OpenAiApiKey = "",
            .OpenAiOrganization = "",
            .OpenAiProject = "",
            .PopplerPdftoppmPath = "",
            .PopplerPdftotextPath = "",
            .GhostscriptExePath = "",
            .PdfOcrDpi = 300,
            .PdfOcrMaxPages = 100,
            .PdfOcrUseDirectTextFirst = True,
            .EnableFileSignatureValidation = True,
            .EnableSensitiveDocumentEncryption = False,
            .SensitiveDocumentRiskThresholdPercent = 70
        }
    End Function

    Public Shared Function SettingsFilePath() As String
        Dim folder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "SaeedAIDocArchive")
        Directory.CreateDirectory(folder)
        Return Path.Combine(folder, "settings.xml")
    End Function

    Public Shared Function Load() As AppSettings
        Dim settingsFile = SettingsFilePath()
        If Not File.Exists(settingsFile) Then
            Dim s = DefaultSettings()
            s.Save()
            Return s
        End If

        Dim x = XDocument.Load(settingsFile)
        Dim root = x.Root
        Dim defaults = DefaultSettings()
        Return New AppSettings With {
            .BaseDataFolder = ValueOf(root, "BaseDataFolder", defaults.BaseDataFolder),
            .ArchiveRootFolder = ValueOf(root, "ArchiveRootFolder", defaults.ArchiveRootFolder),
            .TesseractExePath = ValueOf(root, "TesseractExePath", defaults.TesseractExePath),
            .OcrLanguage = ValueOf(root, "OcrLanguage", defaults.OcrLanguage),
            .CurrentEmployeeName = ValueOf(root, "CurrentEmployeeName", defaults.CurrentEmployeeName),
            .EnableRuleBasedAi = BooleanValue(root, "EnableRuleBasedAi", True),
            .EnableOpenAI = BooleanValue(root, "EnableOpenAI", defaults.EnableOpenAI),
            .AiProvider = ValueOf(root, "AiProvider", defaults.AiProvider),
            .CloudAiEndpoint = ValueOf(root, "CloudAiEndpoint", ValueOf(root, "OpenAiEndpoint", defaults.CloudAiEndpoint)),
            .CloudAiModel = ValueOf(root, "CloudAiModel", ValueOf(root, "OpenAiModel", defaults.CloudAiModel)),
            .CloudAiApiKey = ValueOf(root, "CloudAiApiKey", ValueOf(root, "OpenAiApiKey", defaults.CloudAiApiKey)),
            .CloudAiApiKeyEnvironmentName = ValueOf(root, "CloudAiApiKeyEnvironmentName", defaults.CloudAiApiKeyEnvironmentName),
            .EnableAiFailover = BooleanValue(root, "EnableAiFailover", defaults.EnableAiFailover),
            .AiProviderPriority = ValueOf(root, "AiProviderPriority", defaults.AiProviderPriority),
            .AiRequestTimeoutSeconds = IntegerValue(root, "AiRequestTimeoutSeconds", defaults.AiRequestTimeoutSeconds),
            .ProviderFailureCooldownMinutes = IntegerValue(root, "ProviderFailureCooldownMinutes", defaults.ProviderFailureCooldownMinutes),
            .EnableAutomaticProcessing = BooleanValue(root, "EnableAutomaticProcessing", defaults.EnableAutomaticProcessing),
            .MinimumQualityScoreForApproval = IntegerValue(root, "MinimumQualityScoreForApproval", defaults.MinimumQualityScoreForApproval),
            .OpenAiEndpoint = ValueOf(root, "OpenAiEndpoint", defaults.OpenAiEndpoint),
            .OpenAiModel = ValueOf(root, "OpenAiModel", defaults.OpenAiModel),
            .OpenAiApiKey = ValueOf(root, "OpenAiApiKey", defaults.OpenAiApiKey),
            .OpenAiOrganization = ValueOf(root, "OpenAiOrganization", defaults.OpenAiOrganization),
            .OpenAiProject = ValueOf(root, "OpenAiProject", defaults.OpenAiProject),
            .PopplerPdftoppmPath = ValueOf(root, "PopplerPdftoppmPath", defaults.PopplerPdftoppmPath),
            .PopplerPdftotextPath = ValueOf(root, "PopplerPdftotextPath", defaults.PopplerPdftotextPath),
            .GhostscriptExePath = ValueOf(root, "GhostscriptExePath", defaults.GhostscriptExePath),
            .PdfOcrDpi = IntegerValue(root, "PdfOcrDpi", defaults.PdfOcrDpi),
            .PdfOcrMaxPages = IntegerValue(root, "PdfOcrMaxPages", defaults.PdfOcrMaxPages),
            .PdfOcrUseDirectTextFirst = BooleanValue(root, "PdfOcrUseDirectTextFirst", defaults.PdfOcrUseDirectTextFirst),
            .EnableFileSignatureValidation = BooleanValue(root, "EnableFileSignatureValidation", defaults.EnableFileSignatureValidation),
            .EnableSensitiveDocumentEncryption = BooleanValue(root, "EnableSensitiveDocumentEncryption", defaults.EnableSensitiveDocumentEncryption),
            .SensitiveDocumentRiskThresholdPercent = IntegerValue(root, "SensitiveDocumentRiskThresholdPercent", defaults.SensitiveDocumentRiskThresholdPercent)
        }
    End Function

    Public Sub Save()
        Directory.CreateDirectory(BaseDataFolder)
        Directory.CreateDirectory(ArchiveRootFolder)
        Dim x = New XDocument(
            New XElement("Settings",
                New XElement("BaseDataFolder", BaseDataFolder),
                New XElement("ArchiveRootFolder", ArchiveRootFolder),
                New XElement("TesseractExePath", TesseractExePath),
                New XElement("OcrLanguage", OcrLanguage),
                New XElement("CurrentEmployeeName", CurrentEmployeeName),
                New XElement("EnableRuleBasedAi", EnableRuleBasedAi.ToString()),
                New XElement("EnableOpenAI", EnableOpenAI.ToString()),
                New XElement("AiProvider", AiProvider),
                New XElement("CloudAiEndpoint", CloudAiEndpoint),
                New XElement("CloudAiModel", CloudAiModel),
                New XElement("CloudAiApiKey", ""),
                New XElement("CloudAiApiKeyEnvironmentName", CloudAiApiKeyEnvironmentName),
                New XElement("EnableAiFailover", EnableAiFailover.ToString()),
                New XElement("AiProviderPriority", AiProviderPriority),
                New XElement("AiRequestTimeoutSeconds", AiRequestTimeoutSeconds.ToString()),
                New XElement("ProviderFailureCooldownMinutes", ProviderFailureCooldownMinutes.ToString()),
                New XElement("EnableAutomaticProcessing", EnableAutomaticProcessing.ToString()),
                New XElement("MinimumQualityScoreForApproval", MinimumQualityScoreForApproval.ToString()),
                New XElement("OpenAiEndpoint", OpenAiEndpoint),
                New XElement("OpenAiModel", OpenAiModel),
                New XElement("OpenAiApiKey", ""),
                New XElement("OpenAiOrganization", OpenAiOrganization),
                New XElement("OpenAiProject", OpenAiProject),
                New XElement("PopplerPdftoppmPath", PopplerPdftoppmPath),
                New XElement("PopplerPdftotextPath", PopplerPdftotextPath),
                New XElement("GhostscriptExePath", GhostscriptExePath),
                New XElement("PdfOcrDpi", PdfOcrDpi.ToString()),
                New XElement("PdfOcrMaxPages", PdfOcrMaxPages.ToString()),
                New XElement("PdfOcrUseDirectTextFirst", PdfOcrUseDirectTextFirst.ToString()),
                New XElement("EnableFileSignatureValidation", EnableFileSignatureValidation.ToString()),
                New XElement("EnableSensitiveDocumentEncryption", EnableSensitiveDocumentEncryption.ToString()),
                New XElement("SensitiveDocumentRiskThresholdPercent", SensitiveDocumentRiskThresholdPercent.ToString())
            )
        )
        x.Save(SettingsFilePath())
    End Sub

    Private Shared Function ValueOf(root As XElement, name As String, defaultValue As String) As String
        If root Is Nothing OrElse root.Element(name) Is Nothing Then Return defaultValue
        Return root.Element(name).Value
    End Function

    Private Shared Function BooleanValue(root As XElement, name As String, defaultValue As Boolean) As Boolean
        Dim v = ValueOf(root, name, defaultValue.ToString())
        Dim b As Boolean
        If Boolean.TryParse(v, b) Then Return b
        Return defaultValue
    End Function

    Private Shared Function IntegerValue(root As XElement, name As String, defaultValue As Integer) As Integer
        Dim v = ValueOf(root, name, defaultValue.ToString())
        Dim n As Integer
        If Integer.TryParse(v, n) Then Return n
        Return defaultValue
    End Function
End Class
