using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using App.Core.Evaluation;
using App.Core.Model;

namespace App.Core.Execution
{
    /// <summary>
    /// Plans and executes rule actions. Dry-run and real execution share this exact code path —
    /// Execute(plan, dryRun: true) never calls into IFileOperations' mutating members, so what
    /// you see in a preview is exactly what would happen for real. Actions within a rule run in
    /// order per file, and each subsequent action acts on the *result* of the previous one.
    /// </summary>
    public class RuleEngine
    {
        private readonly IFileOperations _fileOps;
        private readonly IConflictPrompt _conflictPrompt;

        public RuleEngine(IFileOperations fileOps, IConflictPrompt conflictPrompt = null)
        {
            _fileOps = fileOps;
            _conflictPrompt = conflictPrompt;
        }

        public IEnumerable<FileEntry> GetMatches(Rule rule, IEnumerable<FileEntry> files)
        {
            return files.Where(f => ConditionEvaluator.Matches(rule, f));
        }

        /// <summary>
        /// Plans and executes every action of the rule, in order, for every matching file.
        /// A file already seen in this run (e.g. duplicate scan results) is processed once —
        /// the per-run "locking" guard called for in the project brief.
        /// </summary>
        public List<ExecutionResult> ExecuteRule(Rule rule, IEnumerable<FileEntry> files, bool dryRun)
        {
            IReadOnlyList<FileEntry> fileList = files as IReadOnlyList<FileEntry> ?? files.ToList();

            if (DuplicateDetector.RuleChecksDuplicates(rule.RootCondition))
            {
                DuplicateDetector.MarkDuplicates(fileList, _fileOps);
            }

            var results = new List<ExecutionResult>();
            var seenPaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            DateTime now = DateTime.Now;

            foreach (FileEntry file in GetMatches(rule, fileList))
            {
                if (!seenPaths.Add(file.FullPath))
                {
                    continue;
                }

                string currentPath = file.FullPath;

                foreach (RuleAction action in rule.Actions)
                {
                    PlannedAction plan = PlanAction(file, action, currentPath, now);
                    ExecutionResult result = Execute(plan, dryRun);
                    results.Add(result);

                    if (!result.Success)
                    {
                        break; // don't chain further actions onto a failed step
                    }

                    if (plan.Skipped)
                    {
                        continue; // conflict skip: file untouched, next action still sees the same path
                    }

                    if (action.Type == ActionType.DeleteToRecycleBin)
                    {
                        break; // file is gone, nothing left to chain onto
                    }

                    if (ChangesLocation(action.Type) && plan.ResolvedDestinationPath != null)
                    {
                        currentPath = plan.ResolvedDestinationPath;
                    }
                }
            }

            return results;
        }

        private static bool ChangesLocation(ActionType type) =>
            type == ActionType.Move || type == ActionType.Rename ||
            type == ActionType.AddExtension || type == ActionType.RemoveExtension;

        public PlannedAction PlanAction(FileEntry file, RuleAction action, string currentPath, DateTime? now = null)
        {
            DateTime effectiveNow = now ?? DateTime.Now;
            var plan = new PlannedAction
            {
                File = file,
                Action = action,
                OriginalPath = currentPath
            };

            switch (action.Type)
            {
                case ActionType.DeleteToRecycleBin:
                case ActionType.Open:
                    return plan;

                case ActionType.OpenWith:
                case ActionType.ExecuteExternal:
                    plan.ResolvedDestinationPath = VariableResolver.Resolve(action.Destination, file, currentPath, effectiveNow);
                    plan.ResolvedArguments = VariableResolver.Resolve(action.Arguments, file, currentPath, effectiveNow);
                    return plan;

                case ActionType.CreateFolder:
                case ActionType.Zip:
                    plan.ResolvedDestinationPath = MakeAbsolute(
                        VariableResolver.Resolve(action.Destination, file, currentPath, effectiveNow), currentPath);
                    return plan;

                case ActionType.AddExtension:
                    {
                        string ext = VariableResolver.Resolve(action.Destination, file, currentPath, effectiveNow).TrimStart('.');
                        string desired = currentPath + "." + ext;
                        plan.ResolvedDestinationPath = ResolveConflict(desired, action.OnConflict, plan);
                        return plan;
                    }

                case ActionType.RemoveExtension:
                    {
                        string dir = Path.GetDirectoryName(currentPath);
                        string nameNoExt = Path.GetFileNameWithoutExtension(currentPath);
                        string desired = Path.Combine(dir ?? string.Empty, nameNoExt);
                        plan.ResolvedDestinationPath = ResolveConflict(desired, action.OnConflict, plan);
                        return plan;
                    }

                case ActionType.Rename:
                    {
                        string dir = Path.GetDirectoryName(currentPath);
                        string newName = VariableResolver.Resolve(action.Destination, file, currentPath, effectiveNow);
                        string desired = Path.Combine(dir ?? string.Empty, newName);
                        plan.ResolvedDestinationPath = ResolveConflict(desired, action.OnConflict, plan);
                        return plan;
                    }

                default: // Move, Copy
                    {
                        string template = action.Destination ?? string.Empty;
                        string resolved = MakeAbsolute(VariableResolver.Resolve(template, file, currentPath, effectiveNow), currentPath);
                        bool includesFileName = template.Contains("{FileName}") || template.Contains("{OriginalName}");
                        string desired = includesFileName ? resolved : Path.Combine(resolved, Path.GetFileName(currentPath));
                        plan.ResolvedDestinationPath = ResolveConflict(desired, action.OnConflict, plan);
                        return plan;
                    }
            }
        }

        /// <summary>
        /// A Destination template without a drive letter or {Directory} token (e.g.
        /// "{Year}_{Month}\{Day}") used to resolve relative to the process's working directory —
        /// which for a portable exe is wherever it happens to be run from, not the folder being
        /// processed. Anchor it to the current file's own directory instead.
        /// </summary>
        private static string MakeAbsolute(string path, string currentPath)
        {
            if (string.IsNullOrEmpty(path) || Path.IsPathRooted(path))
            {
                return path;
            }

            string baseDirectory = Path.GetDirectoryName(currentPath) ?? string.Empty;
            return Path.Combine(baseDirectory, path);
        }

        public ExecutionResult Execute(PlannedAction plan, bool dryRun)
        {
            if (plan.Skipped || dryRun)
            {
                return new ExecutionResult { Plan = plan, Success = true };
            }

            try
            {
                switch (plan.Action.Type)
                {
                    case ActionType.Move:
                    case ActionType.Rename:
                    case ActionType.AddExtension:
                    case ActionType.RemoveExtension:
                        _fileOps.Move(plan.OriginalPath, plan.ResolvedDestinationPath);
                        break;
                    case ActionType.Copy:
                        _fileOps.Copy(plan.OriginalPath, plan.ResolvedDestinationPath);
                        break;
                    case ActionType.DeleteToRecycleBin:
                        _fileOps.DeleteToRecycleBin(plan.OriginalPath);
                        break;
                    case ActionType.Open:
                        _fileOps.OpenFile(plan.OriginalPath);
                        break;
                    case ActionType.OpenWith:
                        _fileOps.OpenFileWith(plan.ResolvedDestinationPath, plan.OriginalPath);
                        break;
                    case ActionType.ExecuteExternal:
                        _fileOps.ExecuteExternal(plan.ResolvedDestinationPath, plan.ResolvedArguments);
                        break;
                    case ActionType.CreateFolder:
                        _fileOps.CreateDirectory(plan.ResolvedDestinationPath);
                        break;
                    case ActionType.Zip:
                        _fileOps.AddToZip(plan.OriginalPath, plan.ResolvedDestinationPath);
                        break;
                }
                return new ExecutionResult { Plan = plan, Success = true };
            }
            catch (Exception ex)
            {
                return new ExecutionResult { Plan = plan, Success = false, ErrorMessage = ex.Message };
            }
        }

        private string ResolveConflict(string desiredPath, ConflictResolution resolution, PlannedAction plan)
        {
            if (!_fileOps.FileExists(desiredPath))
            {
                return desiredPath;
            }

            if (resolution == ConflictResolution.Ask)
            {
                resolution = _conflictPrompt?.Resolve(desiredPath, plan.OriginalPath) ?? ConflictResolution.Rename;
            }

            switch (resolution)
            {
                case ConflictResolution.Skip:
                    plan.Skipped = true;
                    plan.SkipReason = "Doelbestand bestaat al.";
                    return desiredPath;
                case ConflictResolution.Overwrite:
                    return desiredPath;
                case ConflictResolution.Rename:
                    return FindAvailableName(desiredPath);
                default:
                    return desiredPath;
            }
        }

        private string FindAvailableName(string path)
        {
            string dir = Path.GetDirectoryName(path);
            string nameNoExt = Path.GetFileNameWithoutExtension(path);
            string ext = Path.GetExtension(path);

            int counter = 1;
            string candidate;
            do
            {
                candidate = Path.Combine(dir, $"{nameNoExt} ({counter}){ext}");
                counter++;
            } while (_fileOps.FileExists(candidate));

            return candidate;
        }
    }
}
