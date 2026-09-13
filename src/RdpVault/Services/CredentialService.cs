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
    /// Encrypts a plaintext value using DPAPI (CryptProtectData, current-user scoped) and
    /// hex-encodes it into the form used by the "password 51:b:&lt;value&gt;" field of a
    /// .rdp file. Hex encoding guarantees the result contains no line breaks or control
    /// characters, which the .rdp line-oriented format requires.
    /// </summary>
    public string ProtectForRdpFile(string plainText)
    {
        var passwordBytes = Encoding.Unicode.GetBytes(plainText);
        var inputBlob = new NativeMethods.DATA_BLOB
        {
            cbData = (uint)passwordBytes.Length,
            pbData = Marshal.AllocHGlobal(passwordBytes.Length),
        };

        try
        {
            Marshal.Copy(passwordBytes, 0, inputBlob.pbData, passwordBytes.Length);

            var outputBlob = new NativeMethods.DATA_BLOB();
            if (!NativeMethods.CryptProtectData(ref inputBlob, null, IntPtr.Zero, IntPtr.Zero, IntPtr.Zero, 0, ref outputBlob))
            {
                throw new Win32Exception(Marshal.GetLastWin32Error(), "Failed to protect credential for .rdp file.");
            }

            try
            {
                var encryptedBytes = new byte[outputBlob.cbData];
                Marshal.Copy(outputBlob.pbData, encryptedBytes, 0, (int)outputBlob.cbData);
                return Convert.ToHexString(encryptedBytes);
            }
            finally
            {
                NativeMethods.LocalFree(outputBlob.pbData);
            }
        }
        finally
        {
            Marshal.FreeHGlobal(inputBlob.pbData);
        }
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

        [StructLayout(LayoutKind.Sequential)]
        public struct DATA_BLOB
        {
            public uint cbData;
            public IntPtr pbData;
        }

        [DllImport("crypt32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
        public static extern bool CryptProtectData(
            ref DATA_BLOB dataIn,
            string? dataDescription,
            IntPtr optionalEntropy,
            IntPtr reserved,
            IntPtr promptStruct,
            uint flags,
            ref DATA_BLOB dataOut);

        [DllImport("kernel32.dll")]
        public static extern IntPtr LocalFree(IntPtr handle);
    }
}
