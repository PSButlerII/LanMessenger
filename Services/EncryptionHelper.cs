using System.Security.Cryptography;
using System.Text;

namespace LanMessenger.Services
{
        
    /// <summary>
    /// Provides helper methods for encrypting and decrypting text using symmetric encryption.
    /// </summary>
    /// <remarks>The EncryptionHelper class offers static methods to securely encrypt and decrypt string data.
    /// It is intended for scenarios where simple symmetric encryption is required. The encryption key is hardcoded in
    /// this implementation; for production use, ensure that keys are managed securely and not embedded in source code.
    /// This class is not thread-specific and can be used concurrently from multiple threads.</remarks>
    public class EncryptionHelper
    {
        /// <summary>
        /// 
        /// </summary>
        /// <param name="clearText"></param>
        /// <returns></returns>
        public static string Encrypt(string clearText)
        {
            byte[] salt = new byte[16];
            using (var rng = RandomNumberGenerator.Create())
            {
                rng.GetBytes(salt);
            }
            using (var aes = Aes.Create())
            {
                aes.Key = Encoding.UTF8.GetBytes("mysecretkey12345"); // replace with your own key
                aes.IV = salt;
                using (var encryptor = aes.CreateEncryptor())
                {
                    byte[] encrypted = encryptor.TransformFinalBlock(Encoding.UTF8.GetBytes(clearText), 0, clearText.Length);
                    return Convert.ToBase64String(salt.Concat(encrypted).ToArray());
                }
            }
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="cipherText"></param>
        /// <returns></returns>
        public static string Decrypt(string cipherText)
        {
            byte[] encrypted = Convert.FromBase64String(cipherText);
            byte[] salt = encrypted.Take(16).ToArray();
            encrypted = encrypted.Skip(16).ToArray();
            using (var aes = Aes.Create())
            {
                aes.Key = Encoding.UTF8.GetBytes("mysecretkey12345"); // replace with your own key
                aes.IV = salt;
                using (var decryptor = aes.CreateDecryptor())
                {
                    byte[] decrypted = decryptor.TransformFinalBlock(encrypted, 0, encrypted.Length);
                    return Encoding.UTF8.GetString(decrypted);
                }
            }
        }


    }   

}
