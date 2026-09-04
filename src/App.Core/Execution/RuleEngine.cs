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
            var results = new List<ExecutionResult>();
            var seenPaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (FileEntry file in GetMatches(rule, files))
            {
                if (!seenPaths.Add(file.FullPath))
                {
                    continue;
                }

                string currentPath = file.FullPath;

                foreach (RuleAction action in rule.Actions)
                {
                    PlannedAction plan = PlanAction(file, action, currentPath);
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

                    if (plan.ResolvedDestinationPath != null)
                    {
                        currentPath = plan.ResolvedDestinationPath;
                    }
                }
            }

            return results;
        }

        public PlannedAction PlanAction(FileEntry file, RuleAction action, string currentPath)
        {
            var plan = new PlannedAction
            {
                File = file,
                Action = action,
                OriginalPath = currentPath
            };

            if (action.Type == ActionType.DeleteToRecycleBin)
            {
                return plan;
            }

            string desiredPath = ComputeDesiredPath(currentPath, action);
            plan.ResolvedDestinationPath = ResolveConflict(desiredPath, action.OnConflict, plan);
            return plan;
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
                        _fileOps.Move(plan.OriginalPath, plan.ResolvedDestinationPath);
                        break;
                    case ActionType.Copy:
                        _fileOps.Copy(plan.OriginalPath, plan.ResolvedDestinationPath);
                        break;
                    case ActionType.DeleteToRecycleBin:
                        _fileOps.DeleteToRecycleBin(plan.OriginalPath);
                        break;
                }
                return new ExecutionResult { Plan = plan, Success = true };
            }
            catch (Exception ex)
            {
                return new ExecutionResult { Plan = plan, Success = false, ErrorMessage = ex.Message };
            }
        }

        private static string ComputeDesiredPath(string currentPath, RuleAction action)
        {
            if (action.Type == ActionType.Rename)
            {
                string directory = Path.GetDirectoryName(currentPath);
                return Path.Combine(directory ?? string.Empty, action.Destination);
            }

            string fileName = Path.GetFileName(currentPath);
            return Path.Combine(action.Destination, fileName);
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
