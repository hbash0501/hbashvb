Option Strict Off
Imports System
Imports System.IO
Imports System.Globalization
Imports System.Runtime.InteropServices
Imports System.Threading

Public Class WiaScanService
    Public Function ScanToImage(destinationFolder As String) As String
        Directory.CreateDirectory(destinationFolder)
        Dim dialog As Object = Nothing
        Dim imageFile As Object = Nothing
        Dim output As String = ""
        Try
            If Thread.CurrentThread.GetApartmentState() <> ApartmentState.STA Then Throw New InvalidOperationException("افتح المسح من زر «مسح بالسكانر» في المنصة.")
            dialog = CreateObject("WIA.CommonDialog")
            ' ScannerDeviceType, UnspecifiedIntent, MaximizeQuality; cancellation returns Nothing.
            imageFile = dialog.ShowAcquireImage(1, 0, 131072, "{B96B3CAE-0728-11D3-9D7B-0000F81EF32E}", True, True, False)
            If imageFile Is Nothing Then Return ""
            Dim extension As String
            Select Case CStr(imageFile.FormatID).ToUpperInvariant()
                Case "{B96B3CAE-0728-11D3-9D7B-0000F81EF32E}"
                    extension = ".jpg"
                Case "{B96B3CAB-0728-11D3-9D7B-0000F81EF32E}"
                    extension = ".bmp"
                Case "{B96B3CAF-0728-11D3-9D7B-0000F81EF32E}"
                    extension = ".png"
                Case "{B96B3CB1-0728-11D3-9D7B-0000F81EF32E}"
                    extension = ".tif"
                Case Else
                    Throw New InvalidOperationException("صيغة الصورة التي أعادها تعريف السكانر غير مدعومة. اختر JPEG أو PNG أو TIFF أو BMP في نافذة المسح.")
            End Select
            output = Path.Combine(destinationFolder, "scan_" & DateTime.Now.ToString("yyyyMMdd_HHmmssfff", CultureInfo.InvariantCulture) & "_" & Guid.NewGuid().ToString("N") & extension)
            imageFile.SaveFile(output)
            If Not File.Exists(output) OrElse New FileInfo(output).Length = 0 Then Throw New IOException("لم يحفظ السكانر صورة صالحة.")
            Return output
        Catch ex As Exception
            If output <> "" Then
                Try
                    If File.Exists(output) Then File.Delete(output)
                Catch
                End Try
            End If
            Throw New ApplicationException("تعذر المسح بالسكانر. تأكد من تشغيل الجهاز وتثبيت تعريف WIA الخاص به على Windows ثم حاول مرة أخرى." & Environment.NewLine & ex.Message, ex)
        Finally
            If imageFile IsNot Nothing AndAlso Marshal.IsComObject(imageFile) Then Marshal.FinalReleaseComObject(imageFile)
            If dialog IsNot Nothing AndAlso Marshal.IsComObject(dialog) Then Marshal.FinalReleaseComObject(dialog)
        End Try
    End Function
End Class
