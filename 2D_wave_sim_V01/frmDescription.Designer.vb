<Global.Microsoft.VisualBasic.CompilerServices.DesignerGenerated()> _
Partial Class frmDescription
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
        Me.TextBox1 = New System.Windows.Forms.TextBox()
        Me.btnSaveDescription = New System.Windows.Forms.Button()
        Me.btnCancelDescription = New System.Windows.Forms.Button()
        Me.SuspendLayout()
        '
        'TextBox1
        '
        Me.TextBox1.BorderStyle = System.Windows.Forms.BorderStyle.FixedSingle
        Me.TextBox1.Font = New System.Drawing.Font("Microsoft Sans Serif", 11.25!, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, CType(0, Byte))
        Me.TextBox1.Location = New System.Drawing.Point(22, 23)
        Me.TextBox1.Multiline = True
        Me.TextBox1.Name = "TextBox1"
        Me.TextBox1.Size = New System.Drawing.Size(662, 267)
        Me.TextBox1.TabIndex = 0
        '
        'btnSaveDescription
        '
        Me.btnSaveDescription.Font = New System.Drawing.Font("Microsoft Sans Serif", 11.25!, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, CType(0, Byte))
        Me.btnSaveDescription.Location = New System.Drawing.Point(575, 305)
        Me.btnSaveDescription.Name = "btnSaveDescription"
        Me.btnSaveDescription.Size = New System.Drawing.Size(109, 32)
        Me.btnSaveDescription.TabIndex = 1
        Me.btnSaveDescription.Text = "OK"
        Me.btnSaveDescription.UseVisualStyleBackColor = True
        '
        'btnCancelDescription
        '
        Me.btnCancelDescription.Font = New System.Drawing.Font("Microsoft Sans Serif", 11.25!, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, CType(0, Byte))
        Me.btnCancelDescription.Location = New System.Drawing.Point(460, 305)
        Me.btnCancelDescription.Name = "btnCancelDescription"
        Me.btnCancelDescription.Size = New System.Drawing.Size(109, 32)
        Me.btnCancelDescription.TabIndex = 1
        Me.btnCancelDescription.Text = "Cancel"
        Me.btnCancelDescription.UseVisualStyleBackColor = True
        '
        'frmDescription
        '
        Me.AutoScaleDimensions = New System.Drawing.SizeF(6.0!, 13.0!)
        Me.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font
        Me.ClientSize = New System.Drawing.Size(705, 349)
        Me.Controls.Add(Me.btnCancelDescription)
        Me.Controls.Add(Me.btnSaveDescription)
        Me.Controls.Add(Me.TextBox1)
        Me.Name = "frmDescription"
        Me.Text = "Edit project description"
        Me.ResumeLayout(False)
        Me.PerformLayout()

    End Sub

    Friend WithEvents TextBox1 As TextBox
    Friend WithEvents btnSaveDescription As Button
    Friend WithEvents btnCancelDescription As Button
End Class
