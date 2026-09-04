using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace App.Infrastructure.Safety
{
    /// <summary>
    /// Hardcoded protected roots per §3.8, plus user-configurable extra protected folders and
    /// extensions from AppSettings (Fase 4 Settings screen). The hardcoded roots always apply on
    /// top of whatever the user adds — configuration only widens protection, never narrows it.
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
        private readonly HashSet<string> _protectedExtensions;

        public ProtectedPathGuard(IEnumerable<string> extraProtectedRoots = null, IEnumerable<string> protectedExtensions = null)
        {
            _protectedRoots = DefaultProtectedRoots.ToList();
            if (extraProtectedRoots != null)
            {
                _protectedRoots.AddRange(extraProtectedRoots);
            }

            _protectedExtensions = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            if (protectedExtensions != null)
            {
                foreach (string ext in protectedExtensions)
                {
                    _protectedExtensions.Add(ext.TrimStart('.'));
                }
            }
        }

        public bool IsProtected(string path)
        {
            string full = Path.GetFullPath(path);

            bool underProtectedRoot = _protectedRoots.Any(root =>
                full.StartsWith(Path.GetFullPath(root), StringComparison.OrdinalIgnoreCase));

            string extension = Path.GetExtension(full).TrimStart('.');
            bool protectedExtension = extension.Length > 0 && _protectedExtensions.Contains(extension);

            return underProtectedRoot || protectedExtension;
        }
    }
}
