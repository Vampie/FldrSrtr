using System;
using App.Core.Model;

namespace App.Core.Execution
{
    /// <summary>
    /// Reverses Move/Rename/AddExtension/RemoveExtension (move the file back) and Copy (delete
    /// the copy, since the original was never touched). Delete-to-Recycle-Bin, and every
    /// non-relocating action (Open, CreateFolder, Zip, ...) aren't undoable here — Recycle Bin
    /// restore is left to the user via Explorer, per the project brief.
    /// </summary>
    public class UndoService
    {
        private readonly IFileOperations _fileOps;

        public UndoService(IFileOperations fileOps)
        {
            _fileOps = fileOps;
        }

        public static bool SupportsUndo(ActionType type) =>
            type == ActionType.Move || type == ActionType.Rename ||
            type == ActionType.AddExtension || type == ActionType.RemoveExtension ||
            type == ActionType.Copy;

        public UndoResult Undo(UndoableAction action)
        {
            if (!SupportsUndo(action.ActionType))
            {
                return UndoResult.Fail("Deze actie kan niet ongedaan worden gemaakt.");
            }

            try
            {
                if (action.ActionType == ActionType.Copy)
                {
                    if (!_fileOps.FileExists(action.NewPath))
                    {
                        return UndoResult.Fail("De kopie is niet meer te vinden op de doellocatie.");
                    }
                    _fileOps.DeleteToRecycleBin(action.NewPath);
                    return UndoResult.Ok();
                }

                if (!_fileOps.FileExists(action.NewPath))
                {
                    return UndoResult.Fail("Bestand niet meer te vinden op de nieuwe locatie.");
                }
                if (_fileOps.FileExists(action.OriginalPath))
                {
                    return UndoResult.Fail("Er staat alweer een bestand op de oorspronkelijke locatie.");
                }

                _fileOps.Move(action.NewPath, action.OriginalPath);
                return UndoResult.Ok();
            }
            catch (Exception ex)
            {
                return UndoResult.Fail(ex.Message);
            }
        }
    }
}
