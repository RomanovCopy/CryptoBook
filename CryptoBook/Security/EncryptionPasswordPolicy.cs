namespace CryptoBook.Security;

/// <summary>Applies to new file encryption, never to reading existing files.</summary>
public static class EncryptionPasswordPolicy
{
    public const int MinimumLength = 8;
    public const int MaximumLength = 128;

    public static bool IsStrong(ReadOnlySpan<char> password)
    {
        if(password.Length is < MinimumLength or > MaximumLength)
            return false;
        Span<char> distinct = stackalloc char[MaximumLength];
        int count = 0;
        bool onlyDigits = true;
        foreach(char value in password)
        {
            if(char.IsControl(value)) return false;
            onlyDigits &= char.IsDigit(value);
            char normalized = char.ToLowerInvariant(value);
            if(!distinct[..count].Contains(normalized)) distinct[count++] = normalized;
        }
        if(onlyDigits || count < 6) return false;
        for(int period = 1; period <= password.Length / 2; period++)
        {
            bool repeated = true;
            for(int i = period; i < password.Length && repeated; i++)
                repeated = password[i] == password[i % period];
            if(repeated) return false;
        }
        foreach(string weak in new[] { "password", "qwerty", "123456", "abcdef", "пароль", "йцукен", "letmein" })
            if(password.Contains(weak, StringComparison.OrdinalIgnoreCase)) return false;
        return true;
    }
}
