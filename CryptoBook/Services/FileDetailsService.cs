using CryptoBook.DTO;
using CryptoBook.Infrastructure;
using CryptoBook.Interfaces;

using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Reflection.Metadata;
using System.Reflection.PortableExecutable;
using System.Security.AccessControl;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Security.Principal;

namespace CryptoBook.Services;

public sealed class FileDetailsService: IFileDetailsService
{
    public Task<IReadOnlyList<FilePropertyEntry>> ReadAsync(
        FileDetailsTarget target, CancellationToken cancellationToken = default) =>
        Task.Run<IReadOnlyList<FilePropertyEntry>>(() => Read(target, cancellationToken), cancellationToken);

    private static IReadOnlyList<FilePropertyEntry> Read(FileDetailsTarget target, CancellationToken token)
    {
        var rows = new List<FilePropertyEntry>();
        Add(rows, "General", "Name", FileTypeInspector.EscapeName(target.Name));
        Add(rows, "General", "Path", FileTypeInspector.EscapeName(target.DisplayPath));
        Add(rows, "General", "StorageLocation", FileTypeInspector.EscapeName(target.Path));
        if(target.LocalPath is not string path)
        {
            Add(rows, "General", "Size", Bytes(target.Size));
            AddDate(rows, "Modified", target.LastWriteTimeUtc);
            Add(rows, "General", "Availability", L("RemoteLimit"));
            return rows;
        }

        token.ThrowIfCancellationRequested();
        FileAttributes attributes = File.GetAttributes(path);
        bool directory = attributes.HasFlag(FileAttributes.Directory);
        FileSystemInfo info = directory ? new DirectoryInfo(path) : new FileInfo(path);
        Add(rows, "General", "Kind", L(directory ? "Directory" : "File"));
        Add(rows, "General", "Extension", System.IO.Path.GetExtension(path));
        if(info is FileInfo file)
            Add(rows, "General", "Size", Bytes(file.Length));
        AddDate(rows, "Created", info.CreationTimeUtc);
        AddDate(rows, "Modified", info.LastWriteTimeUtc);
        AddDate(rows, "Accessed", info.LastAccessTimeUtc);
        Add(rows, "General", "Attributes", $"{attributes} (0x{(uint)attributes:X8})");
        foreach(FileAttributes flag in Enum.GetValues<FileAttributes>())
            if(attributes.HasFlag(flag))
                rows.Add(new(L("General"), flag.ToString(), "True", $"FileAttributes.{flag}"));
        TrySection(rows, "General", () => Add(rows, "General", "LinkTarget", info.LinkTarget));
        TrySection(rows, "Storage", () =>
        {
            var storage = WindowsFileStorageDetails.Read(path);
            Add(rows, "Storage", "AllocationSize", Bytes(storage.AllocationSize));
            Add(rows, "Storage", "HardLinks", storage.Links);
            Add(rows, "Storage", "FileId", storage.FileId);
            Add(rows, "Storage", "VolumeId", storage.VolumeId);
        });
        if(!directory)
            TrySection(rows, "Storage", () => Add(rows, "Storage", "StoredSize",
                WindowsFileStorageDetails.ReadCompressedSize(path).ToString("N0", CultureInfo.CurrentCulture) + " B"));
        TrySection(rows, "Storage", () =>
        {
            string? root = System.IO.Path.GetPathRoot(System.IO.Path.GetFullPath(path));
            if(root is null)
                return;
            var drive = new DriveInfo(root);
            Add(rows, "Storage", "FileSystem", drive.DriveFormat);
            Add(rows, "Storage", "VolumeLabel", drive.VolumeLabel);
            Add(rows, "Storage", "DriveType", drive.DriveType);
        });
        token.ThrowIfCancellationRequested();
        TrySection(rows, "Security", () => ReadSecurity(info, rows));
        TrySection(rows, "Streams", () =>
        {
            var streams = WindowsFileStorageDetails.ReadStreams(path, token);
            if(streams.Count == 0)
                Add(rows, "Streams", "Status", L("NoStreams"));
            foreach(var stream in streams)
                rows.Add(new(L("Streams"), stream.Name, Bytes(stream.Size), stream.Name));
            // Download provenance is plain text. Keep the read bounded, and never
            // open URLs or interpret the contents of any other alternate stream.
            if(streams.Any(stream => stream.Name.Equals(":Zone.Identifier:$DATA", StringComparison.OrdinalIgnoreCase)))
            {
                using var reader = new StreamReader(new FileStream(path + ":Zone.Identifier", FileMode.Open,
                    FileAccess.Read, FileShare.ReadWrite | FileShare.Delete));
                char[] text = new char[16384];
                int count = reader.ReadBlock(text, 0, text.Length);
                rows.Add(new(L("Streams"), "Zone.Identifier", new string(text, 0, count), "Zone.Identifier"));
                if(count == text.Length && reader.Peek() >= 0)
                    Add(rows, "Streams", "Status", L("ZoneTruncated"));
            }
        });
        if(!directory)
        {
            token.ThrowIfCancellationRequested();
            TrySection(rows, "Version", () => ReadVersion(path, rows));
        }
        token.ThrowIfCancellationRequested();
        TrySection(rows, "Metadata", () => WindowsShellPropertyReader.Read(path, rows, token));
        return rows;
    }

    private static void ReadVersion(string path, List<FilePropertyEntry> rows)
    {
        FileVersionInfo version = FileVersionInfo.GetVersionInfo(path);
        if(string.IsNullOrEmpty(version.FileVersion) && string.IsNullOrEmpty(version.ProductVersion))
        {
            Add(rows, "Version", "Status", L("NoVersion"));
            return;
        }
        foreach(var property in typeof(FileVersionInfo).GetProperties()
            .Where(property => property.CanRead && property.Name != nameof(FileVersionInfo.FileName)))
        {
            object? value = property.GetValue(version);
            if(value is string text && string.IsNullOrWhiteSpace(text))
                continue;
            rows.Add(new(L("Version"), property.Name,
                Convert.ToString(value, CultureInfo.CurrentCulture) ?? "", $"FileVersionInfo.{property.Name}"));
        }
    }

    public Task<IReadOnlyList<FilePropertyEntry>> ReadSignatureAsync(
        string path, CancellationToken cancellationToken = default) => Task.Run<IReadOnlyList<FilePropertyEntry>>(() =>
        {
            cancellationToken.ThrowIfCancellationRequested();
            var rows = new List<FilePropertyEntry>();
            TrySection(rows, "Signature", () =>
            {
                AuthenticodeStatus status = new WindowsAuthenticodeVerifier().VerifyOffline(path, out int statusCode);
                rows.Add(new(L("Signature"), L("EmbeddedSignature"), L("Signature" + status), "Status"));
                rows.Add(new(L("Signature"), "WinVerifyTrust", $"0x{statusCode:X8}", "WinVerifyTrust.Result"));
                Add(rows, "Signature", "Availability", L("SignatureLimit"));
                ReadCertificate(path, rows);
            });
            cancellationToken.ThrowIfCancellationRequested();
            TrySection(rows, "Signature", () =>
            {
                var catalogs = WindowsCatalogSignatureReader.Read(path, cancellationToken);
                Add(rows, "Signature", "CatalogCount", catalogs.Count);
                int index = 0;
                foreach(var catalog in catalogs)
                {
                    string prefix = $"Catalog[{index++}].";
                    var catalogRows = new List<FilePropertyEntry>();
                    Add(catalogRows, "Signature", "CatalogPath", catalog.Path);
                    Add(catalogRows, "Signature", "Status", L(catalog.StatusCode == 0 ? "SignatureValid" : "SignatureInvalid"));
                    catalogRows.Add(new(L("Signature"), "WinVerifyTrust", $"0x{catalog.StatusCode:X8}", "WinVerifyTrust.Result"));
                    catalogRows.Add(new(L("Signature"), catalog.Algorithm, catalog.Hash, "MemberHash"));
                    ReadCertificate(catalog.Path, catalogRows);
                    rows.AddRange(catalogRows.Select(row => row with { Key = prefix + row.Key }));
                }
            });
            cancellationToken.ThrowIfCancellationRequested();
            return rows;
        }, cancellationToken);

    private static void ReadCertificate(string path, List<FilePropertyEntry> rows)
    {
        try
        {
            using X509Certificate2? certificate = WindowsSignerCertificateReader.Read(path);
            if(certificate is null)
            {
                Add(rows, "Signature", "Certificate", L("NoEmbeddedCertificate"));
                return;
            }
            Add(rows, "Signature", "Subject", certificate.Subject);
            Add(rows, "Signature", "Issuer", certificate.Issuer);
            Add(rows, "Signature", "SerialNumber", certificate.SerialNumber);
            Add(rows, "Signature", "Thumbprint", certificate.Thumbprint);
            rows.Add(new(L("Signature"), "SHA-256", certificate.GetCertHashString(HashAlgorithmName.SHA256), "Certificate.SHA256"));
            Add(rows, "Signature", "ValidFrom", certificate.NotBefore.ToString("O"));
            Add(rows, "Signature", "ValidTo", certificate.NotAfter.ToString("O"));
            Add(rows, "Signature", "SignatureAlgorithm", certificate.SignatureAlgorithm.FriendlyName);
            rows.Add(new(L("Signature"), "Public key algorithm", certificate.PublicKey.Oid.Value ?? "", "Certificate.PublicKeyAlgorithm"));
            foreach(X509Extension extension in certificate.Extensions)
                rows.Add(new(L("Signature"), extension.Oid?.FriendlyName ?? "", extension.Format(true), extension.Oid?.Value ?? ""));
        }
        catch(CryptographicException)
        {
            Add(rows, "Signature", "Certificate", L("NoEmbeddedCertificate"));
        }
    }

    internal static void ReadPeHeaders(string path, List<FilePropertyEntry> rows)
    {
        using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
        using var reader = new PEReader(stream);
        void AddProperties(object header, string prefix)
        {
            foreach(var property in header.GetType().GetProperties())
            {
                object? value = property.GetValue(header);
                if(value is DirectoryEntry entry)
                {
                    rows.Add(new("PE", property.Name + ".RVA", $"0x{entry.RelativeVirtualAddress:X8}", prefix + property.Name + ".RVA"));
                    rows.Add(new("PE", property.Name + ".Size", entry.Size.ToString(CultureInfo.InvariantCulture), prefix + property.Name + ".Size"));
                }
                else
                    rows.Add(new("PE", property.Name, value?.ToString() ?? "", prefix + property.Name));
            }
        }
        AddProperties(reader.PEHeaders.CoffHeader, "COFF.");
        if(reader.PEHeaders.PEHeader is { } pe)
            AddProperties(pe, "PE.");
        if(reader.PEHeaders.CorHeader is { } cor)
            AddProperties(cor, "CLR.");
        foreach(SectionHeader section in reader.PEHeaders.SectionHeaders)
            AddProperties(section, $"Section[{section.Name}].");
        if(reader.HasMetadata)
        {
            var metadata = reader.GetMetadataReader();
            if(metadata.IsAssembly)
            {
                AssemblyDefinition assembly = metadata.GetAssemblyDefinition();
                rows.Add(new(".NET", "Assembly", metadata.GetString(assembly.Name), "Assembly.Name"));
                rows.Add(new(".NET", "Version", assembly.Version.ToString(), "Assembly.Version"));
            }
            foreach(AssemblyReferenceHandle handle in metadata.AssemblyReferences)
            {
                AssemblyReference reference = metadata.GetAssemblyReference(handle);
                rows.Add(new(".NET", metadata.GetString(reference.Name), reference.Version.ToString(), "AssemblyReference"));
            }
        }
    }

    private static void ReadSecurity(FileSystemInfo info, List<FilePropertyEntry> rows)
    {
        const AccessControlSections sections = AccessControlSections.Owner |
            AccessControlSections.Group | AccessControlSections.Access;
        FileSystemSecurity security = info is DirectoryInfo directory
            ? directory.GetAccessControl(sections) : ((FileInfo)info).GetAccessControl(sections);
        Add(rows, "Security", "Owner", DescribeIdentity(security.GetOwner(typeof(SecurityIdentifier))));
        Add(rows, "Security", "Group", DescribeIdentity(security.GetGroup(typeof(SecurityIdentifier))));
        Add(rows, "Security", "AclProtected", security.AreAccessRulesProtected);
        Add(rows, "Security", "AclCanonical", security.AreAccessRulesCanonical);
        Add(rows, "Security", "Sddl", security.GetSecurityDescriptorSddlForm(sections));
        int index = 0;
        foreach(FileSystemAccessRule rule in security.GetAccessRules(true, true, typeof(SecurityIdentifier)))
        {
            string value = $"{rule.AccessControlType}: {rule.FileSystemRights}; " +
                $"IsInherited={rule.IsInherited}; InheritanceFlags={rule.InheritanceFlags}; PropagationFlags={rule.PropagationFlags}";
            rows.Add(new(L("Security"), DescribeIdentity(rule.IdentityReference), value, $"ACL[{index++}]"));
        }
        Add(rows, "Security", "Availability", L("AclLimit"));
    }

    private static string DescribeIdentity(IdentityReference? identity)
    {
        if(identity is null)
            return "";
        try { return $"{identity.Translate(typeof(NTAccount)).Value} ({identity.Value})"; }
        catch(IdentityNotMappedException) { return identity.Value; }
    }

    public async Task<IReadOnlyList<FilePropertyEntry>> ComputeHashesAsync(
        string path, IProgress<double>? progress = null, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var before = new FileInfo(path);
        long length = before.Length;
        DateTime modified = before.LastWriteTimeUtc;
        // Deny new writers/replacements while hashing; fail explicitly if an
        // existing writer prevents a stable read rather than return stale hashes.
        await using var stream = new FileStream(path, FileMode.Open, FileAccess.Read,
            FileShare.Read, 1024 * 1024, FileOptions.Asynchronous | FileOptions.SequentialScan);
        using var sha256 = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        using var sha512 = IncrementalHash.CreateHash(HashAlgorithmName.SHA512);
        byte[] buffer = new byte[1024 * 1024];
        long completed = 0;
        int count;
        while((count = await stream.ReadAsync(buffer, cancellationToken).ConfigureAwait(false)) > 0)
        {
            sha256.AppendData(buffer, 0, count);
            sha512.AppendData(buffer, 0, count);
            completed += count;
            progress?.Report(length == 0 ? 100 : Math.Min(100, completed * 100d / length));
        }
        cancellationToken.ThrowIfCancellationRequested();
        before.Refresh();
        if(before.Length != length || before.LastWriteTimeUtc != modified || completed != length)
            throw new IOException(L("FileChanged"));
        progress?.Report(100);
        return [new(L("Hashes"), "SHA-256", Convert.ToHexString(sha256.GetHashAndReset()), "SHA256"),
            new(L("Hashes"), "SHA-512", Convert.ToHexString(sha512.GetHashAndReset()), "SHA512")];
    }

    internal static void TrySection(List<FilePropertyEntry> rows, string category, Action read)
    {
        try { read(); }
        catch(Exception exception) when(exception is not OperationCanceledException &&
            exception is not OutOfMemoryException)
        {
            rows.Add(new(L(category), L("Unavailable"), exception.Message, "Error"));
        }
    }

    internal static void Add(List<FilePropertyEntry> rows, string category, string key, object? value)
    {
        string text = Convert.ToString(value, CultureInfo.CurrentCulture) ?? "";
        if(!string.IsNullOrWhiteSpace(text))
            rows.Add(new(L(category), L(key), text, key));
    }

    private static void AddDate(List<FilePropertyEntry> rows, string key, DateTime value) =>
        Add(rows, "General", key, $"{value.ToLocalTime():yyyy-MM-dd HH:mm:ss.fffffff zzz}\nUTC: {value.ToUniversalTime():O}");

    private static string Bytes(long value) => $"{value:N0} B ({value / 1048576d:N3} MiB)";
    internal static string L(string key) => LocalizationManager.GetString("FileDetails." + key);
}
