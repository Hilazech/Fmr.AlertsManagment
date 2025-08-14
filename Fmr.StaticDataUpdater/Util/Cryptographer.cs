using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using System.Text;

namespace Fmr.StaticDataUpdater.Util
{
    public class Cryptographer
    {
        const string KEY = "96681D248723AD60A43CD3EB8B5667E16FF83D2947F1DD926A0A7F33C64A6DFB6415AC972C9502137C17DCE383A09C692EBCB18A0055DAE0439538F9036EC94F";
        const string PWD = "0B2622DC4B2FE8B7DF60B1240AE9E790C0E3425CC1FEAC34CF375A3CDC0D7A1C";
        const string ABC = "abcdefghijklmnopqrstuvwxyz0123456789";
        static readonly Cryptographer _inctance = new Cryptographer();
        static readonly Random _rand = new Random();
        public static Cryptographer Inctance
        { 
            get { return _inctance; }
        }

        private Cryptographer(){}

        public string EncryptString(string input)
        {
            return EncryptString(input, KEY, PWD);
        }

        public string EncryptString(string input, string key, string pwd)
        {
            // Test data
            string data = input;
            byte[] utfdata = UTF8Encoding.UTF8.GetBytes(data);
            byte[] saltBytes = UTF8Encoding.UTF8.GetBytes(key);
            byte[] pwdBytes = UTF8Encoding.UTF8.GetBytes(pwd);
            // Our symmetric encryption algorithm
            AesManaged aes = new AesManaged();

            // We're using the PBKDF2 standard for password-based key generation
            Rfc2898DeriveBytes rfc = new Rfc2898DeriveBytes(pwdBytes, saltBytes, 1);

            // Setting our parameters
            aes.BlockSize = aes.LegalBlockSizes[0].MaxSize;
            aes.KeySize = aes.LegalKeySizes[0].MaxSize;

            aes.Key = rfc.GetBytes(aes.KeySize / 8);
            aes.IV = rfc.GetBytes(aes.BlockSize / 8);

            ICryptoTransform encryptTransf = aes.CreateEncryptor();

            MemoryStream encryptStream = new MemoryStream();
            CryptoStream encryptor = new CryptoStream(encryptStream, encryptTransf, CryptoStreamMode.Write);

            encryptor.Write(utfdata, 0, utfdata.Length);
            encryptor.Flush();
            encryptor.Close();

            byte[] encryptBytes = encryptStream.ToArray();
            string encryptedString = Convert.ToBase64String(encryptBytes);
            return encryptedString;
            // return HttpServerUtility.UrlTokenEncode(encryptBytes);

        }

        public string DecryptString(string base64Input)
        {
            return DecryptString(base64Input, KEY, PWD);
        }

        public string DecryptString(string base64Input, string key, string pwd)
        {
            try
            {
                //byte[] encryptBytes = UTF8Encoding.UTF8.GetBytes(input);
                //byte[] encryptBytes = HttpServerUtility.UrlTokenDecode(base64Input); 
                byte[] encryptBytes = Convert.FromBase64String(base64Input);

                byte[] saltBytes = UTF8Encoding.UTF8.GetBytes(key);
                byte[] pwdBytes = UTF8Encoding.UTF8.GetBytes(pwd);
                // Our symmetric encryption algorithm
                AesManaged aes = new AesManaged();

                // We're using the PBKDF2 standard for password-based key generation
                Rfc2898DeriveBytes rfc = new Rfc2898DeriveBytes(pwdBytes, saltBytes, 1);

                // Setting our parameters
                aes.BlockSize = aes.LegalBlockSizes[0].MaxSize;
                aes.KeySize = aes.LegalKeySizes[0].MaxSize;

                aes.Key = rfc.GetBytes(aes.KeySize / 8);
                aes.IV = rfc.GetBytes(aes.BlockSize / 8);

                // Now, decryption
                ICryptoTransform decryptTrans = aes.CreateDecryptor();

                // Output stream, can be also a FileStream
                MemoryStream decryptStream = new MemoryStream();
                CryptoStream decryptor = new CryptoStream(decryptStream, decryptTrans, CryptoStreamMode.Write);

                decryptor.Write(encryptBytes, 0, encryptBytes.Length);
                decryptor.Flush();
                decryptor.Close();

                // Showing our decrypted content
                byte[] decryptBytes = decryptStream.ToArray();
                string decryptedString = UTF8Encoding.UTF8.GetString(decryptBytes, 0, decryptBytes.Length);

                return decryptedString;
            }
            catch
            {
                return "";
            }
        }

        public static string RandomString(int length)
        {
            string res = "";
            int abcLength = ABC.Length;
            for (int i = 0; i < length; i++)
            {
                int pos;
                if (i < 2) //starts allways with string
                {
                    pos = _rand.Next(abcLength - 10);
                }
                else
                {
                    pos = _rand.Next(abcLength);
                }
                res += ABC[pos];
            }
            return res;
        }

        public int[] RandNumbers(int length)
        {
            List<int> list = new List<int>();
            List<int> target = new List<int>();
            for (int i = 1; i <= length; i++)
            {
                list.Add(i);
            }
            for (int i = 0; i < length; i++)
            {
                int res = _rand.Next(list.Count);
                target.Add(list[res]);
                list.RemoveAt(res);
            }
            return target.ToArray();
        }

        public string ComputeHash(string pstrPwd)
        {
            string strHex = string.Empty;
            // A hash function works on a byte array, so we will create two arrays, 
            // one for our resulting hash and one for the given text.
            byte[] HashValue, MessageBytes = UnicodeEncoding.Default.GetBytes(pstrPwd);
            // Now we create an object that will hash our text:
            SHA512 sha512 = new SHA512Managed();
            // And finally we calculate the hash and convert it to a hexadecimal string. 
            // Which we can store in a database for example
            HashValue = sha512.ComputeHash(MessageBytes);
            foreach (byte b in HashValue)
                strHex += string.Format("{0:x2}", b);
            return strHex;
        }

        /// <summary>
        /// Check hash value
        /// </summary>
        /// <param name="strOriginal">Original string (ex: password entered by user)</param>
        /// <param name="strHash">Hash value (ex: Hashed value from database)</param>
        /// <returns>true if equal, false if failure</returns>
        public bool CheckHash(string strOriginal, string strHash)
        {
            string strOrigHash = ComputeHash(strOriginal);
            return (strOrigHash == strHash);
        }
    }
}
