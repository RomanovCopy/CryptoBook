namespace CryptoBook.DTO
{
    /// <summary>
    /// Версия в формате SemVer 2.0.0 без учёта build metadata.
    /// </summary>
    public sealed class SemanticVersion : IComparable<SemanticVersion>, IEquatable<SemanticVersion>
    {
        private readonly IReadOnlyList<string> preReleaseIdentifiers;

        public int Major { get; }
        public int Minor { get; }
        public int Patch { get; }
        public bool IsPreRelease => preReleaseIdentifiers.Count > 0;

        private SemanticVersion(
            int major,
            int minor,
            int patch,
            IReadOnlyList<string> preReleaseIdentifiers)
        {
            Major = major;
            Minor = minor;
            Patch = patch;
            this.preReleaseIdentifiers = preReleaseIdentifiers;
        }

        public static bool TryParse(string? value, out SemanticVersion? version)
        {
            version = null;
            if(string.IsNullOrWhiteSpace(value))
                return false;

            string normalized = value.Trim();
            if(normalized.StartsWith("v", StringComparison.OrdinalIgnoreCase))
                normalized = normalized[1..];

            int buildSeparator = normalized.IndexOf('+');
            if(buildSeparator >= 0)
            {
                string buildMetadata = normalized[(buildSeparator + 1)..];
                if(buildMetadata.Length == 0 ||
                   buildMetadata.Contains('+') ||
                   !AreValidIdentifiers(buildMetadata, allowLeadingZeroes: true))
                {
                    return false;
                }

                normalized = normalized[..buildSeparator];
            }

            string coreAndPreRelease = normalized;
            string[] parts = coreAndPreRelease.Split('-', 2);
            string[] core = parts[0].Split('.');
            if(core.Length != 3 ||
               !TryParseCorePart(core[0], out int major) ||
               !TryParseCorePart(core[1], out int minor) ||
               !TryParseCorePart(core[2], out int patch))
            {
                return false;
            }

            var identifiers = new List<string>();
            if(parts.Length == 2)
            {
                if(!AreValidIdentifiers(parts[1], allowLeadingZeroes: false))
                    return false;

                foreach(string identifier in parts[1].Split('.'))
                {
                    identifiers.Add(identifier);
                }
            }

            version = new SemanticVersion(major, minor, patch, identifiers);
            return true;
        }

        public int CompareTo(SemanticVersion? other)
        {
            if(other is null)
                return 1;

            int result = Major.CompareTo(other.Major);
            if(result != 0)
                return result;
            result = Minor.CompareTo(other.Minor);
            if(result != 0)
                return result;
            result = Patch.CompareTo(other.Patch);
            if(result != 0)
                return result;

            if(IsPreRelease != other.IsPreRelease)
                return IsPreRelease ? -1 : 1;

            for(int index = 0;
                index < Math.Min(preReleaseIdentifiers.Count, other.preReleaseIdentifiers.Count);
                index++)
            {
                string left = preReleaseIdentifiers[index];
                string right = other.preReleaseIdentifiers[index];
                bool leftNumeric = left.All(char.IsDigit);
                bool rightNumeric = right.All(char.IsDigit);
                if(leftNumeric && rightNumeric)
                {
                    result = left.Length.CompareTo(right.Length);
                    if(result == 0)
                        result = string.CompareOrdinal(left, right);
                }
                else if(leftNumeric != rightNumeric)
                {
                    result = leftNumeric ? -1 : 1;
                }
                else
                {
                    result = string.CompareOrdinal(left, right);
                }

                if(result != 0)
                    return result;
            }

            return preReleaseIdentifiers.Count.CompareTo(other.preReleaseIdentifiers.Count);
        }

        public bool Equals(SemanticVersion? other) => CompareTo(other) == 0;

        public override bool Equals(object? obj) =>
            obj is SemanticVersion other && Equals(other);

        public override int GetHashCode()
        {
            HashCode hash = new();
            hash.Add(Major);
            hash.Add(Minor);
            hash.Add(Patch);
            foreach(string identifier in preReleaseIdentifiers)
                hash.Add(identifier, StringComparer.Ordinal);
            return hash.ToHashCode();
        }

        public override string ToString()
        {
            string value = $"{Major}.{Minor}.{Patch}";
            return IsPreRelease
                ? $"{value}-{string.Join('.', preReleaseIdentifiers)}"
                : value;
        }

        private static bool TryParseCorePart(string value, out int number)
        {
            number = 0;
            return value.Length > 0 &&
                (value == "0" || !value.StartsWith('0')) &&
                int.TryParse(value, out number) &&
                number >= 0;
        }

        private static bool AreValidIdentifiers(
            string value,
            bool allowLeadingZeroes)
        {
            if(string.IsNullOrWhiteSpace(value))
                return false;

            foreach(string identifier in value.Split('.'))
            {
                if(identifier.Length == 0 ||
                   identifier.Any(character =>
                       !IsAsciiLetterOrDigit(character) && character != '-'))
                {
                    return false;
                }

                if(!allowLeadingZeroes &&
                   identifier.All(character => character is >= '0' and <= '9') &&
                   identifier.Length > 1 &&
                   identifier[0] == '0')
                {
                    return false;
                }
            }

            return true;
        }

        private static bool IsAsciiLetterOrDigit(char character) =>
            character is >= '0' and <= '9' or
                         >= 'A' and <= 'Z' or
                         >= 'a' and <= 'z';
    }
}
