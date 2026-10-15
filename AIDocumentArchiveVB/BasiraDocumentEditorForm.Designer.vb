Imports System.Drawing
Imports System.Windows.Forms
<Global.Microsoft.VisualBasic.CompilerServices.DesignerGenerated()>
Partial Public Class BasiraDocumentEditorForm
    Inherits Form
    Private components As System.ComponentModel.IContainer
    Friend WithEvents btnSave As Button
    Friend txtDocumentText As RichTextBox
    Friend lblNote As Label
    Friend txtTitle As TextBox
    Friend txtGeneralCategory As TextBox
    Friend txtSubCategory As TextBox
    Friend txtReferenceNumber As TextBox
    Friend txtSourceEntity As TextBox
    Friend txtDestinationEntity As TextBox
    Friend txtDueDateText As TextBox
    Friend txtTags As TextBox
    Friend txtConfidentiality As TextBox
    Friend txtPriority As TextBox

    Protected Overrides Sub Dispose(disposing As Boolean)
        If disposing AndAlso components IsNot Nothing Then components.Dispose()
        MyBase.Dispose(disposing)
    End Sub
    Private Sub InitializeComponent()
        components = New System.ComponentModel.Container()
        Size = New Size(880, 820)
        MinimumSize = New Size(700, 640)
        StartPosition = FormStartPosition.CenterParent
        RightToLeft = RightToLeft.Yes
        Font = New Font("Segoe UI", 10.5F)
        BackColor = BasiraTheme.Canvas
        Text = "بصيرة — تحرير بيانات المستند ونصه"
        Dim tabs As New TabControl With {.Dock = DockStyle.Fill, .RightToLeft = RightToLeft.Yes, .RightToLeftLayout = True}
        Dim metadata As New TabPage("بيانات المستند") With {.Padding = New Padding(16), .BackColor = Color.White, .AutoScroll = True}
        Dim table As New TableLayoutPanel With {.Dock = DockStyle.Top, .AutoSize = True, .ColumnCount = 1, .RowCount = 20, .Padding = New Padding(4)}
        table.Controls.Add(New Label With {.Text = "العنوان", .AutoSize = True, .Margin = New Padding(4, 10, 4, 5), .ForeColor = BasiraTheme.Muted}, 0, 0)
        txtTitle = New TextBox With {.Dock = DockStyle.Fill, .Margin = New Padding(4), .Font = New Font("Segoe UI", 11.0F)}
        table.Controls.Add(txtTitle, 0, 1)
        table.Controls.Add(New Label With {.Text = "التصنيف العام", .AutoSize = True, .Margin = New Padding(4, 10, 4, 5), .ForeColor = BasiraTheme.Muted}, 0, 2)
        txtGeneralCategory = New TextBox With {.Dock = DockStyle.Fill, .Margin = New Padding(4), .Font = New Font("Segoe UI", 11.0F)}
        table.Controls.Add(txtGeneralCategory, 0, 3)
        table.Controls.Add(New Label With {.Text = "التصنيف الفرعي", .AutoSize = True, .Margin = New Padding(4, 10, 4, 5), .ForeColor = BasiraTheme.Muted}, 0, 4)
        txtSubCategory = New TextBox With {.Dock = DockStyle.Fill, .Margin = New Padding(4), .Font = New Font("Segoe UI", 11.0F)}
        table.Controls.Add(txtSubCategory, 0, 5)
        table.Controls.Add(New Label With {.Text = "رقم المرجع", .AutoSize = True, .Margin = New Padding(4, 10, 4, 5), .ForeColor = BasiraTheme.Muted}, 0, 6)
        txtReferenceNumber = New TextBox With {.Dock = DockStyle.Fill, .Margin = New Padding(4), .Font = New Font("Segoe UI", 11.0F)}
        table.Controls.Add(txtReferenceNumber, 0, 7)
        table.Controls.Add(New Label With {.Text = "الجهة الصادرة", .AutoSize = True, .Margin = New Padding(4, 10, 4, 5), .ForeColor = BasiraTheme.Muted}, 0, 8)
        txtSourceEntity = New TextBox With {.Dock = DockStyle.Fill, .Margin = New Padding(4), .Font = New Font("Segoe UI", 11.0F)}
        table.Controls.Add(txtSourceEntity, 0, 9)
        table.Controls.Add(New Label With {.Text = "الجهة المستقبلة", .AutoSize = True, .Margin = New Padding(4, 10, 4, 5), .ForeColor = BasiraTheme.Muted}, 0, 10)
        txtDestinationEntity = New TextBox With {.Dock = DockStyle.Fill, .Margin = New Padding(4), .Font = New Font("Segoe UI", 11.0F)}
        table.Controls.Add(txtDestinationEntity, 0, 11)
        table.Controls.Add(New Label With {.Text = "الموعد: yyyy/MM/dd أو dd/MM/yyyy؛ أضف هـ للهجري", .AutoSize = True, .Margin = New Padding(4, 10, 4, 5), .ForeColor = BasiraTheme.Muted}, 0, 12)
        txtDueDateText = New TextBox With {.Dock = DockStyle.Fill, .Margin = New Padding(4), .Font = New Font("Segoe UI", 11.0F)}
        table.Controls.Add(txtDueDateText, 0, 13)
        table.Controls.Add(New Label With {.Text = "الكلمات المفتاحية", .AutoSize = True, .Margin = New Padding(4, 10, 4, 5), .ForeColor = BasiraTheme.Muted}, 0, 14)
        txtTags = New TextBox With {.Dock = DockStyle.Fill, .Margin = New Padding(4), .Font = New Font("Segoe UI", 11.0F)}
        table.Controls.Add(txtTags, 0, 15)
        table.Controls.Add(New Label With {.Text = "السرية", .AutoSize = True, .Margin = New Padding(4, 10, 4, 5), .ForeColor = BasiraTheme.Muted}, 0, 16)
        txtConfidentiality = New TextBox With {.Dock = DockStyle.Fill, .Margin = New Padding(4), .Font = New Font("Segoe UI", 11.0F)}
        table.Controls.Add(txtConfidentiality, 0, 17)
        table.Controls.Add(New Label With {.Text = "الأولوية", .AutoSize = True, .Margin = New Padding(4, 10, 4, 5), .ForeColor = BasiraTheme.Muted}, 0, 18)
        txtPriority = New TextBox With {.Dock = DockStyle.Fill, .Margin = New Padding(4), .Font = New Font("Segoe UI", 11.0F)}
        table.Controls.Add(txtPriority, 0, 19)
        metadata.Controls.Add(table)
        Dim textPage As New TabPage("تصحيح النص") With {.Padding = New Padding(16), .BackColor = Color.White}
        txtDocumentText = New RichTextBox With {.Dock = DockStyle.Fill, .Font = New Font("Segoe UI", 12.0F), .RightToLeft = RightToLeft.Yes, .DetectUrls = False, .BorderStyle = BorderStyle.None}
        textPage.Controls.Add(txtDocumentText)
        tabs.TabPages.Add(metadata)
        tabs.TabPages.Add(textPage)
        Dim footer As New Panel With {.Dock = DockStyle.Bottom, .Height = 92, .Padding = New Padding(16)}
        btnSave = BasiraTheme.Button("حفظ التعديلات", True)
        btnSave.Dock = DockStyle.Right
        lblNote = New Label With {.Dock = DockStyle.Fill, .ForeColor = BasiraTheme.Muted, .Text = "تحفظ نسخة من البيانات والنص السابقين قبل التعديل."}
        footer.Controls.Add(lblNote)
        footer.Controls.Add(btnSave)
        Controls.Add(tabs)
        Controls.Add(footer)
    End Sub
End Class
