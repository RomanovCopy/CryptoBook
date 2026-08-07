using CryptoBook.DTO;
using CryptoBook.Interfaces;

using System.Reflection;

namespace CryptoBook.Services
{
    public sealed class AssemblyApplicationVersionProvider:
        IApplicationVersionProvider
    {
        private readonly Assembly assembly;

        public AssemblyApplicationVersionProvider()
            : this(typeof(App).Assembly)
        {
        }

        internal AssemblyApplicationVersionProvider(Assembly assembly)
        {
            this.assembly = assembly ??
                throw new ArgumentNullException(nameof(assembly));
        }

        public SemanticVersion GetCurrentVersion()
        {
            string? informationalVersion = assembly
                .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?
                .InformationalVersion;
            if(SemanticVersion.TryParse(
                informationalVersion?.Split('+', 2)[0],
                out SemanticVersion? semanticVersion) &&
               semanticVersion is not null)
            {
                return semanticVersion;
            }

            Version? assemblyVersion = assembly.GetName().Version;
            string fallback = assemblyVersion is null
                ? "0.0.0"
                : $"{Math.Max(assemblyVersion.Major, 0)}." +
                  $"{Math.Max(assemblyVersion.Minor, 0)}." +
                  $"{Math.Max(assemblyVersion.Build, 0)}";
            if(SemanticVersion.TryParse(fallback, out semanticVersion) &&
               semanticVersion is not null)
            {
                return semanticVersion;
            }

            throw new InvalidOperationException(
                "The application version could not be determined.");
        }
    }
}
