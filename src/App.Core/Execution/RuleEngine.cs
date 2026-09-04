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
    /// you see in a preview is exactly what would happen for real.
    /// </summary>
    public class RuleEngine
    {
        private readonly IFileOperations _fileOps;

        public RuleEngine(IFileOperations fileOps)
        {
            _fileOps = fileOps;
        }

        public IEnumerable<FileEntry> GetMatches(Rule rule, IEnumerable<FileEntry> files)
        {
            return files.Where(f => ConditionEvaluator.Matches(rule, f));
        }

        public List<PlannedAction> Plan(Rule rule, IEnumerable<FileEntry> files)
        {
            var plans = new List<PlannedAction>();
            foreach (FileEntry file in GetMatches(rule, files))
            {
                foreach (RuleAction action in rule.Actions)
                {
                    plans.Add(PlanAction(file, action));
                }
            }
            return plans;
        }

        public PlannedAction PlanAction(FileEntry file, RuleAction action)
        {
            var plan = new PlannedAction
            {
                File = file,
                Action = action,
                OriginalPath = file.FullPath
            };

            if (action.Type == ActionType.DeleteToRecycleBin)
            {
                return plan;
            }

            string desiredPath = ComputeDesiredPath(file, action);
            plan.ResolvedDestinationPath = ResolveConflict(desiredPath, action.OnConflict, plan);
            return plan;
        }

        public ExecutionResult Execute(PlannedAction plan, bool dryRun)
        {
            if (plan.Skipped)
            {
                return new ExecutionResult { Plan = plan, Success = true };
            }

            if (dryRun)
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

        private static string ComputeDesiredPath(FileEntry file, RuleAction action)
        {
            return action.Type == ActionType.Rename
                ? Path.Combine(file.Directory, action.Destination)
                : Path.Combine(action.Destination, file.Name);
        }

        private string ResolveConflict(string desiredPath, ConflictResolution resolution, PlannedAction plan)
        {
            if (!_fileOps.FileExists(desiredPath))
            {
                return desiredPath;
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
