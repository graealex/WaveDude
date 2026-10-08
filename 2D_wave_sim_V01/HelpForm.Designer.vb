<Global.Microsoft.VisualBasic.CompilerServices.DesignerGenerated()> _
Partial Class HelpForm
    Inherits System.Windows.Forms.Form

    'Form overrides dispose to clean up the component list.
    <System.Diagnostics.DebuggerNonUserCode()> _
    Protected Overrides Sub Dispose(ByVal disposing As Boolean)
        Try
            If disposing AndAlso components IsNot Nothing Then
                components.Dispose()
            End If
        Finally
            MyBase.Dispose(disposing)
        End Try
    End Sub

    'Required by the Windows Form Designer
    Private components As System.ComponentModel.IContainer

    'NOTE: The following procedure is required by the Windows Form Designer
    'It can be modified using the Windows Form Designer.
    'Do not modify it using the code editor.
    <System.Diagnostics.DebuggerStepThrough()> _
    Private Sub InitializeComponent()
        Me.rtbHelp = New System.Windows.Forms.RichTextBox()
        Me.pnlButtons = New System.Windows.Forms.FlowLayoutPanel()
        Me.btnClose = New System.Windows.Forms.Button()
        Me.pnlButtons.SuspendLayout()
        Me.SuspendLayout()
        '
        'rtbHelp
        '
        Me.rtbHelp.BackColor = System.Drawing.SystemColors.Window
        Me.rtbHelp.BorderStyle = System.Windows.Forms.BorderStyle.None
        Me.rtbHelp.Dock = System.Windows.Forms.DockStyle.Fill
        Me.rtbHelp.Font = New System.Drawing.Font("Arial", 12.0!, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, CType(0, Byte))
        Me.rtbHelp.Location = New System.Drawing.Point(0, 0)
        Me.rtbHelp.Margin = New System.Windows.Forms.Padding(12)
        Me.rtbHelp.Name = "rtbHelp"
        Me.rtbHelp.ReadOnly = True
        Me.rtbHelp.Size = New System.Drawing.Size(1039, 843)
        Me.rtbHelp.TabIndex = 0
        Me.rtbHelp.Text = ""
        '
        'pnlButtons
        '
        Me.pnlButtons.Controls.Add(Me.btnClose)
        Me.pnlButtons.Dock = System.Windows.Forms.DockStyle.Bottom
        Me.pnlButtons.FlowDirection = System.Windows.Forms.FlowDirection.RightToLeft
        Me.pnlButtons.Location = New System.Drawing.Point(0, 843)
        Me.pnlButtons.Name = "pnlButtons"
        Me.pnlButtons.Padding = New System.Windows.Forms.Padding(8)
        Me.pnlButtons.Size = New System.Drawing.Size(1039, 46)
        Me.pnlButtons.TabIndex = 1
        '
        'btnClose
        '
        Me.btnClose.DialogResult = System.Windows.Forms.DialogResult.OK
        Me.btnClose.Location = New System.Drawing.Point(930, 11)
        Me.btnClose.Name = "btnClose"
        Me.btnClose.Size = New System.Drawing.Size(90, 30)
        Me.btnClose.TabIndex = 0
        Me.btnClose.Text = "Close"
        Me.btnClose.UseVisualStyleBackColor = True
        '
        'HelpForm
        '
        Me.AcceptButton = Me.btnClose
        Me.CancelButton = Me.btnClose
        Me.ClientSize = New System.Drawing.Size(1039, 889)
        Me.Controls.Add(Me.rtbHelp)
        Me.Controls.Add(Me.pnlButtons)
        Me.MinimizeBox = False
        Me.MinimumSize = New System.Drawing.Size(560, 400)
        Me.Name = "HelpForm"
        Me.ShowIcon = False
        Me.ShowInTaskbar = False
        Me.StartPosition = System.Windows.Forms.FormStartPosition.CenterParent
        Me.Text = "Help - Wave Maker 2D"
        Me.pnlButtons.ResumeLayout(False)
        Me.ResumeLayout(False)

    End Sub

    Friend WithEvents rtbHelp As System.Windows.Forms.RichTextBox
    Friend WithEvents pnlButtons As System.Windows.Forms.FlowLayoutPanel
    Friend WithEvents btnClose As System.Windows.Forms.Button

End Class
