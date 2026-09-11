using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Text;

namespace RdpVault.Services;

public record CredentialData(string Username, string Password);

/// <summary>
/// Wraps the Windows Credential Manager (wincred/advapi32) APIs.
/// Credentials are stored under the "RdpVault:" target prefix, separate
/// from the "TERMSRV/&lt;host&gt;" targets that mstsc.exe reads directly.
/// </summary>
public class CredentialService
{
    private const string TargetPrefix = "RdpVault:";

    public void SaveCredential(string name, string username, string password)
    {
        var passwordBytes = Encoding.Unicode.GetBytes(password);
        var blob = Marshal.AllocHGlobal(passwordBytes.Length);
        try
        {
            Marshal.Copy(passwordBytes, 0, blob, passwordBytes.Length);

            var credential = new NativeMethods.CREDENTIAL
            {
                Type = NativeMethods.CredTypeGeneric,
                TargetName = TargetPrefix + name,
                CredentialBlobSize = (uint)passwordBytes.Length,
                CredentialBlob = blob,
                Persist = NativeMethods.CredPersistLocalMachine,
                UserName = username,
            };

            if (!NativeMethods.CredWrite(ref credential, 0))
            {
                throw new Win32Exception(Marshal.GetLastWin32Error(), "Failed to write credential to Windows Credential Manager.");
            }
        }
        finally
        {
            Marshal.FreeHGlobal(blob);
        }
    }

    public CredentialData? GetCredential(string name)
    {
        if (!NativeMethods.CredRead(TargetPrefix + name, NativeMethods.CredTypeGeneric, 0, out var credentialPtr))
        {
            return null;
        }

        try
        {
            var credential = Marshal.PtrToStructure<NativeMethods.CREDENTIAL>(credentialPtr);

            var passwordBytes = new byte[credential.CredentialBlobSize];
            if (credential.CredentialBlobSize > 0)
            {
                Marshal.Copy(credential.CredentialBlob, passwordBytes, 0, (int)credential.CredentialBlobSize);
            }

            return new CredentialData(credential.UserName ?? string.Empty, Encoding.Unicode.GetString(passwordBytes));
        }
        finally
        {
            NativeMethods.CredFree(credentialPtr);
        }
    }

    public void DeleteCredential(string name)
    {
        if (!NativeMethods.CredDelete(TargetPrefix + name, NativeMethods.CredTypeGeneric, 0))
        {
            const int errorNotFound = 1168;
            var error = Marshal.GetLastWin32Error();
            if (error != errorNotFound)
            {
                throw new Win32Exception(error, "Failed to delete credential from Windows Credential Manager.");
            }
        }
    }

    /// <summary>
    /// Encrypts a plaintext value using CredProtect (DPAPI, current-user scoped) into the
    /// text form used by the "password 51:b:&lt;value&gt;" field of a .rdp file.
    /// </summary>
    public string ProtectForRdpFile(string plainText)
    {
        var inputLength = plainText.Length + 1;
        var outputLength = 0;

        NativeMethods.CredProtect(true, plainText, inputLength, null, ref outputLength, out _);

        var output = new StringBuilder(outputLength);
        if (!NativeMethods.CredProtect(true, plainText, inputLength, output, ref outputLength, out _))
        {
            throw new Win32Exception(Marshal.GetLastWin32Error(), "Failed to protect credential for .rdp file.");
        }

        return output.ToString();
    }

    private static class NativeMethods
    {
        public const uint CredTypeGeneric = 1;
        public const uint CredPersistLocalMachine = 2;

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        public struct CREDENTIAL
        {
            public uint Flags;
            public uint Type;
            public string TargetName;
            public string? Comment;
            public System.Runtime.InteropServices.ComTypes.FILETIME LastWritten;
            public uint CredentialBlobSize;
            public IntPtr CredentialBlob;
            public uint Persist;
            public uint AttributeCount;
            public IntPtr Attributes;
            public string? TargetAlias;
            public string? UserName;
        }

        [DllImport("advapi32.dll", EntryPoint = "CredWriteW", SetLastError = true, CharSet = CharSet.Unicode)]
        public static extern bool CredWrite(ref CREDENTIAL credential, uint flags);

        [DllImport("advapi32.dll", EntryPoint = "CredReadW", SetLastError = true, CharSet = CharSet.Unicode)]
        public static extern bool CredRead(string target, uint type, uint flags, out IntPtr credentialPtr);

        [DllImport("advapi32.dll", EntryPoint = "CredDeleteW", SetLastError = true, CharSet = CharSet.Unicode)]
        public static extern bool CredDelete(string target, uint type, uint flags);

        [DllImport("advapi32.dll", EntryPoint = "CredFree")]
        public static extern void CredFree(IntPtr buffer);

        [DllImport("advapi32.dll", EntryPoint = "CredProtectW", SetLastError = true, CharSet = CharSet.Unicode)]
        public static extern bool CredProtect(
            bool fAsSelf,
            string credentials,
            int credentialsLength,
            StringBuilder? protectedCredentials,
            ref int protectedCredentialsLength,
            out uint protectionType);
    }
}
