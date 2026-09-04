namespace CryptoBook.Services
{
    /// <summary>Controls whether the built-in updater may run an unsigned installer.</summary>
    public enum UnsignedReleasePolicy
    {
        /// <summary>Only an installer with a valid Authenticode signature may run.</summary>
        RequireAuthenticodeSignature,

        /// <summary>
        /// An explicitly declared unsigned GitHub release may run after its
        /// SHA-256 hash has been verified against the release manifest.
        /// </summary>
        AllowWithVerifiedChecksum
    }
}
