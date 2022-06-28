'This module's imports and settings.
Option Compare Binary
Option Explicit On
Option Infer Off
Option Strict On

Imports Microsoft.VisualBasic
Imports System
Imports System.Collections.Generic
Imports System.Convert
Imports System.Environment
Imports System.IO
Imports System.Linq
Imports System.Math

'This module contains this program's core procedures.
Public Module GameLibraryModule
   'This enumeration lists the signatures of the supported library file types.
   Private Enum SignaturesE As Integer
      EASignature    'Electronic Arts.
      MSSignature    'Mythos Software.
   End Enum

   Private Const FILE_NAME_LENGTH As Integer = 13                                                                                                                   'The maximum length allowed for a file name including a terminating null character.
   Private ReadOnly BYTES_TO_TEXT As Func(Of Byte(), String) = Function(Bytes() As Byte) (New String((From ByteO In Bytes Select ToChar(ByteO)).ToArray()))         'This procedure converts the specified bytes to text.
   Private ReadOnly FILE_TYPES() As String = {"Electornic Arts", "Mythos Software"}                                                                                 'The descriptions for the supported library file types.
   Private ReadOnly SIGNATURES() As String = {"EALIB", "LIB" & ToChar(&H1A%)}                                                                                       'The signatures of the supported library file types.
   Private ReadOnly TERMINATOR As Char = ControlChars.NullChar                                                                                                      'The null character that terminates a file name.
   Private ReadOnly TEXT_TO_BYTES As Func(Of String, Byte()) = Function(Text As String) ((From Character In Text.ToCharArray Select ToByte(Character)).ToArray())   'This procedure converts the specified text to bytes.

   'This procedure creates a library file containing the files in the specified folder.
   Private Sub CreateLibrary(SourcePath As String, Signature As SignaturesE)
      Try
         Dim FileOffset As New Integer
         Dim Files As New List(Of FileInfo)(My.Computer.FileSystem.GetDirectoryInfo(SourcePath).GetFiles("*.*"))
         Dim HeaderSize As Integer = SIGNATURES(Signature).Length + (2 + ((Files.Count + 1) * ((FILE_NAME_LENGTH + If(Signature = SignaturesE.EASignature, 1, 0)) + 4)))
         Dim IsLast As New Byte
         Dim LibraryPath As String = Path.GetFullPath(SourcePath)

         If LibraryPath.EndsWith("\") Then LibraryPath = LibraryPath.Substring(0, LibraryPath.Length - 1)

         Console.WriteLine($"Creating {FILE_TYPES(Signature)} library file...")

         FileOffset = HeaderSize + 1
         Using LibraryFile As New BinaryWriter(File.OpenWrite($"{LibraryPath}.lib"))
            With LibraryFile
               .Write(TEXT_TO_BYTES(SIGNATURES(Signature)))
               .Write(CUShort(Files.Count))

               For FileIndex As Integer = 0 To Files.Count
                  IsLast = ToByte(Abs(ToInt32(FileIndex = Files.Count)))
                  If FileIndex = Files.Count Then
                     .Write(New String(TERMINATOR, FILE_NAME_LENGTH))
                     If Signature = SignaturesE.EASignature Then .Write(ToChar(IsLast))
                     .Write(CUInt(FileOffset))
                  Else
                     .Write(TEXT_TO_BYTES(Files(FileIndex).Name))
                     .Write(TEXT_TO_BYTES(New String(TERMINATOR, FILE_NAME_LENGTH - Files(FileIndex).Name.Length)))
                     If Signature = SignaturesE.EASignature Then .Write(ToChar(IsLast))
                     .Write(CUInt(FileOffset))
                     FileOffset += CInt(Files(FileIndex).Length)
                  End If
               Next FileIndex

               For Each FileO As FileInfo In Files
                  .Write(File.ReadAllBytes(FileO.FullName))
                  Console.WriteLine(FileO.Name.ToUpper())
               Next FileO
            End With
         End Using

         Console.WriteLine($"{Files.Count} file(s) added.")
      Catch ExceptionO As Exception
         HandleError(ExceptionO)
      End Try
   End Sub

   'This procedure opens the specified library and gives the command to extract its files.
   Private Sub ExtractLibrary(LibraryPath As String)
      Try
         Dim Signature As String = Nothing

         Using LibraryFile As New BinaryReader(File.OpenRead(LibraryPath))
            Signature = BYTES_TO_TEXT(LibraryFile.ReadBytes(5))
            If Signature.StartsWith(SIGNATURES(SignaturesE.EASignature)) Then
               ExtractLibraryFiles(LibraryFile, LibraryPath, SignaturesE.EASignature)
            ElseIf Signature.StartsWith(SIGNATURES(SignaturesE.MSSignature)) Then
               ExtractLibraryFiles(LibraryFile, LibraryPath, SignaturesE.MSSignature)
            Else
               Console.WriteLine("This is not a supported library file.")
            End If
         End Using
      Catch ExceptionO As Exception
         HandleError(ExceptionO)
      End Try
   End Sub

   'This procecure extracts the specified library's files.
   Private Sub ExtractLibraryFiles(LibraryFile As BinaryReader, LibraryPath As String, Signature As SignaturesE)
      Try
         Dim FileCount As New Integer
         Dim FileName As String = Nothing
         Dim FileNames As New List(Of String)
         Dim FileOffsets As New List(Of Integer)
         Dim OutputPath As String = LibraryPath.Substring(0, LibraryPath.IndexOf("."))

         Console.WriteLine($"{FILE_TYPES(Signature)} library file detected...")

         LibraryFile.BaseStream.Seek(SIGNATURES(Signature).Length, SeekOrigin.Begin)
         FileCount = LibraryFile.ReadUInt16()
         For FileIndex As Integer = 0 To FileCount
            FileName = BYTES_TO_TEXT(LibraryFile.ReadBytes(FILE_NAME_LENGTH))
            FileNames.Add(FileName.Substring(0, FileName.IndexOf(TERMINATOR)))
            If Signature = SignaturesE.EASignature Then LibraryFile.ReadByte()
            FileOffsets.Add(LibraryFile.ReadInt32())
         Next FileIndex

         If Not Directory.Exists(OutputPath) Then Directory.CreateDirectory(OutputPath)

         For FileIndex As Integer = 0 To FileNames.Count - 2
            LibraryFile.BaseStream.Seek(FileOffsets(FileIndex) + If(Signature = SignaturesE.EASignature, 0, 1), SeekOrigin.Begin)
            File.WriteAllBytes(Path.Combine(OutputPath, FileNames(FileIndex)), LibraryFile.ReadBytes(FileOffsets(FileIndex + 1) - FileOffsets(FileIndex)))
            Console.WriteLine(FileNames(FileIndex))
         Next FileIndex

         Console.WriteLine($"{FileCount} file(s) extracted.")
      Catch ExceptionO As Exception
         HandleError(ExceptionO)
      End Try
   End Sub

   'This procedure handles any errors that occur.
   Private Sub HandleError(ExceptionO As Exception)
      Try
         Console.WriteLine("Error:")
         Console.WriteLine(ExceptionO.Message)
         Console.WriteLine()
      Catch
         [Exit](0)
      End Try
   End Sub

   'This procedure is executed when this program is started.
   Public Sub Main()
      Try
         Dim LibraryPath As String = Nothing
         Dim SelectedType As New Char
         Dim SelectedTypeIndex As New Integer

         With My.Application.Info
            Console.WriteLine($"{ .Title} v{ .Version}, by: { .CompanyName} { .Copyright}")
            Console.WriteLine()
         End With

         If My.Application.CommandLineArgs.Count = 1 Then
            LibraryPath = My.Application.CommandLineArgs(0)
         Else
            Console.Write("Library file or directory (add ""\*"" for directories): ")
            LibraryPath = Console.ReadLine()
         End If

         If LibraryPath.StartsWith("""") Then LibraryPath = LibraryPath.Substring(1)
         If LibraryPath.EndsWith("""") Then LibraryPath = LibraryPath.Substring(0, LibraryPath.Length - 1)

         If Not LibraryPath = Nothing Then
            If LibraryPath.EndsWith("\*") Then
               LibraryPath = LibraryPath.Substring(0, LibraryPath.IndexOf("\*"))

               Console.WriteLine()
               Console.WriteLine("Select a library type:")
               For TypeIndex As Integer = FILE_TYPES.GetLowerBound(0) To FILE_TYPES.GetUpperBound(0)
                  Console.WriteLine($" {TypeIndex} = {FILE_TYPES(TypeIndex)}")
               Next TypeIndex

               SelectedType = Console.ReadKey(intercept:=True).KeyChar
               If Integer.TryParse(SelectedType, SelectedTypeIndex) Then
                  CreateLibrary(LibraryPath, DirectCast(SelectedTypeIndex, SignaturesE))
               Else
                  Console.WriteLine("Invalid library type selection.")
               End If
            Else
               If Not LibraryPath.EndsWith(".") Then LibraryPath &= "."
               LibraryPath = Path.GetFullPath(LibraryPath)
               ExtractLibrary(LibraryPath)
            End If
         End If
      Catch ExceptionO As Exception
         HandleError(ExceptionO)
      End Try
   End Sub
End Module
