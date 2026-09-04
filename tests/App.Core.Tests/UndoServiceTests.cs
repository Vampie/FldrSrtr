using System.Collections.Generic;
using App.Core.Execution;
using App.Core.Model;
using FluentAssertions;
using Xunit;

namespace App.Core.Tests
{
    public class UndoServiceTests
    {
        private class FakeFileOperations : IFileOperations
        {
            public HashSet<string> ExistingFiles { get; } = new HashSet<string>();
            public List<(string From, string To)> Moved { get; } = new List<(string, string)>();
            public List<string> DeletedToRecycleBin { get; } = new List<string>();

            public bool FileExists(string path) => ExistingFiles.Contains(path);
            public void Move(string sourcePath, string destinationPath)
            {
                Moved.Add((sourcePath, destinationPath));
                ExistingFiles.Remove(sourcePath);
                ExistingFiles.Add(destinationPath);
            }
            public void Copy(string sourcePath, string destinationPath) => ExistingFiles.Add(destinationPath);
            public void DeleteToRecycleBin(string path)
            {
                DeletedToRecycleBin.Add(path);
                ExistingFiles.Remove(path);
            }
            public string ComputeSha256(string path) => path;
            public void CreateDirectory(string path) { }
            public void AddToZip(string filePath, string zipPath) { }
            public void OpenFile(string path) { }
            public void OpenFileWith(string applicationPath, string filePath) { }
            public void ExecuteExternal(string executablePath, string arguments) { }
        }

        [Fact]
        public void Undo_Move_MovesFileBack()
        {
            var fileOps = new FakeFileOperations();
            fileOps.ExistingFiles.Add(@"D:\Archive\invoice.pdf");
            var undo = new UndoService(fileOps);

            UndoResult result = undo.Undo(new UndoableAction
            {
                ActionType = ActionType.Move,
                OriginalPath = @"C:\Downloads\invoice.pdf",
                NewPath = @"D:\Archive\invoice.pdf"
            });

            result.Success.Should().BeTrue();
            fileOps.Moved.Should().ContainSingle(m => m.From == @"D:\Archive\invoice.pdf" && m.To == @"C:\Downloads\invoice.pdf");
        }

        [Fact]
        public void Undo_Copy_DeletesTheCopyToRecycleBin()
        {
            var fileOps = new FakeFileOperations();
            fileOps.ExistingFiles.Add(@"D:\Archive\invoice.pdf");
            var undo = new UndoService(fileOps);

            UndoResult result = undo.Undo(new UndoableAction
            {
                ActionType = ActionType.Copy,
                OriginalPath = @"C:\Downloads\invoice.pdf",
                NewPath = @"D:\Archive\invoice.pdf"
            });

            result.Success.Should().BeTrue();
            fileOps.DeletedToRecycleBin.Should().Contain(@"D:\Archive\invoice.pdf");
        }

        [Fact]
        public void Undo_DeleteToRecycleBin_IsNotSupported()
        {
            var undo = new UndoService(new FakeFileOperations());

            UndoResult result = undo.Undo(new UndoableAction { ActionType = ActionType.DeleteToRecycleBin });

            result.Success.Should().BeFalse();
        }

        [Fact]
        public void Undo_WhenOriginalPathAlreadyOccupied_Fails()
        {
            var fileOps = new FakeFileOperations();
            fileOps.ExistingFiles.Add(@"D:\Archive\invoice.pdf");
            fileOps.ExistingFiles.Add(@"C:\Downloads\invoice.pdf"); // something already there
            var undo = new UndoService(fileOps);

            UndoResult result = undo.Undo(new UndoableAction
            {
                ActionType = ActionType.Move,
                OriginalPath = @"C:\Downloads\invoice.pdf",
                NewPath = @"D:\Archive\invoice.pdf"
            });

            result.Success.Should().BeFalse();
            fileOps.Moved.Should().BeEmpty();
        }

        [Fact]
        public void Undo_WhenNewPathMissing_Fails()
        {
            var undo = new UndoService(new FakeFileOperations());

            UndoResult result = undo.Undo(new UndoableAction
            {
                ActionType = ActionType.Rename,
                OriginalPath = @"C:\Downloads\invoice.pdf",
                NewPath = @"C:\Downloads\renamed.pdf"
            });

            result.Success.Should().BeFalse();
        }
    }
}
