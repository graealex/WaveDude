' Wave Dude 2D - interactive 2D wave simulator
' Copyright (C) 2026 JJM Vleggaar (Huygens Optics)
' SPDX-License-Identifier: GPL-3.0-or-later
'
' This file is part of Wave Dude 2D.
'
' Wave Dude 2D is free software: you can redistribute it and/or modify
' it under the terms of the GNU General Public License as published by
' the Free Software Foundation, either version 3 of the License, or
' (at your option) any later version.
'
' Wave Dude 2D is distributed in the hope that it will be useful,
' but WITHOUT ANY WARRANTY; without even the implied warranty of
' MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the
' GNU General Public License for more details.
'
' You should have received a copy of the GNU General Public License
' along with Wave Dude 2D. If not, see <https://www.gnu.org/licenses/>.

Option Strict On
Option Explicit On

Imports System.Collections
Imports System.Collections.Generic
Imports System.Globalization
Imports System.IO
Imports System.Text
Imports System.Web.Script.Serialization

''' <summary>
''' The configuration of one project, stored as configuration.json in the main folder of the project.
'''
''' The file is a JSON object. The keys below are always present. Every tab that gets functionality later
''' stores its own settings as a section (a nested JSON object), for example:
'''   Dim section = project.Config.GetSection("probes")
'''   section("count") = 3
'''   project.Save()
''' Keys that this version of the program does not know are kept and written back unchanged, so a file
''' written by a newer version is not damaged by an older one (it is refused when its format_version is newer).
''' </summary>
Public NotInheritable Class ProjectConfig

    ''' <summary>Name of the configuration file inside a project folder.</summary>
    Public Const FileName As String = "configuration.json"

    ''' <summary>Name of the empty placeholder file that projects had before configuration.json existed.</summary>
    Public Const LegacyFileName As String = "Configuration.txt"

    ''' <summary>Version of the file layout. Increase it when a change makes old files incompatible.</summary>
    Public Const CurrentFormatVersion As Integer = 1

    Private Const KeyFormatVersion As String = "format_version"
    Private Const KeyProjectName As String = "project_name"
    Private Const KeyCreated As String = "created"
    Private Const KeyModified As String = "modified"
    Private Const KeyDescription As String = "description"

    ' insertion ordered in practice, so the file keeps a stable, readable order
    Private ReadOnly _values As Dictionary(Of String, Object)

    ''' <summary>A new configuration with the standard keys only.</summary>
    Public Sub New(projectName As String)
        Dim stamp As String = Timestamp(DateTime.UtcNow)
        _values = New Dictionary(Of String, Object)()
        _values(KeyFormatVersion) = CurrentFormatVersion
        _values(KeyProjectName) = projectName
        _values(KeyCreated) = stamp
        _values(KeyModified) = stamp
    End Sub

    Private Sub New(values As Dictionary(Of String, Object))
        _values = values
        If Not _values.ContainsKey(KeyFormatVersion) Then _values(KeyFormatVersion) = CurrentFormatVersion
    End Sub

    ' ---------------------------------------------------------------- standard values

    Public ReadOnly Property FormatVersion As Integer
        Get
            Return GetInteger(KeyFormatVersion, CurrentFormatVersion)
        End Get
    End Property

    ''' <summary>Name of the project. It is always the name of the project folder.</summary>
    Public Property ProjectName As String
        Get
            Return GetString(KeyProjectName, "")
        End Get
        Set(value As String)
            _values(KeyProjectName) = value
        End Set
    End Property

    ''' <summary>When the project was created (UTC, ISO 8601 text).</summary>
    Public ReadOnly Property Created As String
        Get
            Return GetString(KeyCreated, "")
        End Get
    End Property

    ''' <summary>When the file was last saved (UTC, ISO 8601 text).</summary>
    Public ReadOnly Property Modified As String
        Get
            Return GetString(KeyModified, "")
        End Get
    End Property

    ''' <summary>A free-form description of the project (Form1's btnDescription/frmDescription), empty when none was set.</summary>
    Public Property Description As String
        Get
            Return GetString(KeyDescription, "")
        End Get
        Set(value As String)
            _values(KeyDescription) = value
        End Set
    End Property

    ' ---------------------------------------------------------------- general access (for the tabs)

    Public Function ContainsKey(key As String) As Boolean
        Return _values.ContainsKey(key)
    End Function

    Public Function GetString(key As String, fallback As String) As String
        Dim v As Object = Nothing
        If _values.TryGetValue(key, v) AndAlso v IsNot Nothing Then
            Return Convert.ToString(v, CultureInfo.InvariantCulture)
        End If
        Return fallback
    End Function

    Public Function GetInteger(key As String, fallback As Integer) As Integer
        Dim v As Object = Nothing
        If _values.TryGetValue(key, v) AndAlso v IsNot Nothing Then
            Try
                Return Convert.ToInt32(v, CultureInfo.InvariantCulture)
            Catch ex As Exception When TypeOf ex Is FormatException OrElse TypeOf ex Is InvalidCastException OrElse TypeOf ex Is OverflowException
                Return fallback
            End Try
        End If
        Return fallback
    End Function

    ''' <summary>
    ''' Sets a value. Allowed values: String, Boolean, numbers, Nothing, a Dictionary(Of String, Object)
    ''' or a list or array of these.
    ''' </summary>
    Public Sub SetValue(key As String, value As Object)
        _values(key) = value
    End Sub

    ''' <summary>
    ''' The nested object with the given name; it is created (empty) when it does not exist yet.
    ''' Changes made to the returned dictionary are part of the configuration and are written by Save.
    ''' </summary>
    Public Function GetSection(name As String) As Dictionary(Of String, Object)
        Dim v As Object = Nothing
        If _values.TryGetValue(name, v) Then
            Dim existing As Dictionary(Of String, Object) = TryCast(v, Dictionary(Of String, Object))
            If existing IsNot Nothing Then Return existing
        End If
        Dim section As New Dictionary(Of String, Object)()
        _values(name) = section
        Return section
    End Function

    ' ---------------------------------------------------------------- file

    ''' <summary>Reads a configuration file. Throws when it cannot be read, is not valid or is from a newer version.</summary>
    Public Shared Function Load(filePath As String) As ProjectConfig
        Dim text As String = File.ReadAllText(filePath, Encoding.UTF8)

        Dim serializer As New JavaScriptSerializer()
        serializer.MaxJsonLength = Integer.MaxValue

        Dim root As Dictionary(Of String, Object) = Nothing
        Try
            root = TryCast(serializer.DeserializeObject(text), Dictionary(Of String, Object))
        Catch ex As Exception When TypeOf ex Is ArgumentException OrElse TypeOf ex Is InvalidOperationException
            Throw New InvalidDataException("The file " & Path.GetFileName(filePath) & " is not valid JSON: " & ex.Message, ex)
        End Try
        If root Is Nothing Then
            Throw New InvalidDataException("The file " & Path.GetFileName(filePath) & " is not a project configuration (a JSON object was expected).")
        End If

        Dim config As New ProjectConfig(root)
        If config.FormatVersion > CurrentFormatVersion Then
            Throw New InvalidDataException("The file " & Path.GetFileName(filePath) & " was made by a newer version of the program (format " &
                                           config.FormatVersion.ToString(CultureInfo.InvariantCulture) & ").")
        End If
        Return config
    End Function

    ''' <summary>Writes the configuration to a file (overwriting an existing file) and updates the modified time.</summary>
    Public Sub Save(filePath As String)
        _values(KeyFormatVersion) = CurrentFormatVersion
        _values(KeyModified) = Timestamp(DateTime.UtcNow)

        Dim sb As New StringBuilder()
        WriteObject(sb, _values, 0)
        sb.AppendLine()

        Dim folderPath As String = Path.GetDirectoryName(filePath)
        If Not String.IsNullOrEmpty(folderPath) Then Directory.CreateDirectory(folderPath)

        ' write to a temporary file first so a failed save never leaves a half-written configuration behind
        Dim tmp As String = filePath & ".tmp"
        File.WriteAllText(tmp, sb.ToString(), New UTF8Encoding(False))
        If File.Exists(filePath) Then File.Delete(filePath)
        File.Move(tmp, filePath)
    End Sub

    ' ---------------------------------------------------------------- JSON writing

    Private Shared Function Timestamp(value As DateTime) As String
        Return value.ToString("yyyy-MM-dd'T'HH:mm:ss'Z'", CultureInfo.InvariantCulture)
    End Function

    Private Shared Sub WriteValue(sb As StringBuilder, value As Object, level As Integer)
        If value Is Nothing Then
            sb.Append("null")
            Return
        End If

        Select Case Type.GetTypeCode(value.GetType())
            Case TypeCode.Boolean
                sb.Append(If(Convert.ToBoolean(value, CultureInfo.InvariantCulture), "true", "false"))
            Case TypeCode.Byte, TypeCode.SByte, TypeCode.Int16, TypeCode.UInt16, TypeCode.Int32,
                 TypeCode.UInt32, TypeCode.Int64, TypeCode.UInt64, TypeCode.Decimal
                sb.Append(Convert.ToString(value, CultureInfo.InvariantCulture))
            Case TypeCode.Single, TypeCode.Double
                Dim d As Double = Convert.ToDouble(value, CultureInfo.InvariantCulture)
                If Double.IsNaN(d) OrElse Double.IsInfinity(d) Then
                    sb.Append("null")
                Else
                    sb.Append(d.ToString("R", CultureInfo.InvariantCulture))
                End If
            Case TypeCode.DateTime
                sb.Append(Quote(Timestamp(Convert.ToDateTime(value, CultureInfo.InvariantCulture).ToUniversalTime())))
            Case TypeCode.String, TypeCode.Char
                sb.Append(Quote(Convert.ToString(value, CultureInfo.InvariantCulture)))
            Case TypeCode.Object
                Dim dict As IDictionary = TryCast(value, IDictionary)
                Dim list As IEnumerable = TryCast(value, IEnumerable)
                If dict IsNot Nothing Then
                    WriteObject(sb, dict, level)
                ElseIf list IsNot Nothing Then
                    WriteArray(sb, list, level)
                Else
                    sb.Append(Quote(Convert.ToString(value, CultureInfo.InvariantCulture)))
                End If
            Case Else
                sb.Append("null")
        End Select
    End Sub

    Private Shared Sub WriteObject(sb As StringBuilder, dict As IDictionary, level As Integer)
        If dict.Count = 0 Then
            sb.Append("{}")
            Return
        End If

        sb.AppendLine("{")
        Dim index As Integer = 0
        For Each entry As DictionaryEntry In dict
            index += 1
            sb.Append(Indent(level + 1))
            sb.Append(Quote(Convert.ToString(entry.Key, CultureInfo.InvariantCulture)))
            sb.Append(": ")
            WriteValue(sb, entry.Value, level + 1)
            If index < dict.Count Then sb.Append(",")
            sb.AppendLine()
        Next
        sb.Append(Indent(level))
        sb.Append("}")
    End Sub

    Private Shared Sub WriteArray(sb As StringBuilder, list As IEnumerable, level As Integer)
        Dim items As New List(Of Object)()
        Dim allScalar As Boolean = True
        For Each item As Object In list
            items.Add(item)
            If item IsNot Nothing AndAlso (TypeOf item Is IDictionary OrElse (TypeOf item Is IEnumerable AndAlso Not TypeOf item Is String)) Then
                allScalar = False
            End If
        Next

        If items.Count = 0 Then
            sb.Append("[]")
        ElseIf allScalar Then
            sb.Append("[")
            For i As Integer = 0 To items.Count - 1
                If i > 0 Then sb.Append(", ")
                WriteValue(sb, items(i), level + 1)
            Next
            sb.Append("]")
        Else
            sb.AppendLine("[")
            For i As Integer = 0 To items.Count - 1
                sb.Append(Indent(level + 1))
                WriteValue(sb, items(i), level + 1)
                If i < items.Count - 1 Then sb.Append(",")
                sb.AppendLine()
            Next
            sb.Append(Indent(level))
            sb.Append("]")
        End If
    End Sub

    Private Shared Function Indent(level As Integer) As String
        Return New String(" "c, level * 2)
    End Function

    Private Shared Function Quote(text As String) As String
        If text Is Nothing Then Return """"""
        Dim sb As New StringBuilder()
        sb.Append(""""c)
        For Each ch As Char In text
            Select Case ch
                Case """"c
                    sb.Append("\""")
                Case "\"c
                    sb.Append("\\")
                Case ChrW(10)
                    sb.Append("\n")
                Case ChrW(13)
                    sb.Append("\r")
                Case ChrW(9)
                    sb.Append("\t")
                Case Else
                    If AscW(ch) < 32 Then
                        sb.Append("\u").Append(AscW(ch).ToString("x4", CultureInfo.InvariantCulture))
                    Else
                        sb.Append(ch)
                    End If
            End Select
        Next
        sb.Append(""""c)
        Return sb.ToString()
    End Function

End Class
