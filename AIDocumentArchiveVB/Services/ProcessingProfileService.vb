Imports System.Collections.Generic

Public Class ProcessingProfileService
    Public Function GetProfiles() As List(Of ProcessingProfile)
        Return New List(Of ProcessingProfile) From {
            New ProcessingProfile With {
                .Name = "المسار السريع",
                .Description = "OCR والكيانات المحلية والتصنيف السريع بأقل زمن.",
                .RunOcr = True,
                .ExtractEntities = True,
                .RunQuickAi = True,
                .RunDeepAiForSensitiveDocuments = False,
                .GenerateReplyDraftWhenNeeded = False,
                .ApplyAutomaticRouting = True,
                .MinimumQualityScore = 70
            },
            New ProcessingProfile With {
                .Name = "المسار الإداري المتوازن",
                .Description = "المسار الافتراضي للخطابات والمعاملات اليومية.",
                .RunOcr = True,
                .ExtractEntities = True,
                .RunQuickAi = True,
                .RunDeepAiForSensitiveDocuments = True,
                .GenerateReplyDraftWhenNeeded = True,
                .ApplyAutomaticRouting = True,
                .MinimumQualityScore = 80
            },
            New ProcessingProfile With {
                .Name = "المراجعة الحساسة",
                .Description = "تدقيق موسع للمعاملات السرية والعاجلة قبل الاعتماد.",
                .RunOcr = True,
                .ExtractEntities = True,
                .RunQuickAi = True,
                .RunDeepAiForSensitiveDocuments = True,
                .GenerateReplyDraftWhenNeeded = True,
                .ApplyAutomaticRouting = True,
                .MinimumQualityScore = 90
            }
        }
    End Function

    Public Function FindByName(name As String) As ProcessingProfile
        For Each profile In GetProfiles()
            If String.Equals(profile.Name, name, System.StringComparison.OrdinalIgnoreCase) Then Return profile
        Next
        Return GetProfiles()(1)
    End Function
End Class
