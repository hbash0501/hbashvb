Imports System.Drawing
Imports System.Windows.Forms
<Global.Microsoft.VisualBasic.CompilerServices.DesignerGenerated()>
Partial Public Class BasiraRevisionViewerForm
    Inherits Form
    Private components As System.ComponentModel.IContainer
    Friend WithEvents gridVersions As DataGridView
    Friend currentText As RichTextBox
    Friend previousText As RichTextBox
    Friend lblDifference As Label
    Protected Overrides Sub Dispose(disposing As Boolean)
        If disposing AndAlso components IsNot Nothing Then components.Dispose()
        MyBase.Dispose(disposing)
    End Sub
    Private Sub InitializeComponent()
        components = New System.ComponentModel.Container()
        Size = New Size(1180, 810)
        MinimumSize = New Size(850, 650)
        Font = New Font("Segoe UI", 10.0F)
        RightToLeft = RightToLeft.Yes
        StartPosition = FormStartPosition.CenterParent
        Text = "بصيرة — قراءة النسخ السابقة ومقارنتها"
        BackColor = BasiraTheme.Canvas
        gridVersions = BasiraTheme.Grid()
        gridVersions.Dock = DockStyle.Top
        gridVersions.Height = 185
        gridVersions.Columns.Add("CapturedAt", "وقت حفظ النسخة")
        gridVersions.Columns.Add("Title", "العنوان السابق")
        gridVersions.Columns.Add("Status", "الحالة السابقة")
        Dim table As New TableLayoutPanel With {.Dock = DockStyle.Fill, .ColumnCount = 2, .RowCount = 2, .Padding = New Padding(14)}
        table.ColumnStyles.Add(New ColumnStyle(SizeType.Percent, 50))
        table.ColumnStyles.Add(New ColumnStyle(SizeType.Percent, 50))
        table.RowStyles.Add(New RowStyle(SizeType.Absolute, 42))
        table.RowStyles.Add(New RowStyle(SizeType.Percent, 100))
        currentText = BasiraTheme.ReadBox()
        previousText = BasiraTheme.ReadBox()
        table.Controls.Add(New Label With {.Text = "النسخة الحالية", .Dock = DockStyle.Fill, .ForeColor = BasiraTheme.Teal}, 0, 0)
        table.Controls.Add(New Label With {.Text = "النسخة السابقة", .Dock = DockStyle.Fill, .ForeColor = BasiraTheme.Muted}, 1, 0)
        table.Controls.Add(currentText, 0, 1)
        table.Controls.Add(previousText, 1, 1)
        lblDifference = New Label With {.Dock = DockStyle.Bottom, .Height = 55, .Padding = New Padding(16, 10, 16, 0), .ForeColor = BasiraTheme.Muted}
        Controls.Add(table)
        Controls.Add(gridVersions)
        Controls.Add(lblDifference)
    End Sub
End Class
