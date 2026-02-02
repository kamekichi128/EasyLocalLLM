using System;
using System.Security.Cryptography;
using System.Text;

namespace EasyLocalLLM.LLM.Manager
{
    /// <summary>
    /// 暗号化・復号化ユーティリティ（AES-256）
    /// </summary>
    internal static class ChatEncryption
    {
        private const int KeySize = 256;
        private const int IvSize = 128;
        private const int IterationCount = 10000;

        /// <summary>
        /// 文字列を暗号化
        /// </summary>
        public static string Encrypt(string plainText, string password)
        {
            if (string.IsNullOrEmpty(plainText))
                return plainText;

            if (string.IsNullOrEmpty(password))
                throw new ArgumentException("Password cannot be empty", nameof(password));

            using (var rng = new RNGCryptoServiceProvider())
            {
                byte[] saltBuffer = new byte[16];
                rng.GetBytes(saltBuffer);

                using (var pbkdf2 = new Rfc2898DeriveBytes(password, saltBuffer, IterationCount, HashAlgorithmName.SHA256))
                {
                    byte[] keyBytes = pbkdf2.GetBytes(KeySize / 8);

                    using (var aes = Aes.Create())
                    {
                        aes.KeySize = KeySize;
                        aes.Mode = CipherMode.CBC;
                        aes.Padding = PaddingMode.PKCS7;
                        aes.Key = keyBytes;

                        using (var encryptor = aes.CreateEncryptor(aes.Key, aes.IV))
                        {
                            byte[] plainBytes = Encoding.UTF8.GetBytes(plainText);
                            byte[] cipherBytes = encryptor.TransformFinalBlock(plainBytes, 0, plainBytes.Length);

                            // IV + Salt + Ciphertext を連結
                            byte[] result = new byte[aes.IV.Length + saltBuffer.Length + cipherBytes.Length];
                            Buffer.BlockCopy(aes.IV, 0, result, 0, aes.IV.Length);
                            Buffer.BlockCopy(saltBuffer, 0, result, aes.IV.Length, saltBuffer.Length);
                            Buffer.BlockCopy(cipherBytes, 0, result, aes.IV.Length + saltBuffer.Length, cipherBytes.Length);

                            return Convert.ToBase64String(result);
                        }
                    }
                }
            }
        }

        /// <summary>
        /// 文字列を復号化
        /// </summary>
        public static string Decrypt(string cipherText, string password)
        {
            if (string.IsNullOrEmpty(cipherText))
                return cipherText;

            if (string.IsNullOrEmpty(password))
                throw new ArgumentException("Password cannot be empty", nameof(password));

            try
            {
                byte[] cipherBytes = Convert.FromBase64String(cipherText);

                int ivSize = IvSize / 8;
                int saltSize = 16;
                int minLength = ivSize + saltSize;

                if (cipherBytes.Length < minLength)
                    throw new InvalidOperationException("Invalid ciphertext format");

                byte[] iv = new byte[ivSize];
                byte[] salt = new byte[saltSize];
                byte[] actualCipher = new byte[cipherBytes.Length - minLength];

                Buffer.BlockCopy(cipherBytes, 0, iv, 0, ivSize);
                Buffer.BlockCopy(cipherBytes, ivSize, salt, 0, saltSize);
                Buffer.BlockCopy(cipherBytes, minLength, actualCipher, 0, actualCipher.Length);

                using (var pbkdf2 = new Rfc2898DeriveBytes(password, salt, IterationCount, HashAlgorithmName.SHA256))
                {
                    byte[] keyBytes = pbkdf2.GetBytes(KeySize / 8);

                    using (var aes = Aes.Create())
                    {
                        aes.KeySize = KeySize;
                        aes.Mode = CipherMode.CBC;
                        aes.Padding = PaddingMode.PKCS7;
                        aes.Key = keyBytes;
                        aes.IV = iv;

                        using (var decryptor = aes.CreateDecryptor(aes.Key, aes.IV))
                        {
                            byte[] plainBytes = decryptor.TransformFinalBlock(actualCipher, 0, actualCipher.Length);
                            return Encoding.UTF8.GetString(plainBytes);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException("Failed to decrypt data. Invalid password or corrupted data.", ex);
            }
        }
    }
}
