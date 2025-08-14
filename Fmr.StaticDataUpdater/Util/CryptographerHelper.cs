using System;
using System.Collections.Generic;
using System.Text;

namespace Fmr.StaticDataUpdater.Util
{
    public static class CryptographerHelper
    {
        static readonly char[] _padding = { '=' };

        public static string Decrypt(string val)
        {
            if (string.IsNullOrWhiteSpace(val)) return val;
            string incoming = val.Replace('_', '/').Replace('-', '+');
            switch (val.Length % 4)
            {
                case 2: incoming += "=="; break;
                case 3: incoming += "="; break;
            }
            var res = Cryptographer.Inctance.DecryptString(incoming);
            if (string.IsNullOrWhiteSpace(res))
                return val;
            return res;
        }

        public static string Encrypt(string val)
        {
            if (string.IsNullOrWhiteSpace(val)) return val;
            var res = Cryptographer.Inctance.EncryptString(val).TrimEnd(_padding).Replace('+', '-').Replace('/', '_');
            if (string.IsNullOrWhiteSpace(res))
                return val;
            return res;
        }
    }
}
