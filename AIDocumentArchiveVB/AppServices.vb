Public Module AppServices
    Public Property Settings As AppSettings
    Public Property Repository As ArchiveRepository
    Public Property Ai As AiAssistantService
    Public Property Auth As AuthService
    Public Property Cache As ProcessingCacheService
    Public Property ProcessingQueue As ProcessingJobQueueService
    Public Property Speed As SpeedEngineService
    Public Property EnterpriseDb As EnterpriseSqliteService
    Public Property AiGateway As AiGatewayService
    Public Property ResilientAi As MultiAiResilienceService
    Public Property QualityGate As DocumentQualityGateService
    Public Property ProcessingProfiles As ProcessingProfileService
    Public Property IntelligentAutomation As IntelligentAutomationService
    Public Property Orchestrator As DocumentWorkspaceOrchestrator
    Public Property Security As DocumentSecurityService
    Public Property EnterprisePipeline As EnterpriseDocumentPipelineService

    Public Sub Initialize()
        Settings = AppSettings.Load()
        Security = New DocumentSecurityService(Settings)
        Security.MigrateLegacyPlainTextKeys(Settings)

        Repository = New ArchiveRepository(Settings)
        Repository.EnsureSeedData()
        Ai = New AiAssistantService(Repository, Settings)
        Auth = New AuthService(Settings)

        Cache = New ProcessingCacheService(Settings)
        ProcessingQueue = New ProcessingJobQueueService(Settings)
        ProcessingQueue.Start(If(Settings.EnableAutomaticProcessing, 2, 1))
        Speed = New SpeedEngineService(Settings, Repository, ProcessingQueue, Cache)

        EnterpriseDb = New EnterpriseSqliteService(Settings)
        EnterpriseDb.Initialize()
        EnterpriseDb.MigrateFromXmlRepository(Repository)

        ResilientAi = New MultiAiResilienceService(Settings)
        AiGateway = New AiGatewayService(Settings, Cache, EnterpriseDb, ResilientAi)
        QualityGate = New DocumentQualityGateService(Repository, Settings)
        ProcessingProfiles = New ProcessingProfileService()
        IntelligentAutomation = New IntelligentAutomationService(Settings, Repository, ProcessingQueue, Speed, AiGateway, QualityGate, ProcessingProfiles, EnterpriseDb)
        Orchestrator = New DocumentWorkspaceOrchestrator(Settings, Repository, ProcessingQueue, Speed, AiGateway, EnterpriseDb)
        EnterprisePipeline = New EnterpriseDocumentPipelineService(Settings, Repository, Security, IntelligentAutomation, ProcessingProfiles, QualityGate, ProcessingQueue, EnterpriseDb)

    End Sub
End Module
