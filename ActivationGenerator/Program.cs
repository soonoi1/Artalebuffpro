using System;
using System.Security.Cryptography;
using System.Text;

namespace ActivationGenerator
{
    class Program
    {
        // RSA Private Key XML used to sign activation codes (must keep secure, only in generator)
        private const string PrivateKeyXml = "<RSAKeyValue><Modulus>xzHvcGoqnooJuHwFvVSbyaowifIdxlCCjbhU+HDy4FObswzJ06k/JnsyOePigr0539C20nvASY2tl2KjOMu/dw8eYz90rCox2+C2hBQwtPvh4/zv1ta/D/GNbktGgBiVwmaRwGN3B2Hglnc5g9WtIyROZWwnVz8OUFs2CGBPS90=</Modulus><Exponent>AQAB</Exponent><P>/NGL7sn41RN/PQzQ88HE2PhTyLj9i64l5wdJejb1wi+GilD2X3x/9Z4SQNBncrptj7khYcRWapvrxsUvbKyDcw==</P><Q>ybOkFyGRRYxqC/i43eKai/WEpjWxbdPvAK2bw5nqicJ1Jdtt0VoIGHvsYCqUrPwIDaePSt9NyddA2aRXHfM/bw==</Q><DP>ex/eQ1vvqG3HYMcWGDB9GqHNxAp7yIP2h44f9bpAc+LLZh9J7XTnqInkH9afGtu6Me2aWU/SOjdXW0V69DUMCw==</DP><DQ>JhSBVVCkEaJZ3xq9JD8E+ImI2qxmbBrIE7OzJbGoYwvQfC46RH0f7CdxUBKZ8TK//nv1BKi2EfZOqwho3iGvhw==</DQ><InverseQ>lZQ4AJUYWWGHWEl0LO0ipj22nEcqXnTmtuNEdIR0RgYq9xY9vwwQ+K4wGzXGjfm/j89ngzKiaQ7UQ670PnBUsw==</InverseQ><D>M24gLT0sHdz0H47eCIFE6++mMqexqld1LdQvFCpNez/7DmK55Y1oQa5DTJEbFbh3reL8oSHUhukurcyI6gwpNcX10DUFxCaS/zjNOABgrWSZRDZ48+r6aLKlFtXDzeuPSaokR4bGy7k8qE1nvbBsrWcODsH35g1R+wqq7MbYWJ0=</D></RSAKeyValue>";

        static void Main(string[] args)
        {
            Console.Title = "ArtaleProBuff 激活码生成器";
            Console.ForegroundColor = ConsoleColor.Magenta;
            Console.WriteLine("========================================");
            Console.WriteLine("       ArtaleProBuff 激活码生成器       ");
            Console.WriteLine("========================================");
            Console.ResetColor();

            while (true)
            {
                Console.Write("\n请输入用户的机器码 (例如 A1B2-C3D4-E5F6-7890): ");
                string? rawHwid = Console.ReadLine()?.Trim();
                if (string.IsNullOrEmpty(rawHwid))
                {
                    Console.ForegroundColor = ConsoleColor.Red;
                    Console.WriteLine("❌ 机器码不能为空！");
                    Console.ResetColor();
                    continue;
                }

                // Clean and validate HWID format
                rawHwid = rawHwid.Replace(" ", "").ToUpper();
                string[] parts = rawHwid.Split('-');
                if (parts.Length != 4)
                {
                    Console.ForegroundColor = ConsoleColor.Red;
                    Console.WriteLine("❌ 机器码格式不正确！正确格式为 16位分段，例如 A1B2-C3D4-E5F6-7890");
                    Console.ResetColor();
                    continue;
                }

                // Extract authorized HWID bytes from format
                byte[] authorizedHwidBytes = new byte[8];
                try
                {
                    string joined = string.Join("", parts);
                    if (joined.Length != 16) throw new Exception();
                    for (int i = 0; i < 8; i++)
                    {
                        authorizedHwidBytes[i] = Convert.ToByte(joined.Substring(i * 2, 2), 16);
                    }
                }
                catch
                {
                    Console.ForegroundColor = ConsoleColor.Red;
                    Console.WriteLine("❌ 机器码解析失败，请检查是否包含非十六进制字符！");
                    Console.ResetColor();
                    continue;
                }

                Console.Write("请输入有效期天数 (输入数字如 30，或直接按回车表示永久 99 年): ");
                string? rawDays = Console.ReadLine()?.Trim();
                int days = 365 * 99; // Default perpetual (99 years)
                if (!string.IsNullOrEmpty(rawDays))
                {
                    if (!int.TryParse(rawDays, out days) || days <= 0)
                    {
                        Console.ForegroundColor = ConsoleColor.Red;
                        Console.WriteLine("❌ 有效天数必须为正整数！已使用默认永久授权。");
                        Console.ResetColor();
                        days = 365 * 99;
                    }
                }

                DateTime expiration = DateTime.Now.AddDays(days);
                byte[] expDateBytes = BitConverter.GetBytes(expiration.ToBinary());

                // Prepare payload: HWID bytes (8) + Expiration Date bytes (8) = 16 bytes
                byte[] payload = new byte[16];
                Buffer.BlockCopy(authorizedHwidBytes, 0, payload, 0, 8);
                Buffer.BlockCopy(expDateBytes, 0, payload, 8, 8);

                // Sign payload using RSA private key
                byte[] signature;
                try
                {
                    using (var rsa = new RSACryptoServiceProvider())
                    {
                        rsa.FromXmlString(PrivateKeyXml);
                        signature = rsa.SignData(payload, CryptoConfig.MapNameToOID("SHA256"));
                    }
                }
                catch (Exception ex)
                {
                    Console.ForegroundColor = ConsoleColor.Red;
                    Console.WriteLine($"❌ RSA 签名生成失败: {ex.Message}");
                    Console.ResetColor();
                    continue;
                }

                // Combine payload and signature: 16 + 128 = 144 bytes
                byte[] allBytes = new byte[144];
                Buffer.BlockCopy(payload, 0, allBytes, 0, 16);
                Buffer.BlockCopy(signature, 0, allBytes, 16, 128);

                // Encode to Base64
                string activationCode = Convert.ToBase64String(allBytes);

                Console.ForegroundColor = ConsoleColor.Green;
                Console.WriteLine("\n✨ 激活码生成成功！");
                Console.ResetColor();
                Console.WriteLine($"授权机器: {rawHwid}");
                Console.WriteLine($"到期时间: {expiration:yyyy-MM-dd HH:mm:ss} ({(days >= 365 * 90 ? "永久" : $"{days} 天")})");
                Console.ForegroundColor = ConsoleColor.Cyan;
                Console.WriteLine("\n--- 请复制以下激活码给用户 ---");
                Console.WriteLine(activationCode);
                Console.WriteLine("------------------------------\n");
                Console.ResetColor();
            }
        }
    }
}
