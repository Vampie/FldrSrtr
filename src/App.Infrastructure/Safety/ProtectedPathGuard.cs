using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace App.Infrastructure.Safety
{
    /// <summary>
    /// Hardcoded protected roots per §3.8. User-configurable extra protected folders/extensions
    /// arrive in Fase 4 alongside the rest of the settings UI.
    /// </summary>
    public class ProtectedPathGuard
    {
        private static readonly string[] DefaultProtectedRoots =
        {
            @"C:\Windows",
            @"C:\Program Files",
            @"C:\Program Files (x86)",
            @"C:\ProgramData"
        };

        private readonly List<string> _protectedRoots;

        public ProtectedPathGuard(IEnumerable<string> extraProtectedRoots = null)
        {
            _protectedRoots = DefaultProtectedRoots.ToList();
            if (extraProtectedRoots != null)
            {
                _protectedRoots.AddRange(extraProtectedRoots);
            }
        }

        public bool IsProtected(string path)
        {
            string full = Path.GetFullPath(path);
            return _protectedRoots.Any(root =>
                full.StartsWith(Path.GetFullPath(root), StringComparison.OrdinalIgnoreCase));
        }
    }
}
