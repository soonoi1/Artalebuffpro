using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using Microsoft.Win32;

namespace ArtaleProBuff
{
    public static class LicenseManager
    {
        private const string LicenseFileName = "license.lic";
        
        // RSA Public Key XML used to verify activation codes (compiled into client app)
        private const string PublicKeyXml = "<RSAKeyValue><Modulus>xzHvcGoqnooJuHwFvVSbyaowifIdxlCCjbhU+HDy4FObswzJ06k/JnsyOePigr0539C20nvASY2tl2KjOMu/dw8eYz90rCox2+C2hBQwtPvh4/zv1ta/D/GNbktGgBiVwmaRwGN3B2Hglnc5g9WtIyROZWwnVz8OUFs2CGBPS90=</Modulus><Exponent>AQAB</Exponent></RSAKeyValue>";

        /// <summary>
        /// Gets the unique Machine Code (Hardware ID) of the current computer.
        /// Bound to the OS installation MachineGuid for reliability.
        /// </summary>
        public static string GetMachineCode()
        {
            string raw = "";
            try
            {
                using (var key = Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Microsoft\Cryptography"))
                {
                    if (key != null)
                    {
                        object val = key.GetValue("MachineGuid");
                        if (val != null)
                        {
                            raw = val.ToString() ?? "";
                        }
                    }
                }
            }
            catch { }

            if (string.IsNullOrEmpty(raw))
            {
                raw = Environment.MachineName + Environment.ProcessorCount + Environment.UserName;
            }

            using (var sha = SHA256.Create())
            {
                byte[] bytes = sha.ComputeHash(Encoding.UTF8.GetBytes(raw + "ArtaleProBuffSalt"));
                StringBuilder sb = new StringBuilder();
                for (int i = 0; i < 8; i++) // 8 bytes = 16 hex chars
                {
                    sb.Append(bytes[i].ToString("X2"));
                    if (i % 2 == 1 && i < 7) sb.Append("-");
                }
                return sb.ToString(); // e.g. "A1B2-C3D4-E5F6-7890"
            }
        }

        /// <summary>
        /// Checks if the software is currently activated on this machine.
        /// </summary>
        public static bool IsActivated()
        {
            string licPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, LicenseFileName);
            if (!File.Exists(licPath)) return false;

            try
            {
                string code = File.ReadAllText(licPath).Trim();
                return VerifyActivationCode(code);
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// Attempts to activate the software using the provided activation code.
        /// Saves to license.lic if successful.
        /// </summary>
        public static bool Activate(string activationCode)
        {
            if (VerifyActivationCode(activationCode))
            {
                try
                {
                    string licPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, LicenseFileName);
                    File.WriteAllText(licPath, activationCode.Trim());
                    return true;
                }
                catch
                {
                    return false;
                }
            }
            return false;
        }

        /// <summary>
        /// Cryptographically verifies the activation code using RSA signature.
        /// </summary>
        private static bool VerifyActivationCode(string code)
        {
            if (string.IsNullOrWhiteSpace(code)) return false;

            try
            {
                byte[] allBytes = Convert.FromBase64String(code);
                // Expected format: Payload (16 bytes) + Signature (128 bytes for 1024-bit RSA)
                if (allBytes.Length != 144) return false;

                byte[] payload = new byte[16];
                byte[] signature = new byte[128];
                Buffer.BlockCopy(allBytes, 0, payload, 0, 16);
                Buffer.BlockCopy(allBytes, 16, signature, 0, 128);

                // Verify signature
                using (var rsa = new RSACryptoServiceProvider())
                {
                    rsa.FromXmlString(PublicKeyXml);
                    if (!rsa.VerifyData(payload, CryptoConfig.MapNameToOID("SHA256"), signature))
                    {
                        return false;
                    }
                }

                // Verify payload details
                // Payload: Authorized Machine Code Hashed bytes (8 bytes) + Expiration Date Binary (8 bytes)
                byte[] authorizedHwidBytes = new byte[8];
                byte[] expDateBytes = new byte[8];
                Buffer.BlockCopy(payload, 0, authorizedHwidBytes, 0, 8);
                Buffer.BlockCopy(payload, 8, expDateBytes, 0, 8);

                // Reconstruct authorized HWID hex string
                StringBuilder sb = new StringBuilder();
                for (int i = 0; i < 8; i++)
                {
                    sb.Append(authorizedHwidBytes[i].ToString("X2"));
                    if (i % 2 == 1 && i < 7) sb.Append("-");
                }
                string authorizedHwid = sb.ToString();

                // Reconstruct expiration date
                long binaryDate = BitConverter.ToInt64(expDateBytes, 0);
                DateTime expiration = DateTime.FromBinary(binaryDate);

                // Compare with current machine properties
                string currentHwid = GetMachineCode();
                if (!string.Equals(authorizedHwid, currentHwid, StringComparison.OrdinalIgnoreCase))
                {
                    return false; // HWID mismatch!
                }

                if (DateTime.Now > expiration)
                {
                    return false; // License expired!
                }

                return true;
            }
            catch
            {
                return false;
            }
        }
    }
}
